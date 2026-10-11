using AlpacaMedic.Data;
using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AlpacaMedic.Services;

public class RiskLevels
{
    public int LowMax { get; set; } = 5;
    public int HighMin { get; set; } = 12;
    public int CriticalMin { get; set; } = 20;
    public string Level(int score) => score >= CriticalMin ? "Critical" : score >= HighMin ? "High" : score > LowMax ? "Medium" : "Low";
}

public class RiskAnalysisService(RiskDb db, AttackPathService pathService, IOptions<RiskLevels> levels)
{
    public RiskLevels Thresholds => levels.Value;

    public async Task<List<RiskScenarioRow>> Calculate(CancellationToken cancellationToken = default)
    {
        // Take one consistent SQLite snapshot, then release the transaction before enumerating paths.
        await using var snapshot = await db.Database.BeginTransactionAsync(cancellationToken);
        var components = await db.AttackComponents.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var exposed = await db.AttackSurfaceEntries.AsNoTracking().Where(e => e.IsExposed).Select(e => e.SystemAssetId).ToListAsync(cancellationToken);
        var connections = await db.AttackConnections.AsNoTracking().OrderBy(c => c.Id).ToListAsync(cancellationToken);
        var systems = await db.SystemAssets.AsNoTracking().ToDictionaryAsync(s => s.Id, cancellationToken);
        var techniques = await db.AttackTechniques.AsNoTracking().Where(t => db.AttackComponents.Any(c => c.AttackTechniqueId == t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);
        var systemRelations = await db.SystemAssetRelations.AsNoTracking().ToDictionaryAsync(r => r.Id, cancellationToken);
        var assets = await db.BusinessAssets.AsNoTracking().OrderBy(a => a.Name).ThenBy(a => a.Id).ToListAsync(cancellationToken);
        var memberships = await db.BusinessProcessAssets.AsNoTracking().ToListAsync(cancellationToken);
        var processes = await db.BusinessProcesses.AsNoTracking().ToDictionaryAsync(p => p.Id, cancellationToken);
        var assetRelations = await db.BusinessAssetRelations.AsNoTracking().ToListAsync(cancellationToken);
        await snapshot.CommitAsync(cancellationToken);

        var assetProcesses = memberships.GroupBy(m => m.BusinessAssetId).ToDictionary(g => g.Key,
            g => g.Select(m => processes[m.BusinessProcessId]).OrderBy(p => p.Name).ToArray());
        var consequence = assets.ToDictionary(a => a.Id, a => assetProcesses.TryGetValue(a.Id, out var linked)
            ? linked.Sum(p => p.Consequence) : 0m);
        var maximum = consequence.Values.Sum();
        var steps = components.Values.ToDictionary(c => c.Id, c => new RiskAttackStep(c.Id, c.SystemAssetId,
            systems[c.SystemAssetId].Name, c.AttackTechniqueId, techniques[c.AttackTechniqueId].TechniqueId,
            techniques[c.AttackTechniqueId].Name, c.Likelihood, c.Comment));
        var edges = connections.ToDictionary(c => c.Id, c => new RiskPathConnection(c.Id, c.FromAttackComponentId,
            c.ToAttackComponentId, c.SystemAssetRelationId, systemRelations[c.SystemAssetRelationId].RelationType));
        var rows = new List<RiskScenarioRow>();
        foreach (var initial in components.Values.Where(c => c.IsInitial && exposed.Contains(c.SystemAssetId)).OrderBy(c => c.Id))
        {
            var paths = pathService.Find(initial.Id, components, connections, cancellationToken);
            var reachableSystems = paths.SelectMany(p => p.ComponentIds).Select(id => components[id].SystemAssetId).ToHashSet();
            var reachableAssets = assetRelations.Where(r => reachableSystems.Contains(r.SystemAssetId))
                .GroupBy(r => r.BusinessAssetId).ToDictionary(g => g.Key, g => g.Select(r => r.SystemAssetId).Distinct().Order().ToArray());
            var affected = assets.Where(a => reachableAssets.ContainsKey(a.Id)).Select(a => new RiskBusinessAssetRow(a.Id,
                a.Name, assetProcesses.GetValueOrDefault(a.Id, []), consequence[a.Id], reachableAssets[a.Id])).ToArray();
            var accumulated = affected.Sum(a => a.Consequence);
            var ratio = maximum == 0 ? 0 : accumulated / maximum;
            var index = maximum == 0 ? 1 : Enumerable.Range(1, 5).First(i => accumulated * 5 <= maximum * i);
            var likelihood = paths.Max(p => p.PathLikelihood);
            var score = index * likelihood;
            rows.Add(new(initial.Id, steps[initial.Id], paths.Select((p, i) => new RiskPathRow(i + 1,
                p.ComponentIds.Select(id => steps[id]).ToArray(), p.ConnectionIds.Select(id => edges[id]).ToArray(),
                p.PathLikelihood, p.CycleToComponentId)).ToArray(), affected, accumulated, maximum, ratio, index,
                likelihood, score, levels.Value.Level(score)));
        }
        return rows.OrderByDescending(r => r.RiskScore).ThenBy(r => r.Id).ToList();
    }
}

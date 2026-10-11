using AlpacaMedic.Data;
using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;

namespace AlpacaMedic.Services;
public record RiskRow(int Id, SystemAsset SystemAsset, AttackTechnique Technique, string Scenario, decimal BlastRadius, int Likelihood, decimal RiskScore, string RiskLevel, BusinessProcess[] BusinessProcesses, BusinessAsset[] BusinessAssets, CisControl[] CisControls);
public class RiskService(RiskDb db, IConfiguration config)
{
    public object Thresholds => new
    {
        LowMax,
        HighMin,
        CriticalMin
    };
    private decimal LowMax => config.GetValue("RiskLevels:LowMax", 5m);
    private decimal HighMin => config.GetValue("RiskLevels:HighMin", 12m);
    private decimal CriticalMin => config.GetValue("RiskLevels:CriticalMin", 20m);

    public string Level(decimal score)
    {
        if (score <= LowMax)
            return "Low";
        if (score < HighMin)
            return "Medium";
        if (score < CriticalMin)
            return "High";
        return "Critical";
    }

    public async Task<object[]> Systems()
    {
        var systems = await db.SystemAssets.AsNoTracking().ToListAsync();
        var mappings = await db.AssetMappings.ToListAsync();
        var assets = await db.BusinessAssets.ToListAsync();
        var processes = await db.BusinessProcesses.ToListAsync();
        return systems.Select(s =>
        {
            var linked = assets.Where(a => mappings.Any(m => m.SystemAssetId == s.Id && m.BusinessAssetId == a.Id)).ToArray();
            return (object)new
            {
                s.Id,
                s.Name,
                s.Description,
                s.AssetType,
                s.Zone,
                s.BlastRadius,
                s.BlastRadiusReason,
                BusinessAssets = linked,
                DirectConsequence = processes.Where(p => linked.Any(a => a.BusinessProcessId == p.Id)).Sum(p => p.Consequence)
            };
        }).ToArray();
    }

    public async Task<List<RiskRow>> Risks()
    {
        var threats = await db.SystemThreats.AsNoTracking().ToListAsync();
        var systems = await db.SystemAssets.ToDictionaryAsync(x => x.Id);
        var techniques = await db.AttackTechniques.ToDictionaryAsync(x => x.Id);
        var assets = await db.BusinessAssets.ToListAsync();
        var mappings = await db.AssetMappings.ToListAsync();
        var processes = await db.BusinessProcesses.ToListAsync();
        var controls = await db.CisControls.ToListAsync();
        var selected = await db.ThreatControls.ToListAsync();
        return threats.Select(t =>
        {
            var s = systems[t.SystemAssetId];
            var linked = assets.Where(a => mappings.Any(m => m.SystemAssetId == s.Id && m.BusinessAssetId == a.Id)).ToArray();
            var score = s.BlastRadius * t.Likelihood;
            return new RiskRow(t.Id, s, techniques[t.AttackTechniqueId], t.Scenario, s.BlastRadius, t.Likelihood, score, Level(score), processes.Where(p => linked.Any(a => a.BusinessProcessId == p.Id)).ToArray(), linked.ToArray(), controls.Where(c => selected.Any(x => x.SystemThreatId == t.Id && x.CisControlId == c.Id && x.IsRecommended)).ToArray());
        }).OrderByDescending(x => x.RiskScore).ToList();
    }
}

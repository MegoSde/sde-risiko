using System.ComponentModel.DataAnnotations;
using AlpacaMedic.Data;
using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;

namespace AlpacaMedic.Services;
public class AnalysisService(RiskDb db, RiskService risks, CisPriorityService priorities)
{
    public async Task<object> Processes() => await db.BusinessProcesses.AsNoTracking().ToListAsync();
    public async Task<object> Assets() => await db.BusinessAssets.AsNoTracking().ToListAsync();
    public async Task<object> Mappings() => await db.AssetMappings.AsNoTracking().ToListAsync();
    public async Task<object> Access() => await db.SystemAccess.AsNoTracking().ToListAsync();
    public async Task<object> Threats() => await db.SystemThreats.AsNoTracking().ToListAsync();
    public async Task<object> Techniques() => await db.AttackTechniques.AsNoTracking().ToListAsync();
    public async Task<object> Controls() => await db.CisControls.AsNoTracking().ToListAsync();
    public async Task<object> Recommended() => await db.TechniqueControls.AsNoTracking().ToListAsync();
    public async Task<object> Selected() => await db.ThreatControls.AsNoTracking().ToListAsync();
    public async Task<object> Dashboard()
    {
        var rows = await risks.Risks();
        return new
        {
            BusinessProcesses = await db.BusinessProcesses.CountAsync(),
            BusinessAssets = await db.BusinessAssets.CountAsync(),
            SystemAssets = await db.SystemAssets.CountAsync(),
            AttackFindings = rows.Count,
            Risks = rows.Count,
            HighestRiskScore = rows.Select(x => x.RiskScore).DefaultIfEmpty().Max(),
            Priorities = await priorities.Calculate()
        };
    }

    public async Task<BusinessProcess> SaveProcess(int? id, BusinessProcess input)
    {
        var row = id.HasValue ? await db.BusinessProcesses.FindAsync(id.Value) : new BusinessProcess();
        if (row == null)
            throw new KeyNotFoundException("Posten findes ikke.");
        row.Name = input.Name.Trim();
        if (row.Name.Length == 0)
            throw new ValidationException("Navn skal udfyldes.");
        row.Description = input.Description;
        row.Confidentiality = input.Confidentiality;
        row.Integrity = input.Integrity;
        row.Availability = input.Availability;
        row.Consequence = ConsequenceService.Calculate(row.Confidentiality, row.Integrity, row.Availability);
        if (!id.HasValue)
            db.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    public async Task<BusinessAsset> SaveAsset(int? id, BusinessAsset input)
    {
        if (!await db.BusinessProcesses.AnyAsync(x => x.Id == input.BusinessProcessId))
            throw new ValidationException("Vælg en eksisterende proces.");
        var row = id.HasValue ? await db.BusinessAssets.FindAsync(id.Value) : new BusinessAsset();
        if (row == null)
            throw new KeyNotFoundException("Posten findes ikke.");
        row.Name = input.Name.Trim();
        if (row.Name.Length == 0)
            throw new ValidationException("Navn skal udfyldes.");
        row.Description = input.Description;
        row.BusinessProcessId = input.BusinessProcessId;
        if (!id.HasValue)
            db.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    public async Task<SystemAsset> SaveSystem(int? id, SystemAsset input)
    {
        if (input.BlastRadius * 2 != decimal.Truncate(input.BlastRadius * 2))
            throw new ValidationException("Blast Radius skal være i trin på 0,5.");
        if (input.BlastRadius > 1 && string.IsNullOrWhiteSpace(input.BlastRadiusReason))
            throw new ValidationException("Begrund Blast Radius over 1.");
        var row = id.HasValue ? await db.SystemAssets.FindAsync(id.Value) : new SystemAsset();
        if (row == null)
            throw new KeyNotFoundException("Posten findes ikke.");
        row.Name = input.Name.Trim();
        if (row.Name.Length == 0)
            throw new ValidationException("Navn skal udfyldes.");
        row.Description = input.Description;
        row.AssetType = input.AssetType;
        row.Zone = input.Zone;
        row.BlastRadius = input.BlastRadius;
        row.BlastRadiusReason = input.BlastRadiusReason;
        if (!id.HasValue)
            db.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    public async Task<SystemThreat> SaveThreat(int? id, SystemThreat input)
    {
        if (!await db.SystemAssets.AnyAsync(x => x.Id == input.SystemAssetId) || !await db.AttackTechniques.AnyAsync(x => x.Id == input.AttackTechniqueId))
            throw new ValidationException("Vælg et eksisterende system og en teknik.");
        var row = id.HasValue ? await db.SystemThreats.FindAsync(id.Value) : new SystemThreat();
        if (row == null)
            throw new KeyNotFoundException("Posten findes ikke.");
        if (string.IsNullOrWhiteSpace(input.Scenario))
            throw new ValidationException("Beskriv scenariet.");
        var changed = !id.HasValue || row.AttackTechniqueId != input.AttackTechniqueId;
        row.SystemAssetId = input.SystemAssetId;
        row.AttackTechniqueId = input.AttackTechniqueId;
        row.Scenario = input.Scenario;
        row.Likelihood = input.Likelihood;
        if (!id.HasValue)
            db.Add(row);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        if (changed)
        {
            db.ThreatControls.RemoveRange(db.ThreatControls.Where(x => x.SystemThreatId == row.Id));
            var recommended = await db.TechniqueControls.Where(x => x.AttackTechniqueId == row.AttackTechniqueId).ToListAsync();
            db.ThreatControls.AddRange(recommended.Select(x => new ThreatCisControl { SystemThreatId = row.Id, CisControlId = x.CisControlId, IsRecommended = true }));
            await db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return row;
    }

    public record MappingInput(int BusinessAssetId, int SystemAssetId);
    public async Task<(bool Created, BusinessAssetSystemAsset Value)> AddMapping(MappingInput input)
    {
        if (!await db.BusinessAssets.AnyAsync(x => x.Id == input.BusinessAssetId) || !await db.SystemAssets.AnyAsync(x => x.Id == input.SystemAssetId))
            throw new ValidationException("Ukendt asset.");
        var existing = await db.AssetMappings.FirstOrDefaultAsync(x => x.BusinessAssetId == input.BusinessAssetId && x.SystemAssetId == input.SystemAssetId);
        if (existing != null)
            return (false, existing);
        var row = new BusinessAssetSystemAsset
        {
            BusinessAssetId = input.BusinessAssetId,
            SystemAssetId = input.SystemAssetId
        };
        db.Add(row);
        await db.SaveChangesAsync();
        return (true, row);
    }

    public async Task<(bool Created, SystemAssetAccess Value)> AddAccess(SystemAssetAccess input)
    {
        if (input.SourceSystemAssetId == input.TargetSystemAssetId)
            throw new ValidationException("Et system kan ikke pege på sig selv.");
        if (!await db.SystemAssets.AnyAsync(x => x.Id == input.SourceSystemAssetId) || !await db.SystemAssets.AnyAsync(x => x.Id == input.TargetSystemAssetId))
            throw new ValidationException("Ukendt system.");
        var row = await db.SystemAccess.FirstOrDefaultAsync(x => x.SourceSystemAssetId == input.SourceSystemAssetId && x.TargetSystemAssetId == input.TargetSystemAssetId);
        if (row != null)
        {
            row.Description = input.Description;
            await db.SaveChangesAsync();
            return (false, row);
        }

        input.Id = 0;
        db.Add(input);
        await db.SaveChangesAsync();
        return (true, input);
    }

    public async Task<ThreatCisControl> SetControl(int threatId, int controlId, ThreatCisControl input)
    {
        if (!await db.SystemThreats.AnyAsync(x => x.Id == threatId) || !await db.CisControls.AnyAsync(x => x.Id == controlId))
            throw new KeyNotFoundException("Posten findes ikke.");
        var row = await db.ThreatControls.FindAsync(threatId, controlId);
        if (row == null)
        {
            row = new()
            {
                SystemThreatId = threatId,
                CisControlId = controlId
            };
            db.Add(row);
        }

        row.IsRecommended = input.IsRecommended;
        row.Comment = input.Comment;
        await db.SaveChangesAsync();
        return row;
    }

    public async Task Delete(string resource, int id)
    {
        object? row = resource switch
        {
            "business-processes" => await db.BusinessProcesses.FindAsync(id),
            "business-assets" => await db.BusinessAssets.FindAsync(id),
            "system-assets" => await db.SystemAssets.FindAsync(id),
            "system-threats" => await db.SystemThreats.FindAsync(id),
            "asset-mappings" => await db.AssetMappings.FindAsync(id),
            "system-access" => await db.SystemAccess.FindAsync(id),
            _ => null
        };
        if (row == null)
            throw new KeyNotFoundException("Posten findes ikke.");
        db.Remove(row);
        await db.SaveChangesAsync();
    }
}

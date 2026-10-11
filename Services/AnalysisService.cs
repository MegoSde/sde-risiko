using System.ComponentModel.DataAnnotations;
using AlpacaMedic.Data;
using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace AlpacaMedic.Services;
public class AnalysisService(RiskDb db, CisPriorityService priorities, RiskAnalysisService risks)
{
    public async Task<object> Systems() => await db.SystemAssets.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    public async Task<object> Processes() => await db.BusinessProcesses.AsNoTracking().ToListAsync();
    public record BusinessAssetRow(int Id, string Name, string Description, int[] BusinessProcessIds);
    public class AssetInput
    {
        [Required]
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int? BusinessProcessId { get; set; }
    }

    public async Task<object> Assets()
    {
        var assets = await db.BusinessAssets.AsNoTracking().ToListAsync();
        var memberships = await db.BusinessProcessAssets.AsNoTracking().ToListAsync();
        return assets.Select(asset => new BusinessAssetRow(asset.Id, asset.Name, asset.Description,
            memberships.Where(link => link.BusinessAssetId == asset.Id).Select(link => link.BusinessProcessId).Order().ToArray())).ToArray();
    }
    public async Task<object> BusinessRelations(int? businessAssetId) => await db.BusinessAssetRelations.AsNoTracking()
        .Where(x => !businessAssetId.HasValue || x.BusinessAssetId == businessAssetId.Value).ToListAsync();
    public async Task<object> SystemRelations(int? sourceSystemAssetId) => await db.SystemAssetRelations.AsNoTracking()
        .Where(x => !sourceSystemAssetId.HasValue || x.SourceSystemAssetId == sourceSystemAssetId.Value).ToListAsync();
    public async Task<object> Techniques(string? search)
    {
        var term = (search ?? "").Trim().ToLowerInvariant();
        return await db.AttackTechniques.AsNoTracking()
            .Where(t => term == "" || t.TechniqueId.ToLower().Contains(term) || t.Name.ToLower().Contains(term)
                || t.Tactic.ToLower().Contains(term)
                || db.AttackTechniqueTactics.Any(link => link.AttackTechniqueId == t.Id && db.AttackTactics.Any(tactic =>
                    tactic.Id == link.AttackTacticId && (tactic.ShortName.ToLower().Contains(term) || tactic.Name.ToLower().Contains(term)))))
            .OrderBy(t => t.TechniqueId.ToLower() == term ? 0 : 1).ThenBy(t => t.TechniqueId).Take(25).ToListAsync();
    }
    public async Task<AttackTechnique> Technique(int id) => await db.AttackTechniques.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id)
        ?? throw new KeyNotFoundException("Teknikken findes ikke.");
    public async Task<object> Controls() => await db.CisControls.AsNoTracking().ToListAsync();
    public async Task<object> Recommended() => await db.TechniqueControls.AsNoTracking().ToListAsync();
    public async Task<object> Selected() => await db.ComponentControls.AsNoTracking().ToListAsync();
    public async Task<object> Dashboard()
    {
        var scenarios = await risks.Calculate();
        var exposed = await db.AttackSurfaceEntries.Where(x => x.IsExposed).Select(x => x.SystemAssetId).ToListAsync();
        var components = await db.AttackComponents.CountAsync();
        return new
        {
            AttackSurface = exposed.Count,
            UnassessedEntryPoints = exposed.Count(id => !scenarios.Any(r => r.InitialAttackComponent.SystemAssetId == id)),
            RiskScenarios = scenarios.Count,
            RiskLevels = new[] { "Low", "Medium", "High", "Critical" }
                .Select(level => new { Level = level, Count = scenarios.Count(r => r.RiskLevel == level) }),
            TopRisks = scenarios.OrderByDescending(r => r.RiskScore).ThenBy(r => r.Id).Take(5),
            BusinessProcesses = await db.BusinessProcesses.CountAsync(),
            BusinessAssets = await db.BusinessAssets.CountAsync(),
            SystemAssets = await db.SystemAssets.CountAsync(),
            AttackComponents = components,
            AttackFindings = components,
            AttackConnections = await db.AttackConnections.CountAsync(),
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

    public async Task<BusinessAssetRow> SaveAsset(int? id, AssetInput input)
    {
        var name = input.Name.Trim();
        if (name.Length == 0)
            throw new ValidationException("Navn skal udfyldes.");
        if (input.BusinessProcessId.HasValue && !await db.BusinessProcesses.AnyAsync(x => x.Id == input.BusinessProcessId.Value))
            throw new ValidationException("Vælg en eksisterende proces.");
        var key = BusinessAsset.NameKey(name);
        var existing = await db.BusinessAssets.FirstOrDefaultAsync(x => x.NormalizedName == key);
        var row = id.HasValue ? await db.BusinessAssets.FindAsync(id.Value) : existing ?? new BusinessAsset();
        if (row == null)
            throw new KeyNotFoundException("Asset findes ikke.");
        if (id.HasValue && existing != null && existing.Id != row.Id)
            throw new ValidationException("Et andet asset har allerede dette navn. Vælg et unikt navn.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Reusing a tag adds membership without changing the shared name or description.
        if (id.HasValue || existing == null)
        {
            row.Name = name;
            row.NormalizedName = key;
            row.Description = input.Description ?? row.Description;
        }
        if (!id.HasValue && existing == null)
            db.Add(row);
        try
        {
            await db.SaveChangesAsync();
            if (input.BusinessProcessId.HasValue && !await db.BusinessProcessAssets.AnyAsync(x => x.BusinessProcessId == input.BusinessProcessId.Value && x.BusinessAssetId == row.Id))
            {
                db.BusinessProcessAssets.Add(new BusinessProcessAsset { BusinessProcessId = input.BusinessProcessId.Value, BusinessAssetId = row.Id });
                await db.SaveChangesAsync();
            }
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 or 1555 })
        {
            throw new ValidationException("Navnet eller procestilknytningen findes allerede. Prøv igen.");
        }
        await transaction.CommitAsync();
        var processIds = await db.BusinessProcessAssets.Where(x => x.BusinessAssetId == row.Id).Select(x => x.BusinessProcessId).Order().ToArrayAsync();
        return new BusinessAssetRow(row.Id, row.Name, row.Description, processIds);
    }

    public async Task RemoveAssetFromProcess(int assetId, int processId)
    {
        var link = await db.BusinessProcessAssets.FindAsync(processId, assetId);
        if (link == null)
            throw new KeyNotFoundException("Procestilknytningen findes ikke.");
        db.Remove(link);
        await db.SaveChangesAsync();
    }

    public async Task<SystemAsset> SaveSystem(int? id, SystemAsset input)
    {
        var row = id.HasValue ? await db.SystemAssets.FindAsync(id.Value) : new SystemAsset();
        if (row == null)
            throw new KeyNotFoundException("Posten findes ikke.");
        row.Name = input.Name.Trim();
        if (row.Name.Length == 0)
            throw new ValidationException("Navn skal udfyldes.");
        row.Description = input.Description;
        row.AssetType = input.AssetType;
        if (!id.HasValue)
            db.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    public async Task<BusinessAssetSystemRelation> SaveBusinessRelation(int? id, BusinessAssetSystemRelation input)
    {
        Validator.ValidateObject(input, new ValidationContext(input), validateAllProperties: true);
        if (!await db.BusinessAssets.AnyAsync(x => x.Id == input.BusinessAssetId) || !await db.SystemAssets.AnyAsync(x => x.Id == input.SystemAssetId))
            throw new ValidationException("Vælg eksisterende assets.");
        if (await db.BusinessAssetRelations.AnyAsync(x => x.Id != (id ?? 0) && x.BusinessAssetId == input.BusinessAssetId && x.SystemAssetId == input.SystemAssetId && x.RelationType == input.RelationType))
            throw new ValidationException("Relationen findes allerede.");
        var row = id.HasValue ? await db.BusinessAssetRelations.FindAsync(id.Value) : new BusinessAssetSystemRelation();
        if (row == null)
            throw new KeyNotFoundException("Relationen findes ikke.");
        row.BusinessAssetId = input.BusinessAssetId;
        row.SystemAssetId = input.SystemAssetId;
        row.RelationType = input.RelationType;
        row.Description = input.Description;
        if (!id.HasValue)
            db.Add(row);
        await SaveRelationChanges();
        return row;
    }

    public async Task<SystemAssetRelation> SaveSystemRelation(int? id, SystemAssetRelation input)
    {
        Validator.ValidateObject(input, new ValidationContext(input), validateAllProperties: true);
        if (input.SourceSystemAssetId == input.TargetSystemAssetId)
            throw new ValidationException("Et system kan ikke relateres til sig selv.");
        if (!await db.SystemAssets.AnyAsync(x => x.Id == input.SourceSystemAssetId) || !await db.SystemAssets.AnyAsync(x => x.Id == input.TargetSystemAssetId))
            throw new ValidationException("Vælg eksisterende systemassets.");
        if (await db.SystemAssetRelations.AnyAsync(x => x.Id != (id ?? 0) && x.SourceSystemAssetId == input.SourceSystemAssetId && x.TargetSystemAssetId == input.TargetSystemAssetId && x.RelationType == input.RelationType))
            throw new ValidationException("Relationen findes allerede.");
        var row = id.HasValue ? await db.SystemAssetRelations.FindAsync(id.Value) : new SystemAssetRelation();
        if (row == null)
            throw new KeyNotFoundException("Relationen findes ikke.");
        if (id.HasValue && await db.AttackConnections.AnyAsync(x => x.SystemAssetRelationId == id.Value)
            && (row.SourceSystemAssetId != input.SourceSystemAssetId || row.TargetSystemAssetId != input.TargetSystemAssetId))
            throw new ValidationException("Relationen bruges af Attack Connections. Fjern forbindelserne, før relationens assets ændres.");
        row.SourceSystemAssetId = input.SourceSystemAssetId;
        row.TargetSystemAssetId = input.TargetSystemAssetId;
        row.RelationType = input.RelationType;
        row.Description = input.Description;
        if (!id.HasValue)
            db.Add(row);
        await SaveRelationChanges();
        return row;
    }

    private async Task SaveRelationChanges()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            throw new ValidationException("Relationen findes allerede.");
        }
    }

    public async Task<AttackComponentCisControl> SetControl(int threatId, int controlId, AttackComponentCisControl input)
    {
        if (!await db.AttackComponents.AnyAsync(x => x.Id == threatId) || !await db.CisControls.AnyAsync(x => x.Id == controlId))
            throw new KeyNotFoundException("Posten findes ikke.");
        var row = await db.ComponentControls.FindAsync(threatId, controlId);
        if (row == null)
        {
            row = new()
            {
                AttackComponentId = threatId,
                CisControlId = controlId
            };
            db.Add(row);
        }

        row.IsRecommended = input.IsRecommended;
        row.Comment = input.Comment;
        await db.SaveChangesAsync();
        return row;
    }

    public async Task<object> AttackSurface(int? systemAssetId) => await db.AttackSurfaceEntries.AsNoTracking()
        .Where(x => !systemAssetId.HasValue || x.SystemAssetId == systemAssetId).OrderBy(x => x.Id).ToListAsync();

    public async Task<(bool Created, AttackSurfaceEntry Value)> SaveAttackSurface(int? id, AttackSurfaceEntry input)
    {
        Validator.ValidateObject(input, new ValidationContext(input), true);
        if (!await db.SystemAssets.AnyAsync(x => x.Id == input.SystemAssetId))
            throw new ValidationException("System Asset findes ikke.");
        var row = id.HasValue
            ? await db.AttackSurfaceEntries.FindAsync(id.Value) ?? throw new KeyNotFoundException("Markeringen findes ikke.")
            : await db.AttackSurfaceEntries.FirstOrDefaultAsync(x => x.SystemAssetId == input.SystemAssetId);
        if (await db.AttackSurfaceEntries.AnyAsync(x => x.SystemAssetId == input.SystemAssetId && (row == null || x.Id != row.Id)))
            throw new ValidationException("System Asset findes allerede i Attack Surface.");
        var created = row == null;
        row ??= new AttackSurfaceEntry();
        row.SystemAssetId = input.SystemAssetId;
        row.IsExposed = input.IsExposed;
        row.ExposureType = input.ExposureType;
        row.Description = input.Description;
        if (created) db.Add(row);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        { throw new ValidationException("System Asset findes allerede i Attack Surface. Genindlæs siden."); }
        return (created, row);
    }

    public async Task Delete(string resource, int id)
    {
        object? row = resource switch
        {
            "business-processes" => await db.BusinessProcesses.FindAsync(id),
            "business-assets" => await db.BusinessAssets.FindAsync(id),
            "attack-surface" => await db.AttackSurfaceEntries.FindAsync(id),
            "system-assets" => await db.SystemAssets.FindAsync(id),
            "attack-components" => await db.AttackComponents.FindAsync(id),
            "attack-connections" => await db.AttackConnections.FindAsync(id),
            "business-asset-relations" => await db.BusinessAssetRelations.FindAsync(id),
            "system-asset-relations" => await db.SystemAssetRelations.FindAsync(id),
            _ => null
        };
        if (row == null)
            throw new KeyNotFoundException("Posten findes ikke.");
        db.Remove(row);
        await db.SaveChangesAsync();
    }
}

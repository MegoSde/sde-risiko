using System.ComponentModel.DataAnnotations;
using AlpacaMedic.Data;
using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;

namespace AlpacaMedic.Services;
public class AttackAnalysisService(RiskDb db)
{
    public class AssessmentInput
    {
        [Range(1, 5)] public int Likelihood { get; set; } = 1;
        [Required(AllowEmptyStrings = true), MaxLength(4000)] public string Comment { get; set; } = "";
    }
    public class NextInput : AssessmentInput
    {
        public int AttackTechniqueId { get; set; }
        public int SystemAssetRelationId { get; set; }
    }
    public record Creation(AttackComponent Component, bool ComponentCreated, AttackConnection? Connection, bool ConnectionCreated);

    public async Task<object> Components() => await (from component in db.AttackComponents.AsNoTracking()
        join technique in db.AttackTechniques.AsNoTracking() on component.AttackTechniqueId equals technique.Id
        orderby component.Id
        select new { component.Id, component.SystemAssetId, component.AttackTechniqueId, component.Likelihood, component.Comment, component.IsInitial, Technique = technique }).ToListAsync();

    public async Task<object> Connections(int? fromAttackComponentId, int? toAttackComponentId) => await db.AttackConnections.AsNoTracking()
        .Where(c => (!fromAttackComponentId.HasValue || c.FromAttackComponentId == fromAttackComponentId)
            && (!toAttackComponentId.HasValue || c.ToAttackComponentId == toAttackComponentId)).OrderBy(c => c.Id).ToListAsync();

    public async Task<object> NextAssets(int id)
    {
        var component = await db.AttackComponents.FindAsync(id) ?? throw new KeyNotFoundException("Komponenten findes ikke.");
        return await (from relation in db.SystemAssetRelations.AsNoTracking()
            join asset in db.SystemAssets.AsNoTracking() on relation.TargetSystemAssetId equals asset.Id
            where relation.SourceSystemAssetId == component.SystemAssetId
            orderby asset.Name, relation.RelationType
            select new { SystemAssetRelationId = relation.Id, relation.SourceSystemAssetId, relation.TargetSystemAssetId, relation.RelationType, relation.Description, Asset = asset }).ToListAsync();
    }

    private async Task<(AttackComponent Component, bool Created)> GetOrCreate(int assetId, int techniqueId, AssessmentInput input)
    {
        if (!await db.SystemAssets.AnyAsync(a => a.Id == assetId) || !await db.AttackTechniques.AnyAsync(t => t.Id == techniqueId))
            throw new ValidationException("Vælg et eksisterende asset og en ATT&CK-teknik.");
        var existing = await db.AttackComponents.SingleOrDefaultAsync(c => c.SystemAssetId == assetId && c.AttackTechniqueId == techniqueId);
        if (existing != null) return (existing, false); // Shared assessment must never be overwritten by another incoming path.
        var component = new AttackComponent { SystemAssetId = assetId, AttackTechniqueId = techniqueId, Likelihood = input.Likelihood, Comment = input.Comment };
        db.AttackComponents.Add(component);
        await db.SaveChangesAsync();
        var suggestions = await db.TechniqueControls.Where(c => c.AttackTechniqueId == techniqueId).ToListAsync();
        db.ComponentControls.AddRange(suggestions.Select(c => new AttackComponentCisControl { AttackComponentId = component.Id, CisControlId = c.CisControlId, IsRecommended = true }));
        await db.SaveChangesAsync();
        return (component, true);
    }

    public async Task<Creation> Start(AttackComponent input)
    {
        Validator.ValidateObject(input, new ValidationContext(input), true);
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await db.AttackSurfaceEntries.AnyAsync(e => e.SystemAssetId == input.SystemAssetId && e.IsExposed))
            throw new ValidationException("Et startpunkt skal være et valgt Attack Surface-asset.");
        var (component, created) = await GetOrCreate(input.SystemAssetId, input.AttackTechniqueId, new AssessmentInput { Likelihood = input.Likelihood, Comment = input.Comment });
        component.IsInitial = true;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(component, created, null, false);
    }

    public async Task<Creation> Next(int fromId, NextInput input)
    {
        Validator.ValidateObject(input, new ValidationContext(input), true);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var source = await db.AttackComponents.FindAsync(fromId) ?? throw new KeyNotFoundException("Komponenten findes ikke.");
        var relation = await db.SystemAssetRelations.FindAsync(input.SystemAssetRelationId);
        if (relation == null || relation.SourceSystemAssetId != source.SystemAssetId)
            throw new ValidationException("Vælg en udgående Asset Model-relation fra komponentens asset.");
        var (component, created) = await GetOrCreate(relation.TargetSystemAssetId, input.AttackTechniqueId, input);
        var (connection, connected) = await Connect(new AttackConnection { FromAttackComponentId = fromId, ToAttackComponentId = component.Id, SystemAssetRelationId = relation.Id });
        await transaction.CommitAsync();
        return new(component, created, connection, connected);
    }

    public async Task<(AttackConnection Connection, bool Created)> Connect(AttackConnection input)
    {
        var source = await db.AttackComponents.FindAsync(input.FromAttackComponentId);
        var target = await db.AttackComponents.FindAsync(input.ToAttackComponentId);
        var relation = await db.SystemAssetRelations.FindAsync(input.SystemAssetRelationId);
        if (source == null || target == null || relation == null || source.Id == target.Id
            || relation.SourceSystemAssetId != source.SystemAssetId || relation.TargetSystemAssetId != target.SystemAssetId)
            throw new ValidationException("Forbindelsen skal følge en udgående Asset Model-relation mellem komponenternes assets.");
        var existing = await db.AttackConnections.SingleOrDefaultAsync(c => c.FromAttackComponentId == source.Id && c.ToAttackComponentId == target.Id && c.SystemAssetRelationId == relation.Id);
        if (existing != null) return (existing, false);
        var connection = new AttackConnection { FromAttackComponentId = source.Id, ToAttackComponentId = target.Id, SystemAssetRelationId = relation.Id };
        db.AttackConnections.Add(connection);
        await db.SaveChangesAsync();
        return (connection, true);
    }

    public async Task<(AttackConnection Connection, bool Created)> AddConnection(AttackConnection input)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await Connect(input);
        await transaction.CommitAsync();
        return result;
    }

    public async Task<AttackComponent> Update(int id, AssessmentInput input)
    {
        Validator.ValidateObject(input, new ValidationContext(input), true);
        var component = await db.AttackComponents.FindAsync(id) ?? throw new KeyNotFoundException("Komponenten findes ikke.");
        component.Likelihood = input.Likelihood; component.Comment = input.Comment;
        await db.SaveChangesAsync();
        return component;
    }
}

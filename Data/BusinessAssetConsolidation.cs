using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;

namespace AlpacaMedic.Data;

// Runs inside the startup seed transaction after the schema migration. Use .NET's
// Unicode casing so Danish names are merged consistently with subsequent tag creation.
public static class BusinessAssetConsolidation
{
    public static async Task Initialize(RiskDb db)
    {
        if (!await db.BusinessAssets.AnyAsync(asset => asset.NormalizedName == null))
            return;
        var assets = await db.BusinessAssets.OrderBy(asset => asset.Id).ToListAsync();
        var memberships = await db.BusinessProcessAssets.ToListAsync();
        var relations = await db.BusinessAssetRelations.ToListAsync();
        foreach (var group in assets.GroupBy(asset => BusinessAsset.NameKey(asset.Name)))
        {
            var canonical = group.First();
            var ids = group.Select(asset => asset.Id).ToHashSet();
            if (ids.Count > 1)
            {
                var oldMemberships = memberships.Where(link => ids.Contains(link.BusinessAssetId)).ToArray();
                var oldRelations = relations.Where(link => ids.Contains(link.BusinessAssetId)).ToArray();
                db.BusinessProcessAssets.RemoveRange(oldMemberships);
                db.BusinessAssetRelations.RemoveRange(oldRelations);
                await db.SaveChangesAsync();
                db.BusinessAssets.RemoveRange(group.Skip(1));
                await db.SaveChangesAsync();
                db.BusinessProcessAssets.AddRange(oldMemberships.Select(link => link.BusinessProcessId).Distinct()
                    .Select(processId => new BusinessProcessAsset { BusinessAssetId = canonical.Id, BusinessProcessId = processId }));
                db.BusinessAssetRelations.AddRange(oldRelations.GroupBy(link => new { link.SystemAssetId, link.RelationType })
                    .Select(links => new BusinessAssetSystemRelation
                    {
                        Id = links.Min(link => link.Id),
                        BusinessAssetId = canonical.Id,
                        SystemAssetId = links.Key.SystemAssetId,
                        RelationType = links.Key.RelationType,
                        Description = JoinDescriptions(links.Select(link => link.Description))
                    }));
            }
            canonical.Name = canonical.Name.Trim();
            canonical.NormalizedName = group.Key;
            canonical.Description = JoinDescriptions(group.Select(asset => asset.Description));
            await db.SaveChangesAsync();
        }
    }

    private static string JoinDescriptions(IEnumerable<string> descriptions) =>
        string.Join("\n", descriptions.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct());
}

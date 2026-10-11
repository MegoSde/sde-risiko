using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;

namespace AlpacaMedic.Data;
public class RiskDb(DbContextOptions<RiskDb> options) : DbContext(options)
{
    public DbSet<BusinessProcess> BusinessProcesses => Set<BusinessProcess>();
    public DbSet<BusinessAsset> BusinessAssets => Set<BusinessAsset>();
    public DbSet<BusinessProcessAsset> BusinessProcessAssets => Set<BusinessProcessAsset>();
    public DbSet<SystemAsset> SystemAssets => Set<SystemAsset>();
    public DbSet<BusinessAssetSystemRelation> BusinessAssetRelations => Set<BusinessAssetSystemRelation>();
    public DbSet<SystemAssetRelation> SystemAssetRelations => Set<SystemAssetRelation>();
    public DbSet<AttackTactic> AttackTactics => Set<AttackTactic>();
    public DbSet<AttackTechniqueTactic> AttackTechniqueTactics => Set<AttackTechniqueTactic>();
    public DbSet<AttackTechnique> AttackTechniques => Set<AttackTechnique>();
    public DbSet<AttackComponent> AttackComponents => Set<AttackComponent>();
    public DbSet<AttackConnection> AttackConnections => Set<AttackConnection>();
    public DbSet<CisControl> CisControls => Set<CisControl>();
    public DbSet<AttackTechniqueCisControl> TechniqueControls => Set<AttackTechniqueCisControl>();
    public DbSet<AttackComponentCisControl> ComponentControls => Set<AttackComponentCisControl>();

    public DbSet<AttackSurfaceEntry> AttackSurfaceEntries => Set<AttackSurfaceEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AttackTechnique>().HasIndex(x => x.TechniqueId).IsUnique();
        b.Entity<AttackTactic>().HasIndex(x => x.ShortName).IsUnique();
        b.Entity<AttackTechniqueTactic>().HasKey(x => new { x.AttackTechniqueId, x.AttackTacticId });
        b.Entity<AttackTechniqueTactic>().HasOne<AttackTechnique>().WithMany().HasForeignKey(x => x.AttackTechniqueId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AttackTechniqueTactic>().HasOne<AttackTactic>().WithMany().HasForeignKey(x => x.AttackTacticId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AttackTechniqueCisControl>().HasKey(x => new { x.AttackTechniqueId, x.CisControlId });
        b.Entity<AttackComponentCisControl>().HasKey(x => new { x.AttackComponentId, x.CisControlId });
        b.Entity<AttackSurfaceEntry>().HasIndex(x => x.SystemAssetId).IsUnique();
        b.Entity<AttackSurfaceEntry>().HasOne<SystemAsset>().WithMany().HasForeignKey(x => x.SystemAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BusinessAsset>().HasIndex(x => x.NormalizedName).IsUnique();
        b.Entity<BusinessProcessAsset>().HasKey(x => new { x.BusinessProcessId, x.BusinessAssetId });
        b.Entity<BusinessProcessAsset>().HasOne<BusinessProcess>().WithMany().HasForeignKey(x => x.BusinessProcessId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BusinessProcessAsset>().HasOne<BusinessAsset>().WithMany().HasForeignKey(x => x.BusinessAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BusinessAssetSystemRelation>().HasOne<BusinessAsset>().WithMany().HasForeignKey(x => x.BusinessAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BusinessAssetSystemRelation>().HasOne<SystemAsset>().WithMany().HasForeignKey(x => x.SystemAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BusinessAssetSystemRelation>().HasIndex(x => new { x.BusinessAssetId, x.SystemAssetId, x.RelationType }).IsUnique();
        b.Entity<SystemAssetRelation>().HasOne<SystemAsset>().WithMany().HasForeignKey(x => x.SourceSystemAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SystemAssetRelation>().HasOne<SystemAsset>().WithMany().HasForeignKey(x => x.TargetSystemAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SystemAssetRelation>().HasIndex(x => new { x.SourceSystemAssetId, x.TargetSystemAssetId, x.RelationType }).IsUnique();
        b.Entity<AttackComponent>().HasIndex(x => new { x.SystemAssetId, x.AttackTechniqueId }).IsUnique();
        b.Entity<AttackConnection>().HasIndex(x => new { x.FromAttackComponentId, x.ToAttackComponentId, x.SystemAssetRelationId }).IsUnique();
        b.Entity<AttackConnection>().HasOne<AttackComponent>().WithMany().HasForeignKey(x => x.FromAttackComponentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AttackConnection>().HasOne<AttackComponent>().WithMany().HasForeignKey(x => x.ToAttackComponentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AttackConnection>().HasOne<SystemAssetRelation>().WithMany().HasForeignKey(x => x.SystemAssetRelationId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AttackComponent>().HasOne<SystemAsset>().WithMany().HasForeignKey(x => x.SystemAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AttackComponent>().HasOne<AttackTechnique>().WithMany().HasForeignKey(x => x.AttackTechniqueId);
        b.Entity<AttackTechniqueCisControl>().HasOne<AttackTechnique>().WithMany().HasForeignKey(x => x.AttackTechniqueId);
        b.Entity<AttackTechniqueCisControl>().HasOne<CisControl>().WithMany().HasForeignKey(x => x.CisControlId);
        b.Entity<AttackComponentCisControl>().HasOne<AttackComponent>().WithMany().HasForeignKey(x => x.AttackComponentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AttackComponentCisControl>().HasOne<CisControl>().WithMany().HasForeignKey(x => x.CisControlId);
    }
}

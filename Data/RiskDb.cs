using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;

namespace AlpacaMedic.Data;
public class RiskDb(DbContextOptions<RiskDb> options) : DbContext(options)
{
    public DbSet<BusinessProcess> BusinessProcesses => Set<BusinessProcess>();
    public DbSet<BusinessAsset> BusinessAssets => Set<BusinessAsset>();
    public DbSet<SystemAsset> SystemAssets => Set<SystemAsset>();
    public DbSet<BusinessAssetSystemAsset> AssetMappings => Set<BusinessAssetSystemAsset>();
    public DbSet<SystemAssetAccess> SystemAccess => Set<SystemAssetAccess>();
    public DbSet<AttackTechnique> AttackTechniques => Set<AttackTechnique>();
    public DbSet<SystemThreat> SystemThreats => Set<SystemThreat>();
    public DbSet<CisControl> CisControls => Set<CisControl>();
    public DbSet<AttackTechniqueCisControl> TechniqueControls => Set<AttackTechniqueCisControl>();
    public DbSet<ThreatCisControl> ThreatControls => Set<ThreatCisControl>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AttackTechniqueCisControl>().HasKey(x => new { x.AttackTechniqueId, x.CisControlId });
        b.Entity<ThreatCisControl>().HasKey(x => new { x.SystemThreatId, x.CisControlId });
        b.Entity<BusinessAsset>().HasOne<BusinessProcess>().WithMany().HasForeignKey(x => x.BusinessProcessId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BusinessAssetSystemAsset>().HasOne<BusinessAsset>().WithMany().HasForeignKey(x => x.BusinessAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BusinessAssetSystemAsset>().HasOne<SystemAsset>().WithMany().HasForeignKey(x => x.SystemAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BusinessAssetSystemAsset>().HasIndex(x => new { x.BusinessAssetId, x.SystemAssetId }).IsUnique();
        b.Entity<SystemAssetAccess>().HasOne<SystemAsset>().WithMany().HasForeignKey(x => x.SourceSystemAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SystemAssetAccess>().HasOne<SystemAsset>().WithMany().HasForeignKey(x => x.TargetSystemAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SystemAssetAccess>().HasIndex(x => new { x.SourceSystemAssetId, x.TargetSystemAssetId }).IsUnique();
        b.Entity<SystemThreat>().HasOne<SystemAsset>().WithMany().HasForeignKey(x => x.SystemAssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SystemThreat>().HasOne<AttackTechnique>().WithMany().HasForeignKey(x => x.AttackTechniqueId);
        b.Entity<AttackTechniqueCisControl>().HasOne<AttackTechnique>().WithMany().HasForeignKey(x => x.AttackTechniqueId);
        b.Entity<AttackTechniqueCisControl>().HasOne<CisControl>().WithMany().HasForeignKey(x => x.CisControlId);
        b.Entity<ThreatCisControl>().HasOne<SystemThreat>().WithMany().HasForeignKey(x => x.SystemThreatId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ThreatCisControl>().HasOne<CisControl>().WithMany().HasForeignKey(x => x.CisControlId);
    }
}

using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;

namespace AlpacaMedic.Data;

public static class SeedData
{
    // Local CIS teaching catalog and case. Mappings are suggestions, not official CIS/MITRE mappings.
    public static async Task Initialize(RiskDb db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await BusinessAssetConsolidation.Initialize(db);
        // Only seed the case on a pristine installation; never mix demo rows into an existing analysis.
        var seedDemo = !await db.CisControls.AnyAsync() && !await db.BusinessProcesses.AnyAsync() && !await db.BusinessAssets.AnyAsync() && !await db.SystemAssets.AnyAsync();
        if (!await db.CisControls.AnyAsync())
        {
            db.CisControls.Add(new CisControl { Id = 1, Number = 1,
                Name = "Inventory and Control of Enterprise Assets",
                Description = "Registrér og vedligehold overblik over organisationens enheder." });
            db.CisControls.Add(new CisControl { Id = 2, Number = 2,
                Name = "Inventory and Control of Software Assets",
                Description = "Registrér godkendt software og fjern uønskede programmer." });
            db.CisControls.Add(new CisControl { Id = 3, Number = 3,
                Name = "Data Protection",
                Description = "Klassificér og beskyt data gennem hele livscyklussen." });
            db.CisControls.Add(new CisControl { Id = 4, Number = 4,
                Name = "Secure Configuration of Enterprise Assets and Software",
                Description = "Anvend og vedligehold sikre konfigurationer." });
            db.CisControls.Add(new CisControl { Id = 5, Number = 5,
                Name = "Account Management",
                Description = "Administrér konti og deres livscyklus." });
            db.CisControls.Add(new CisControl { Id = 6, Number = 6,
                Name = "Access Control Management",
                Description = "Begræns adgange og anvend stærk autentifikation." });
            db.CisControls.Add(new CisControl { Id = 7, Number = 7,
                Name = "Continuous Vulnerability Management",
                Description = "Identificér og afhjælp sårbarheder løbende." });
            db.CisControls.Add(new CisControl { Id = 8, Number = 8,
                Name = "Audit Log Management",
                Description = "Indsaml, beskyt og gennemgå hændelseslogs." });
            db.CisControls.Add(new CisControl { Id = 9, Number = 9,
                Name = "Email and Web Browser Protections",
                Description = "Beskyt e-mail og browser mod skadeligt indhold." });
            db.CisControls.Add(new CisControl { Id = 10, Number = 10,
                Name = "Malware Defenses",
                Description = "Forebyg og detektér skadelig software." });
            db.CisControls.Add(new CisControl { Id = 11, Number = 11,
                Name = "Data Recovery",
                Description = "Sikr afprøvet gendannelse af data og systemer." });
            db.CisControls.Add(new CisControl { Id = 12, Number = 12,
                Name = "Network Infrastructure Management",
                Description = "Administrér og beskyt netværksinfrastrukturen." });
            db.CisControls.Add(new CisControl { Id = 13, Number = 13,
                Name = "Network Monitoring and Defense",
                Description = "Overvåg netværket og reagér på mistænkelig aktivitet." });
            db.CisControls.Add(new CisControl { Id = 14, Number = 14,
                Name = "Security Awareness and Skills Training",
                Description = "Træn medarbejdere i sikker adfærd." });
            db.CisControls.Add(new CisControl { Id = 15, Number = 15,
                Name = "Service Provider Management",
                Description = "Vurdér og følg op på leverandørers sikkerhed." });
            db.CisControls.Add(new CisControl { Id = 16, Number = 16,
                Name = "Application Software Security",
                Description = "Integrér sikkerhed i udvikling og drift af applikationer." });
            db.CisControls.Add(new CisControl { Id = 17, Number = 17,
                Name = "Incident Response Management",
                Description = "Forbered og øv håndtering af sikkerhedshændelser." });
            db.CisControls.Add(new CisControl { Id = 18, Number = 18,
                Name = "Penetration Testing",
                Description = "Afprøv sikkerheden med kontrollerede angreb." });
            await db.SaveChangesAsync();
        }

        // Teaching suggestions are separate from the official MITRE catalog.
        var suggestions = new Dictionary<string, int[]>
        {
            ["T1190"] = [7, 16, 18], ["T1078"] = [5, 6, 8], ["T1059"] = [2, 4, 10, 13]
        };
        foreach (var (techniqueId, controls) in suggestions)
        {
            var technique = await db.AttackTechniques.SingleOrDefaultAsync(x => x.TechniqueId == techniqueId);
            if (technique == null) continue;
            foreach (var controlId in controls)
                if (!await db.TechniqueControls.AnyAsync(x => x.AttackTechniqueId == technique.Id && x.CisControlId == controlId))
                    db.TechniqueControls.Add(new AttackTechniqueCisControl { AttackTechniqueId = technique.Id, CisControlId = controlId });
        }
        await db.SaveChangesAsync();
        if (seedDemo)
            await SeedAssetModel(db);
        await transaction.CommitAsync();
    }

    private static async Task SeedAssetModel(RiskDb db)
    {
        var clinical = new BusinessProcess { Name = "Clinical Care and Follow-up", Confidentiality = 3, Integrity = 3, Availability = 3 };
        var safety = new BusinessProcess { Name = "Adverse Event Reporting", Confidentiality = 3, Integrity = 3, Availability = 2 };
        var research = new BusinessProcess { Name = "Research", Confidentiality = 3, Integrity = 3, Availability = 2 };
        var employees = new BusinessProcess { Name = "Employee Administration", Confidentiality = 3, Integrity = 2, Availability = 2 };
        foreach (var process in new[] { clinical, safety, research, employees })
        {
            process.Consequence = Services.ConsequenceService.Calculate(process.Confidentiality, process.Integrity, process.Availability);
            db.BusinessProcesses.Add(process);
        }
        await db.SaveChangesAsync();
        var business = new[]
        {
            new BusinessAsset { Name = "Clinical Patient Data", NormalizedName = BusinessAsset.NameKey("Clinical Patient Data") },
            new BusinessAsset { Name = "Patient Identity", NormalizedName = BusinessAsset.NameKey("Patient Identity") },
            new BusinessAsset { Name = "Adverse Event Data", NormalizedName = BusinessAsset.NameKey("Adverse Event Data") },
            new BusinessAsset { Name = "Research Data", NormalizedName = BusinessAsset.NameKey("Research Data") },
            new BusinessAsset { Name = "Employee Identity", NormalizedName = BusinessAsset.NameKey("Employee Identity") }
        };
        db.BusinessAssets.AddRange(business);
        var systems = new[]
        {
            new SystemAsset { Name = "PatientWebApp", AssetType = "Application" },
            new SystemAsset { Name = "WEB01", AssetType = "Host" },
            new SystemAsset { Name = "PostgreSQLDB01", AssetType = "Service" },
            new SystemAsset { Name = "DB01", AssetType = "Host" },
            new SystemAsset { Name = "FreeIPA", AssetType = "Service" },
            new SystemAsset { Name = "IPA01", AssetType = "Host" },
            new SystemAsset { Name = "WAZUH", AssetType = "Service" },
            new SystemAsset { Name = "WAZUH01", AssetType = "Host" },
            new SystemAsset { Name = "DMZ", AssetType = "Network" },
            new SystemAsset { Name = "OfficeNetwork", AssetType = "Network" },
            new SystemAsset { Name = "OfficePC", AssetType = "Endpoint" },
            new SystemAsset { Name = "Firewall", AssetType = "Network" },
            new SystemAsset { Name = "CloudMail", AssetType = "CloudService" }
        };
        db.SystemAssets.AddRange(systems);
        await db.SaveChangesAsync();
        var processIds = new[] { clinical.Id, clinical.Id, safety.Id, research.Id, employees.Id };
        for (var i = 0; i < business.Length; i++)
            db.BusinessProcessAssets.Add(new BusinessProcessAsset { BusinessAssetId = business[i].Id, BusinessProcessId = processIds[i] });
        var systemByName = systems.ToDictionary(x => x.Name);
        foreach (var asset in business)
            db.BusinessAssetRelations.Add(new BusinessAssetSystemRelation
            {
                BusinessAssetId = asset.Id,
                SystemAssetId = systemByName[asset.Name == "Employee Identity" ? "FreeIPA" : "PostgreSQLDB01"].Id,
                RelationType = asset.Name == "Employee Identity" ? "ProcessedBy" : "StoredIn"
            });
        var relations = new (string Source, string Type, string Target)[]
        {
            ("PatientWebApp", "HostedOn", "WEB01"),
            ("PatientWebApp", "ConnectsTo", "PostgreSQLDB01"),
            ("PatientWebApp", "AuthenticatesVia", "FreeIPA"),
            ("PostgreSQLDB01", "HostedOn", "DB01"),
            ("FreeIPA", "HostedOn", "IPA01"),
            ("WAZUH", "HostedOn", "WAZUH01"),
            ("WEB01", "LocatedIn", "DMZ"),
            ("DB01", "LocatedIn", "DMZ"),
            ("IPA01", "LocatedIn", "DMZ"),
            ("OfficePC", "LocatedIn", "OfficeNetwork")
        };
        foreach (var (source, type, target) in relations)
            db.SystemAssetRelations.Add(new SystemAssetRelation
            {
                SourceSystemAssetId = systemByName[source].Id,
                TargetSystemAssetId = systemByName[target].Id,
                RelationType = type
            });
        await db.SaveChangesAsync();
    }

}

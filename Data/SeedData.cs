using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;

namespace AlpacaMedic.Data;

public static class SeedData
{
    // Small, local teaching catalog. Mappings are suggestions, not official CIS/MITRE mappings.
    public static async Task Initialize(RiskDb db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
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

        if (!await db.AttackTechniques.AnyAsync())
        {
            db.AttackTechniques.Add(new AttackTechnique
            {
                Id = 1, TechniqueId = "T1190",
                Name = "Exploit Public-Facing Application",
                Tactic = "Initial Access",
                Description = "Angriberen udnytter en sårbarhed i en offentligt tilgængelig applikation.",
                MitreUrl = "https://attack.mitre.org/techniques/T1190/"
            });
            db.AttackTechniques.Add(new AttackTechnique
            {
                Id = 2, TechniqueId = "T1078",
                Name = "Valid Accounts",
                Tactic = "Initial Access, Persistence, Privilege Escalation, Defense Evasion",
                Description = "Angriberen misbruger gyldige konti til adgang eller fortsat tilstedeværelse.",
                MitreUrl = "https://attack.mitre.org/techniques/T1078/"
            });
            db.AttackTechniques.Add(new AttackTechnique
            {
                Id = 3, TechniqueId = "T1059",
                Name = "Command and Scripting Interpreter",
                Tactic = "Execution",
                Description = "Angriberen bruger kommandofortolkere eller scripts til at udføre handlinger.",
                MitreUrl = "https://attack.mitre.org/techniques/T1059/"
            });
            await db.SaveChangesAsync();

            db.TechniqueControls.Add(new AttackTechniqueCisControl { AttackTechniqueId = 1, CisControlId = 7 });
            db.TechniqueControls.Add(new AttackTechniqueCisControl { AttackTechniqueId = 1, CisControlId = 16 });
            db.TechniqueControls.Add(new AttackTechniqueCisControl { AttackTechniqueId = 1, CisControlId = 18 });
            db.TechniqueControls.Add(new AttackTechniqueCisControl { AttackTechniqueId = 2, CisControlId = 5 });
            db.TechniqueControls.Add(new AttackTechniqueCisControl { AttackTechniqueId = 2, CisControlId = 6 });
            db.TechniqueControls.Add(new AttackTechniqueCisControl { AttackTechniqueId = 2, CisControlId = 8 });
            db.TechniqueControls.Add(new AttackTechniqueCisControl { AttackTechniqueId = 3, CisControlId = 2 });
            db.TechniqueControls.Add(new AttackTechniqueCisControl { AttackTechniqueId = 3, CisControlId = 4 });
            db.TechniqueControls.Add(new AttackTechniqueCisControl { AttackTechniqueId = 3, CisControlId = 10 });
            db.TechniqueControls.Add(new AttackTechniqueCisControl { AttackTechniqueId = 3, CisControlId = 13 });
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
    }
}

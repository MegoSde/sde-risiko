using System.Text.Json;
using AlpacaMedic.Models;
using Microsoft.EntityFrameworkCore;

namespace AlpacaMedic.Data;

public class AttackImporter
{
    public static async Task Initialize(RiskDb db, string path, ILogger<AttackImporter> logger)
    {
        var count = await db.AttackTechniques.CountAsync();
        if (count > 0)
        {
            logger.LogInformation("MITRE ATT&CK catalog already imported: {Count} techniques", count);
            return;
        }
        logger.LogInformation("Importing MITRE ATT&CK catalog...");
        // Read once at startup; STIX never becomes a runtime query source.
        await using var file = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(file);
        var objects = document.RootElement.GetProperty("objects");
        static string Text(JsonElement obj, string key) => obj.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
        static bool Flag(JsonElement obj, string key) => obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
        static bool Active(JsonElement obj) => !Flag(obj, "revoked") && !Flag(obj, "x_mitre_deprecated");
        var tacticNames = objects.EnumerateArray().Where(o => Text(o, "type") == "x-mitre-tactic" && Active(o))
            .ToDictionary(o => Text(o, "x_mitre_shortname"), o => Text(o, "name"));
        var techniques = new List<(AttackTechnique Technique, string[] Phases)>();
        var seen = new HashSet<string>();
        foreach (var obj in objects.EnumerateArray())
        {
            if (Text(obj, "type") != "attack-pattern" || !Active(obj)) continue;
            if (obj.TryGetProperty("x_mitre_domains", out var domains) && !domains.EnumerateArray().Any(d => d.GetString() == "enterprise-attack")) continue;
            if (!obj.TryGetProperty("external_references", out var references)) continue;
            var reference = references.EnumerateArray().FirstOrDefault(r => Text(r, "source_name") == "mitre-attack" && Text(r, "external_id").StartsWith("T", StringComparison.Ordinal));
            if (reference.ValueKind == JsonValueKind.Undefined) continue;
            var techniqueId = Text(reference, "external_id");
            if (!seen.Add(techniqueId)) continue;
            var phases = obj.TryGetProperty("kill_chain_phases", out var chain)
                ? chain.EnumerateArray().Where(p => Text(p, "kill_chain_name") == "mitre-attack").Select(p => Text(p, "phase_name")).Where(p => p.Length > 0).Distinct().ToArray()
                : [];
            foreach (var phase in phases) tacticNames.TryAdd(phase, phase.Replace('-', ' '));
            var sub = Flag(obj, "x_mitre_is_subtechnique");
            techniques.Add((new AttackTechnique
            {
                TechniqueId = techniqueId, Name = Text(obj, "name"), Description = Text(obj, "description"),
                MitreUrl = Text(reference, "url"), IsSubTechnique = sub,
                ParentTechniqueId = sub && techniqueId.Contains('.') ? techniqueId.Split('.')[0] : null,
                Platforms = obj.TryGetProperty("x_mitre_platforms", out var platforms) ? string.Join(";", platforms.EnumerateArray().Select(p => p.GetString())) : "",
                Tactic = string.Join(", ", phases.Select(p => tacticNames[p]))
            }, phases));
        }
        if (techniques.Count == 0) throw new InvalidDataException("The local Enterprise ATT&CK file contains no active techniques.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var tactics = await db.AttackTactics.ToDictionaryAsync(t => t.ShortName);
        foreach (var (shortName, name) in tacticNames)
            if (!tactics.ContainsKey(shortName))
            {
                var tactic = new AttackTactic { ShortName = shortName, Name = name };
                tactics.Add(shortName, tactic); db.AttackTactics.Add(tactic);
            }
        db.AttackTechniques.AddRange(techniques.Select(t => t.Technique));
        await db.SaveChangesAsync();
        foreach (var (technique, phases) in techniques)
            foreach (var phase in phases)
                db.AttackTechniqueTactics.Add(new AttackTechniqueTactic { AttackTechniqueId = technique.Id, AttackTacticId = tactics[phase].Id });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogInformation("Imported {Techniques} techniques and {Tactics} tactics", techniques.Count, tactics.Count);
    }
}

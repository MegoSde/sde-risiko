using System.ComponentModel.DataAnnotations;

namespace AlpacaMedic.Models;
public class BusinessProcess
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    [Range(1, 3)]
    public int Confidentiality { get; set; } = 1;

    [Range(1, 3)]
    public int Integrity { get; set; } = 1;

    [Range(1, 3)]
    public int Availability { get; set; } = 1;
    public decimal Consequence { get; set; }
}

public class BusinessAsset
{
    public int Id { get; set; }
    public int BusinessProcessId { get; set; }

    [Required]
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class SystemAsset
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    [RegularExpression("Server|Network|Endpoint|Cloud|PersonAccount|Other")]
    public string AssetType { get; set; } = "Server";
    public string Zone { get; set; } = "";

    [Range(1, 5)]
    public decimal BlastRadius { get; set; } = 1;
    public string BlastRadiusReason { get; set; } = "";
}

public class BusinessAssetSystemAsset
{
    public int Id { get; set; }
    public int BusinessAssetId { get; set; }
    public int SystemAssetId { get; set; }
}

public class SystemAssetAccess
{
    public int Id { get; set; }
    public int SourceSystemAssetId { get; set; }
    public int TargetSystemAssetId { get; set; }
    public string Description { get; set; } = "";
}

public class AttackTechnique
{
    public int Id { get; set; }
    public string TechniqueId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Tactic { get; set; } = "";
    public string Description { get; set; } = "";
    public string MitreUrl { get; set; } = "";
}

public class SystemThreat
{
    public int Id { get; set; }
    public int SystemAssetId { get; set; }
    public int AttackTechniqueId { get; set; }

    [Required]
    public string Scenario { get; set; } = "";

    [Range(1, 5)]
    public int Likelihood { get; set; } = 1;
}

public class CisControl
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class AttackTechniqueCisControl
{
    public int AttackTechniqueId { get; set; }
    public int CisControlId { get; set; }
}

public class ThreatCisControl
{
    public int SystemThreatId { get; set; }
    public int CisControlId { get; set; }
    public bool IsRecommended { get; set; }
    public string Comment { get; set; } = "";
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
    [JsonIgnore]
    public string? NormalizedName { get; set; }

    public static string NameKey(string name) => name.Trim().ToUpperInvariant();

    [Required]
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class BusinessProcessAsset
{
    public int BusinessProcessId { get; set; }
    public int BusinessAssetId { get; set; }
}

public class SystemAsset
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    [Required, RegularExpression("Data|Application|Service|Host|Network|Endpoint|Identity|CloudService|Person|External|Other")]
    public string AssetType { get; set; } = "Host";


}

public class BusinessAssetSystemRelation
{
    public int Id { get; set; }
    public int BusinessAssetId { get; set; }
    public int SystemAssetId { get; set; }

    [Required, RegularExpression("StoredIn|ProcessedBy|UsedBy|ProtectedBy|DependsOn|RepresentedBy|Other")]
    public string RelationType { get; set; } = "DependsOn";
    public string Description { get; set; } = "";
}

public class SystemAssetRelation
{
    public int Id { get; set; }
    public int SourceSystemAssetId { get; set; }
    public int TargetSystemAssetId { get; set; }

    [Required, RegularExpression("HostedOn|LocatedIn|ConnectsTo|AuthenticatesVia|AccessedVia|AdministeredFrom|Uses|DependsOn|MemberOf|ProtectedBy|RoutesThrough|Other")]
    public string RelationType { get; set; } = "DependsOn";
    public string Description { get; set; } = "";
}

public class AttackTechnique
{
    public int Id { get; set; }
    public string TechniqueId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsSubTechnique { get; set; }
    public string? ParentTechniqueId { get; set; }
    public string Platforms { get; set; } = "";
    public string Tactic { get; set; } = "";
    public string Description { get; set; } = "";
    public string MitreUrl { get; set; } = "";
}

public class AttackComponent
{
    public int Id { get; set; }
    public bool IsInitial { get; set; }
    public int SystemAssetId { get; set; }
    public int AttackTechniqueId { get; set; }

    [Required(AllowEmptyStrings = true), MaxLength(4000)]
    public string Comment { get; set; } = "";

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

public class AttackComponentCisControl
{
    public int AttackComponentId { get; set; }
    public int CisControlId { get; set; }
    public bool IsRecommended { get; set; }
    public string Comment { get; set; } = "";
}

public class AttackSurfaceEntry
{
    public int Id { get; set; }
    public int SystemAssetId { get; set; }
    public bool IsExposed { get; set; }
    [Required, RegularExpression("Internet|RemoteAccess|InternalNetwork|UserInteraction|ThirdParty|Cloud|Physical|Other")]
    public string ExposureType { get; set; } = "Other";
    [Required(AllowEmptyStrings = true), MaxLength(2000)]
    public string Description { get; set; } = "";
}

public class AttackTactic
{
    public int Id { get; set; }
    public string ShortName { get; set; } = "";
    public string Name { get; set; } = "";
}

public class AttackTechniqueTactic
{
    public int AttackTechniqueId { get; set; }
    public int AttackTacticId { get; set; }
}

public class AttackConnection
{
    public int Id { get; set; }
    public int FromAttackComponentId { get; set; }
    public int ToAttackComponentId { get; set; }
    public int SystemAssetRelationId { get; set; }
}

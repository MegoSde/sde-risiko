namespace AlpacaMedic.Models;

public record RiskAttackStep(int Id, int SystemAssetId, string SystemAssetName, int AttackTechniqueId,
    string TechniqueId, string TechniqueName, int Likelihood, string Comment);
public record RiskPathConnection(int Id, int FromAttackComponentId, int ToAttackComponentId, int SystemAssetRelationId, string RelationType);
public record RiskPathRow(int Number, RiskAttackStep[] Components, RiskPathConnection[] Connections, int PathLikelihood, int? CycleToComponentId);
public record RiskBusinessAssetRow(int Id, string Name, BusinessProcess[] BusinessProcesses, decimal Consequence, int[] ReachedViaSystemAssetIds);
public record RiskScenarioRow(int Id, RiskAttackStep InitialAttackComponent, RiskPathRow[] Paths,
    RiskBusinessAssetRow[] ReachableBusinessAssets, decimal AccumulatedConsequence, decimal MaximumConsequence,
    decimal ImpactRatio, int ConsequenceIndex, int ScenarioLikelihood, int RiskScore, string RiskLevel);

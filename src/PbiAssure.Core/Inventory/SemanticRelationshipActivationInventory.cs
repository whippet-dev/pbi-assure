namespace PbiAssure.Core.Inventory;

/// <summary>
/// Proven, static evidence that an inactive relationship is referenced by a bounded
/// <c>USERELATIONSHIP</c> call in analysed DAX. This is review context, not a runtime or Service verdict.
/// </summary>
public sealed record SemanticRelationshipActivationInventory(
    string State,
    IReadOnlyList<SemanticRelationshipActivationSourceInventory> Sources);

public sealed record SemanticRelationshipActivationSourceInventory(
    string Table,
    string ObjectName,
    string ObjectType,
    bool ReachableFromReport);

public static class SemanticRelationshipActivationStates
{
    public const string ActivatedByReportUsedDax = "ActivatedByReportUsedDax";
    public const string ReferencedOnlyByUnusedDax = "ReferencedOnlyByUnusedDax";
    public const string NoDetectedActivation = "NoDetectedActivation";
}

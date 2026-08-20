namespace PbiAssure.Core.Inventory;

public sealed record UnresolvedSemanticDependency(
    string SemanticModel,
    string FromTable,
    string FromObjectName,
    string FromObjectType,
    string? FromHierarchyName,
    string DependencyKind,
    string ReferenceText,
    string Reason,
    string EvidencePath)
{
    /// <summary>
    /// The structured result of resolving <see cref="ReferenceText"/>. This must be used for machine
    /// decisions; <see cref="Reason"/> is retained only as human-readable diagnostic context.
    /// </summary>
    public string ResolutionOutcome { get; init; } = UnresolvedSemanticDependencyResolutionOutcomes.NotFound;
}

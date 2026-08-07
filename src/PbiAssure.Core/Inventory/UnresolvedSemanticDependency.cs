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
    string EvidencePath);

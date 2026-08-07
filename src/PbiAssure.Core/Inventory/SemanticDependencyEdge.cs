namespace PbiAssure.Core.Inventory;

public sealed record SemanticDependencyEdge(
    string SemanticModel,
    string FromTable,
    string FromObjectName,
    string FromObjectType,
    string? FromHierarchyName,
    string ToTable,
    string ToObjectName,
    string ToObjectType,
    string? ToHierarchyName,
    string DependencyKind,
    string EvidencePath,
    string EvidenceText);

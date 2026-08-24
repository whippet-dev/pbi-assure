using System.Text.Json.Serialization;

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
    string EvidenceText)
{
    /// <summary>
    /// In-process provenance for a model-structure edge. It is intentionally omitted from the public
    /// inventory until a broader relationship-provenance contract is separately designed.
    /// </summary>
    [JsonIgnore]
    public string? StructuralProvenance { get; init; }
}

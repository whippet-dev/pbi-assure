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

    /// <summary>
    /// The project-relative path of the report that owns the source, when the source is a report
    /// measure; null for every other source. In process only, like the target's owner: the public edge
    /// is unchanged, and its <see cref="EvidencePath"/> (the owning report's extension file) remains the
    /// published provenance. See <see cref="SemanticNodeIdentity"/>.
    /// </summary>
    [JsonIgnore]
    public string? FromReport { get; init; }

    /// <summary>The owning report of the target, when the target is a report measure; otherwise null.</summary>
    [JsonIgnore]
    public string? ToReport { get; init; }
}

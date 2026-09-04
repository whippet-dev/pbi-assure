using System.Text.Json.Serialization;

namespace PbiAssure.Core.Inventory;

public sealed record VisualFieldReference(
    string Table,
    string ObjectName,
    string ObjectType,
    string? HierarchyName,
    string UsageContext,
    string? Role,
    string EvidencePath)
{
    [JsonIgnore]
    public string ReferenceOrigin { get; init; } = VisualReferenceOrigins.Unknown;

    [JsonIgnore]
    public string ReferenceRelevance { get; init; } = VisualReferenceRelevance.Ambiguous;

    [JsonIgnore]
    public string? FormattingObject { get; init; }

    [JsonIgnore]
    public string? FormattingProperty { get; init; }

    [JsonIgnore]
    public string? SelectorKind { get; init; }

    [JsonIgnore]
    public string? MatchedProjectionQueryRef { get; init; }

    /// <summary>
    /// True only when this reference is carried by a PBIR role projection that explicitly persists
    /// <c>hidden: true</c>. This is separate from visual/container and semantic-object visibility.
    /// </summary>
    [JsonIgnore]
    public bool IsHiddenProjection { get; init; }
}

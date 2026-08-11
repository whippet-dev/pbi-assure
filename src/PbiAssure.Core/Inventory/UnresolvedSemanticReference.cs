using System.Text.Json.Serialization;

namespace PbiAssure.Core.Inventory;

public sealed record UnresolvedSemanticReference(
    string Report,
    string SemanticModel,
    string? Page,
    string? Visual,
    string ArtifactPath,
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
}

using System.Text.Json.Serialization;

namespace PbiAssure.Core.Inventory;

public sealed record SemanticUsageEvidence(
    string Report,
    string? Page,
    string? Visual,
    string ArtifactPath,
    string UsageContext,
    string? Role,
    string EvidencePath)
{
    /// <summary>True only when the originating PBIR role projection explicitly persists hidden.</summary>
    [JsonIgnore]
    public bool IsHiddenProjection { get; init; }
}

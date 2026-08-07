namespace PbiAssure.Core.Inventory;

public sealed record SemanticUsageEvidence(
    string Report,
    string? Page,
    string? Visual,
    string ArtifactPath,
    string UsageContext,
    string? Role,
    string EvidencePath);

namespace PbiAssure.Core.Inventory;

public sealed record SemanticUsageEvidence(
    string Report,
    string Page,
    string Visual,
    string UsageContext,
    string? Role,
    string EvidencePath);

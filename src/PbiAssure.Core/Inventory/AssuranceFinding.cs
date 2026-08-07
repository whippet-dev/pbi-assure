namespace PbiAssure.Core.Inventory;

public sealed record AssuranceFinding(
    string RuleId,
    string RuleVersion,
    string Category,
    string Severity,
    string Message,
    string Recommendation,
    string? Report,
    string? Page,
    string? PageDisplayName,
    string? Visual,
    string? SemanticModel,
    string? Table,
    string? ObjectName,
    string ArtifactPath,
    IReadOnlyList<string> EvidencePaths,
    string AssessmentType,
    string? ReferenceUrl);

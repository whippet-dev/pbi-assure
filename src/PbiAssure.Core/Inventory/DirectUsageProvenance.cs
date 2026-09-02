namespace PbiAssure.Core.Inventory;

/// <summary>
/// Export-oriented provenance for one direct semantic reference in a locally bound report. This is
/// derived from a <see cref="ProjectInventory"/> and deliberately does not alter semantic usage.
/// </summary>
public sealed record DirectSemanticUsageProvenance(
    string SemanticModel,
    string Table,
    string ObjectName,
    string ObjectType,
    string SemanticUsage,
    string ClassificationConfidence,
    string Report,
    string ReportPath,
    string? Page,
    string? PageId,
    string? VisualId,
    string? VisualType,
    string UsageContext,
    string? UsageRole,
    string UserFacing,
    string ArtifactPath,
    string EvidencePath);

/// <summary>
/// One v1 catalogue-eligible semantic object with direct-report provenance summarized by stable
/// report, page and visual identities. It is a derived export helper, not a semantic usage state.
/// </summary>
public sealed record SemanticObjectDirectUsageSummary(
    string SemanticModel,
    string Table,
    string ObjectName,
    string ObjectType,
    string SemanticUsage,
    string ClassificationConfidence,
    string UserFacing,
    int DirectUsageCount,
    int ReportCount,
    int PageCount,
    int VisualCount,
    IReadOnlyList<string> UsageContexts,
    IReadOnlyList<string> UsageRoles);

/// <summary>Values for export-only user-facing provenance.</summary>
public static class UserFacingStates
{
    public const string Yes = "Yes";
    public const string No = "No";
    public const string Unclear = "Unclear";
}

/// <summary>
/// In-process result of normalizing direct semantic usage for the eventual Export Builder. It is not
/// attached to <see cref="ProjectInventory"/>, so it does not change the JSON inventory contract.
/// </summary>
public sealed record DirectUsageProvenanceAnalysis(
    IReadOnlyList<DirectSemanticUsageProvenance> Usages,
    IReadOnlyList<SemanticObjectDirectUsageSummary> ObjectSummaries);

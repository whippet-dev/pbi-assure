using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Reporting.Exports;

/// <summary>
/// Groups parser-level direct evidence into the report usage a catalogue user can act on. Evidence paths
/// are retained separately: they prove a usage but do not turn one report usage into several CSV rows.
/// </summary>
internal static class LogicalDirectUsageAnalyzer
{
    public static IReadOnlyList<LogicalDirectUsage> Analyze(ProjectInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return DirectUsageProvenanceAnalyzer.Analyze(inventory).Usages
            .GroupBy(usage => Key(usage), StringComparer.OrdinalIgnoreCase)
            .Select(group => Create(inventory, group.ToArray()))
            .OrderBy(usage => usage.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.ObjectType, StringComparer.Ordinal)
            .ThenBy(usage => usage.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.ReportPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.PageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.VisualId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.UsageContext, StringComparer.Ordinal)
            .ThenBy(usage => usage.UsageRole, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static LogicalDirectUsage Create(ProjectInventory inventory, DirectSemanticUsageProvenance[] evidence)
    {
        var userFacing = evidence.Select(item => item.UserFacing).Distinct(StringComparer.Ordinal).ToArray();
        if (userFacing.Length != 1)
        {
            throw new InvalidOperationException($"Logical direct usage '{Key(evidence[0])}' has conflicting UserFacing classifications: {string.Join(", ", userFacing)}.");
        }

        var semanticUsage = evidence.Select(item => item.SemanticUsage).Distinct(StringComparer.Ordinal).ToArray();
        var confidence = evidence.Select(item => item.ClassificationConfidence).Distinct(StringComparer.Ordinal).ToArray();
        if (semanticUsage.Length != 1 || confidence.Length != 1)
        {
            throw new InvalidOperationException($"Logical direct usage '{Key(evidence[0])}' has conflicting semantic classification facts.");
        }

        var representative = evidence
            .OrderBy(item => item.ArtifactPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.EvidencePath, StringComparer.Ordinal)
            .First();
        return new LogicalDirectUsage(
            representative,
            FindVisualLabel(inventory, representative),
            evidence.Length,
            DistinctSorted(evidence.Select(item => item.ArtifactPath)),
            DistinctSorted(evidence.Select(item => item.EvidencePath)));
    }

    private static string? FindVisualLabel(ProjectInventory inventory, DirectSemanticUsageProvenance usage)
    {
        if (string.IsNullOrWhiteSpace(usage.ReportPath) || string.IsNullOrWhiteSpace(usage.PageId) || string.IsNullOrWhiteSpace(usage.VisualId))
        {
            return null;
        }

        var visual = inventory.Reports
            .Where(report => PathsEqual(report.RelativePath, usage.ReportPath))
            .SelectMany(report => report.Pages)
            .Where(page => string.Equals(page.Name, usage.PageId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(page => page.Visuals)
            .SingleOrDefault(candidate => string.Equals(candidate.Name, usage.VisualId, StringComparison.OrdinalIgnoreCase));
        return visual is null ? null : VisualPresentation.DisplayName(visual);
    }

    private static string[] DistinctSorted(IEnumerable<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string Key(DirectSemanticUsageProvenance usage) => string.Join('\u001f',
        usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType, usage.ReportPath,
        usage.PageId ?? string.Empty, usage.VisualId ?? string.Empty, usage.UsageContext, usage.UsageRole ?? string.Empty);

    private static bool PathsEqual(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Replace('\\', '/'), right.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
}

internal sealed record LogicalDirectUsage(
    DirectSemanticUsageProvenance Provenance,
    string? Visual,
    int EvidenceCount,
    IReadOnlyList<string> ArtifactPaths,
    IReadOnlyList<string> EvidencePaths)
{
    public string SemanticModel => Provenance.SemanticModel;
    public string Table => Provenance.Table;
    public string ObjectName => Provenance.ObjectName;
    public string ObjectType => Provenance.ObjectType;
    public string SemanticUsage => Provenance.SemanticUsage;
    public string ClassificationConfidence => Provenance.ClassificationConfidence;
    public string Report => Provenance.Report;
    public string ReportPath => Provenance.ReportPath;
    public string? Page => Provenance.Page;
    public string? PageId => Provenance.PageId;
    public string? VisualId => Provenance.VisualId;
    public string? VisualType => Provenance.VisualType;
    public string UsageContext => Provenance.UsageContext;
    public string? UsageRole => Provenance.UsageRole;
    public string UserFacing => Provenance.UserFacing;
}

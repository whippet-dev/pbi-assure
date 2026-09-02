using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

/// <summary>
/// Derives export-ready direct report provenance from retained scan facts. This deliberately leaves
/// semantic dependency analysis and its five usage states untouched.
/// </summary>
internal static class DirectUsageProvenanceAnalyzer
{
    public static DirectUsageProvenanceAnalysis Analyze(ProjectInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var eligibleUsages = inventory.SemanticObjectUsages
            .Where(usage => IsV1Eligible(inventory, usage))
            .OrderBy(usage => usage.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.ObjectType, StringComparer.Ordinal)
            .ThenBy(usage => usage.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var directUsages = eligibleUsages
            .SelectMany(usage => usage.DirectReportReferences.Select(evidence => Normalize(inventory, usage, evidence)))
            .Distinct()
            .OrderBy(usage => usage.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.ObjectType, StringComparer.Ordinal)
            .ThenBy(usage => usage.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.ReportPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.PageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.VisualId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.UsageContext, StringComparer.Ordinal)
            .ThenBy(usage => usage.UsageRole, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.EvidencePath, StringComparer.Ordinal)
            .ToArray();

        var directUsagesByObject = directUsages
            .GroupBy(usage => ObjectKey(usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var summaries = eligibleUsages
            .Select(usage => Summarize(usage, directUsagesByObject.GetValueOrDefault(ObjectKey(
                usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType)) ?? []))
            .ToArray();

        return new DirectUsageProvenanceAnalysis(directUsages, summaries);
    }

    private static bool IsV1Eligible(ProjectInventory inventory, SemanticObjectUsage usage) =>
        !inventory.IsSystemGeneratedSemanticObject(usage) &&
        usage.ObjectType is SemanticObjectTypes.Column or SemanticObjectTypes.Measure;

    private static DirectSemanticUsageProvenance Normalize(
        ProjectInventory inventory,
        SemanticObjectUsage usage,
        SemanticUsageEvidence evidence)
    {
        var report = FindReport(inventory, evidence);
        var page = FindPage(report, evidence.Page);
        var visual = FindVisual(page, evidence.Visual);
        var reference = FindReference(visual, usage, evidence);

        return new DirectSemanticUsageProvenance(
            usage.SemanticModel,
            usage.Table,
            usage.ObjectName,
            usage.ObjectType,
            usage.UsageState,
            usage.ClassificationConfidence,
            report?.Name ?? evidence.Report,
            report?.RelativePath ?? string.Empty,
            page?.DisplayName ?? evidence.Page,
            page?.Name ?? evidence.Page,
            visual?.Name ?? evidence.Visual,
            visual?.VisualType,
            evidence.UsageContext,
            evidence.Role,
            ClassifyUserFacing(evidence, reference),
            evidence.ArtifactPath,
            evidence.EvidencePath);
    }

    private static SemanticObjectDirectUsageSummary Summarize(
        SemanticObjectUsage usage,
        DirectSemanticUsageProvenance[] directUsages)
    {
        return new SemanticObjectDirectUsageSummary(
            usage.SemanticModel,
            usage.Table,
            usage.ObjectName,
            usage.ObjectType,
            usage.UsageState,
            usage.ClassificationConfidence,
            AggregateUserFacing(directUsages),
            directUsages.Length,
            directUsages.Select(directUsage => directUsage.ReportPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            directUsages.Select(directUsage => LocationKey(directUsage.ReportPath, directUsage.PageId))
                .Where(key => key is not null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            directUsages.Select(directUsage => LocationKey(directUsage.ReportPath, directUsage.PageId, directUsage.VisualId))
                .Where(key => key is not null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            DistinctSorted(directUsages.Select(directUsage => directUsage.UsageContext)),
            DistinctSorted(directUsages.Select(directUsage => directUsage.UsageRole)));
    }

    private static string AggregateUserFacing(IReadOnlyList<DirectSemanticUsageProvenance> directUsages)
    {
        if (directUsages.Any(usage => usage.UserFacing == UserFacingStates.Yes))
        {
            return UserFacingStates.Yes;
        }

        return directUsages.Any(usage => usage.UserFacing == UserFacingStates.Unclear)
            ? UserFacingStates.Unclear
            : UserFacingStates.No;
    }

    private static string ClassifyUserFacing(
        SemanticUsageEvidence evidence,
        VisualFieldReference? reference)
    {
        return evidence.UsageContext switch
        {
            UsageContexts.Projection => UserFacingStates.Yes,
            UsageContexts.Drillthrough => UserFacingStates.Yes,
            UsageContexts.Filter or UsageContexts.Sort => UserFacingStates.No,
            UsageContexts.Formatting => ClassifyFormattingUserFacing(reference),
            UsageContexts.Other => UserFacingStates.Unclear,
            _ => UserFacingStates.Unclear,
        };
    }

    private static string ClassifyFormattingUserFacing(VisualFieldReference? reference)
    {
        if (reference?.ReferenceOrigin == VisualReferenceOrigins.FormattingSelectorIdentity)
        {
            return UserFacingStates.No;
        }

        return reference?.ReferenceOrigin == VisualReferenceOrigins.FormattingPropertyExpression &&
               reference.ReferenceRelevance == VisualReferenceRelevance.Active
            ? UserFacingStates.Yes
            : UserFacingStates.Unclear;
    }

    private static ReportInventory? FindReport(ProjectInventory inventory, SemanticUsageEvidence evidence)
    {
        var namedReports = inventory.Reports
            .Where(report => string.Equals(report.Name, evidence.Report, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var artifactMatch = namedReports.Where(report => ContainsArtifact(report, evidence.ArtifactPath)).ToArray();

        return artifactMatch.Length == 1
            ? artifactMatch[0]
            : namedReports.Length == 1
                ? namedReports[0]
                : null;
    }

    private static bool ContainsArtifact(ReportInventory report, string artifactPath)
    {
        if (PathsEqual(report.DefinitionPath, artifactPath))
        {
            return true;
        }

        return report.Pages.Any(page =>
            PathsEqual(page.DefinitionPath, artifactPath) ||
            page.Visuals.Any(visual => PathsEqual(visual.RelativePath, artifactPath)));
    }

    private static PageInventory? FindPage(ReportInventory? report, string? pageId)
    {
        if (report is null || string.IsNullOrWhiteSpace(pageId))
        {
            return null;
        }

        var pages = report.Pages
            .Where(page => string.Equals(page.Name, pageId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return pages.Length == 1 ? pages[0] : null;
    }

    private static VisualInventory? FindVisual(PageInventory? page, string? visualId)
    {
        if (page is null || string.IsNullOrWhiteSpace(visualId))
        {
            return null;
        }

        var visuals = page.Visuals
            .Where(visual => string.Equals(visual.Name, visualId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return visuals.Length == 1 ? visuals[0] : null;
    }

    private static VisualFieldReference? FindReference(
        VisualInventory? visual,
        SemanticObjectUsage usage,
        SemanticUsageEvidence evidence)
    {
        return visual?.FieldReferences.SingleOrDefault(reference =>
            string.Equals(reference.Table, usage.Table, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(reference.ObjectName, usage.ObjectName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(reference.ObjectType, usage.ObjectType, StringComparison.Ordinal) &&
            string.Equals(reference.HierarchyName, usage.HierarchyName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(reference.EvidencePath, evidence.EvidencePath, StringComparison.Ordinal));
    }

    private static string[] DistinctSorted(IEnumerable<string?> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string? LocationKey(string reportPath, string? pageId, string? visualId = null)
    {
        if (string.IsNullOrWhiteSpace(reportPath) || string.IsNullOrWhiteSpace(pageId) ||
            (visualId is not null && string.IsNullOrWhiteSpace(visualId)))
        {
            return null;
        }

        return string.Join('\u001f', reportPath, pageId, visualId ?? string.Empty);
    }

    private static string ObjectKey(string model, string table, string objectName, string objectType) =>
        string.Join('\u001f', model, table, objectType, objectName);

    private static bool PathsEqual(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Replace('\\', '/'), right.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
}

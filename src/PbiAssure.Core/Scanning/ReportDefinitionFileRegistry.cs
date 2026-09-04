using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal enum ReportDefinitionFileMatch
{
    ExactPath,
    PathTemplate,
    DirectoryTree,
}

internal sealed record ReportDefinitionFileRule(
    string LimitationId,
    string ConstructType,
    string Pattern,
    ReportDefinitionFileMatch MatchKind,
    string Classification,
    string SupportState,
    string DependencyImpact,
    IReadOnlyList<string> Concerns,
    string Reason);

/// <summary>
/// The file-level contract for locally persisted report artifacts. Known PBIR files are classified by
/// path, not by schema version: the generic PBIR reference extractor still runs when a known file uses a
/// newer schema, so schema verification remains a separate observation rather than a usage limitation.
/// </summary>
internal static class ReportDefinitionFileRegistry
{
    public static IReadOnlySet<string> DefinitionExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json", ".pbir" };

    public static IReadOnlyList<ReportDefinitionFileRule> Rules { get; } =
    [
        Analyzed("PBI-LIMIT-REPORT-CONNECTION", "reportConnection", "definition.pbir",
            ReportDefinitionFileMatch.ExactPath, "The report connection definition is analysed."),
        Analyzed("PBI-LIMIT-REPORT-DEFINITION", "reportDefinition", "definition/report.json",
            ReportDefinitionFileMatch.ExactPath,
            "The report definition and its generic semantic references are analysed."),
        Analyzed("PBI-LIMIT-REPORT-VERSION", "reportVersionMetadata", "definition/version.json",
            ReportDefinitionFileMatch.ExactPath, "PBIR version metadata is analysed."),
        Analyzed("PBI-LIMIT-REPORT-EXTENSION", "reportExtension", "definition/reportExtensions.json",
            ReportDefinitionFileMatch.ExactPath,
            "Report measures and their persisted semantic-reference metadata are analysed."),
        Analyzed("PBI-LIMIT-REPORT-PAGES", "pagesMetadata", "definition/pages/pages.json",
            ReportDefinitionFileMatch.ExactPath, "Page ordering and landing-page metadata are analysed."),
        Analyzed("PBI-LIMIT-REPORT-PAGE", "pageDefinition", "definition/pages/*/page.json",
            ReportDefinitionFileMatch.PathTemplate,
            "Page definitions and their generic semantic references are analysed."),
        Analyzed("PBI-LIMIT-REPORT-VISUAL", "visualDefinition",
            "definition/pages/*/visuals/*/visual.json", ReportDefinitionFileMatch.PathTemplate,
            "Visual definitions and their generic semantic references are analysed."),
        Analyzed("PBI-LIMIT-REPORT-MOBILE", "mobileVisualDefinition",
            "definition/pages/*/visuals/*/mobile.json", ReportDefinitionFileMatch.PathTemplate,
            "Mobile visual state and its generic semantic references are analysed."),
        Analyzed("PBI-LIMIT-REPORT-BOOKMARKS", "bookmarksMetadata", "definition/bookmarks/bookmarks.json",
            ReportDefinitionFileMatch.ExactPath, "Bookmark ordering metadata is analysed."),
        Analyzed("PBI-LIMIT-REPORT-BOOKMARK", "bookmarkDefinition",
            "definition/bookmarks/*.bookmark.json", ReportDefinitionFileMatch.PathTemplate,
            "Bookmark definitions are analysed."),
        Packaging("PBI-LIMIT-REPORT-RESOURCE", "reportResource", "StaticResources",
            "Static report resources are recognised packaging. They are not report semantic-reference artifacts."),
        Packaging("PBI-LIMIT-REPORT-LOCAL-SETTINGS", "reportLocalSettings", ".pbi",
            "Local report settings are recognised packaging and are not part of the persisted report definition."),
    ];

    public static ReportDefinitionFileRule Fallback { get; } = new(
        LimitationId: "PBI-LIMIT-REPORT-UNRECOGNIZED",
        ConstructType: "unrecognizedReportDefinitionFile",
        Pattern: string.Empty,
        MatchKind: ReportDefinitionFileMatch.ExactPath,
        Classification: ConstructClassifications.Unrecognized,
        SupportState: ConstructSupportStates.Unrecognized,
        DependencyImpact: ConstructDependencyImpacts.MayCreateDependencies,
        Concerns: [AnalysisConcerns.Dependency],
        Reason: "This report metadata file is not recognised by this version of PBI Assure and was not " +
                "analysed. It could contain semantic references that are absent from the usage graph.");

    public static ReportDefinitionFileRule Classify(string reportRelativePath)
    {
        var matches = MatchingRules(reportRelativePath);
        return matches.Count > 0 ? matches[0] : Fallback;
    }

    public static IReadOnlyList<ReportDefinitionFileRule> MatchingRules(string reportRelativePath)
    {
        if (string.IsNullOrWhiteSpace(reportRelativePath))
        {
            return [];
        }

        var normalized = ProjectFilePaths.Normalize(reportRelativePath);
        return Rules.Where(rule => Matches(rule, normalized)).ToArray();
    }

    public static bool IsDefinitionArtifact(string relativePath) =>
        DefinitionExtensions.Contains(Path.GetExtension(relativePath));

    private static bool Matches(ReportDefinitionFileRule rule, string normalizedPath) => rule.MatchKind switch
    {
        ReportDefinitionFileMatch.ExactPath =>
            string.Equals(normalizedPath, rule.Pattern, StringComparison.OrdinalIgnoreCase),
        ReportDefinitionFileMatch.DirectoryTree =>
            string.Equals(normalizedPath, rule.Pattern, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(rule.Pattern.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase),
        ReportDefinitionFileMatch.PathTemplate => MatchesTemplate(normalizedPath, rule.Pattern),
        _ => false,
    };

    private static bool MatchesTemplate(string normalizedPath, string template)
    {
        var pathSegments = normalizedPath.Split('/');
        var templateSegments = template.Split('/');
        return pathSegments.Length == templateSegments.Length && pathSegments.Zip(templateSegments)
            .All(pair => MatchesSegment(pair.First, pair.Second));
    }

    private static bool MatchesSegment(string value, string template)
    {
        var wildcard = template.IndexOf('*');
        return wildcard < 0
            ? string.Equals(value, template, StringComparison.OrdinalIgnoreCase)
            : value.StartsWith(template[..wildcard], StringComparison.OrdinalIgnoreCase) &&
              value.EndsWith(template[(wildcard + 1)..], StringComparison.OrdinalIgnoreCase);
    }

    private static ReportDefinitionFileRule Analyzed(
        string limitationId,
        string constructType,
        string pattern,
        ReportDefinitionFileMatch matchKind,
        string reason) => new(
        limitationId, constructType, pattern, matchKind,
        ConstructClassifications.Analyzed, ConstructSupportStates.Analyzed,
        ConstructDependencyImpacts.NoKnownDependencyEffect, [], reason);

    private static ReportDefinitionFileRule Packaging(
        string limitationId,
        string constructType,
        string pattern,
        string reason) => new(
        limitationId, constructType, pattern, ReportDefinitionFileMatch.DirectoryTree,
        ConstructClassifications.Packaging, ConstructSupportStates.NotYetAnalyzed,
        ConstructDependencyImpacts.NoKnownDependencyEffect, [], reason);
}

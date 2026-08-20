using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class ConfiguredCustomThemeRule : IAssuranceRule
{
    private const string RuleVersion = "1.0.0";
    private const string ReferenceUrl = "https://learn.microsoft.com/power-bi/developer/projects/projects-report";

    private static readonly HashSet<string> UnavailableOutcomes = new(StringComparer.Ordinal)
    {
        ThemeResolutionOutcomes.ReferenceNameMissing,
        ThemeResolutionOutcomes.PackageItemNotFound,
        ThemeResolutionOutcomes.AmbiguousPackageItem,
        ThemeResolutionOutcomes.InvalidPackagePath,
        ThemeResolutionOutcomes.ResourceFileMissing,
        ThemeResolutionOutcomes.InvalidJson,
        ThemeResolutionOutcomes.ResourceUnreadable,
    };

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        foreach (var report in inventory.Reports)
        {
            var theme = report.Theme.CustomSource;
            if (theme is null || !UnavailableOutcomes.Contains(theme.ResolutionOutcome))
            {
                continue;
            }

            var name = theme.ReferenceName ?? "the configured custom theme";
            yield return new AssuranceFinding(
                RuleId: "PBI-COMPAT-002",
                RuleVersion,
                AssuranceCategories.Compatibility,
                FindingSeverities.Warning,
                $"The configured custom theme '{name}' {Describe(theme.ResolutionOutcome)}.",
                "Reselect or reimport the custom theme in Power BI Desktop, then save the project and confirm the configured theme is available.",
                report.Name,
                Page: null,
                PageDisplayName: null,
                Visual: null,
                SemanticModel: null,
                Table: null,
                ObjectName: theme.ReferenceName,
                ArtifactPath: Path.Combine(report.RelativePath, "definition", "report.json"),
                EvidencePaths: [theme.EvidencePath],
                AssessmentType: AssessmentTypes.Finding,
                ReferenceUrl);
        }
    }

    private static string Describe(string outcome) => outcome switch
    {
        ThemeResolutionOutcomes.ReferenceNameMissing => "does not name a resource",
        ThemeResolutionOutcomes.PackageItemNotFound => "has no matching resource package item",
        ThemeResolutionOutcomes.AmbiguousPackageItem => "matches more than one resource package item",
        ThemeResolutionOutcomes.InvalidPackagePath => "has an invalid resource package path",
        ThemeResolutionOutcomes.ResourceFileMissing => "references a resource file that was not found",
        ThemeResolutionOutcomes.InvalidJson => "references a resource file that could not be read as JSON",
        ThemeResolutionOutcomes.ResourceUnreadable => "references a resource file that could not be read",
        _ => "could not be resolved",
    };
}

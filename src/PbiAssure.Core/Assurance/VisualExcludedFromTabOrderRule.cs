using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class VisualExcludedFromTabOrderRule : IAssuranceRule
{
    private const string RuleId = "PBI-ACCESS-003";
    private const string RuleVersion = "1.0.0";
    private static readonly HashSet<string> DecorativeCandidateTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "basicShape",
        "image",
        "textbox",
    };

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        return VisualRuleContexts.Read(inventory)
            .Where(context =>
                context.Visual.IsExplicitlyExcludedFromTabOrder &&
                !context.Visual.IsHidden &&
                !string.Equals(context.Page.Visibility, "HiddenInViewMode", StringComparison.OrdinalIgnoreCase) &&
                (context.Visual.VisualType is null || !DecorativeCandidateTypes.Contains(context.Visual.VisualType)))
            .Select(context => new AssuranceFinding(
                RuleId,
                RuleVersion,
                AssuranceCategories.Accessibility,
                FindingSeverities.Information,
                $"The visible {context.Visual.VisualType ?? "unknown"} visual is excluded from the tab order.",
                "Confirm that keyboard users do not need to reach this visual. If it conveys information or supports interaction, add it to an intentional tab order.",
                context.Report.Name,
                context.Page.Name,
                context.Page.DisplayName,
                context.Visual.Name,
                SemanticModel: null,
                Table: null,
                ObjectName: null,
                context.Visual.RelativePath,
                ["$.position.tabOrder (negative value)"],
                AssessmentTypes.ReviewRequired,
                "https://learn.microsoft.com/power-bi/create-reports/desktop-accessibility-creating-reports"));
    }
}

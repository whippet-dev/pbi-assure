using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class MissingAltTextRule : IAssuranceRule
{
    private const string RuleId = "PBI-ACCESS-001";
    private const string RuleVersion = "1.1.0";

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        return VisualRuleContexts.ReadEffectivelyVisible(inventory)
            .Where(context => context.Visual.IsInTabOrder && !context.Visual.Accessibility.HasAltText)
            .Select(context => new AssuranceFinding(
                RuleId,
                RuleVersion,
                AssuranceCategories.Accessibility,
                FindingSeverities.Warning,
                $"The {context.Visual.VisualType ?? "unknown"} visual is included in keyboard navigation but has no configured alt text.",
                "Add concise alt text describing the visual's purpose and insight. If the object is purely decorative, remove it from the tab order instead.",
                context.Report.Name,
                context.Page.Name,
                context.Page.DisplayName,
                context.Visual.Name,
                SemanticModel: null,
                Table: null,
                ObjectName: null,
                context.Visual.RelativePath,
                ["$.position.tabOrder", "$.visual..altText (not found)"],
                AssessmentTypes.Finding,
                "https://learn.microsoft.com/power-bi/create-reports/desktop-accessibility-creating-reports"));
    }
}

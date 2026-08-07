using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class DeprecatedQnaVisualRule : IAssuranceRule
{
    private const string RuleId = "PBI-COMPAT-001";
    private const string RuleVersion = "1.0.0";

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        return VisualRuleContexts.Read(inventory)
            .Where(context => string.Equals(
                context.Visual.VisualType,
                "qnaVisual",
                StringComparison.OrdinalIgnoreCase))
            .Select(context => new AssuranceFinding(
                RuleId,
                RuleVersion,
                AssuranceCategories.Compatibility,
                FindingSeverities.Warning,
                "The report contains a Power BI Q&A visual, which Microsoft is retiring in December 2026.",
                "Plan removal or replacement before retirement. Preserve any required result as a supported standard visual or an approved alternative.",
                context.Report.Name,
                context.Page.Name,
                context.Page.DisplayName,
                context.Visual.Name,
                SemanticModel: null,
                Table: null,
                ObjectName: null,
                context.Visual.RelativePath,
                ["$.visual.visualType"],
                AssessmentTypes.Finding,
                "https://learn.microsoft.com/power-bi/visuals/power-bi-visualization-q-and-a"));
    }
}

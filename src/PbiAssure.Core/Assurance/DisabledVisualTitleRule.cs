using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class DisabledVisualTitleRule : IAssuranceRule
{
    private const string RuleId = "PBI-ACCESS-004";
    private const string RuleVersion = "1.0.0";
    private static readonly HashSet<string> TitleOptionalTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "actionButton",
        "basicShape",
        "image",
        "textbox",
    };

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        return VisualRuleContexts.Read(inventory)
            .Where(context =>
                context.Visual.IsInTabOrder &&
                context.Visual.Accessibility.TitleIsVisible == false &&
                (context.Visual.VisualType is null || !TitleOptionalTypes.Contains(context.Visual.VisualType)))
            .Select(context => new AssuranceFinding(
                RuleId,
                RuleVersion,
                AssuranceCategories.Accessibility,
                FindingSeverities.Information,
                $"The {context.Visual.VisualType ?? "unknown"} visual has its title explicitly disabled.",
                "Confirm that the visual still has a clear accessible name and sufficient context. Enable a meaningful title where the surrounding page does not provide an equivalent label.",
                context.Report.Name,
                context.Page.Name,
                context.Page.DisplayName,
                context.Visual.Name,
                SemanticModel: null,
                Table: null,
                ObjectName: null,
                context.Visual.RelativePath,
                ["$.visual.visualContainerObjects.title[0].properties.show"],
                AssessmentTypes.ReviewRequired,
                "https://learn.microsoft.com/power-bi/create-reports/desktop-accessibility-creating-reports"));
    }
}

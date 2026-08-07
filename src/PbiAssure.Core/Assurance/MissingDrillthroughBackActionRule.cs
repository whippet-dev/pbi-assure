using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class MissingDrillthroughBackActionRule : IAssuranceRule
{
    private const string RuleId = "PBI-ACCESS-005";
    private const string RuleVersion = "1.0.0";

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        foreach (var report in inventory.Reports)
        {
            foreach (var page in report.Pages.Where(IsDrillthroughPage))
            {
                var hasEnabledBackAction = page.Visuals
                    .SelectMany(visual => visual.Actions)
                    .Any(action => action.IsEnabled == true && string.Equals(
                        action.ActionType,
                        "Back",
                        StringComparison.OrdinalIgnoreCase));
                if (hasEnabledBackAction)
                {
                    continue;
                }

                yield return new AssuranceFinding(
                    RuleId,
                    RuleVersion,
                    AssuranceCategories.Accessibility,
                    FindingSeverities.Information,
                    "The drillthrough page has no enabled Back action in its static visual metadata.",
                    "Confirm that keyboard and screen-reader users can return to the source page. Restore or add an accessible Back button if no equivalent mechanism is available.",
                    report.Name,
                    page.Name,
                    page.DisplayName,
                    Visual: null,
                    SemanticModel: null,
                    Table: null,
                    ObjectName: page.PageBinding?.Name,
                    page.DefinitionPath,
                    ["$.pageBinding.type"],
                    AssessmentTypes.ReviewRequired,
                    "https://learn.microsoft.com/power-bi/guidance/report-drillthrough");
            }
        }
    }

    private static bool IsDrillthroughPage(PageInventory page)
    {
        return string.Equals(page.PageBinding?.Type, "Drillthrough", StringComparison.OrdinalIgnoreCase);
    }
}

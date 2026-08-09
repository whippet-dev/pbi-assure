using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class DuplicateTabOrderRule : IAssuranceRule
{
    private const string RuleId = "PBI-ACCESS-002";
    private const string RuleVersion = "1.0.0";

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        foreach (var report in inventory.Reports)
        {
            foreach (var page in report.Pages)
            {
                var duplicates = page.Visuals
                    .Where(visual => visual.HasExplicitTabOrder)
                    .GroupBy(visual => visual.Position.TabOrder!.Value)
                    .Where(group => group.Count() > 1);

                foreach (var duplicate in duplicates)
                {
                    yield return new AssuranceFinding(
                        RuleId,
                        RuleVersion,
                        AssuranceCategories.Accessibility,
                        FindingSeverities.Warning,
                        $"{duplicate.Count()} visuals share explicit keyboard-order rank {duplicate.Key} on this page.",
                        "Assign a unique, intentional tab order that follows the page's logical reading sequence.",
                        report.Name,
                        page.Name,
                        page.DisplayName,
                        Visual: null,
                        SemanticModel: null,
                        Table: null,
                        ObjectName: null,
                        page.RelativePath,
                        duplicate.Select(visual => visual.RelativePath + "#$.position.tabOrder").ToArray(),
                        AssessmentTypes.Finding,
                        "https://learn.microsoft.com/power-bi/create-reports/desktop-accessibility-creating-reports");
                }
            }
        }
    }
}

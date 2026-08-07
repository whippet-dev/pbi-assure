using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class DrillthroughPageRule : IAssuranceRule
{
    private const string RuleVersion = "1.0.0";
    private const string ReferenceUrl = "https://learn.microsoft.com/power-bi/guidance/report-drillthrough";

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        foreach (var report in inventory.Reports)
        {
            foreach (var page in report.Pages.Where(IsDrillthroughPage))
            {
                var binding = page.PageBinding!;
                if (binding.Parameters.Count == 0)
                {
                    yield return Finding(
                        ruleId: "PBI-NAV-009",
                        severity: FindingSeverities.Error,
                        message: "The page is configured as a drillthrough page but has no drillthrough parameters.",
                        recommendation: "Add the intended drillthrough field or change the page binding if this is no longer a drillthrough destination.",
                        report,
                        page,
                        evidencePath: "$.pageBinding.parameters");
                }

                var filterNames = page.Filters
                    .Select(filter => filter.Name)
                    .OfType<string>()
                    .ToHashSet(StringComparer.Ordinal);
                for (var index = 0; index < binding.Parameters.Count; index++)
                {
                    var parameter = binding.Parameters[index];
                    if (string.IsNullOrWhiteSpace(parameter.BoundFilter))
                    {
                        yield return Finding(
                            ruleId: "PBI-NAV-010",
                            severity: FindingSeverities.Warning,
                            message: $"Drillthrough parameter '{parameter.Name ?? $"at index {index}"}' has no bound page filter.",
                            recommendation: "Bind the drillthrough parameter to its page filter or recreate the drillthrough field configuration.",
                            report,
                            page,
                            evidencePath: $"$.pageBinding.parameters[{index}]");
                    }
                    else if (!filterNames.Contains(parameter.BoundFilter))
                    {
                        yield return Finding(
                            ruleId: "PBI-NAV-011",
                            severity: FindingSeverities.Error,
                            message: $"Drillthrough parameter '{parameter.Name ?? $"at index {index}"}' references page filter '{parameter.BoundFilter}', but that filter does not exist.",
                            recommendation: "Repair the bound-filter name or recreate the drillthrough field so the parameter and page filter are synchronized.",
                            report,
                            page,
                            evidencePath: $"$.pageBinding.parameters[{index}].boundFilter");
                    }
                }
            }
        }
    }

    private static bool IsDrillthroughPage(PageInventory page)
    {
        return string.Equals(page.PageBinding?.Type, "Drillthrough", StringComparison.OrdinalIgnoreCase);
    }

    private static AssuranceFinding Finding(
        string ruleId,
        string severity,
        string message,
        string recommendation,
        ReportInventory report,
        PageInventory page,
        string evidencePath)
    {
        return new AssuranceFinding(
            ruleId,
            RuleVersion,
            AssuranceCategories.Navigation,
            severity,
            message,
            recommendation,
            report.Name,
            page.Name,
            page.DisplayName,
            Visual: null,
            SemanticModel: null,
            Table: null,
            ObjectName: page.PageBinding?.Name,
            page.DefinitionPath,
            [evidencePath],
            AssessmentTypes.Finding,
            ReferenceUrl);
    }
}

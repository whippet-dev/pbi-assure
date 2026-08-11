using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class ReportInteractionRule : IAssuranceRule
{
    private const string RuleVersion = "1.0.0";
    private const string PbirReferenceUrl = "https://learn.microsoft.com/power-bi/developer/projects/projects-report";
    private const string TooltipReferenceUrl = "https://learn.microsoft.com/power-bi/create-reports/desktop-tooltips";

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        foreach (var report in inventory.Reports)
        {
            foreach (var finding in EvaluateVisualInteractions(report))
            {
                yield return finding;
            }

            foreach (var finding in EvaluateTooltipBindings(report))
            {
                yield return finding;
            }
        }
    }

    private static IEnumerable<AssuranceFinding> EvaluateVisualInteractions(ReportInventory report)
    {
        foreach (var page in report.Pages)
        {
            var visualNames = page.Visuals
                .Select(visual => visual.Name)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var interaction in page.VisualInteractions)
            {
                var missingEndpoints = new List<string>();
                if (string.IsNullOrWhiteSpace(interaction.SourceVisual) ||
                    !visualNames.Contains(interaction.SourceVisual))
                {
                    missingEndpoints.Add($"source '{interaction.SourceVisual ?? "<missing>"}'");
                }

                if (string.IsNullOrWhiteSpace(interaction.TargetVisual) ||
                    !visualNames.Contains(interaction.TargetVisual))
                {
                    missingEndpoints.Add($"target '{interaction.TargetVisual ?? "<missing>"}'");
                }

                if (missingEndpoints.Count == 0)
                {
                    continue;
                }

                yield return new AssuranceFinding(
                    RuleId: "PBI-NAV-012",
                    RuleVersion,
                    AssuranceCategories.Navigation,
                    FindingSeverities.Error,
                    $"The page's {interaction.InteractionType ?? "unknown"} visual interaction has missing endpoints: {string.Join(", ", missingEndpoints)}.",
                    "Recreate the interaction against existing visuals or remove the stale interaction metadata.",
                    report.Name,
                    page.Name,
                    page.DisplayName,
                    Visual: null,
                    SemanticModel: null,
                    Table: null,
                    ObjectName: interaction.TargetVisual ?? interaction.SourceVisual,
                    page.DefinitionPath,
                    [interaction.EvidencePath],
                    AssessmentTypes.Finding,
                    PbirReferenceUrl);
            }
        }
    }

    private static IEnumerable<AssuranceFinding> EvaluateTooltipBindings(ReportInventory report)
    {
        var pages = report.Pages.ToDictionary(page => page.Name, StringComparer.Ordinal);
        foreach (var page in report.Pages)
        {
            foreach (var visual in page.Visuals)
            {
                foreach (var binding in visual.TooltipBindings)
                {
                    if (binding.IsEnabled == false)
                    {
                        continue;
                    }

                    if (binding.HasDynamicConfiguration)
                    {
                        yield return TooltipFinding(
                            ruleId: "PBI-NAV-016",
                            severity: FindingSeverities.Information,
                            message: $"The visual's {BindingName(binding)} binding contains a dynamic expression that cannot be fully reconciled from static PBIR metadata.",
                            recommendation: "Test the tooltip in every relevant state and confirm that its dynamic target remains available and appropriate.",
                            report,
                            page,
                            visual,
                            binding,
                            assessmentType: AssessmentTypes.ReviewRequired);
                    }

                    if (binding.IsEnabled != true)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(binding.TargetPage))
                    {
                        if (binding.HasExplicitTarget && !binding.HasDynamicConfiguration)
                        {
                            yield return TooltipFinding(
                                ruleId: "PBI-NAV-014",
                                severity: FindingSeverities.Warning,
                                message: $"The enabled {BindingName(binding)} binding has no target page.",
                                recommendation: "Choose a report-tooltip page or disable the binding if the default tooltip is intended.",
                                report,
                                page,
                                visual,
                                binding);
                        }

                        continue;
                    }

                    if (!pages.TryGetValue(binding.TargetPage, out var targetPage))
                    {
                        yield return TooltipFinding(
                            ruleId: "PBI-NAV-013",
                            severity: FindingSeverities.Error,
                            message: $"The enabled {BindingName(binding)} binding targets page '{binding.TargetPage}', but that page does not exist in the report.",
                            recommendation: "Choose an existing report-tooltip page or remove the stale tooltip binding.",
                            report,
                            page,
                            visual,
                            binding);
                    }
                    else if (!string.Equals(targetPage.PageType, "Tooltip", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return TooltipFinding(
                            ruleId: "PBI-NAV-015",
                            severity: FindingSeverities.Warning,
                            message: $"The enabled {BindingName(binding)} binding targets page '{targetPage.DisplayName}', but that page is not configured as a Tooltip page.",
                            recommendation: "Configure the target as a report-tooltip page or select the intended existing tooltip page.",
                            report,
                            page,
                            visual,
                            binding);
                    }
                }
            }
        }
    }

    private static AssuranceFinding TooltipFinding(
        string ruleId,
        string severity,
        string message,
        string recommendation,
        ReportInventory report,
        PageInventory page,
        VisualInventory visual,
        VisualTooltipBindingInventory binding,
        string assessmentType = AssessmentTypes.Finding)
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
            visual.Name,
            SemanticModel: null,
            Table: null,
            ObjectName: binding.TargetPage,
            visual.RelativePath,
            [binding.EvidencePath],
            assessmentType,
            TooltipReferenceUrl);
    }

    private static string BindingName(VisualTooltipBindingInventory binding)
    {
        return binding.BindingKind == VisualTooltipBindingKinds.VisualHeader
            ? "visual-header tooltip"
            : "report tooltip";
    }
}

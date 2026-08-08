using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal static class AssuranceRuleEngine
{
    private static readonly IAssuranceRule[] Rules =
    [
        new DeprecatedQnaVisualRule(),
        new MissingSemanticModelRule(),
        new UnresolvedReportReferenceRule(),
        new PowerQueryLineageRule(),
        new MissingAltTextRule(),
        new DuplicateTabOrderRule(),
        new VisualExcludedFromTabOrderRule(),
        new DisabledVisualTitleRule(),
        new NavigationAssuranceRule(),
        new DrillthroughPageRule(),
        new MissingDrillthroughBackActionRule(),
        new ReportInteractionRule(),
    ];

    public static AssuranceFinding[] Evaluate(ProjectInventory inventory)
    {
        return Rules
            .SelectMany(rule => rule.Evaluate(inventory))
            .OrderBy(finding => SeverityOrder(finding.Severity))
            .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
            .ThenBy(finding => finding.Report, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.PageDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.Visual, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int SeverityOrder(string severity)
    {
        return severity switch
        {
            FindingSeverities.Error => 0,
            FindingSeverities.Warning => 1,
            _ => 2,
        };
    }
}

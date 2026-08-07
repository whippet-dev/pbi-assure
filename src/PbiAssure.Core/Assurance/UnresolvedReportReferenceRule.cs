using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class UnresolvedReportReferenceRule : IAssuranceRule
{
    private const string RuleId = "PBI-MODEL-001";
    private const string RuleVersion = "1.0.0";

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        var visualPaths = VisualRuleContexts.Read(inventory).ToDictionary(
            context => Key(context.Report.Name, context.Page.Name, context.Visual.Name),
            context => context.Visual.RelativePath,
            StringComparer.OrdinalIgnoreCase);
        var pageNames = inventory.Reports
            .SelectMany(report => report.Pages.Select(page => (
                Report: report.Name,
                Page: page.Name,
                page.DisplayName)))
            .ToDictionary(
                item => string.Join('\u001f', item.Report, item.Page),
                item => item.DisplayName,
                StringComparer.OrdinalIgnoreCase);

        return inventory.UnresolvedSemanticReferences
            .GroupBy(reference => string.Join(
                '\u001f',
                reference.Report,
                reference.Page,
                reference.Visual,
                reference.ObjectType,
                reference.Table,
                reference.HierarchyName ?? string.Empty,
                reference.ObjectName), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var reference = group.First();
                visualPaths.TryGetValue(Key(reference.Report, reference.Page, reference.Visual), out var visualPath);
                pageNames.TryGetValue(string.Join('\u001f', reference.Report, reference.Page), out var pageDisplayName);
                return new AssuranceFinding(
                    RuleId,
                    RuleVersion,
                    AssuranceCategories.ModelIntegrity,
                    FindingSeverities.Error,
                    $"The visual references {reference.Table}[{reference.ObjectName}], but that {reference.ObjectType.ToLowerInvariant()} could not be resolved in the matching semantic model.",
                    "Repair or remove the stale binding and confirm the visual still returns the intended result.",
                    reference.Report,
                    reference.Page,
                    pageDisplayName,
                    reference.Visual,
                    SemanticModel: reference.Report,
                    reference.Table,
                    reference.ObjectName,
                    visualPath ?? string.Empty,
                    group.Select(item => item.EvidencePath).Distinct(StringComparer.Ordinal).ToArray(),
                    AssessmentTypes.Finding,
                    ReferenceUrl: null);
            });
    }

    private static string Key(string report, string page, string visual)
    {
        return string.Join('\u001f', report, page, visual);
    }
}

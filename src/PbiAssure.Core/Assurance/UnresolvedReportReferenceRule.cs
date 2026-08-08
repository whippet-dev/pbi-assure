using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class UnresolvedReportReferenceRule : IAssuranceRule
{
    private const string RuleId = "PBI-MODEL-001";
    private const string RuleVersion = "1.0.0";

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
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
                reference.Page ?? string.Empty,
                reference.Visual ?? string.Empty,
                reference.ArtifactPath,
                reference.ObjectType,
                reference.Table,
                reference.HierarchyName ?? string.Empty,
                reference.ObjectName), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var reference = group.First();
                string? pageDisplayName = null;
                if (reference.Page is not null)
                {
                    pageNames.TryGetValue(string.Join('\u001f', reference.Report, reference.Page), out pageDisplayName);
                }

                var container = reference.Visual is not null
                    ? "visual"
                    : reference.Page is not null
                        ? "page"
                        : "report";
                return new AssuranceFinding(
                    RuleId,
                    RuleVersion,
                    AssuranceCategories.ModelIntegrity,
                    FindingSeverities.Error,
                    $"The {container} references {reference.Table}[{reference.ObjectName}], but that {reference.ObjectType.ToLowerInvariant()} could not be resolved in the matching semantic model.",
                    $"Repair or remove the stale binding and confirm the {container} still behaves as intended.",
                    reference.Report,
                    reference.Page,
                    pageDisplayName,
                    reference.Visual,
                    SemanticModel: reference.SemanticModel,
                    reference.Table,
                    reference.ObjectName,
                    reference.ArtifactPath,
                    group.Select(item => item.EvidencePath).Distinct(StringComparer.Ordinal).ToArray(),
                    AssessmentTypes.Finding,
                    ReferenceUrl: null);
            });
    }
}

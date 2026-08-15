using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class UnresolvedReportReferenceRule : IAssuranceRule
{
    private const string RuleId = "PBI-MODEL-001";
    private const string RuleVersion = "1.1.0";

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
            .Where(group => group.Any(reference =>
                reference.ReferenceRelevance != VisualReferenceRelevance.HighConfidencePersisted))
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
                var actionableReferences = group
                    .Where(item => item.ReferenceRelevance != VisualReferenceRelevance.HighConfidencePersisted)
                    .ToArray();
                var hasNonFilterContext = actionableReferences.Any(item => item.UsageContext != UsageContexts.Filter);
                var hasDrillthroughContext = reference.Visual is null &&
                                             actionableReferences.Any(item => item.UsageContext == UsageContexts.Drillthrough);
                // Desktop PBIR also stores field-only filterConfig entries for ordinary projections.
                // Keep a Filter context alongside another visual context only when an actual filter condition is present.
                var hasConfiguredFilterCondition = actionableReferences.Any(item =>
                    item.UsageContext == UsageContexts.Filter &&
                    item.EvidencePath.Contains(".filter.", StringComparison.OrdinalIgnoreCase));
                var referenceContexts = actionableReferences
                    .Where(item => item.UsageContext != UsageContexts.Filter ||
                        (!hasDrillthroughContext && (!hasNonFilterContext || hasConfiguredFilterCondition)))
                    .GroupBy(
                        item => string.Join('\u001f', item.UsageContext, item.Role ?? string.Empty),
                        StringComparer.OrdinalIgnoreCase)
                    .Select(contextGroup => new FindingReferenceContext(
                        contextGroup.First().UsageContext,
                        contextGroup.First().Role))
                    .ToArray();

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
                    ReferenceUrl: null)
                {
                    ReferenceContexts = referenceContexts,
                };
            });
    }
}

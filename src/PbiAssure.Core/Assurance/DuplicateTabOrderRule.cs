using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class DuplicateTabOrderRule : IAssuranceRule
{
    private const string RuleId = "PBI-ACCESS-002";
    private const string RuleVersion = "1.1.0";

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        foreach (var report in inventory.Reports)
        {
            foreach (var page in report.Pages)
            {
                var duplicates = VisualGroupHierarchyResolver.Resolve(page)
                    .Where(container => container.IsComparable && container.Position.TabOrder is >= 0)
                    .GroupBy(container => new ScopeKey(container.ParentGroup?.Name))
                    .SelectMany(scope => scope
                        .GroupBy(container => container.Position.TabOrder!.Value)
                        .Where(group => group.Count() > 1)
                        .Select(group => new DuplicateScope(scope.Key, group)));

                foreach (var duplicate in duplicates)
                {
                    var scopeDescription = duplicate.Scope.ParentGroupName is null
                        ? "the page root"
                        : $"group '{duplicate.Scope.ParentGroupName}'";
                    yield return new AssuranceFinding(
                        RuleId,
                        RuleVersion,
                        AssuranceCategories.Accessibility,
                        FindingSeverities.Warning,
                        $"{duplicate.Containers.Count()} page items share explicit tab-order rank {duplicate.Containers.Key} within {scopeDescription}.",
                        "Assign a unique, intentional tab order that follows the page's logical reading sequence.",
                        report.Name,
                        page.Name,
                        page.DisplayName,
                        Visual: null,
                        SemanticModel: null,
                        Table: null,
                        ObjectName: null,
                        page.RelativePath,
                        duplicate.Containers.Select(container => container.RelativePath + "#$.position.tabOrder").ToArray(),
                        AssessmentTypes.Finding,
                        "https://learn.microsoft.com/power-bi/create-reports/desktop-accessibility-creating-reports")
                    {
                        VisualGroup = duplicate.Scope.ParentGroupName,
                    };
                }
            }
        }
    }

    private sealed record ScopeKey(string? ParentGroupName);

    private sealed record DuplicateScope(
        ScopeKey Scope,
        IGrouping<int, VisualContainerScope> Containers);
}

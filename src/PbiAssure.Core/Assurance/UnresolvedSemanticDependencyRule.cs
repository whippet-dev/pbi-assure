using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class UnresolvedSemanticDependencyRule : IAssuranceRule
{
    private const string RuleId = "PBI-MODEL-005";
    private const string RuleVersion = "1.0.0";

    private static readonly HashSet<string> StructuredKinds =
    [
        SemanticDependencyKinds.SortBy,
        SemanticDependencyKinds.HierarchyLevel,
        SemanticDependencyKinds.RelationshipEndpoint,
        SemanticDependencyKinds.PerspectiveMember,
        SemanticDependencyKinds.ReportMeasure,
    ];

    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory) =>
        CreateFindings(inventory.UnresolvedSemanticDependencies);

    internal static AssuranceFinding[] CreateFindings(
        IReadOnlyList<UnresolvedSemanticDependency> dependencies)
    {
        return dependencies
            .Where(IsSafeToSurface)
            .GroupBy(GroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => CreateFinding(group.First(), group))
            .OrderBy(finding => finding.SemanticModel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.Message, StringComparer.Ordinal)
            .ToArray();
    }

    internal static bool IsSafeToSurface(UnresolvedSemanticDependency dependency)
    {
        return StructuredKinds.Contains(dependency.DependencyKind) &&
               dependency.Reason.Contains("was not found", StringComparison.OrdinalIgnoreCase);
    }

    private static AssuranceFinding CreateFinding(
        UnresolvedSemanticDependency dependency,
        IEnumerable<UnresolvedSemanticDependency> groupedDependencies)
    {
        var evidencePaths = groupedDependencies
            .Select(item => item.EvidencePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AssuranceFinding(
            RuleId,
            RuleVersion,
            AssuranceCategories.ModelIntegrity,
            FindingSeverities.Warning,
            $"PBI Assure could not find the object referenced by {SourceDescription(dependency)}: {dependency.ReferenceText}.",
            "Check whether the referenced object was renamed or removed, then repair or remove the reference.",
            Report: null,
            Page: null,
            PageDisplayName: null,
            Visual: null,
            dependency.SemanticModel,
            Table: string.IsNullOrWhiteSpace(dependency.FromTable) ? null : dependency.FromTable,
            dependency.FromObjectName,
            ArtifactPath: evidencePaths[0],
            EvidencePaths: evidencePaths,
            AssessmentTypes.Finding,
            ReferenceUrl: null);
    }

    private static string SourceDescription(UnresolvedSemanticDependency dependency)
    {
        var objectName = string.IsNullOrWhiteSpace(dependency.FromTable)
            ? dependency.FromObjectName
            : $"{dependency.FromTable}[{dependency.FromObjectName}]";

        return dependency.DependencyKind switch
        {
            SemanticDependencyKinds.SortBy => $"the sort-by setting for {objectName}",
            SemanticDependencyKinds.HierarchyLevel => $"hierarchy level {objectName}",
            SemanticDependencyKinds.RelationshipEndpoint => $"relationship {dependency.FromObjectName}",
            SemanticDependencyKinds.PerspectiveMember => $"perspective {dependency.FromObjectName}",
            SemanticDependencyKinds.ReportMeasure => $"report measure {objectName}",
            _ => objectName,
        };
    }

    private static string GroupKey(UnresolvedSemanticDependency dependency) =>
        string.Join(
            '\u001f',
            dependency.SemanticModel,
            dependency.FromTable,
            dependency.FromObjectName,
            dependency.FromObjectType,
            dependency.FromHierarchyName ?? string.Empty,
            dependency.DependencyKind,
            dependency.ReferenceText,
            dependency.Reason);
}

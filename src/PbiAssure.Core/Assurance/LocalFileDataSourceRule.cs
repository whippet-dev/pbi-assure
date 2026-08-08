using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class LocalFileDataSourceRule : IAssuranceRule
{
    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        return inventory.DataSources
            .Where(source => source.LocationKind is DataSourceLocationKinds.LocalFile or
                DataSourceLocationKinds.NetworkFile)
            .GroupBy(source => string.Join('\u001f',
                source.SemanticModel, source.QueryName, source.Table, source.Partition),
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var source = group.First();
                var locationLabel = group.Any(item => item.LocationKind == DataSourceLocationKinds.NetworkFile)
                    ? "a network file location"
                    : "a file location on the developer's computer";
                return new AssuranceFinding(
                    "PBI-SOURCE-001",
                    "1.0.0",
                    AssuranceCategories.Compatibility,
                    FindingSeverities.Warning,
                    $"This Power Query uses {locationLabel}, which may not be available to other developers or refresh services.",
                    "Move the file to an approved shared location or configure a managed gateway and confirm refresh works outside the original development environment.",
                    Report: null,
                    Page: null,
                    PageDisplayName: null,
                    Visual: null,
                    SemanticModel: source.SemanticModel,
                    Table: source.Table,
                    ObjectName: source.QueryName,
                    ArtifactPath: source.ArtifactPath,
                    EvidencePaths: ["M connector call; location withheld"],
                    AssessmentType: AssessmentTypes.Finding,
                    ReferenceUrl: null);
            });
    }
}

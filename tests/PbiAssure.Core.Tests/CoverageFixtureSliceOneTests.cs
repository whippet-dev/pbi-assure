using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class CoverageFixtureSliceOneTests
{
    [Fact]
    public void CanonicalCoverageFixtureExercisesCoreUsagePowerQueryAndReportContexts()
    {
        var inventory = ProjectScanner.Scan(FixturePath());

        AssertUsage(inventory, "Fact", "DirectlyUsedMeasure", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "Fact", "IndirectlyUsedMeasure", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Fact", "ApparentlyUnusedMeasure", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "Fact", "UnusedBranchMeasure", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "Fact", "UnusedBranchColumn", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "Dimension", "StructurallyRequiredColumn", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "Fact", "HiddenProjectionOnlyColumn", SemanticUsageStates.DirectlyUsed);

        AssertTableUsage(inventory, "Fact", SemanticUsageStates.DirectlyUsed);
        AssertTableUsage(inventory, "Dimension", SemanticUsageStates.IndirectlyUsed);
        AssertTableUsage(inventory, "AggregationCoverage", SemanticUsageStates.StructurallyRequired);
        AssertTableUsage(inventory, "CalculatedDiagnostic", SemanticUsageStates.ApparentlyUnused);

        AssertDependency(inventory, SemanticDependencyKinds.Dax,
            "Fact", "DirectlyUsedMeasure", "Fact", "IndirectlyUsedMeasure");
        AssertDependency(inventory, SemanticDependencyKinds.Dax,
            "Fact", "CalculatedDependencyColumn", "Fact", "CalculatedSourceColumn");
        AssertDependency(inventory, SemanticDependencyKinds.Dax,
            "CalculatedDiagnostic", "CalculatedDiagnostic", "Fact", "CalculatedSourceColumn");
        AssertDependency(inventory, SemanticDependencyKinds.SortBy,
            "Fact", "SortLabel", "Fact", "SortKey");
        AssertDependency(inventory, SemanticDependencyKinds.HierarchyLevel,
            "Fact", "HierarchyLevelOne", "Fact", "HierarchyColumn");
        AssertDependency(inventory, SemanticDependencyKinds.RelationshipEndpoint,
            string.Empty, "ActiveRelationship", "Dimension", "StructurallyRequiredColumn");
        Assert.Contains(inventory.SemanticDependencies, dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.ContainingTable &&
            dependency.FromTable == "Fact" &&
            dependency.FromObjectName == "DirectlyUsedMeasure" &&
            dependency.ToObjectType == SemanticObjectTypes.Table &&
            dependency.ToObjectName == "Fact");

        AssertQuery(inventory, "LoadedFactQuery", PowerQueryUsageStates.LoadedToModel);
        AssertQuery(inventory, "ReferencedQuery", PowerQueryUsageStates.SupportingQuery);
        AssertQuery(inventory, "UnusedQuery", PowerQueryUsageStates.ApparentlyUnused);
        var dynamicQuery = AssertQuery(inventory, "DynamicQuery", PowerQueryUsageStates.SupportingQuery);
        Assert.True(dynamicQuery.HasDynamicReferences);
        AssertQueryDependency(inventory, "LoadedFactQuery", PowerQuerySourceKinds.TablePartition,
            "ReferencedQuery", PowerQuerySourceKinds.NamedExpression);
        AssertQueryDependency(inventory, "LineageTransformQuery", PowerQuerySourceKinds.NamedExpression,
            "Fact", PowerQuerySourceKinds.TablePartition);
        AssertQuery(inventory, "AddColumnLineageQuery", PowerQueryUsageStates.SupportingQuery);
        AssertQuery(inventory, "GroupLineageQuery", PowerQueryUsageStates.SupportingQuery);
        AssertQuery(inventory, "CombineLineageQuery", PowerQueryUsageStates.SupportingQuery);
        AssertQuery(inventory, "UnpivotLineageQuery", PowerQueryUsageStates.SupportingQuery);

        AssertColumnLineage(inventory, "Fact", "LineageMergeKey", PowerQueryColumnUsageKinds.MergeKey);
        AssertColumnLineage(inventory, "Dimension", "LineageExpandedColumn", PowerQueryColumnUsageKinds.ExpandedColumn);
        AssertColumnLineage(inventory, "Fact", "LineageSelectedColumn", PowerQueryColumnUsageKinds.SelectedColumn);
        AssertColumnLineage(inventory, "Fact", "LineageRemovedColumn", PowerQueryColumnUsageKinds.RemovedColumn);
        AssertColumnLineage(inventory, "Fact", "LineageRenamedColumn", PowerQueryColumnUsageKinds.RenamedColumn);
        AssertColumnLineage(inventory, "Fact", "LineageTransformedColumn", PowerQueryColumnUsageKinds.TransformedColumn);
        AssertColumnLineage(inventory, "Fact", "LineageAddLeft", PowerQueryColumnUsageKinds.AddedColumnExpression,
            "AddColumnLineageQuery");
        AssertColumnLineage(inventory, "Fact", "LineageAddRight", PowerQueryColumnUsageKinds.AddedColumnExpression,
            "AddColumnLineageQuery");
        AssertColumnLineage(inventory, "Fact", "LineageGroupKey", PowerQueryColumnUsageKinds.GroupingKey,
            "GroupLineageQuery");
        AssertColumnLineage(inventory, "Fact", "LineageGroupValue", PowerQueryColumnUsageKinds.AggregationExpression,
            "GroupLineageQuery");
        AssertColumnLineage(inventory, "Fact", "LineageCombineFact", PowerQueryColumnUsageKinds.CombinedColumn,
            "CombineLineageQuery");
        AssertColumnLineage(inventory, "Dimension", "LineageCombineDimension", PowerQueryColumnUsageKinds.CombinedColumn,
            "CombineLineageQuery");
        AssertColumnLineage(inventory, "Fact", "LineageUnpivotKeepA", PowerQueryColumnUsageKinds.UnpivotRetainedColumn,
            "UnpivotLineageQuery");
        AssertColumnLineage(inventory, "Fact", "LineageUnpivotKeepB", PowerQueryColumnUsageKinds.UnpivotRetainedColumn,
            "UnpivotLineageQuery");

        AssertSource(inventory, "LocalFileQuery", "File", DataSourceLocationKinds.LocalFile);
        AssertSource(inventory, "NetworkFileQuery", "Folder", DataSourceLocationKinds.NetworkFile);
        AssertSource(inventory, "RelativeFileQuery", "File", DataSourceLocationKinds.RelativeFile);
        AssertSource(inventory, "WebSourceQuery", "Web", DataSourceLocationKinds.WebAddress);
        AssertSource(inventory, "NamedServerQuery", "SQL Server", DataSourceLocationKinds.NamedServer);
        AssertSource(inventory, "DynamicSourceQuery", "File", DataSourceLocationKinds.DynamicOrUnspecified);

        AssertReference(inventory, "DirectlyUsedMeasure", UsageContexts.Projection, "main-page", "projection-visual");
        AssertReference(inventory, "VisualFilterOnlyColumn", UsageContexts.Filter, "main-page", "filter-only-visual");
        AssertReference(inventory, "VisualSortOnlyColumn", UsageContexts.Sort, "main-page", "sort-only-visual");
        AssertReference(inventory, "ReportFilterColumn", UsageContexts.Filter, page: null, visual: null);
        AssertReference(inventory, "PageFilterColumn", UsageContexts.Filter, "main-page", visual: null);

        var hiddenUsage = Assert.Single(inventory.SemanticObjectUsages, candidate =>
            candidate.Table == "Fact" && candidate.ObjectName == "HiddenProjectionOnlyColumn");
        var hiddenEvidence = Assert.Single(hiddenUsage.DirectReportReferences);
        Assert.Equal(UsageContexts.Projection, hiddenEvidence.UsageContext);
        Assert.True(hiddenEvidence.IsHiddenProjection);
        var projectionVisual = inventory.Reports.Single(report => report.Name == "PbiAssureCoverage")
            .Pages.Single(page => page.Name == "main-page")
            .Visuals.Single(visual => visual.Name == "projection-visual");
        Assert.True(Assert.Single(projectionVisual.FieldReferences, reference =>
            reference.ObjectName == "HiddenProjectionOnlyColumn").IsHiddenProjection);
        var provenance = DirectUsageProvenanceAnalyzer.Analyze(inventory);
        Assert.Equal(UserFacingStates.No, Assert.Single(provenance.ObjectSummaries, summary =>
            summary.ObjectName == "HiddenProjectionOnlyColumn").UserFacing);
        Assert.Equal(UserFacingStates.Yes, Assert.Single(provenance.ObjectSummaries, summary =>
            summary.ObjectName == "DirectlyUsedMeasure").UserFacing);
    }

    private static void AssertUsage(ProjectInventory inventory, string table, string name, string state)
    {
        var usage = Assert.Single(inventory.SemanticObjectUsages, candidate =>
            candidate.Table == table && candidate.ObjectName == name);
        Assert.Equal(state, usage.UsageState);
    }

    private static void AssertTableUsage(ProjectInventory inventory, string table, string state)
    {
        var usage = Assert.Single(inventory.SemanticTableUsages, candidate =>
            candidate.SemanticModel == "PbiAssureCoverage" && candidate.Table == table);
        Assert.Equal(state, usage.UsageState);
    }

    private static void AssertDependency(
        ProjectInventory inventory,
        string kind,
        string fromTable,
        string fromObject,
        string toTable,
        string toObject)
    {
        Assert.Contains(inventory.SemanticDependencies, dependency =>
            dependency.DependencyKind == kind &&
            dependency.FromTable == fromTable &&
            dependency.FromObjectName == fromObject &&
            dependency.ToTable == toTable &&
            dependency.ToObjectName == toObject);
    }

    private static PowerQueryUsage AssertQuery(ProjectInventory inventory, string name, string state)
    {
        var usage = Assert.Single(inventory.PowerQueryUsages, candidate => candidate.QueryName == name);
        Assert.Equal(state, usage.UsageState);
        return usage;
    }

    private static void AssertQueryDependency(ProjectInventory inventory, string fromQuery, string fromKind,
        string toQuery, string toKind) =>
        Assert.Contains(inventory.PowerQueryDependencies, dependency =>
            dependency.SemanticModel == "PbiAssureCoverage" &&
            dependency.FromQueryName == fromQuery && dependency.FromSourceKind == fromKind &&
            dependency.ToQueryName == toQuery && dependency.ToSourceKind == toKind);

    private static void AssertColumnLineage(
        ProjectInventory inventory,
        string table,
        string column,
        string kind,
        string consumer = "LineageTransformQuery")
    {
        Assert.Contains(inventory.PowerQueryColumnUsages, usage =>
            usage.SourceTable == table &&
            usage.SourceColumn == column &&
            usage.ConsumerQuery == consumer &&
            usage.UsageKind == kind);
    }

    private static void AssertSource(ProjectInventory inventory, string query, string connectorFamily, string locationKind)
    {
        Assert.Contains(inventory.DataSources, source =>
            source.QueryName == query && source.ConnectorFamily == connectorFamily && source.LocationKind == locationKind);
    }

    private static void AssertReference(
        ProjectInventory inventory,
        string objectName,
        string context,
        string? page,
        string? visual)
    {
        var usage = Assert.Single(inventory.SemanticObjectUsages, candidate =>
            candidate.Table == "Fact" && candidate.ObjectName == objectName);
        Assert.Contains(usage.DirectReportReferences, evidence =>
            evidence.UsageContext == context &&
            evidence.Page == page &&
            evidence.Visual == visual);
    }

    private static string FixturePath() => Path.Combine(
        FindRepositoryRoot(), "tests", "fixtures", "pbi-assure-coverage");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// A Desktop composite model persists its DirectQuery tables as <c>entity</c> partitions that carry no
/// M — only the name of the shared expression that connects to the remote model. That expression is
/// what serves the table, so it is a supporting query rather than an orphan, and its connector is the
/// table's source. The fixture is byte-for-byte Desktop output; see its README for provenance.
/// </summary>
public sealed class DesktopEntityPartitionEvidenceFixtureTests
{
    private const string Expression = "DirectQuery to AS - Tab Order Test Sample";
    private const string RemoteDateTable = "LocalDateTable_0f7e2929-1040-44dc-904f-1255dbea66ce";

    [Fact]
    public void DesktopPersistsTheEntityPartitionWithItsSourceBlock()
    {
        var model = Assert.Single(ScanFixture().SemanticModels);

        var partition = Assert.Single(Table(model, "testTable").Partitions);
        Assert.Equal("testTable", partition.Name);
        Assert.Equal("entity", partition.SourceType);
        Assert.Equal("directQuery", partition.Mode);
        Assert.Null(partition.Expression);
        Assert.Equal("testTable", partition.EntityName);
        Assert.Equal(Expression, partition.ExpressionSource);

        // The remote model's date table travels the same way; the local Enter data table does not.
        Assert.Equal(Expression, Assert.Single(Table(model, RemoteDateTable).Partitions).ExpressionSource);
        var local = Assert.Single(Table(model, "localData").Partitions);
        Assert.Equal("m", local.SourceType);
        Assert.Null(local.ExpressionSource);
        Assert.Null(local.EntityName);

        var expression = Assert.Single(model.NamedExpressions);
        Assert.Equal(Expression, expression.Name);
        Assert.Contains("AnalysisServices.Database(", expression.Expression, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSharedExpressionIsASupportingQueryUsedByTheRemoteTables()
    {
        var inventory = ScanFixture();

        var usage = Assert.Single(inventory.PowerQueryUsages, item => item.QueryName == Expression);
        Assert.Equal(PowerQueryUsageStates.SupportingQuery, usage.UsageState);
        Assert.Equal(PowerQueryRoles.HelperOrStaging, usage.QueryRole);
        Assert.Equal(
            [RemoteDateTable, "testTable"],
            usage.ReferencedBy.Select(reference => reference.FromQueryName).Order(StringComparer.Ordinal).ToArray());
        Assert.All(usage.ReferencedBy, reference =>
        {
            Assert.Equal(PowerQuerySourceKinds.TablePartition, reference.FromSourceKind);
            Assert.Equal(reference.FromQueryName, reference.FromTable);
        });

        Assert.Contains(inventory.PowerQueryDependencies, edge =>
            edge.FromQueryName == "testTable" && edge.FromPartition == "testTable" &&
            edge.ToQueryName == Expression && edge.ToSourceKind == PowerQuerySourceKinds.NamedExpression);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
        Assert.Equal(0, inventory.ApparentlyUnusedPowerQueryCount);
        // The remote tables are not M queries and get no query card of their own.
        Assert.Equal(2, inventory.PowerQueryCount);
    }

    [Fact]
    public void TheRemoteConnectorIsAttributedToEachRemoteTable()
    {
        var inventory = ScanFixture();

        foreach (var table in new[] { "testTable", RemoteDateTable })
        {
            var source = Assert.Single(inventory.DataSources, item => item.QueryName == table);
            Assert.Equal(PowerQuerySourceKinds.TablePartition, source.QuerySourceKind);
            Assert.Equal(table, source.Table);
            Assert.Equal(table, source.Partition);
            Assert.Equal("Analysis Services", source.ConnectorFamily);
            Assert.Equal("AnalysisServices.Database", source.ConnectorFunction);
            Assert.Equal(DataSourceLocationKinds.NamedServer, source.LocationKind);
            Assert.EndsWith($"tables/{table}.tmdl", source.ArtifactPath.Replace('\\', '/'), StringComparison.Ordinal);
        }

        // The expression keeps its own row; the local table keeps its entered data.
        Assert.Equal("Analysis Services", Assert.Single(inventory.DataSources, item => item.QueryName == Expression).ConnectorFamily);
        Assert.Equal("Entered data", Assert.Single(inventory.DataSources, item => item.QueryName == "localData").ConnectorFamily);
        Assert.Equal(4, inventory.DataSourceCount);
        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.LimitationId == "PBI-LIMIT-MODEL-PARTITION-SOURCE");

        var html = HtmlReportRenderer.Render(inventory);
        Assert.DoesNotContain("No recognised data sources", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Analysis Services</strong>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SemanticUsageOfTheRemoteTableIsUnchanged()
    {
        var inventory = ScanFixture();

        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "testTable", "Category").UsageState);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "testTable", "Value").UsageState);
        Assert.Equal(SemanticUsageStates.StructurallyRequired, Usage(inventory, "testTable", "Date").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "localData", "localData").UsageState);
        Assert.All(inventory.SemanticObjectUsages, usage =>
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
    }

    [Theory]
    [InlineData("\t\t\texpressionSource: 'DirectQuery to AS - Somewhere Else'\n", "'DirectQuery to AS - Somewhere Else', which is not defined in this project")]
    [InlineData("", "names no shared expression")]
    public void AnUnresolvableExpressionSourceIsSurfacedNotSkipped(string expressionSourceLine, string expectedReason)
    {
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Entity partition control",
        [
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Remote.tmdl",
                "table Remote\n\tcolumn Key\n\t\tdataType: int64\n\t\tsourceColumn: Key\n" +
                "\tpartition Remote = entity\n\t\tmode: directQuery\n\t\tsource\n\t\t\tentityName: Remote\n" +
                expressionSourceLine),
            File("Model.SemanticModel/definition/expressions.tmdl",
                "expression 'DirectQuery to AS - Actual' =\n\t\tlet\n\t\t\tSource = AnalysisServices.Database(\"powerbi://api.powerbi.com/v1.0/myorg/ws\", \"Actual\")\n\t\tin\n\t\t\tSource\n"),
        ]));

        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == "PBI-LIMIT-MODEL-PARTITION-SOURCE");
        Assert.Equal(AnalysisLimitationCauses.ReferenceUnresolved, limitation.Cause);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.Equal("Remote", limitation.Table);
        Assert.Equal("Remote", limitation.ObjectName);
        Assert.Contains(expectedReason, limitation.Reason, StringComparison.Ordinal);

        // No edge or source was invented, and the orphan role is withheld rather than asserted.
        Assert.Empty(inventory.PowerQueryDependencies);
        Assert.DoesNotContain(inventory.DataSources, source => source.QueryName == "Remote");
        var expression = Assert.Single(inventory.PowerQueryUsages, item => item.QueryName == "DirectQuery to AS - Actual");
        Assert.Equal(PowerQueryUsageStates.ApparentlyUnused, expression.UsageState);
        Assert.Null(expression.QueryRole);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Remote", "Key").ClassificationConfidence);
    }

    [Fact]
    public void OrdinaryMPartitionsAreUntouched()
    {
        var inventory = ScanFixture();

        var local = Assert.Single(inventory.PowerQueryUsages, item => item.QueryName == "localData");
        Assert.Equal(PowerQueryUsageStates.LoadedToModel, local.UsageState);
        Assert.Equal(PowerQueryRoles.LoadedOnly, local.QueryRole);
        Assert.Single(inventory.SemanticTablePowerQueryContexts);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static SemanticTableInventory Table(SemanticModelInventory model, string name) =>
        Assert.Single(model.Tables, table => table.Name == name);

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == objectName);

    private static ProjectFileContent File(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));

    private static ProjectInventory ScanFixture() => ProjectScanner.Scan(Path.Combine(
        RepositoryRoot(), "tests", "fixtures", "desktop-entity-partition-evidence"));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

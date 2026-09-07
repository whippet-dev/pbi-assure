using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// A column renamed in the model keeps its query-side name in <c>sourceColumn</c>. Matching Power Query
/// output columns by display name therefore lost the lineage for exactly those columns — the ones whose
/// two names differ and where the mapping is least obvious to a reader.
///
/// Desktop persistence of a differing <c>sourceColumn</c> is established: see
/// <c>pbi-assure-coverage/PbiAssureCoverage.SemanticModel/definition/tables/Metric Selector.tmdl</c>
/// (<c>column 'Metric Selector'</c> / <c>sourceColumn 'Value1'</c>) and the auto date tables in
/// <c>desktop-semantic-constructs</c> (<c>column Date</c> / <c>sourceColumn [Date]</c>). The cases below
/// are synthetic because no Desktop fixture pairs a renamed column with an M consumer of that column.
/// </summary>
public sealed class PowerQueryColumnSourceColumnTests
{
    [Fact]
    public void ARenamedColumnKeepsItsLineageThroughSourceColumn()
    {
        var usages = Scan(
            columns: "\tcolumn Renamed\n\t\tdataType: string\n\t\tsummarizeBy: none\n\t\tsourceColumn: Original\n",
            consumer: "Table.SelectColumns(Fact, {\"Original\"})");

        var usage = Assert.Single(usages);
        Assert.Equal("Renamed", usage.SourceColumn);
        // The query-side name is retained as the origin, so the two names stay visible.
        Assert.Equal("Original", usage.OriginColumn);
        Assert.Equal(PowerQueryColumnUsageKinds.SelectedColumn, usage.UsageKind);
        Assert.Equal("Consumer", usage.ConsumerQuery);
    }

    [Fact]
    public void AColumnWithNoSourceColumnStillMatchesOnItsOwnName()
    {
        var usages = Scan(
            columns: "\tcolumn Plain\n\t\tdataType: string\n\t\tsummarizeBy: none\n",
            consumer: "Table.SelectColumns(Fact, {\"Plain\"})");

        var usage = Assert.Single(usages);
        Assert.Equal("Plain", usage.SourceColumn);
        Assert.Null(usage.OriginColumn);
    }

    [Fact]
    public void ConsumerSideRenameLineageIsUnchanged()
    {
        var usages = Scan(
            columns: "\tcolumn Plain\n\t\tdataType: string\n\t\tsummarizeBy: none\n\t\tsourceColumn: Plain\n",
            consumer: "Table.RenameColumns(Fact, {{\"Plain\", \"Output\"}})");

        var usage = Assert.Single(usages);
        Assert.Equal("Plain", usage.SourceColumn);
        Assert.Equal(PowerQueryColumnUsageKinds.RenamedColumn, usage.UsageKind);
        Assert.Equal("Table.RenameColumns", usage.MFunction);
    }

    [Fact]
    public void SourceColumnResolvesThroughTheSourceQuerysOwnRenameChain()
    {
        // The source partition renames Raw to Original; the model then renamed that column to Renamed.
        var usages = Scan(
            columns: "\tcolumn Renamed\n\t\tdataType: string\n\t\tsummarizeBy: none\n\t\tsourceColumn: Original\n",
            consumer: "Table.SelectColumns(Fact, {\"Raw\"})",
            factExpression: "let\n  Source = #table(type table [Raw = text], {{\"a\"}}),\n" +
                            "  Final = Table.RenameColumns(Source, {{\"Raw\", \"Original\"}})\nin\n  Final");

        var usage = Assert.Single(usages);
        Assert.Equal("Renamed", usage.SourceColumn);
        Assert.Equal("Raw", usage.OriginColumn);
    }

    [Fact]
    public void TwoRenamedColumnsDoNotCrossWire()
    {
        var usages = Scan(
            columns:
                "\tcolumn FirstRenamed\n\t\tdataType: string\n\t\tsummarizeBy: none\n\t\tsourceColumn: AlphaSource\n\n" +
                "\tcolumn SecondRenamed\n\t\tdataType: string\n\t\tsummarizeBy: none\n\t\tsourceColumn: BetaSource\n",
            consumer: "Table.SelectColumns(Fact, {\"AlphaSource\", \"BetaSource\"})");

        Assert.Equal(2, usages.Length);
        var alpha = Assert.Single(usages, usage => usage.OriginColumn == "AlphaSource");
        var beta = Assert.Single(usages, usage => usage.OriginColumn == "BetaSource");
        Assert.Equal("FirstRenamed", alpha.SourceColumn);
        Assert.Equal("SecondRenamed", beta.SourceColumn);
    }

    [Fact]
    public void CollidingSourceNamesProduceNoInventedLineage()
    {
        // One column is named Shared, another claims Shared as its source name. Nothing in the metadata
        // says which the query column feeds, so neither gets a lineage row.
        var usages = Scan(
            columns:
                "\tcolumn Shared\n\t\tdataType: string\n\t\tsummarizeBy: none\n\n" +
                "\tcolumn Aliased\n\t\tdataType: string\n\t\tsummarizeBy: none\n\t\tsourceColumn: Shared\n",
            consumer: "Table.SelectColumns(Fact, {\"Shared\"})");

        Assert.Empty(usages);
    }

    [Fact]
    public void CanonicalLineageControlsAreUnchanged()
    {
        var inventory = ProjectScanner.Scan(Path.Combine(
            FindRepositoryRoot(), "tests", "fixtures", "pbi-assure-coverage"));

        foreach (var (table, column, kind, consumer) in new[]
        {
            ("Fact", "LineageMergeKey", PowerQueryColumnUsageKinds.MergeKey, "LineageTransformQuery"),
            ("Dimension", "LineageExpandedColumn", PowerQueryColumnUsageKinds.ExpandedColumn, "LineageTransformQuery"),
            ("Fact", "LineageSelectedColumn", PowerQueryColumnUsageKinds.SelectedColumn, "LineageTransformQuery"),
            ("Fact", "LineageRenamedColumn", PowerQueryColumnUsageKinds.RenamedColumn, "LineageTransformQuery"),
            ("Fact", "LineageAddLeft", PowerQueryColumnUsageKinds.AddedColumnExpression, "AddColumnLineageQuery"),
            ("Fact", "LineageGroupKey", PowerQueryColumnUsageKinds.GroupingKey, "GroupLineageQuery"),
            ("Fact", "LineageUnpivotKeepA", PowerQueryColumnUsageKinds.UnpivotRetainedColumn, "UnpivotLineageQuery"),
        })
        {
            Assert.Contains(inventory.PowerQueryColumnUsages, usage =>
                usage.SourceTable == table && usage.SourceColumn == column &&
                usage.UsageKind == kind && usage.ConsumerQuery == consumer);
        }
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static PowerQueryColumnUsage[] Scan(string columns, string consumer, string? factExpression = null)
    {
        var expression = factExpression ?? "#table(type table [Original = text], {{\"a\"}})";
        var indented = string.Join("\n", expression.Split('\n').Select(line => "\t\t\t\t" + line));

        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            // Column lineage is read from let bindings, as every canonical lineage query is written.
            File("Model.SemanticModel/definition/expressions.tmdl",
                "expression Consumer =\n\t\tlet\n\t\t\tStep = " + consumer + "\n\t\tin\n\t\t\tStep\n"),
            File("Model.SemanticModel/definition/tables/Fact.tmdl",
                "table Fact\n\n" + columns +
                "\n\tpartition Fact = m\n\t\tmode: import\n\t\tsource =\n" + indented + "\n"),
        };

        return [.. ProjectScanner.Scan(new InMemoryProjectFileSource("SourceColumn", files)).PowerQueryColumnUsages];
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class PowerQueryColumnLineageTests : IDisposable
{
    private readonly string testRoot;

    public PowerQueryColumnLineageTests()
    {
        testRoot = Path.Combine(Path.GetTempPath(), "PbiAssure.ColumnLineage.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
    }

    [Fact]
    public void ScanFindsTheSevenRepresentativeJoinAndExpandCasesWithoutChangingSemanticUsage()
    {
        WriteTable("Age", ["Age", "Age Bucket"], "#table({}, {})");
        WriteTable("Calendar", ["Week"], "#table({}, {})");
        WriteTable("Issues and Promotions", ["ID", "Issue", "Promotion"], "#table({}, {})");
        WriteTable("Product", ["ProductID", "Price Range"], "#table({}, {})");
        WriteTable("Customer", ["Result"],
            """
            let
                Base = #table({}, {}),
                AgeJoin = Table.NestedJoin(Base, {"Age"}, Age, {"Age"}, "Age.1", JoinKind.LeftOuter),
                AgeExpand = Table.ExpandTableColumn(AgeJoin, "Age.1", {"Age Bucket"}, {"Age Bucket"}),
                IssuesJoin = Table.NestedJoin(AgeExpand, {"ID"}, #"Issues and Promotions", {"ID"}, "Issues", JoinKind.LeftOuter),
                Removed = Table.RemoveColumns(IssuesJoin, {"Unused local column"}),
                IssuesExpand = Table.ExpandTableColumn(Removed, "Issues", {"Issue", "Promotion"}, {"Issue", "Promotion"}),
                ProductJoin = Table.NestedJoin(IssuesExpand, {"ProductID"}, Product, {"ProductID"}, "Product", JoinKind.LeftOuter),
                ProductExpand = Table.ExpandTableColumn(ProductJoin, "Product", {"Price Range"}, {"Price Range"})
            in
                ProductExpand
            """);
        WriteTable("Sales", ["Result"],
            """
            let
                Base = #table({}, {}),
                CalendarJoin = Table.NestedJoin(Base, {"Week"}, Calendar, {"Week"}, "Calendar", JoinKind.LeftOuter)
            in
                CalendarJoin
            """);

        var result = ProjectScanner.Scan(testRoot);

        AssertUsage(result, "Age", "Age", "Customer", PowerQueryColumnUsageKinds.MergeKey);
        AssertUsage(result, "Age", "Age Bucket", "Customer", PowerQueryColumnUsageKinds.ExpandedColumn);
        AssertUsage(result, "Calendar", "Week", "Sales", PowerQueryColumnUsageKinds.MergeKey);
        AssertUsage(result, "Issues and Promotions", "ID", "Customer", PowerQueryColumnUsageKinds.MergeKey);
        AssertUsage(result, "Issues and Promotions", "Issue", "Customer", PowerQueryColumnUsageKinds.ExpandedColumn);
        AssertUsage(result, "Issues and Promotions", "Promotion", "Customer", PowerQueryColumnUsageKinds.ExpandedColumn);
        AssertUsage(result, "Product", "Price Range", "Customer", PowerQueryColumnUsageKinds.ExpandedColumn);
        Assert.All(result.SemanticObjectUsages.Where(usage =>
            usage.Table is "Age" or "Calendar" or "Issues and Promotions" or "Product"), usage =>
            Assert.Equal(SemanticUsageStates.ApparentlyUnused, usage.UsageState));
        Assert.Contains(result.PowerQueryDependencies, edge =>
            edge.FromQueryName == "Customer" && edge.ToQueryName == "Age");
    }

    [Fact]
    public void ScanPropagatesSimpleRenameIdentityAndDeduplicatesRepeatedReferences()
    {
        WriteTable("Source", ["Old", "A", "B"], "#table({}, {})");
        WriteTable("Consumer", ["Result"],
            """
            let
                Alias = Source,
                Renamed = Table.RenameColumns(Alias, {{"Old", "New"}}),
                Selected = Table.SelectColumns(Renamed, {"New"}),
                SelectedAgain = Table.SelectColumns(Renamed, {"New"}),
                Typed = Table.TransformColumnTypes(Source, {{"A", type text}, {"B", Int64.Type}})
            in
                SelectedAgain
            """);
        WriteTable("Second Consumer", ["Result"],
            """
            let
                Selected = Table.SelectColumns(Source, {"Old"})
            in
                Selected
            """);

        var result = ProjectScanner.Scan(testRoot);

        AssertUsage(result, "Source", "Old", "Consumer", PowerQueryColumnUsageKinds.RenamedColumn);
        var selected = Assert.Single(result.PowerQueryColumnUsages, usage =>
            usage.SourceTable == "Source" &&
            usage.SourceColumn == "Old" &&
            usage.ConsumerQuery == "Consumer" &&
            usage.UsageKind == PowerQueryColumnUsageKinds.SelectedColumn);
        Assert.Equal("Table.SelectColumns", selected.MFunction);
        AssertUsage(result, "Source", "A", "Consumer", PowerQueryColumnUsageKinds.TransformedColumn);
        AssertUsage(result, "Source", "B", "Consumer", PowerQueryColumnUsageKinds.TransformedColumn);
        AssertUsage(result, "Source", "Old", "Second Consumer", PowerQueryColumnUsageKinds.SelectedColumn);
    }

    [Fact]
    public void ScanLeavesDynamicColumnListsUnresolvedRatherThanGuessing()
    {
        WriteTable("Source", ["A"], "#table({}, {})");
        WriteTable("Dynamic Consumer", ["Result"],
            """
            let
                ColumnsToKeep = {"A"},
                Selected = Table.SelectColumns(Source, ColumnsToKeep)
            in
                Selected
            """);

        var result = ProjectScanner.Scan(testRoot);

        Assert.Empty(result.PowerQueryColumnUsages);
        Assert.Contains(result.PowerQueryDependencies, edge =>
            edge.FromQueryName == "Dynamic Consumer" && edge.ToQueryName == "Source");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused,
            result.SemanticObjectUsages.Single(usage => usage.Table == "Source" && usage.ObjectName == "A").UsageState);
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private void WriteTable(string tableName, string[] columns, string expression)
    {
        var columnDefinitions = string.Join(Environment.NewLine, columns.Select(column =>
            $"    column '{column.Replace("'", "''")}'\n        dataType: string"));
        WriteFile(
            Path.Combine("Lineage.SemanticModel", "definition", "tables", $"{tableName}.tmdl"),
            $"table '{tableName.Replace("'", "''")}'\n{columnDefinitions}\n    partition '{tableName.Replace("'", "''")}' = m\n        mode: import\n        source =\n{Indent(expression, 12)}\n");
    }

    private static string Indent(string value, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join(Environment.NewLine, value.Replace("\r\n", "\n").Split('\n').Select(line => prefix + line));
    }

    private static void AssertUsage(
        ProjectInventory result,
        string table,
        string column,
        string consumer,
        string kind)
    {
        Assert.Contains(result.PowerQueryColumnUsages, usage =>
            usage.SourceTable == table &&
            usage.SourceColumn == column &&
            usage.ConsumerQuery == consumer &&
            usage.UsageKind == kind);
    }

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(testRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}

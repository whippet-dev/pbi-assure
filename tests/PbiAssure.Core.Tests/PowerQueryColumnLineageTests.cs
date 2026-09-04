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

    [Fact]
    public void ScanExtractsStaticAddGroupCombineAndUnpivotColumnEvidence()
    {
        WriteTable("Add Source", ["Add A", "Add B"], "#table({}, {})");
        WriteTable("Add Consumer", ["Result"], """
            let
                Added = Table.AddColumn(#"Add Source", "Added", each [Add A] & [Add B])
            in
                Added
            """);
        WriteTable("Group Source", ["Group Key", "Group Value"], "#table({}, {})");
        WriteTable("Group Consumer", ["Result"], """
            let
                Grouped = Table.Group(#"Group Source", {"Group Key"}, {{"Total", each List.Sum([Group Value]), type number}})
            in
                Grouped
            """);
        WriteTable("Combine A", ["Combine A"], "#table({}, {})");
        WriteTable("Combine B", ["Combine B"], "#table({}, {})");
        WriteTable("Combine Consumer", ["Result"], """
            let
                Combined = Table.Combine({#"Combine A", #"Combine B"}, {"Combine A", "Combine B"})
            in
                Combined
            """);
        WriteTable("Unpivot Source", ["Keep A", "Keep B", "Value"], "#table({}, {})");
        WriteTable("Unpivot Consumer", ["Result"], """
            let
                Unpivoted = Table.UnpivotOtherColumns(#"Unpivot Source", {"Keep A", "Keep B"}, "Attribute", "Value")
            in
                Unpivoted
            """);

        var result = ProjectScanner.Scan(testRoot);

        AssertUsage(result, "Add Source", "Add A", "Add Consumer", PowerQueryColumnUsageKinds.AddedColumnExpression);
        AssertUsage(result, "Add Source", "Add B", "Add Consumer", PowerQueryColumnUsageKinds.AddedColumnExpression);
        AssertUsage(result, "Group Source", "Group Key", "Group Consumer", PowerQueryColumnUsageKinds.GroupingKey);
        AssertUsage(result, "Group Source", "Group Value", "Group Consumer", PowerQueryColumnUsageKinds.AggregationExpression);
        AssertUsage(result, "Combine A", "Combine A", "Combine Consumer", PowerQueryColumnUsageKinds.CombinedColumn);
        AssertUsage(result, "Combine B", "Combine B", "Combine Consumer", PowerQueryColumnUsageKinds.CombinedColumn);
        AssertUsage(result, "Unpivot Source", "Keep A", "Unpivot Consumer", PowerQueryColumnUsageKinds.UnpivotRetainedColumn);
        AssertUsage(result, "Unpivot Source", "Keep B", "Unpivot Consumer", PowerQueryColumnUsageKinds.UnpivotRetainedColumn);
        Assert.All(result.PowerQueryColumnUsages.Where(usage => usage.ConsumerQuery.EndsWith("Consumer", StringComparison.Ordinal)),
            usage => Assert.False(string.IsNullOrWhiteSpace(usage.StepName)));
    }

    [Fact]
    public void ScanDoesNotGuessDynamicTransformationColumnsOrCombineRuntimeSchemas()
    {
        WriteTable("Source", ["A"], "#table({}, {})");
        WriteTable("Dynamic Add", ["Result"], """
            let
                Added = Table.AddColumn(Source, "Added", each Record.Field(_, ColumnName))
            in
                Added
            """);
        WriteTable("Dynamic Group", ["Result"], """
            let
                Grouped = Table.Group(Source, GroupingColumns, {{"Total", each Record.Field(_, AggregateColumn)}})
            in
                Grouped
            """);
        WriteTable("Schema Combine", ["Result"], """
            let
                Combined = Table.Combine({Source})
            in
                Combined
            """);
        WriteTable("Dynamic Unpivot", ["Result"], """
            let
                Unpivoted = Table.UnpivotOtherColumns(Source, RetainedColumns, "Attribute", "Value")
            in
                Unpivoted
            """);

        var result = ProjectScanner.Scan(testRoot);

        Assert.DoesNotContain(result.PowerQueryColumnUsages, usage =>
            usage.ConsumerQuery is "Dynamic Add" or "Dynamic Group" or "Schema Combine" or "Dynamic Unpivot");
        Assert.Contains(result.PowerQueryDependencies, edge =>
            edge.FromQueryName == "Schema Combine" && edge.ToQueryName == "Source");
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

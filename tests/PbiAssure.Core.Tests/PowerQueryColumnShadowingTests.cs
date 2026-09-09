using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class PowerQueryColumnShadowingTests
{
    [Theory]
    [InlineData("let\n Data = #table({\"ID\"}, {{1}}),\n Selected = Table.SelectColumns(Data, {\"ID\"})\nin\n Selected")]
    [InlineData("let\n Selected = Table.SelectColumns(Data, {\"ID\"}),\n Data = #table({\"ID\"}, {{1}})\nin\n Selected")]
    [InlineData("let\n Data = #table({\"ID\"}, {{1}}),\n Alias = Data,\n Selected = Table.SelectColumns(Alias, {\"ID\"})\nin\n Selected")]
    public void LocalOnlyConsumerCannotInventGlobalColumnEvidence(string expression)
    {
        var inventory = Scan(expression);
        Assert.Empty(inventory.PowerQueryDependencies);
        Assert.Empty(inventory.PowerQueryColumnUsages);
        AssertPowerQueryUsed(inventory, "No");
        var usage = Assert.Single(inventory.SemanticObjectUsages);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, usage.UsageState);
        Assert.Equal("0.26", inventory.SchemaVersion);
    }

    [Theory]
    [InlineData("let\n Selected = Table.SelectColumns(Data, {\"ID\"})\nin\n Selected")]
    [InlineData("let\n Source = Data,\n Selected = Table.SelectColumns(Source, {\"ID\"})\nin\n Selected")]
    [InlineData("let\n data = #table({\"ID\"}, {{1}}),\n Selected = Table.SelectColumns(Data, {\"ID\"})\nin\n Selected")]
    [InlineData("let\n Source = Data,\n source = #table({\"ID\"}, {{1}}),\n Selected = Table.SelectColumns(Source, {\"ID\"})\nin\n Selected")]
    [InlineData("let\n Data = Data,\n Selected = Table.SelectColumns(Data, {\"ID\"})\nin\n Selected")]
    public void GenuineGlobalReferencesAndCaseDistinctStepsKeepLineage(string expression)
    {
        var inventory = Scan(expression);
        Assert.Contains(inventory.PowerQueryDependencies, edge => edge.FromQueryName == "Consumer" && edge.ToQueryName == "Data");
        var column = Assert.Single(inventory.PowerQueryColumnUsages);
        Assert.Equal("Data", column.SourceQuery);
        Assert.Equal("ID", column.SourceColumn);
        Assert.Equal("Consumer", column.ConsumerQuery);
        AssertPowerQueryUsed(inventory, "Yes");
    }

    private static void AssertPowerQueryUsed(ProjectInventory inventory, string expected)
    {
        var lines = SemanticUsageCsvRenderer.Render(inventory).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var headers = lines[0].Split(',');
        var row = Assert.Single(lines.Skip(1)).Split(',');
        Assert.Equal("Data", row[Array.IndexOf(headers, "Table")]);
        Assert.Equal("ID", row[Array.IndexOf(headers, "Object")]);
        Assert.Equal(expected, row[Array.IndexOf(headers, "PowerQueryUsed")]);
    }

    private static ProjectInventory Scan(string expression)
    {
        var files = new Dictionary<string, string>
        {
            ["Model.pbip"] = "{}",
            ["Model.SemanticModel/definition/tables/Data.tmdl"] =
                "table Data\n\tcolumn ID\n\t\tdataType: int64\n\t\tsourceColumn: ID\n" +
                "\tpartition Data = m\n\t\tsource = #table({\"ID\"}, {{99}})\n",
            ["Model.SemanticModel/definition/expressions.tmdl"] =
                "expression Consumer =\n\t" + expression.Replace("\n", "\n\t", StringComparison.Ordinal) + "\n",
        };
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Column shadowing", files.Select(file =>
            new ProjectFileContent(file.Key, Encoding.UTF8.GetBytes(file.Value)))));
    }
}

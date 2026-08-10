using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class SemanticUsageCsvRendererTests : IDisposable
{
    private readonly string testRoot = Path.Combine(Path.GetTempPath(), "PbiAssure.SemanticUsageCsv.Tests", Guid.NewGuid().ToString("N"));

    public SemanticUsageCsvRendererTests()
    {
        Directory.CreateDirectory(testRoot);
    }

    [Fact]
    public void RenderExportsOneDeveloperObjectRowWithAggregatedPowerQueryLineage()
    {
        WriteTable("Source", ["Key", "Description"], "#table({}, {})");
        WriteTable("Consumer", ["Result"],
            """
            let
                Base = #table({}, {}),
                Joined = Table.NestedJoin(Base, {"Key"}, Source, {"Key"}, "Source", JoinKind.LeftOuter),
                Expanded = Table.ExpandTableColumn(Joined, "Source", {"Description"}, {"Description"})
            in
                Expanded
            """);
        WriteFile(Path.Combine("Csv.SemanticModel", "definition", "tables", "LocalDateTable_generated.tmdl"),
            """
            table LocalDateTable_generated
                isHidden
                annotation __PBI_LocalDateTable = true
                column Date
                    dataType: dateTime
            """);

        var inventory = ProjectScanner.Scan(testRoot);
        var rows = ReadCsv(SemanticUsageCsvRenderer.Render(inventory));
        var header = rows[0];
        var sourceKey = RowFor(rows, header, "Source", "Key");
        var sourceDescription = RowFor(rows, header, "Source", "Description");

        Assert.Equal(
            ["Report", "Table", "Object", "ObjectType", "SemanticUsage", "SemanticReason", "ReportLocationCount", "ReportLocations", "PowerQueryUsed", "PowerQueryConsumers", "PowerQueryRoles", "PowerQueryEvidence", "ReviewCandidate"],
            header);
        Assert.Equal(inventory.DeveloperSemanticObjectCount, rows.Count - 1);
        Assert.DoesNotContain(rows.Skip(1), row => Value(row, header, "Table") == "LocalDateTable_generated");
        Assert.Equal("Apparently unused", Value(sourceKey, header, "SemanticUsage"));
        Assert.Equal("Yes", Value(sourceKey, header, "PowerQueryUsed"));
        Assert.Equal("Consumer", Value(sourceKey, header, "PowerQueryConsumers"));
        Assert.Equal("Merge key", Value(sourceKey, header, "PowerQueryRoles"));
        Assert.Equal("Table.NestedJoin", Value(sourceKey, header, "PowerQueryEvidence"));
        Assert.Equal("Yes", Value(sourceKey, header, "ReviewCandidate"));
        Assert.Equal("Expanded column", Value(sourceDescription, header, "PowerQueryRoles"));
        Assert.Equal("Table.ExpandTableColumn", Value(sourceDescription, header, "PowerQueryEvidence"));
    }

    [Fact]
    public void RenderPreservesUsageStatesReasonsLocationsAndCsvEscaping()
    {
        WriteTable("Sales", ["Metric", "Sort", "Direct"], "#table({}, {})");
        var inventory = ProjectScanner.Scan(testRoot);
        var model = inventory.SemanticModels.Single().Name;
        var reportReference = new SemanticUsageEvidence(
            "Report, One",
            "Overview",
            "visual",
            "ignored",
            UsageContexts.Projection,
            "Values",
            "ignored");
        var specialName = "Name, \"quoted\"\nÅ";
        SemanticObjectUsage[] semanticUsages =
        [
            new SemanticObjectUsage(model, "Sales", specialName, SemanticObjectTypes.Column, null, [reportReference, reportReference], SemanticUsageStates.DirectlyUsed),
            new SemanticObjectUsage(model, "Sales", "Sort", SemanticObjectTypes.Column, null, [], SemanticUsageStates.UsedOnlyByUnusedBranch),
            new SemanticObjectUsage(model, "Sales", "Metric", SemanticObjectTypes.Column, null, [], SemanticUsageStates.ApparentlyUnused),
            new SemanticObjectUsage(model, "Sales", "Direct", SemanticObjectTypes.Column, null, [], SemanticUsageStates.StructurallyRequired),
        ];
        SemanticDependencyEdge[] semanticDependencies =
        [
            new SemanticDependencyEdge(model, "Sales", "Metric", SemanticObjectTypes.Column, null, "Sales", "Sort", SemanticObjectTypes.Column, null, SemanticDependencyKinds.SortBy, "ignored", "ignored"),
        ];

        var rows = ReadCsv(SemanticUsageCsvRenderer.Render(inventory with
        {
            SemanticObjectUsages = semanticUsages,
            SemanticDependencies = semanticDependencies,
        }));
        var header = rows[0];
        var direct = RowFor(rows, header, "Sales", specialName);
        var sort = RowFor(rows, header, "Sales", "Sort");
        var metric = RowFor(rows, header, "Sales", "Metric");
        var structural = RowFor(rows, header, "Sales", "Direct");

        Assert.Equal("1", Value(direct, header, "ReportLocationCount"));
        Assert.Equal("Overview > Visual > Values", Value(direct, header, "ReportLocations"));
        Assert.Equal("No", Value(direct, header, "ReviewCandidate"));
        Assert.Equal("Used only by unused branch", Value(sort, header, "SemanticUsage"));
        Assert.Equal("Sorts Sales[Metric]", Value(sort, header, "SemanticReason"));
        Assert.Equal("Yes", Value(sort, header, "ReviewCandidate"));
        Assert.Equal("Apparently unused", Value(metric, header, "SemanticUsage"));
        Assert.Equal("Yes", Value(metric, header, "ReviewCandidate"));
        Assert.Equal("Structurally required", Value(structural, header, "SemanticUsage"));
        Assert.Equal("No", Value(structural, header, "ReviewCandidate"));
        Assert.Contains("\"Name, \"\"quoted\"\"", SemanticUsageCsvRenderer.Render(inventory with { SemanticObjectUsages = semanticUsages }), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"https://example.invalid\")")]
    [InlineData("+SUM(1,1)")]
    [InlineData("-1+1")]
    [InlineData("@SUM(1,1)")]
    [InlineData("\t=1+1")]
    [InlineData("\r=1+1")]
    [InlineData("\n=1+1")]
    public void RenderNeutralizesSpreadsheetFormulaTextWithoutChangingNumericCounts(string hostileName)
    {
        WriteTable("Sales", ["Safe"], "#table({}, {})");
        var inventory = ProjectScanner.Scan(testRoot);
        var model = inventory.SemanticModels.Single().Name;
        SemanticObjectUsage[] semanticUsages =
        [
            new SemanticObjectUsage(
                model,
                "Sales",
                hostileName,
                SemanticObjectTypes.Column,
                null,
                [],
                SemanticUsageStates.ApparentlyUnused),
        ];

        var rows = ReadCsv(SemanticUsageCsvRenderer.Render(inventory with
        {
            SemanticObjectUsages = semanticUsages,
        }));
        var header = rows[0];
        var row = Assert.Single(rows.Skip(1));

        Assert.Equal("'" + hostileName, Value(row, header, "Object"));
        Assert.Equal("0", Value(row, header, "ReportLocationCount"));
        Assert.Equal("Yes", Value(row, header, "ReviewCandidate"));
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
            Path.Combine("Csv.SemanticModel", "definition", "tables", $"{tableName}.tmdl"),
            $"table '{tableName.Replace("'", "''")}'\n{columnDefinitions}\n    partition '{tableName.Replace("'", "''")}' = m\n        mode: import\n        source =\n{Indent(expression, 12)}\n");
    }

    private void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(testRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string Indent(string value, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join(Environment.NewLine, value.Replace("\r\n", "\n").Split('\n').Select(line => prefix + line));
    }

    private static string[] RowFor(IReadOnlyList<string[]> rows, string[] header, string table, string objectName) =>
        Assert.Single(rows.Skip(1), row => Value(row, header, "Table") == table && Value(row, header, "Object") == objectName);

    private static string Value(string[] row, string[] header, string name) => row[Array.IndexOf(header, name)];

    private static List<string[]> ReadCsv(string csv)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (character == '"')
            {
                if (quoted && index + 1 < csv.Length && csv[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (character == '\r' && !quoted && index + 1 < csv.Length && csv[index + 1] == '\n')
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
                row.Clear();
                field.Clear();
                index++;
            }
            else
            {
                field.Append(character);
            }
        }

        return rows;
    }
}

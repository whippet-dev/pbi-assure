using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;
using PbiAssure.Reporting.Exports;

namespace PbiAssure.Core.Tests;

public sealed class TmdlDescriptionTests
{
    [Fact]
    public void DesktopFixtureRetainsExactDescriptionsAndUndescribedControls()
    {
        var inventory = ProjectScanner.Scan(FixtureRoot("desktop-descriptions-sanitized"));
        var model = Assert.Single(inventory.SemanticModels);
        var table = Assert.Single(model.Tables, item => item.Name == "TableA");
        Assert.Equal("Contains test data for description persistence - Desktop authored.", table.Description);
        Assert.Equal("Customer's category: used for grouping.",
            Assert.Single(table.Columns, item => item.Name == "ColumnA1").Description);
        Assert.Null(Assert.Single(table.Columns, item => item.Name == "ColumnA2").Description);
        Assert.Equal("Returns total sales - \n\nbefore adjustments.",
            Assert.Single(table.Measures, item => item.Name == "MeasureA").Description);
        Assert.Null(Assert.Single(table.Measures, item => item.Name == "MeasureB").Description);
        var control = Assert.Single(model.Tables, item => item.Name == "TableB");
        Assert.Null(control.Description);
        Assert.All(control.Columns, column => Assert.Null(column.Description));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void PhysicalLineEndingsDoNotChangeLogicalDescriptionText(string newline)
    {
        var inventory = ScanFixtureText(newline, removeDescriptions: false);
        var description = inventory.SemanticModels.Single().Tables.Single(table => table.Name == "TableA")
            .Measures.Single(measure => measure.Name == "MeasureA").Description;
        Assert.Equal("Returns total sales - \n\nbefore adjustments.", description);
        Assert.DoesNotContain('\r', description!);
    }

    // Synthetic parser-boundary cases, not additional claims about Desktop serialization.
    [Theory]
    [InlineData("\t/// detached\n\n", null)]
    [InlineData("\t/// detached\n\t// ordinary comment\n", null)]
    [InlineData("\t\t/// wrong indentation\n", null)]
    [InlineData("/// wrong indentation\n", null)]
    [InlineData("\t// ordinary comment\n", null)]
    [InlineData("\t/// detached\n\n\t/// attached\n", "attached")]
    [InlineData("\t\t/// wrong indentation\n\t/// attached\n", "attached")]
    [InlineData("\t///  leading and trailing  \n\t/// \n\t/// last\n", " leading and trailing  \n\nlast")]
    public void OnlyContiguousSameIndentationDescriptionsBind(string preceding, string? expected)
    {
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Description boundaries",
        [
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Table.tmdl",
                "table Table\n" + preceding + "\tmeasure Target = 1\n\tmeasure Control = 2\n"),
        ]));
        var table = Assert.Single(Assert.Single(inventory.SemanticModels).Tables);
        Assert.Equal(expected, table.Measures.Single(measure => measure.Name == "Target").Description);
        Assert.Null(table.Measures.Single(measure => measure.Name == "Control").Description);
        Assert.Null(table.Description);
    }

    [Fact]
    public void DescriptionsDoNotChangeClassificationConfidenceDependenciesOrExports()
    {
        var described = ScanFixtureText("\n", removeDescriptions: false);
        var control = ScanFixtureText("\n", removeDescriptions: true);
        Assert.Equal(JsonSerializer.Serialize(control.SemanticObjectUsages), JsonSerializer.Serialize(described.SemanticObjectUsages));
        Assert.Equal(JsonSerializer.Serialize(control.SemanticDependencies), JsonSerializer.Serialize(described.SemanticDependencies));
        AssertOutputsEqual(control, described);
    }

    [Theory]
    [InlineData("desktop-descriptions-sanitized")]
    [InlineData("kpi-detailrows-sanitized")]
    public void DescriptionPropertiesAreIgnoredByJsonAndAllCsvContracts(string fixture)
    {
        var inventory = ProjectScanner.Scan(FixtureRoot(fixture));
        // Populate every type, including report-used objects, so mapping coverage is not header-only.
        var changed = inventory with
        {
            SemanticModels = inventory.SemanticModels.Select(model => model with
            {
                Tables = model.Tables.Select(table => table with
                {
                    Description = "Table metadata sentinel",
                    Columns = table.Columns.Select(column => column with { Description = "Column metadata sentinel" }).ToArray(),
                    Measures = table.Measures.Select(measure => measure with { Description = "Measure metadata\n\nsentinel " }).ToArray(),
                }).ToArray(),
            }).ToArray(),
        };
        Assert.Equal("0.26", inventory.SchemaVersion);
        Assert.Equal(JsonSerializer.Serialize(inventory), JsonSerializer.Serialize(changed));
        Assert.DoesNotContain("\"Description\"", JsonSerializer.Serialize(changed), StringComparison.Ordinal);
        AssertOutputsEqual(inventory, changed);
        foreach (var preset in Enum.GetValues<ExportPreset>())
        {
            Assert.DoesNotContain(ExportPresetCatalog.GetAllowedColumns(preset), column => column.Id == "Description");
        }
    }

    private static void AssertOutputsEqual(ProjectInventory expected, ProjectInventory actual)
    {
        Assert.Equal(SemanticUsageCsvRenderer.Render(expected), SemanticUsageCsvRenderer.Render(actual));
        foreach (var preset in Enum.GetValues<ExportPreset>())
        {
            Assert.Equal(ExportCsvRenderer.Render(expected, new ExportRequest(preset)),
                ExportCsvRenderer.Render(actual, new ExportRequest(preset)));
            var allColumns = new ExportRequest(preset, ExportPresetCatalog.GetAllowedColumns(preset).Select(column => column.Id).ToArray());
            Assert.Equal(ExportCsvRenderer.Render(expected, allColumns), ExportCsvRenderer.Render(actual, allColumns));
        }
    }

    private static ProjectInventory ScanFixtureText(string newline, bool removeDescriptions)
    {
        var root = FixtureRoot("desktop-descriptions-sanitized");
        var files = Directory.EnumerateFiles(Path.Combine(root, "pbi-descriptions.SemanticModel"), "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var lines = System.IO.File.ReadAllLines(path);
                var retained = removeDescriptions ? lines.Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal)) : lines;
                return File(Path.GetRelativePath(root, path).Replace('\\', '/'), string.Join(newline, retained));
            }).ToArray();
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Description fixture", files));
    }

    private static ProjectFileContent File(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));

    private static string FixtureRoot(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return Path.Combine(directory.FullName, "tests", "fixtures", name);
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

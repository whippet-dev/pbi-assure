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
    public void DescriptionsDoNotChangeClassificationConfidenceDependenciesOrExistingExports()
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
    public void DescriptionPropertiesAreIgnoredByJsonAndExistingCsvColumns(string fixture)
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
        Assert.DoesNotContain(ExportPresetCatalog.GetAllowedColumns(ExportPreset.UsageMapping), column => column.Id == "Description");
        Assert.DoesNotContain("Description", ExportPresetCatalog.GetDefaultColumnIds(ExportPreset.DataCatalogue));
    }

    [Fact]
    public void OptionalCatalogueDescriptionExportsExactDesktopTextInRequestedPosition()
    {
        var inventory = ProjectScanner.Scan(FixtureRoot("desktop-descriptions-sanitized"));
        var csv = ExportCsvRenderer.Render(inventory, new ExportRequest(ExportPreset.DataCatalogue,
            ["Table", "Object", "Description", "ObjectType"]));

        Assert.Equal(
            "Table,Object,Description,ObjectType\r\n" +
            "TableA,ColumnA1,Customer's category: used for grouping.,Column\r\n" +
            "TableA,ColumnA2,,Column\r\n" +
            "TableA,MeasureA,\"Returns total sales - \n\nbefore adjustments.\",Measure\r\n" +
            "TableA,MeasureB,,Measure\r\n" +
            "TableB,CollumnB2,,Column\r\n" +
            "TableB,ColumnB1,,Column\r\n", csv);
        Assert.DoesNotContain("Contains test data", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Description", DataCatalogueCsvRenderer.Render(inventory), StringComparison.Ordinal);
        Assert.Contains(ExportPresetCatalog.GetAllowedColumns(ExportPreset.DataCatalogue), column => column.Id == "Description");
        Assert.DoesNotContain("Description", ExportPresetCatalog.GetDefaultColumnIds(ExportPreset.DataCatalogue));
        Assert.Throws<ArgumentException>(() => ExportCsvRenderer.Render(inventory,
            new ExportRequest(ExportPreset.UsageMapping, ["Description"])));
    }

    [Fact]
    public void CatalogueDescriptionUsesFullInventoryIdentityWithoutInferringMissingMetadata()
    {
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Description identity",
        [
            File("First.SemanticModel/definition.pbism", "{}"),
            File("First.SemanticModel/definition/tables/One.tmdl",
                "table One\n\t/// first column\n\tcolumn Shared\n\t\tdataType: int64\n\t/// first measure\n\tmeasure Shared = 1\n"),
            File("First.SemanticModel/definition/tables/Two.tmdl",
                "table Two\n\t/// second table\n\tmeasure Shared = 1\n"),
            File("Second.SemanticModel/definition.pbism", "{}"),
            File("Second.SemanticModel/definition/tables/One.tmdl",
                "table One\n\t/// second model\n\tmeasure Shared = 1\n"),
        ]));
        var request = new ExportRequest(ExportPreset.DataCatalogue,
            ["SemanticModel", "Table", "Object", "ObjectType", "Description"]);
        Assert.Equal(
            "SemanticModel,Table,Object,ObjectType,Description\r\n" +
            "First,One,Shared,Column,first column\r\n" +
            "First,One,Shared,Measure,first measure\r\n" +
            "First,Two,Shared,Measure,second table\r\n" +
            "Second,One,Shared,Measure,second model\r\n", ExportCsvRenderer.Render(inventory, request));
    }

    [Fact]
    public void CatalogueDescriptionUsesSharedCsvEscapingAndFormulaSafety()
    {
        var inventory = ProjectScanner.Scan(FixtureRoot("desktop-descriptions-sanitized"));
        var changed = inventory with
        {
            SemanticModels = inventory.SemanticModels.Select(model => model with
            {
                Tables = model.Tables.Select(table => table with
                {
                    Columns = table.Columns.Select(column => column with { Description = "=value, \"quoted\"\nnext " }).ToArray(),
                }).ToArray(),
            }).ToArray(),
        };
        var csv = ExportCsvRenderer.Render(changed, new ExportRequest(ExportPreset.DataCatalogue, ["Object", "Description"]));
        Assert.Contains("ColumnA1,\"'=value, \"\"quoted\"\"\nnext \"\r\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void WebAndDesktopColumnChoicesAreDerivedFromReportingWithoutDescriptionLists()
    {
        var root = Path.GetFullPath(Path.Combine(FixtureRoot("desktop-descriptions-sanitized"), "..", "..", ".."));
        var web = System.IO.File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Web", "Shared", "ExportCsvPanel.razor"));
        var desktop = System.IO.File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Desktop", "ExportCsvWindow.xaml.cs"));
        Assert.Contains("foreach (var column in AllowedColumns)", web, StringComparison.Ordinal);
        Assert.Contains("@column.Header", web, StringComparison.Ordinal);
        Assert.Contains("Content = column.Header", desktop, StringComparison.Ordinal);
        foreach (var source in new[] { web, desktop })
        {
            Assert.Contains("ExportPresetCatalog.GetAllowedColumns(selectedPreset)", source, StringComparison.Ordinal);
            Assert.Contains("ExportPresetCatalog.GetDefaultColumnIds(selectedPreset)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Description\"", source, StringComparison.Ordinal);
        }
    }

    private static void AssertOutputsEqual(ProjectInventory expected, ProjectInventory actual)
    {
        Assert.Equal(SemanticUsageCsvRenderer.Render(expected), SemanticUsageCsvRenderer.Render(actual));
        foreach (var preset in Enum.GetValues<ExportPreset>())
        {
            Assert.Equal(ExportCsvRenderer.Render(expected, new ExportRequest(preset)),
                ExportCsvRenderer.Render(actual, new ExportRequest(preset)));
            var allColumns = new ExportRequest(preset, ExportPresetCatalog.GetAllowedColumns(preset)
                .Where(column => column.Id != "Description").Select(column => column.Id).ToArray());
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

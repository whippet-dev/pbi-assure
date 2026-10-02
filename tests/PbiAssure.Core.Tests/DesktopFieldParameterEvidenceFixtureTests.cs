using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

// User-authored in Desktop via Modeling > New parameter > Fields, then reopened,
// changed the parameter selection and saved again. The persisted files are unchanged.
public sealed class DesktopFieldParameterEvidenceFixtureTests
{
    [Fact]
    public void DesktopParameterAndMaterialisedTargetsRetainUsageAndEstablishedConfidence()
    {
        var inventory = ProjectScanner.Scan(FixturePath());
        AssertUsage(inventory, "Metric", "Metric", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "Metric", "Metric Fields", SemanticUsageStates.StructurallyRequired);
        AssertUsage(inventory, "Metric", "Metric Order", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Sales", "Total Amount", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "Sales", "Total Qty", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "Sales", "Amount", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Sales", "Qty", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Sales", "Unused Control", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "Sales", "Cost", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "Sales", "Notes", SemanticUsageStates.ApparentlyUnused);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(0, AnalysisCoveragePresentation.Build(inventory).QualifiedObjectCount);
        Assert.All(inventory.AnalysisLimitations, limitation =>
            Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, limitation.DependencyImpact));
        Assert.Equal("0.26", inventory.SchemaVersion);

        var candidates = ApparentlyUnusedReportRenderer.Select(inventory);
        Assert.DoesNotContain(candidates, item => item.Usage.Table == "Metric" && item.Usage.ObjectName == "Metric Fields");
        Assert.Contains(candidates, item => item.Usage.Table == "Sales" && item.Usage.ObjectName == "Unused Control");
        Assert.Contains(candidates, item => item.Usage.Table == "Sales" && item.Usage.ObjectName == "Notes");
    }

    [Fact]
    public void DesktopModelRetainsGroupingSortingHiddenMetadataAndNameofTargets()
    {
        var root = FixturePath();
        var inventory = ProjectScanner.Scan(root);
        var model = Assert.Single(inventory.SemanticModels);
        var metric = Assert.Single(model.Tables, table => table.Name == "Metric");
        Assert.Equal("Metric Order", Assert.Single(metric.Columns, column => column.Name == "Metric").SortByColumn);
        var fields = Assert.Single(metric.Columns, column => column.Name == "Metric Fields");
        Assert.True(fields.IsHidden);
        Assert.Equal("Metric Order", fields.SortByColumn);
        Assert.True(Assert.Single(metric.Columns, column => column.Name == "Metric Order").IsHidden);
        var parameter = Assert.IsType<SemanticFieldParameterInventory>(metric.FieldParameter);
        Assert.Collection(parameter.Entries,
            entry => { Assert.Equal("Sales", entry.Table); Assert.Equal("Total Amount", entry.ObjectName); },
            entry => { Assert.Equal("Sales", entry.Table); Assert.Equal("Total Qty", entry.ObjectName); });
        Assert.Contains("NAMEOF('Sales'[Total Amount])", parameter.Expression, StringComparison.Ordinal);
        Assert.Contains("NAMEOF('Sales'[Total Qty])", parameter.Expression, StringComparison.Ordinal);
        var partition = Assert.Single(metric.Partitions);
        Assert.Equal("calculated", partition.SourceType);
        Assert.Equal(parameter.Expression, partition.Expression);

        // These persisted fields are not all represented separately in the public inventory.
        var tmdl = File.ReadAllText(Path.Combine(root, "field-parameter-fixture-c1.SemanticModel", "definition", "tables", "Metric.tmdl"));
        Assert.Contains("relatedColumnDetails", tmdl, StringComparison.Ordinal);
        Assert.Contains("groupByColumn: 'Metric Fields'", tmdl, StringComparison.Ordinal);
        Assert.Contains("extendedProperty ParameterMetadata", tmdl, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopChartMaterialisesBothTargetsAndSlicerUsesTheParameterLabel()
    {
        var root = FixturePath();
        using var chart = JsonDocument.Parse(File.ReadAllText(VisualPath(root, "8002ae0ad0028348b34e")));
        var visual = chart.RootElement.GetProperty("visual");
        Assert.Equal("clusteredColumnChart", visual.GetProperty("visualType").GetString());
        var y = visual.GetProperty("query").GetProperty("queryState").GetProperty("Y");
        AssertReference(Assert.Single(y.GetProperty("fieldParameters").EnumerateArray()).GetProperty("parameterExpr"),
            "Column", "Metric", "Metric");
        Assert.Collection(y.GetProperty("projections").EnumerateArray(),
            projection => AssertReference(projection.GetProperty("field"), "Measure", "Sales", "Total Amount"),
            projection => AssertReference(projection.GetProperty("field"), "Measure", "Sales", "Total Qty"));
        using var slicer = JsonDocument.Parse(File.ReadAllText(VisualPath(root, "83f98bb448a9b727e248")));
        var slicerVisual = slicer.RootElement.GetProperty("visual");
        Assert.Equal("slicer", slicerVisual.GetProperty("visualType").GetString());
        var projections = slicerVisual.GetProperty("query").GetProperty("queryState").GetProperty("Values").GetProperty("projections");
        AssertReference(Assert.Single(projections.EnumerateArray()).GetProperty("field"), "Column", "Metric", "Metric");

        var visuals = Assert.Single(Assert.Single(ProjectScanner.Scan(root).Reports).Pages).Visuals;
        var chartReferences = Assert.Single(visuals, item => item.VisualType == "clusteredColumnChart").FieldReferences;
        Assert.Contains(chartReferences, reference => reference.Table == "Metric" && reference.ObjectName == "Metric");
        Assert.Contains(chartReferences, reference => reference.Table == "Sales" && reference.ObjectName == "Total Amount");
        Assert.Contains(chartReferences, reference => reference.Table == "Sales" && reference.ObjectName == "Total Qty");
        Assert.Contains(Assert.Single(visuals, item => item.VisualType == "slicer").FieldReferences,
            reference => reference.Table == "Metric" && reference.ObjectName == "Metric");
    }

    private static void AssertReference(JsonElement field, string kind, string table, string name)
    {
        var reference = field.GetProperty(kind);
        Assert.Equal(table, reference.GetProperty("Expression").GetProperty("SourceRef").GetProperty("Entity").GetString());
        Assert.Equal(name, reference.GetProperty("Property").GetString());
    }

    private static void AssertUsage(ProjectInventory inventory, string table, string name, string state)
    {
        var usage = Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == name);
        Assert.Equal(state, usage.UsageState);
        Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
    }

    private static string VisualPath(string root, string visual) => Path.Combine(root,
        "field-parameter-fixture-c1.Report", "definition", "pages", "49890d2db75d50ae01a3", "visuals", visual, "visual.json");

    private static string FixturePath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
                return Path.Combine(directory.FullName, "tests", "fixtures", "desktop-field-parameter-evidence");
        }
        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

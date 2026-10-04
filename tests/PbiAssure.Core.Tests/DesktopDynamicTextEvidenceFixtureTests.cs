using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

// User-created through Desktop UI, saved as PBIP, closed/reopened, verified and saved again.
// The dynamic textbox and custom Narrative value both persist as textbox visuals.
public sealed class DesktopDynamicTextEvidenceFixtureTests
{
    private const string MeasureEvidencePath = "$.visual.objects.values[0].properties.expr.expr.Measure";

    [Fact]
    public void DynamicTextRootsBothMeasuresWithoutPromotingTheUnusedBranch()
    {
        var inventory = ProjectScanner.Scan(FixturePath());
        AssertUsage(inventory, "_Measures", "Headline", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "_Measures", "Narrative", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "_Measures", "Total Sales", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Sales", "Amount", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "_Measures", "Unused Measure", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "Sales", "UnusedValue", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "_Measures", "Column1", SemanticUsageStates.ApparentlyUnused);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.All(inventory.AnalysisLimitations, limitation =>
            Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, limitation.DependencyImpact));
        Assert.Equal(0, AnalysisCoveragePresentation.Build(inventory).QualifiedObjectCount);
        Assert.Equal("0.26", inventory.SchemaVersion);

        var visuals = Assert.Single(Assert.Single(inventory.Reports).Pages).Visuals;
        Assert.Equal(2, visuals.Count);
        Assert.All(visuals, visual => Assert.Equal("textbox", visual.VisualType));
        var references = visuals.SelectMany(visual => visual.FieldReferences).ToArray();
        Assert.Equal(2, references.Length);
        Assert.All(references, reference =>
        {
            Assert.Equal("_Measures", reference.Table);
            Assert.Equal(SemanticObjectTypes.Measure, reference.ObjectType);
            Assert.Equal(UsageContexts.Formatting, reference.UsageContext);
            Assert.Equal(VisualReferenceOrigins.FormattingPropertyExpression, reference.ReferenceOrigin);
            Assert.Equal(VisualReferenceRelevance.Active, reference.ReferenceRelevance);
            Assert.Equal(MeasureEvidencePath, reference.EvidencePath);
        });
        Assert.Contains(references, reference => reference.ObjectName == "Headline");
        Assert.Contains(references, reference => reference.ObjectName == "Narrative");
    }

    [Theory]
    [InlineData("Headline", "fde2cabc3658f33039f9")]
    [InlineData("Narrative", "20349b3eafc33be50f98")]
    public void DesktopTextRunSelectsTheMeasureExpressionAtOneReportLocation(string measureName, string visualId)
    {
        var root = FixturePath();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "desktop-dynamic-text-evidence.Report", "definition", "pages", "a66f8c63ecd7809ceb26",
            "visuals", visualId, "visual.json")));
        var visual = document.RootElement.GetProperty("visual");
        Assert.Equal("textbox", visual.GetProperty("visualType").GetString());
        Assert.False(visual.TryGetProperty("query", out _));
        var objects = visual.GetProperty("objects");
        var paragraphs = Assert.Single(objects.GetProperty("general").EnumerateArray())
            .GetProperty("properties").GetProperty("paragraphs");
        var textRuns = Assert.Single(paragraphs.EnumerateArray()).GetProperty("textRuns");
        var value = Assert.Single(textRuns.EnumerateArray()).GetProperty("value");
        var propertyIdentifier = value.GetProperty("propertyIdentifier");
        Assert.Equal("values", propertyIdentifier.GetProperty("objectName").GetString());
        Assert.Equal("expr", propertyIdentifier.GetProperty("propertyName").GetString());
        var selectorId = value.GetProperty("selector").GetProperty("id").GetString();
        Assert.Equal("Value", selectorId);
        var binding = Assert.Single(objects.GetProperty("values").EnumerateArray());
        Assert.Equal(selectorId, binding.GetProperty("selector").GetProperty("id").GetString());
        var expression = binding.GetProperty("properties").GetProperty("expr").GetProperty("expr");
        var measure = expression.GetProperty("Measure");
        Assert.Equal("_Measures", measure.GetProperty("Expression").GetProperty("SourceRef").GetProperty("Entity").GetString());
        Assert.Equal(measureName, measure.GetProperty("Property").GetString());
        Assert.Equal(selectorId, expression.GetProperty("Annotations").GetProperty("NaturalLanguage")
            .GetProperty("annotation").GetProperty("name").GetString());

        var usage = Usage(ProjectScanner.Scan(root), "_Measures", measureName);
        Assert.Equal(1, usage.DirectReportLocationCount);
        var location = Assert.Single(usage.DirectReportLocations);
        Assert.Equal("Visual", location.LocationKind);
        Assert.False(string.IsNullOrWhiteSpace(location.Page));
        Assert.False(string.IsNullOrWhiteSpace(location.Visual));
        var evidence = Assert.Single(usage.DirectReportReferences);
        Assert.Equal(UsageContexts.Formatting, evidence.UsageContext);
        Assert.Equal(MeasureEvidencePath, evidence.EvidencePath);
    }

    private static void AssertUsage(ProjectInventory inventory, string table, string name, string state)
    {
        var usage = Usage(inventory, table, name);
        Assert.Equal(state, usage.UsageState);
        Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
    }

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table, string name) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == name);

    private static string FixturePath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
                return Path.Combine(directory.FullName, "tests", "fixtures", "desktop-dynamic-text-evidence");
        }
        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

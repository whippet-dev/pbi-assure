using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class CalculationGroupColumnPinTests
{
    [Theory]
    [InlineData("", SemanticUsageStates.ApparentlyUnused)]
    [InlineData("        noSelectionExpression = SELECTEDMEASURE()\n", SemanticUsageStates.StructurallyRequired)]
    [InlineData("        multipleOrEmptySelectionExpression = MAX(Rates[MultipleRate])\n", SemanticUsageStates.ApparentlyUnused)]
    [InlineData("        noSelectionExpression = SELECTEDMEASURE()\n        multipleOrEmptySelectionExpression = MAX(Rates[MultipleRate])\n", SemanticUsageStates.StructurallyRequired)]
    public void OnlyDefaultSelectionPinsTheSelectorWithoutTraversingItsEdges(string selection, string expectedState)
    {
        var inventory = Scan(selection);
        AssertUsage(inventory, "Conversion", "Pick calculation", expectedState);
        AssertUsage(inventory, "Conversion", "Ordinal", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "Conversion", "Converted", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "Rates", "Rate", SemanticUsageStates.UsedOnlyByUnusedBranch);
        AssertUsage(inventory, "Rates", "MultipleRate", selection.Contains("multipleOrEmpty", StringComparison.Ordinal)
            ? SemanticUsageStates.UsedOnlyByUnusedBranch : SemanticUsageStates.ApparentlyUnused);
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch,
            Assert.Single(inventory.SemanticTableUsages, usage => usage.Table == "Conversion").UsageState);
        Assert.Empty(inventory.AnalysisLimitations);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Equal("0.26", inventory.SchemaVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("        noSelectionExpression = SELECTEDMEASURE()\n")]
    public void ReportUseRetainsDirectAndIndirectClassification(string selection)
    {
        var inventory = Scan(selection, reportUsesSelector: true);
        AssertUsage(inventory, "Conversion", "Pick calculation", SemanticUsageStates.DirectlyUsed);
        AssertUsage(inventory, "Conversion", "Ordinal", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Conversion", "Converted", SemanticUsageStates.IndirectlyUsed);
        AssertUsage(inventory, "Rates", "Rate", SemanticUsageStates.IndirectlyUsed);
        Assert.Equal(SemanticUsageStates.DirectlyUsed,
            Assert.Single(inventory.SemanticTableUsages, usage => usage.Table == "Conversion").UsageState);
        Assert.Empty(inventory.AnalysisLimitations);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
    }

    [Fact]
    public void DisplayNameAloneDoesNotIdentifyASelector()
    {
        var inventory = Scan("        noSelectionExpression = SELECTEDMEASURE()\n",
            selectorName: "Name", sourceColumn: "Other");
        AssertUsage(inventory, "Conversion", "Name", SemanticUsageStates.ApparentlyUnused);
        AssertUsage(inventory, "Conversion", "Converted", SemanticUsageStates.UsedOnlyByUnusedBranch);
        Assert.Empty(inventory.AnalysisLimitations);
    }

    private static void AssertUsage(ProjectInventory inventory, string table, string name, string state)
    {
        var usage = Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == name);
        Assert.Equal(state, usage.UsageState);
        Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
    }

    private static ProjectInventory Scan(string selection, bool reportUsesSelector = false,
        string selectorName = "Pick calculation", string sourceColumn = "Name")
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Conversion.tmdl",
                "table Conversion\n    calculationGroup\n" + selection +
                "        calculationItem Converted = SELECTEDMEASURE() * MAX(Rates[Rate])\n" +
                $"    column '{selectorName}'\n        sourceColumn: {sourceColumn}\n        sortByColumn: Ordinal\n" +
                "    column Ordinal\n        sourceColumn: Ordinal\n"),
            File("Model.SemanticModel/definition/tables/Rates.tmdl", "table Rates\n    column Rate\n    column MultipleRate\n"),
        };
        if (reportUsesSelector)
        {
            files.Add(File("Model.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"));
            files.Add(File("Model.Report/definition/report.json",
                "{\"Column\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Conversion\"}},\"Property\":\"" + selectorName + "\"}}"));
        }
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic calculation-group selector controls", files));
    }

    private static ProjectFileContent File(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));
}

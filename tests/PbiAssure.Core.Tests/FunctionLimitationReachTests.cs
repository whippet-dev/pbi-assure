using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// PBI-LIMIT-MODEL-FUNCTION records that a DAX user-defined function may be called from somewhere the
/// scan does not read. A call nobody saw can make the function live, and with it everything its body
/// reaches — and nothing else. The limitation therefore qualifies exactly that closure: a body that
/// references nothing qualifies nothing, a body that references Sales[Amount] keeps Amount uncertain,
/// and an unrelated measure elsewhere in the model is never marked because of it.
/// </summary>
public sealed class FunctionLimitationReachTests
{
    private const string LimitationId = "PBI-LIMIT-MODEL-FUNCTION";

    // ---- 1. Desktop AddTax: parameter-only body ---------------------------------------------

    [Fact]
    public void AParameterOnlyFunctionQualifiesNothingWhileItsLimitationStays()
    {
        var inventory = ScanFixture("desktop-semantic-constructs");

        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.NotNull(limitation.Reach);
        Assert.Empty(limitation.Reach!);
        Assert.Contains(inventory.SemanticObjectUsages, usage => usage.UsageState == SemanticUsageStates.ApparentlyUnused);
        Assert.All(inventory.SemanticObjectUsages, usage =>
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
    }

    [Fact]
    public void AnUnrelatedUnusedMeasureBesideAddTaxStaysEstablished()
    {
        var inventory = Scan(
            "/// AddTax takes in amount and returns amount including tax\nfunction AddTax = (amount : NUMERIC) => amount * 1.1\n");

        var unused = Usage(inventory, "UnusedMeasure");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, unused.UsageState);
        Assert.Equal(ClassificationConfidences.Established, unused.ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Region").ClassificationConfidence);
        Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Empty(SemanticUsageConfidenceQualifier.Qualifying(unused, inventory.AnalysisLimitations));
    }

    // ---- 2 & 3. A dependency-bearing body: its closure stays uncertain, the rest does not ------

    [Fact]
    public void ADependencyBearingFunctionKeepsItsClosureQualifiedAndNothingElse()
    {
        var inventory = ScanFixture("desktop-udf-references");

        // TotalOf -> Sales[Amount]; Doubled -> [Total Amount] -> Sales[Amount]; Region is reached by none.
        var amount = Usage(inventory, "Amount");
        var total = Usage(inventory, "Total Amount");
        var region = Usage(inventory, "Region");
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, amount.UsageState);
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, total.UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, region.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, amount.ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, total.ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established, region.ClassificationConfidence);

        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Equal(LimitationId, Assert.Single(SemanticUsageConfidenceQualifier.Qualifying(amount, inventory.AnalysisLimitations)).LimitationId);
        Assert.Empty(SemanticUsageConfidenceQualifier.Qualifying(region, inventory.AnalysisLimitations));
        // The reach is the closure over the classifier's own edges: functions, the measure and its column, the table.
        Assert.Contains(FieldIdentity.Create("Sales", "Amount", SemanticObjectTypes.Column), limitation.Reach!);
        Assert.Contains(FieldIdentity.Create("Sales", "Total Amount", SemanticObjectTypes.Measure), limitation.Reach!);
        Assert.Contains(FieldIdentity.Create(string.Empty, "Doubled", SemanticObjectTypes.Function), limitation.Reach!);
        Assert.DoesNotContain(FieldIdentity.Create("Sales", "Region", SemanticObjectTypes.Column), limitation.Reach!);
    }

    [Fact]
    public void TheClosureFollowsCallsThroughMeasuresAndSortByColumns()
    {
        var inventory = Scan(
            "function Helper = () => [Derived]\n",
            extraModel:
            "\tmeasure Derived = SUM(Sales[Amount])\n" +
            "\tcolumn Label\n\t\tdataType: string\n\t\tsourceColumn: Label\n\t\tsortByColumn: LabelOrder\n" +
            "\tcolumn LabelOrder\n\t\tdataType: int64\n\t\tsourceColumn: LabelOrder\n" +
            "\tmeasure Labelled = MAX(Sales[Label])\n" +
            "\tmeasure Unrelated = 2\n");

        // Helper -> Derived -> Amount are in the closure; Label/LabelOrder/Labelled/Unrelated are not.
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Derived").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Amount").ClassificationConfidence);
        foreach (var name in new[] { "Label", "LabelOrder", "Labelled", "Unrelated", "UnusedMeasure", "Region" })
        {
            Assert.Equal(ClassificationConfidences.Established, Usage(inventory, name).ClassificationConfidence);
        }
    }

    [Fact]
    public void UnrelatedObjectsOutsideTheClosureStayEstablishedInTheConsumerFixture()
    {
        var inventory = ScanFixture("desktop-udf-measure-consumer");

        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Region").ClassificationConfidence);
        Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
    }

    // ---- 5. Unresolved function content falls back to the conservative reading ----------------

    [Fact]
    public void AnUnresolvedReferenceInAFunctionBodyLeavesTheReachUnbounded()
    {
        var inventory = Scan("function Broken = () => SUM(Sales[Missing])\n");

        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Null(limitation.Reach);
        var unused = Usage(inventory, "UnusedMeasure");
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, unused.ClassificationConfidence);
        Assert.Equal(
            ["PBI-LIMIT-MODEL-FUNCTION", "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE"],
            SemanticUsageConfidenceQualifier.Qualifying(unused, inventory.AnalysisLimitations)
                .Select(item => item.LimitationId).Order(StringComparer.Ordinal).ToArray());
    }

    // ---- 6. Known calls are unchanged ---------------------------------------------------------

    [Fact]
    public void AKnownCallStillMakesTheChainUsedAndEstablished()
    {
        var inventory = ScanFixture("desktop-udf-measure-consumer");

        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "UDF Result").UsageState);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Total Amount").UsageState);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Amount").UsageState);
        Assert.Contains(inventory.SemanticDependencies, edge =>
            edge.FromObjectName == "UDF Result" && edge.ToObjectName == "Doubled" && edge.DependencyKind == SemanticDependencyKinds.FunctionCall);
        foreach (var name in new[] { "UDF Result", "Total Amount", "Amount" })
        {
            Assert.Equal(ClassificationConfidences.Established, Usage(inventory, name).ClassificationConfidence);
        }
    }

    // ---- 7. A model-wide semantic limitation still qualifies model-wide ----------------------

    [Fact]
    public void AModelWideSemanticLimitationStillQualifiesEveryAbsence()
    {
        var inventory = Scan(
            "function AddTax = (amount : NUMERIC) => amount * 1.1\n",
            extraModel: "\tmeasure Dangling = SUM(Sales[Nowhere])\n");

        var unresolved = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE");
        Assert.Null(unresolved.Reach);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "UnusedMeasure").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Region").ClassificationConfidence);
        // The bounded function limitation contributes nothing to that; the model-wide one does.
        Assert.Equal("PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE",
            Assert.Single(SemanticUsageConfidenceQualifier.Qualifying(Usage(inventory, "UnusedMeasure"), inventory.AnalysisLimitations)).LimitationId);
    }

    [Fact]
    public void TheReachIsAnInProcessRefinementOnly()
    {
        var inventory = ScanFixture("desktop-udf-references");

        var json = System.Text.Json.JsonSerializer.Serialize(inventory.AnalysisLimitations);
        Assert.DoesNotContain("Reach", json, StringComparison.Ordinal);
        Assert.Equal("0.26", inventory.SchemaVersion);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == "Sales" && usage.ObjectName == objectName);

    /// <summary>A model with a report-used measure, an unrelated unused measure and an unused column.</summary>
    private static ProjectInventory Scan(string functions, string extraModel = "")
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"),
            File("Model.Report/definition/report.json", "{}"),
            File("Model.Report/definition/pages/p/page.json", "{\"name\":\"p\"}"),
            File("Model.Report/definition/pages/p/visuals/v/visual.json",
                "{\"name\":\"v\",\"visual\":{\"visualType\":\"card\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[" +
                "{\"field\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Sales\"}},\"Property\":\"Shown\"}},\"queryRef\":\"Sales.Shown\"}]}}}}}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/functions.tmdl", functions),
            File("Model.SemanticModel/definition/tables/Sales.tmdl",
                "table Sales\n" +
                "\tmeasure Shown = 1\n" +
                "\tmeasure UnusedMeasure = 1\n" +
                "\tcolumn Amount\n\t\tdataType: int64\n\t\tsourceColumn: Amount\n" +
                "\tcolumn Region\n\t\tdataType: string\n\t\tsourceColumn: Region\n" +
                extraModel +
                "\tpartition Sales = m\n\t\tmode: import\n\t\tsource = #table({\"Amount\", \"Region\"}, {})\n"),
        };

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Function reach", files));
    }

    private static ProjectFileContent File(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));

    private static ProjectInventory ScanFixture(string fixture) => ProjectScanner.Scan(Path.Combine(
        RepositoryRoot(), "tests", "fixtures", fixture));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

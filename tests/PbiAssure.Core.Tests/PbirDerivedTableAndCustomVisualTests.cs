using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Two shapes that Power BI persists routinely were reported as unread metadata that could hide usage,
/// and between them they qualified every absence conclusion in the Sales &amp; Returns sample.
///
/// A TopN filter's derived table is declared beside the alias that names it, and the fields it uses are
/// already read from the subquery's own scope. An imported custom visual package is the visual itself,
/// shipped identically to every report that imports it, so it cannot name this model — the instance's
/// bindings live in its ordinary visual.json.
/// </summary>
public sealed class PbirDerivedTableAndCustomVisualTests
{
    private const string AliasLimitation = "PBI-LIMIT-REPORT-UNRESOLVED-ALIAS";
    private const string UnrecognizedLimitation = "PBI-LIMIT-REPORT-UNRECOGNIZED";

    // ---- A. Derived table sources -------------------------------------------------------------

    /// <summary>
    /// The persisted Sales &amp; Returns shape, reduced to the parts that decide the outcome: a Type 2
    /// From entry carrying its own Query, and a Where that filters against it.
    /// </summary>
    private const string TopNDerivedTable = """
        {"filterConfig":{"filters":[{"type":"TopN","filter":{"Version":2,
          "From":[
            {"Name":"subquery","Expression":{"Subquery":{"Query":{"Version":2,
               "From":[{"Name":"a1","Entity":"A","Type":0},{"Name":"b","Entity":"B","Type":0}],
               "Select":[{"Column":{"Expression":{"SourceRef":{"Source":"a1"}},"Property":"Value"},"Name":"field"}],
               "OrderBy":[{"Direction":2,"Expression":{"Aggregation":{"Expression":{"Column":{
                  "Expression":{"SourceRef":{"Source":"b"}},"Property":"Value"}},"Function":0}}}],
               "Top":1}}},"Type":2},
            {"Name":"a1","Entity":"A","Type":0}],
          "Where":[{"Condition":{"In":{
            "Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"a1"}},"Property":"Value"}}],
            "Table":{"SourceRef":{"Source":"subquery"}}}}}]}}]}}
        """;

    [Fact]
    public void ADerivedTableSourceIsNotAnUnresolvedAlias()
    {
        var inventory = Scan(TopNDerivedTable);

        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.LimitationId == AliasLimitation);
        Assert.Empty(inventory.Reports[0].UnresolvedAliases);
    }

    [Fact]
    public void FieldsInsideTheSubqueryAreStillCaptured()
    {
        var inventory = Scan(TopNDerivedTable);

        // A[Value] is referenced by the outer Where and the inner Select; B[Value] only by the OrderBy
        // inside the subquery, so it proves the nested scope is still read.
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "A").UsageState);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "B").UsageState);
        Assert.All(inventory.SemanticObjectUsages, usage =>
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
    }

    [Fact]
    public void TheDerivedTableItselfCreatesNoSemanticDependency()
    {
        var inventory = Scan(TopNDerivedTable);

        Assert.DoesNotContain(inventory.SemanticDependencies, dependency =>
            dependency.ToTable.Contains("subquery", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(inventory.Reports[0].FieldReferences, reference =>
            reference.Table.Contains("subquery", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(inventory.UnresolvedSemanticDependencies, dependency =>
            dependency.ReferenceText.Contains("subquery", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnAliasWithNeitherEntityNorSubqueryIsStillUnresolved()
    {
        var inventory = Scan("""
            {"filterConfig":{"filters":[{"filter":{"Version":2,
              "From":[{"Name":"ghost","Type":0}],
              "Where":[{"Condition":{"In":{
                "Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"ghost"}},"Property":"Value"}}],
                "Values":[[{"Literal":{"Value":"1L"}}]]}}}]}}]}}
            """);

        Assert.Contains(inventory.AnalysisLimitations, item => item.LimitationId == AliasLimitation);
    }

    [Fact]
    public void AnAliasDeclaredAsBothAnEntityAndASubqueryStaysUnresolved()
    {
        // Nothing in the metadata says which declaration the reference meant, so neither is chosen.
        var inventory = Scan("""
            {"filterConfig":{"filters":[{"filter":{"Version":2,
              "From":[
                {"Name":"dual","Entity":"A","Type":0},
                {"Name":"dual","Expression":{"Subquery":{"Query":{"Version":2,
                   "From":[{"Name":"b","Entity":"B","Type":0}]}}},"Type":2}],
              "Where":[{"Condition":{"In":{
                "Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"dual"}},"Property":"Value"}}],
                "Values":[[{"Literal":{"Value":"1L"}}]]}}}]}}]}}
            """);

        Assert.Contains(inventory.AnalysisLimitations, item => item.LimitationId == AliasLimitation);
    }

    [Fact]
    public void OrdinaryEntityAliasesAreUnchanged()
    {
        var inventory = Scan("""
            {"filterConfig":{"filters":[{"filter":{"Version":2,
              "From":[{"Name":"s","Entity":"A","Type":0}],
              "Where":[{"Condition":{"In":{
                "Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"s"}},"Property":"Value"}}],
                "Values":[[{"Literal":{"Value":"1L"}}]]}}}]}}]}}
            """);

        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.LimitationId == AliasLimitation);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "A").UsageState);
    }

    // ---- B. Custom visual packages -------------------------------------------------------------

    [Theory]
    [InlineData("CustomVisuals/simpleImageABC123/package.json")]
    [InlineData("CustomVisuals/simpleImageABC123/resources/simpleImageABC123.pbiviz.json")]
    public void EvidencedCustomVisualPackageFilesAreRecognisedPackaging(string reportRelativePath)
    {
        var rule = ReportDefinitionFileRegistry.Classify(reportRelativePath);

        Assert.Equal("PBI-LIMIT-REPORT-CUSTOM-VISUAL", rule.LimitationId);
        Assert.Equal(ConstructClassifications.Packaging, rule.Classification);
        Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, rule.DependencyImpact);
        Assert.Equal("customVisualPackage", rule.ConstructType);
    }

    [Theory]
    [InlineData("CustomVisuals/simpleImageABC123/something-else.json")]
    [InlineData("CustomVisuals/simpleImageABC123/resources/notapackage.json")]
    [InlineData("CustomVisuals/package.json")]
    [InlineData("definition/pages/p/unexpected.json")]
    [InlineData("someOtherFolder/package.json")]
    public void OtherReportTreeJsonFilesRemainUnrecognised(string reportRelativePath)
    {
        var rule = ReportDefinitionFileRegistry.Classify(reportRelativePath);

        Assert.Equal(UnrecognizedLimitation, rule.LimitationId);
        Assert.Equal(ConstructClassifications.Unrecognized, rule.Classification);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, rule.DependencyImpact);
    }

    [Fact]
    public void CustomVisualPackagesRaiseNoLimitationWhileUnknownFilesStillDo()
    {
        var withPackages = Scan(
            EntityFilter,
            extraFiles: new Dictionary<string, string>
            {
                ["Model.Report/CustomVisuals/vizA/package.json"] = "{\"visual\":{\"guid\":\"vizA\"}}",
                ["Model.Report/CustomVisuals/vizA/resources/vizA.pbiviz.json"] = "{\"capabilities\":{\"dataRoles\":[]}}",
            });
        var withUnknown = Scan(
            EntityFilter,
            extraFiles: new Dictionary<string, string>
            {
                ["Model.Report/CustomVisuals/vizA/unexpected.json"] = "{}",
            });

        Assert.DoesNotContain(withPackages.AnalysisLimitations, item => item.LimitationId == UnrecognizedLimitation);
        Assert.Contains(withUnknown.AnalysisLimitations, item => item.LimitationId == UnrecognizedLimitation);
    }

    [Fact]
    public void CustomVisualInstanceBindingsAreStillReadFromTheVisualDefinition()
    {
        var inventory = Scan(
            "{}",
            visualJson: "{\"name\":\"v\",\"visual\":{\"visualType\":\"vizA\",\"query\":{\"queryState\":{\"Values\":" +
                "{\"projections\":[{\"field\":{\"Column\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"A\"}}," +
                "\"Property\":\"Value\"}},\"queryRef\":\"A.Value\"}]}}}}}",
            extraFiles: new Dictionary<string, string>
            {
                ["Model.Report/CustomVisuals/vizA/package.json"] = "{\"visual\":{\"guid\":\"vizA\"}}",
                ["Model.Report/CustomVisuals/vizA/resources/vizA.pbiviz.json"] = "{\"capabilities\":{\"dataRoles\":[]}}",
            });

        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "A").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "A").ClassificationConfidence);
    }

    // ---- C. Combined confidence effect ---------------------------------------------------------

    [Fact]
    public void TogetherTheyLeaveAbsenceConclusionsEstablished()
    {
        var files = new Dictionary<string, string>
        {
            ["Model.Report/CustomVisuals/vizA/package.json"] = "{\"visual\":{\"guid\":\"vizA\"}}",
            ["Model.Report/CustomVisuals/vizA/resources/vizA.pbiviz.json"] = "{\"capabilities\":{\"dataRoles\":[]}}",
        };
        var inventory = Scan(TopNDerivedTable, extraFiles: files);

        // B[Value] is used only inside the subquery; C[Value] is in a model nothing references.
        Assert.DoesNotContain(inventory.AnalysisLimitations, item =>
            item.DependencyImpact == ConstructDependencyImpacts.MayCreateDependencies);
        var unused = inventory.SemanticObjectUsages.Where(usage =>
            usage.UsageState is SemanticUsageStates.ApparentlyUnused or SemanticUsageStates.UsedOnlyByUnusedBranch).ToArray();
        Assert.NotEmpty(unused);
        Assert.All(unused, usage =>
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private const string EntityFilter = """
        {"filterConfig":{"filters":[{"filter":{"Version":2,
          "From":[{"Name":"s","Entity":"A","Type":0}],
          "Where":[{"Condition":{"In":{
            "Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"s"}},"Property":"Value"}}],
            "Values":[[{"Literal":{"Value":"1L"}}]]}}}]}}]}}
        """;

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table) =>
        inventory.SemanticObjectUsages.Single(usage => usage.SemanticModel == "Model" && usage.Table == table);

    private static ProjectInventory Scan(
        string reportJson,
        string? visualJson = null,
        IDictionary<string, string>? extraFiles = null)
    {
        var files = new Dictionary<string, string>
        {
            ["Model.pbip"] = "{}",
            ["Model.Report/definition.pbir"] = "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}",
            ["Model.Report/definition/report.json"] = reportJson,
            ["Model.Report/definition/pages/p/page.json"] = "{\"name\":\"p\"}",
            ["Model.Report/definition/pages/p/visuals/v/visual.json"] =
                visualJson ?? "{\"name\":\"v\",\"visual\":{\"visualType\":\"card\"}}",
            ["Model.SemanticModel/definition/tables/A.tmdl"] = "table A\n\tcolumn Value\n\t\tdataType: int64\n",
            ["Model.SemanticModel/definition/tables/B.tmdl"] = "table B\n\tcolumn Value\n\t\tdataType: int64\n",
            ["Model.SemanticModel/definition/tables/C.tmdl"] = "table C\n\tcolumn Value\n\t\tdataType: int64\n",
        };

        foreach (var file in extraFiles ?? new Dictionary<string, string>())
        {
            files[file.Key] = file.Value;
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Derived tables", files.Select(file =>
            new ProjectFileContent(file.Key, Encoding.UTF8.GetBytes(file.Value)))));
    }
}

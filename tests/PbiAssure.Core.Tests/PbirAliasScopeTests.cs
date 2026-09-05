using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class PbirAliasScopeTests
{
    private const string LimitationId = "PBI-LIMIT-REPORT-UNRESOLVED-ALIAS";

    [Fact]
    public void IndependentFiltersResolveReusedAliasesAndPredicateOnlyFields()
    {
        var inventory = Scan("{\"filterConfig\":{\"filters\":[" +
            "{\"filter\":" + Query("A") + "},{\"filter\":" + Query("B") + "}]}}");
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "A").UsageState);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "B").UsageState);
        Assert.All(inventory.SemanticObjectUsages.Where(usage => usage.ObjectName == "Value"), usage =>
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.All(inventory.Reports[0].FieldReferences, reference =>
            Assert.Contains(".Where[0].Condition", reference.EvidencePath, StringComparison.Ordinal));
    }

    [Fact]
    public void SingleScopeAndExplicitEntityExtractionStayUnchanged()
    {
        using var document = JsonDocument.Parse(Query("A"));
        var reference = Assert.Single(PbirFieldReferenceExtractor.Extract(document.RootElement));
        Assert.Equal("A", reference.Table);
        Assert.Equal("Value", reference.ObjectName);

        using var explicitDocument = JsonDocument.Parse(
            "{\"Column\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"B\",\"Source\":\"missing\"}},\"Property\":\"Value\"}}");
        var doubts = new List<string>();
        var explicitReference = Assert.Single(PbirFieldReferenceExtractor.Extract(
            explicitDocument.RootElement, (path, _) => doubts.Add(path)));
        Assert.Equal("B", explicitReference.Table);
        Assert.Empty(doubts);
    }

    [Theory]
    [InlineData("report")]
    [InlineData("page")]
    [InlineData("visual")]
    [InlineData("mobile")]
    public void MissingAliasQualifiesOnlyBoundModelAbsencesAndRetainsExactEvidence(string surface)
    {
        var inventory = Scan("{\"Where\":[{\"Condition\":" + Field("missing") + "}]}", surface);
        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Equal(AnalysisLimitationCauses.ReferenceUnresolved, limitation.Cause);
        Assert.Equal("Model", limitation.SemanticModel);
        Assert.EndsWith(".Column.Expression.SourceRef", limitation.EvidencePath, StringComparison.Ordinal);
        Assert.Contains("missing", limitation.Reason, StringComparison.Ordinal);
        Assert.Equal(SurfacePath(surface), limitation.ArtifactPath);
        Assert.Null(limitation.Table);
        Assert.Empty(inventory.UnresolvedSemanticReferences); // No invented table/object binding.
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "A").UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "A").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established,
            inventory.SemanticObjectUsages.Single(usage => usage.SemanticModel == "Other").ClassificationConfidence);
        Assert.Equal("0.26", inventory.SchemaVersion);
        var json = JsonSerializer.Serialize(inventory);
        Assert.Contains(LimitationId, json, StringComparison.Ordinal);
        Assert.DoesNotContain("UnresolvedAliases", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("[{\"Name\":\"s\"}]")]
    [InlineData("[{\"Name\":\"s\",\"Entity\":\"A\"},{\"Name\":\"s\",\"Entity\":\"B\"}]")]
    [InlineData("[{\"Name\":\"s\",\"Entity\":\"A\"},{\"Name\":\"s\"}]")]
    public void MalformedOrAmbiguousLocalScopeCannotBorrowAnOuterAlias(string from)
    {
        var nested = "{\"From\":" + from + ",\"Where\":[" + Field("s") + "]}";
        var inventory = Scan("{\"From\":[{\"Name\":\"s\",\"Entity\":\"A\"}],\"Nested\":" + nested + "}");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "A").UsageState);
        Assert.Contains(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
    }

    [Fact]
    public void NestedQueryShadowsOuterScopeAndRestoresItForFollowingReferences()
    {
        var json = "{\"From\":[{\"Name\":\"s\",\"Entity\":\"A\"}],\"Nested\":" + Query("B") +
            ",\"Where\":[" + Field("s") + "]}";
        var inventory = Scan(json);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "A").UsageState);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "B").UsageState);
        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
    }

    [Fact]
    public void MissingFromDoesNotBorrowFromSiblingOrOuterQueryAndPositiveEvidenceRemainsEstablished()
    {
        var missing = "{\"Where\":[" + Field("s") + "]}";
        var inventory = Scan("{\"From\":[{\"Name\":\"s\",\"Entity\":\"A\"}],\"Blocks\":[" + Query("A") + "," + missing + "]}");
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "A").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "A").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "B").ClassificationConfidence);
    }

    [Theory]
    [InlineData("{}", "Model")]
    [InlineData("{\"datasetReference\":{\"byConnection\":{}}}", null)]
    [InlineData("{\"datasetReference\":{\"byPath\":{\"path\":\"../Absent.SemanticModel\"}}}", null)]
    public void AliasDoubtUsesTheSameModelBindingAsUsage(string connection, string? expectedModel)
    {
        var inventory = Scan("{\"Where\":[" + Field("s") + "]}", connection: connection);
        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Equal(expectedModel, limitation.SemanticModel);
        Assert.Equal(expectedModel is null ? ClassificationConfidences.Established : ClassificationConfidences.QualifiedByLimitation,
            Usage(inventory, "A").ClassificationConfidence);
    }

    private static string Query(string entity) =>
        "{\"Version\":2,\"From\":[{\"Name\":\"s\",\"Entity\":\"" + entity + "\",\"Type\":0}]," +
        "\"Where\":[{\"Condition\":{\"In\":{\"Expressions\":[" + Field("s") +
        "],\"Values\":[[{\"Literal\":{\"Value\":\"1L\"}}]]}}}]}";

    private static string Field(string alias) =>
        "{\"Column\":{\"Expression\":{\"SourceRef\":{\"Source\":\"" + alias + "\"}},\"Property\":\"Value\"}}";

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table) =>
        inventory.SemanticObjectUsages.Single(usage => usage.SemanticModel == "Model" && usage.Table == table);

    private static string SurfacePath(string surface) => "Model.Report/definition/" + (surface switch
    {
        "report" => "report.json",
        "page" => "pages/p/page.json",
        "visual" => "pages/p/visuals/v/visual.json",
        "mobile" => "pages/p/visuals/v/mobile.json",
        _ => throw new ArgumentException(surface),
    });

    private static ProjectInventory Scan(string json, string surface = "report", string? connection = null)
    {
        var files = new Dictionary<string, string>
        {
            ["Model.pbip"] = "{}",
            ["Model.Report/definition.pbir"] = connection ?? "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}",
            [SurfacePath("report")] = "{}",
            [SurfacePath("page")] = "{\"name\":\"p\"}",
            [SurfacePath("visual")] = "{\"name\":\"v\",\"visual\":{\"visualType\":\"card\"}}",
            ["Model.SemanticModel/definition/tables/A.tmdl"] = "table A\n\tcolumn Value\n\t\tdataType: int64\n",
            ["Model.SemanticModel/definition/tables/B.tmdl"] = "table B\n\tcolumn Value\n\t\tdataType: int64\n",
            ["Other.SemanticModel/definition/tables/C.tmdl"] = "table C\n\tcolumn Value\n\t\tdataType: int64\n",
        };
        files[SurfacePath(surface)] = json;
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Alias scope", files.Select(file =>
            new ProjectFileContent(file.Key, Encoding.UTF8.GetBytes(file.Value)))));
    }
}

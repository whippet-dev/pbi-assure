using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class UnreadReportPageTests
{
    private const string LimitationId = "PBI-LIMIT-REPORT-PAGES-UNREAD";

    [Theory]
    [InlineData(null)]
    [InlineData("{broken")]
    [InlineData("[]")]
    public void FailedPagePreservesReadableEvidenceAndQualifiesOnlyBoundModel(string? definition)
    {
        var files = Files();
        files["Model.Report/definition/pages/unread/visuals/v/visual.json"] = Visual("Absent");
        if (definition is not null)
        {
            files["Model.Report/definition/pages/unread/page.json"] = definition;
        }

        var inventory = Scan(files);
        var page = Assert.Single(Assert.Single(inventory.Reports).Pages);
        Assert.Equal("readable", page.Name);
        Assert.Equal("Used", Assert.Single(Assert.Single(page.Visuals).FieldReferences).ObjectName);
        var limitation = Assert.Single(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Equal("Model.Report/definition/pages/unread/page.json", limitation.ArtifactPath);
        Assert.Equal(AnalysisLimitation.WholeFileEvidence, limitation.EvidencePath);
        Assert.Equal(AnalysisLimitationCauses.ParseFailed, limitation.Cause);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.Equal(AnalysisLimitationScopes.Report, limitation.Scope);
        Assert.Equal("Model", limitation.SemanticModel);
        Assert.Contains(AnalysisConcerns.Dependency, limitation.Concerns);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Used").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Used").ClassificationConfidence);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Absent").UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Absent").ClassificationConfidence);
        Assert.Equal(ClassificationConfidences.Established,
            inventory.SemanticObjectUsages.Single(usage => usage.SemanticModel == "Other").ClassificationConfidence);
        Assert.Equal("0.26", inventory.SchemaVersion);
        Assert.DoesNotContain("UnreadPages", JsonSerializer.Serialize(inventory), StringComparison.Ordinal);
    }

    [Fact]
    public void EachUnreadDirectoryHasItsOwnLimitation()
    {
        var files = Files();
        files["Model.Report/definition/pages/missing/visuals/v/visual.json"] = Visual("Absent");
        files["Model.Report/definition/pages/broken/page.json"] = "{broken";
        var inventory = Scan(files);
        Assert.Single(inventory.Reports[0].Pages);
        Assert.Collection(inventory.AnalysisLimitations.Where(item => item.LimitationId == LimitationId)
                .Select(item => item.ArtifactPath).OrderBy(path => path),
            path => Assert.Equal("Model.Report/definition/pages/broken/page.json", path),
            path => Assert.Equal("Model.Report/definition/pages/missing/page.json", path));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"pageOrder\":[]}")]
    public void PagesIndexAloneIsNotAnUnreadPage(string index)
    {
        var files = Files();
        foreach (var path in files.Keys.Where(path => path.Contains("/pages/", StringComparison.Ordinal)).ToArray())
        {
            files.Remove(path);
        }
        files["Model.Report/definition/pages/pages.json"] = index;
        var inventory = Scan(files);
        Assert.Empty(inventory.Reports[0].Pages);
        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Absent").ClassificationConfidence);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllReadablePagesKeepEvidenceWithOrWithoutAnIndex(bool indexed)
    {
        var files = Files();
        files["Model.Report/definition/pages/second/page.json"] = "{\"name\":\"second\"}";
        files["Model.Report/definition/pages/second/visuals/v/visual.json"] = Visual("Absent");
        if (indexed)
        {
            files["Model.Report/definition/pages/pages.json"] =
                "{\"pageOrder\":[\"second\",\"readable\"],\"activePageName\":\"second\"}";
        }
        var inventory = Scan(files);
        var report = Assert.Single(inventory.Reports);
        Assert.Equal(2, report.Pages.Count);
        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.LimitationId == LimitationId);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Used").UsageState);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Absent").UsageState);
        if (indexed)
        {
            Assert.Equal("second", report.Pages[0].Name);
            Assert.Equal(0, report.Pages[0].Order);
            Assert.Equal("second", report.ActivePageName);
        }
        else
        {
            Assert.All(report.Pages, page => Assert.Null(page.Order));
            Assert.Null(report.ActivePageName);
        }
    }

    [Theory]
    [InlineData("{}", "Model")]
    [InlineData("{\"datasetReference\":{\"byConnection\":{}}}", null)]
    public void UnreadPageUsesTheSameModelBindingAsReportUsage(string connection, string? model)
    {
        var files = Files();
        files["Model.Report/definition.pbir"] = connection;
        files["Model.Report/definition/pages/broken/page.json"] = "{broken";
        var inventory = Scan(files);
        Assert.Equal(model, Assert.Single(inventory.AnalysisLimitations,
            item => item.LimitationId == LimitationId).SemanticModel);
        Assert.Equal(model is null ? ClassificationConfidences.Established : ClassificationConfidences.QualifiedByLimitation,
            Usage(inventory, "Absent").ClassificationConfidence);
    }

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string name) =>
        inventory.SemanticObjectUsages.Single(usage => usage.SemanticModel == "Model" && usage.ObjectName == name);

    private static Dictionary<string, string> Files() => new()
    {
        ["Model.pbip"] = "{}",
        ["Model.Report/definition.pbir"] = "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}",
        ["Model.Report/definition/pages/readable/page.json"] = "{\"name\":\"readable\"}",
        ["Model.Report/definition/pages/readable/visuals/v/visual.json"] = Visual("Used"),
        ["Model.SemanticModel/definition/tables/Fact.tmdl"] =
            "table Fact\n\tcolumn Used\n\t\tdataType: int64\n\tcolumn Absent\n\t\tdataType: int64\n",
        ["Other.SemanticModel/definition/tables/Fact.tmdl"] = "table Fact\n\tcolumn Value\n\t\tdataType: int64\n",
    };

    private static string Visual(string column) =>
        "{\"name\":\"v\",\"visual\":{\"visualType\":\"card\",\"Column\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Fact\"}},\"Property\":\"" + column + "\"}}}";

    private static ProjectInventory Scan(Dictionary<string, string> files) =>
        ProjectScanner.Scan(new InMemoryProjectFileSource("Unread page regression", files.Select(file =>
            new ProjectFileContent(file.Key, Encoding.UTF8.GetBytes(file.Value)))));
}

using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// PBIR documents the pages\ folder as required and pages.json as not. It is an index over the page
/// directories rather than the thing that makes them exist, so a report without it is a valid report
/// whose page ordering and active/landing metadata are simply unknown.
///
/// Reading it as zero pages silently converted every model object the report used into an absence
/// conclusion at full confidence, which is the failure this file exists to prevent.
/// </summary>
public sealed class ReportWithoutPagesIndexTests
{
    [Fact]
    public void PagesAndVisualsAreDiscoveredWithoutAPagesIndex()
    {
        var inventory = Scan(includePagesIndex: false);

        var report = Assert.Single(inventory.Reports);
        var page = Assert.Single(report.Pages);
        Assert.Equal("p1", page.Name);
        Assert.Equal("Page 1", page.DisplayName);
        Assert.Single(page.Visuals);

        // The report's evidence survives, so the measure it projects is still directly used.
        var total = Usage(inventory, "Total");
        Assert.Equal(SemanticUsageStates.DirectlyUsed, total.UsageState);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Amount").UsageState);

        // A report that was read completely is not a limitation.
        Assert.DoesNotContain(
            inventory.AnalysisLimitations,
            limitation => limitation.LimitationId == "PBI-LIMIT-REPORT-PAGES-UNREAD");
    }

    [Fact]
    public void OrderingAndIndexMetadataStayAbsentRatherThanFabricated()
    {
        var report = Assert.Single(Scan(includePagesIndex: false).Reports);

        Assert.Null(report.PagesSchemaUri);
        Assert.Null(report.ActivePageName);
        Assert.Null(report.LandingPageName);
        Assert.Null(Assert.Single(report.Pages).Order);
        Assert.DoesNotContain(
            report.SchemaObservations,
            observation => observation.ArtifactKind == ReportSchemaArtifactKinds.PagesMetadata);
    }

    [Fact]
    public void IndexedReportsAreUnchanged()
    {
        var report = Assert.Single(Scan(includePagesIndex: true).Reports);

        var page = Assert.Single(report.Pages);
        Assert.Equal(0, page.Order);
        Assert.Equal("p1", report.ActivePageName);
        Assert.NotNull(report.PagesSchemaUri);
        Assert.Contains(
            report.SchemaObservations,
            observation => observation.ArtifactKind == ReportSchemaArtifactKinds.PagesMetadata);
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(Scan(true), "Total").UsageState);
    }

    /// <summary>
    /// The safety net. Whatever the reason page directories go unread, the report's field usage is
    /// missing from the analysis, so this class of failure must never present as a full-confidence
    /// empty report again.
    /// </summary>
    [Fact]
    public void UnreadPageDirectoriesProduceADependencyBearingLimitation()
    {
        // A page directory whose page.json is missing is exactly what ParsePage cannot read.
        var inventory = Scan(includePagesIndex: false, includePageDefinition: false);

        Assert.Empty(Assert.Single(inventory.Reports).Pages);
        var limitation = Assert.Single(
            inventory.AnalysisLimitations,
            candidate => candidate.LimitationId == "PBI-LIMIT-REPORT-PAGES-UNREAD");
        Assert.Equal(AnalysisLimitationCauses.ParseFailed, limitation.Cause);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.Equal(AnalysisLimitationScopes.Report, limitation.Scope);
        Assert.Equal("Model", limitation.SemanticModel);

        // And the absence conclusions that follow from the unread report are no longer Established.
        var total = Usage(inventory, "Total");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, total.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, total.ClassificationConfidence);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == objectName);

    private static ProjectInventory Scan(bool includePagesIndex, bool includePageDefinition = true)
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Fact.tmdl",
                "table Fact\n\n\tmeasure Total = SUM ( Fact[Amount] )\n\n" +
                "\tcolumn Amount\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: Amount\n"),
            File("Model.Report/definition.pbir",
                "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"),
            File("Model.Report/definition/pages/p1/visuals/v1/visual.json",
                "{\"name\":\"v1\",\"visual\":{\"visualType\":\"card\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[" +
                "{\"field\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Fact\"}},\"Property\":\"Total\"}}," +
                "\"queryRef\":\"Fact.Total\"}]}}}}}"),
        };

        if (includePageDefinition)
        {
            files.Add(File("Model.Report/definition/pages/p1/page.json",
                "{\"name\":\"p1\",\"displayName\":\"Page 1\"}"));
        }

        if (includePagesIndex)
        {
            files.Add(File("Model.Report/definition/pages/pages.json",
                "{\"$schema\":\"https://developer.microsoft.com/json-schemas/fabric/item/report/definition/pagesMetadata/1.1.0/schema.json\"," +
                "\"pageOrder\":[\"p1\"],\"activePageName\":\"p1\"}"));
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));
}

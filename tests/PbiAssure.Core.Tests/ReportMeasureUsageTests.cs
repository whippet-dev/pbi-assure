using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// The in-process publication of where report measures are used directly. It is collected in the same
/// loop, under the same direct-usage policy, as the rooting of report-measure graph nodes, so the
/// evidence a report measure carries is exactly the evidence that made it a report root.
/// </summary>
public sealed class ReportMeasureUsageTests
{
    [Fact]
    public void EachReportMeasureIsListedWithItsOwnReportsDirectUsage()
    {
        var inventory = ScanFixture("pbi-assure-coverage");

        var active = Assert.Single(inventory.ReportMeasureUsages, usage => usage.Name == "ActiveReportMeasure");
        Assert.Equal("PbiAssureCoverage", active.Report);
        Assert.Equal("Fact", active.Entity);
        Assert.True(active.IsDirectlyReferencedByReport);
        Assert.All(active.DirectReportReferences, evidence => Assert.Equal("PbiAssureCoverage", evidence.Report));
        Assert.Contains(active.DirectReportLocations, location => location.Visual is not null);

        var unused = Assert.Single(inventory.ReportMeasureUsages, usage => usage.Name == "UnusedReportMeasure");
        Assert.Empty(unused.DirectReportReferences);
        Assert.Equal(
            inventory.Reports.Where(report => ReportModelBinder.FindLocalModel(report, inventory.SemanticModels) is not null)
                .Sum(report => report.ReportMeasures.Count),
            inventory.ReportMeasureUsages.Count);
    }

    [Theory]
    [InlineData("pbi-assure-coverage")]
    [InlineData("desktop-udf-measure-consumer")]
    public void PublishedEvidenceIsExactlyWhatRootsAReportMeasure(string fixture)
    {
        var inventory = ScanFixture(fixture);
        var reachability = inventory.ReportScopedNodeReachability
            .Where(node => node.ObjectType == SemanticObjectTypes.ReportMeasure)
            .ToDictionary(node => (node.SemanticModel, node.Report, node.Table, node.ObjectName));

        foreach (var usage in inventory.ReportMeasureUsages)
        {
            // Every report measure has a report-scoped row, whether or not it has edges.
            var node = reachability[(usage.SemanticModel, usage.ReportPath, usage.Entity, usage.Name)];
            if (usage.IsDirectlyReferencedByReport)
            {
                Assert.True(node.ReachableFromReport, usage.Name);
            }
            else
            {
                // Not a root, so reachable only if a report-reachable report measure of the same report
                // references it.
                var reachedThroughAnother = inventory.SemanticDependencies.Any(edge =>
                    edge.ToObjectType == SemanticObjectTypes.ReportMeasure &&
                    edge.ToReport == usage.ReportPath && edge.ToTable == usage.Entity && edge.ToObjectName == usage.Name &&
                    inventory.ReportScopedNodeReachability.Any(source => source.ReachableFromReport &&
                        source.Report == edge.FromReport && source.Table == edge.FromTable &&
                        source.ObjectName == edge.FromObjectName && source.ObjectType == edge.FromObjectType));
                Assert.Equal(reachedThroughAnother, node.ReachableFromReport);
            }
        }
    }

    [Fact]
    public void SameNamedReportMeasuresInTwoReportsKeepTheirOwnEvidence()
    {
        var inventory = ScanTwoReports();

        var inA = Assert.Single(inventory.ReportMeasureUsages, usage => usage.Report == "A");
        var inB = Assert.Single(inventory.ReportMeasureUsages, usage => usage.Report == "B");
        Assert.Equal(("Sales", "Local"), (inA.Entity, inA.Name));
        Assert.Equal(("Sales", "Local"), (inB.Entity, inB.Name));
        Assert.All(inA.DirectReportReferences, evidence => Assert.Equal("A", evidence.Report));
        Assert.True(inA.IsDirectlyReferencedByReport);
        Assert.False(inB.IsDirectlyReferencedByReport);
        Assert.Equal(("A.Report", "B.Report"), (inA.ReportPath, inB.ReportPath));

        // Each is its own report's graph node. The published schema-0.26 row cannot say which report a
        // report measure belongs to, so the name has one row there, reached because A's is.
        Assert.Equal(
            [("A.Report", true), ("B.Report", false)],
            inventory.ReportScopedNodeReachability
                .Where(node => node.ObjectType == SemanticObjectTypes.ReportMeasure && node.ObjectName == "Local")
                .Select(node => (node.Report!, node.ReachableFromReport))
                .ToArray());
        Assert.True(Assert.Single(inventory.SemanticNodeReachability, node =>
            node.ObjectType == SemanticObjectTypes.ReportMeasure && node.ObjectName == "Local").ReachableFromReport);
    }

    [Fact]
    public void ThePublicationIsNotPartOfThePublicJson()
    {
        var json = JsonSerializer.Serialize(ScanFixture("pbi-assure-coverage"));

        Assert.DoesNotContain("ReportMeasureUsages", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportScopedNodeReachability", json, StringComparison.Ordinal);
        Assert.Contains("\"SchemaVersion\":\"0.26\"", json, StringComparison.Ordinal);
    }

    internal static ProjectInventory ScanTwoReports()
    {
        const string measure = "{\"$schema\":\"https://developer.microsoft.com/json-schemas/fabric/item/report/definition/reportExtension/1.0.0/schema.json\"," +
            "\"name\":\"extension\",\"entities\":[{\"name\":\"Sales\",\"measures\":[{\"name\":\"Local\",\"dataType\":\"Decimal\"," +
            "\"expression\":\"{0}\",\"references\":{\"unrecognizedReferences\":false,\"measures\":[]}}]}]}";
        var files = new List<ProjectFileContent>
        {
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Sales.tmdl",
                "table Sales\n" + Column("Amount") + Column("Qty") + Column("Untouched")),
        };
        foreach (var (report, expression, placed) in new[] { ("A", "SUM(Sales[Amount])", true), ("B", "SUM(Sales[Qty])", false) })
        {
            files.Add(File($"{report}.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"));
            files.Add(File($"{report}.Report/definition/reportExtensions.json", measure.Replace("{0}", expression, StringComparison.Ordinal)));
            files.Add(File($"{report}.Report/definition/pages/pages.json", "{\"pageOrder\":[\"p1\"],\"activePageName\":\"p1\"}"));
            files.Add(File($"{report}.Report/definition/pages/p1/page.json", "{\"name\":\"p1\",\"displayName\":\"Page 1\"}"));
            if (placed)
            {
                files.Add(File($"{report}.Report/definition/pages/p1/visuals/v1/visual.json",
                    "{\"name\":\"v1\",\"visual\":{\"visualType\":\"card\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[" +
                    "{\"field\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Sales\"}},\"Property\":\"Local\"}},\"queryRef\":\"Sales.Local\"}]}}}}}"));
            }
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Two reports", files));
    }

    private static string Column(string name) =>
        $"\tcolumn {name}\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: {name}\n";

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));

    private static ProjectInventory ScanFixture(string fixture) =>
        ProjectScanner.Scan(Path.Combine(RepositoryRoot(), "tests", "fixtures", fixture));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

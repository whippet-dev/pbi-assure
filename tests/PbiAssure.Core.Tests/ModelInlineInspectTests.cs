using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ModelInlineInspectTests
{
    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PbiAssure.slnx"))) root = root.Parent;
        return root!.FullName;
    }
    private static ProjectInventory Scan(string fixture = "pbi-assure-coverage") => ProjectScanner.Scan(Path.Combine(Root(), "tests", "fixtures", fixture));

    private static XElement[] Rows(string html) => Regex.Matches(html, "<li class=\"semantic-object\"").Select(start =>
    {
        var depth = 0;
        foreach (Match tag in Regex.Matches(html[start.Index..], "</?li\\b[^>]*>"))
        {
            depth += tag.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
            if (depth == 0) return XElement.Parse(html.Substring(start.Index, tag.Index + tag.Length).Replace("&#x1F;", "", StringComparison.Ordinal));
        }
        throw new InvalidOperationException("Unclosed model row.");
    }).ToArray();
    private static XElement Row(ProjectInventory inventory, string name) => Rows(HtmlReportRenderer.Render(inventory))
        .Single(row => row.Element("div")!.Descendants("strong").Single().Value == name);
    private static XElement? Inspect(XElement row) => row.Element("details");
    private static XElement? Section(XElement? inspect, string name) => inspect?.Elements("section").SingleOrDefault(section => section.Element("h4")!.Value == name);

    [Theory]
    [InlineData("pbi-assure-coverage")]
    [InlineData("desktop-formatting-semantic-reference-sanitized")]
    [InlineData("desktop-dynamic-text-evidence")]
    public void PreviewsAreBoundedPublishedRelationsAndDoNotMutateInventory(string fixture)
    {
        var inventory = Scan(fixture); var original = JsonSerializer.Serialize(inventory);
        var projection = SemanticLineageProjection.Build(inventory);
        var rows = Rows(HtmlReportRenderer.Render(inventory));
        Assert.Equal(inventory.SemanticObjectUsages.Count, rows.Length);
        foreach (var usage in inventory.SemanticObjectUsages)
        {
            var card = projection.CardFor(usage)!; var row = rows.Single(row => (string?)row.Attribute("id") == card.DetailsAnchor);
            Assert.Equal(usage.UsageState, (string?)row.Attribute("data-usage-state"));
            Assert.Equal(usage.ClassificationConfidence, (string?)row.Attribute("data-classification-confidence"));
            if (usage.DirectReportLocationCount == 0 && projection.ReasonFor(usage) is { } reason)
                Assert.Contains(reason, row.Value, StringComparison.Ordinal);
            Assert.Equal("#sum-" + card.Id[4..], row.Element("div")!.Descendants("a").First().Attribute("href")!.Value);
            var inspect = Inspect(row);
            var consumers = Section(inspect, "Used by")?.Descendants("li").ToArray() ?? [];
            Assert.Equal(card.UsedBy.Items.Take(3).Select(item => item.Name), consumers.Select(item => item.Elements().First().Value));
            Assert.All(consumers, consumer => Assert.DoesNotContain("consumer node", consumer.Value, StringComparison.Ordinal));
            var locations = Section(inspect, "Used in the report")?.Descendants("li").ToArray() ?? [];
            Assert.Equal(Math.Min(3, card.UsedInReport.TotalCount), locations.Length);
            Assert.Equal(card.UsedInReport.Items.Take(3).Where(item => item.VisualCardId is not null).Select(item => "#visual-" + item.VisualCardId![4..] + "-summary"),
                locations.SelectMany(item => item.Descendants("a")).Select(link => link.Attribute("href")!.Value));
            if (inspect is null) continue;
            var summary = inspect.Element("summary")!;
            Assert.Equal("More about this object", summary.Value);
            Assert.Contains(card.Title, summary.Attribute("aria-label")!.Value, StringComparison.Ordinal);
            Assert.Empty(summary.Elements()); Assert.Null(inspect.Attribute("name")); Assert.Null(inspect.Attribute("open"));
            Assert.DoesNotContain("sourcePath", inspect.Value, StringComparison.Ordinal);
        }
        Assert.Equal(original, JsonSerializer.Serialize(inventory));
    }

    [Theory]
    [InlineData("DirectlyUsedMeasure", SemanticUsageStates.DirectlyUsed, true, true)]
    [InlineData("BaseAmount", SemanticUsageStates.IndirectlyUsed, true, false)]
    [InlineData("RelationshipKey", SemanticUsageStates.StructurallyRequired, false, false)]
    [InlineData("UnusedBranchColumn", SemanticUsageStates.UsedOnlyByUnusedBranch, true, false)]
    [InlineData("ApparentlyUnusedMeasure", SemanticUsageStates.ApparentlyUnused, false, false)]
    public void EveryStateKeepsItsMeaningAndOnlyShowsEstablishedUse(string name, string state, bool consumers, bool locations)
    {
        var inventory = Scan(); var row = Row(inventory, name); var inspect = Inspect(row);
        Assert.Equal(state, row.Attribute("data-usage-state")!.Value);
        Assert.Equal(consumers, Section(inspect, "Used by") is not null);
        Assert.Equal(locations, Section(inspect, "Used in the report") is not null);
        if (state == SemanticUsageStates.StructurallyRequired) Assert.Contains("Relationship key", row.Value, StringComparison.Ordinal);
        if (state == SemanticUsageStates.UsedOnlyByUnusedBranch) Assert.Contains("unused object", row.Value, StringComparison.Ordinal);
        if (state == SemanticUsageStates.ApparentlyUnused)
        {
            Assert.Contains("Apparently unused", row.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("safe to delete", row.Value, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(Section(inspect, "DAX"));
        }
    }

    [Fact]
    public void PhysicalColumnHasNoDaxAndCalculatedColumnHasItsSavedExpression()
    {
        var inventory = Scan();
        Assert.Null(Section(Inspect(Row(inventory, "BaseAmount")), "DAX"));
        var dax = Section(Inspect(Row(inventory, "CalculatedDependencyColumn")), "DAX")!;
        Assert.Equal("Fact[CalculatedSourceColumn]", dax.Descendants("code").Single().Value);
        Assert.Equal("Full definition", dax.Element("a")!.Value);
    }

    [Fact]
    public void UnusedPhysicalColumnsDoNotAcquireEmptyDisclosures()
    {
        var inventory = Scan(); var projection = SemanticLineageProjection.Build(inventory);
        var usage = inventory.SemanticObjectUsages.First(usage => usage.ObjectType == SemanticObjectTypes.Column && usage.UsageState == SemanticUsageStates.ApparentlyUnused);
        var row = Rows(HtmlReportRenderer.Render(inventory)).Single(row => (string?)row.Attribute("id") == projection.ObjectRowId(usage));
        Assert.Null(Inspect(row));
        Assert.Contains("Apparently unused", row.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1", "1", false)]
    [InlineData("1\n2\n3\n4\n5\n6\n7", "1\n2\n3\n4\n5\n6", true)]
    [InlineData("1\r\n2\r3\n4\n5\n6\n7", "1\n2\n3\n4\n5\n6", true)]
    [InlineData("<script>&\"", "<script>&\"", false)]
    public void DaxUsesSixLinesAndEscapesSavedText(string expression, string expected, bool truncated) => CheckExpression(expression, expected, truncated);

    [Fact]
    public void DaxUsesAtMostSixHundredCharactersAndNeverEmitsTheHiddenRemainder()
    {
        CheckExpression(new string('x', 600), new string('x', 600), false);
        CheckExpression(new string('x', 600) + "ONLY IN FULL DEFINITION", new string('x', 600), true);
        CheckExpression(new string('x', 599) + "😀tail", new string('x', 599), true);
    }

    private static void CheckExpression(string expression, string expected, bool truncated)
    {
        var inventory = Scan();
        inventory = inventory with { SemanticModels = inventory.SemanticModels.Select(model => model with { Tables = model.Tables.Select(table => table with {
            Measures = table.Measures.Select(measure => measure.Name == "DirectlyUsedMeasure" ? measure with { Expression = expression } : measure).ToArray()
        }).ToArray() }).ToArray() };
        var card = SemanticLineageProjection.Build(inventory).CardFor(inventory.SemanticObjectUsages.Single(usage => usage.ObjectName == "DirectlyUsedMeasure"))!;
        var dax = Section(Inspect(Row(inventory, "DirectlyUsedMeasure")), "DAX")!;
        Assert.Equal(expected, dax.Descendants("code").Single().Value);
        Assert.Equal(truncated, dax.Value.Contains("Preview — expression shortened.", StringComparison.Ordinal));
        Assert.Equal("#def-" + card.Id[4..], dax.Element("a")!.Attribute("href")!.Value);
        Assert.DoesNotContain("ONLY IN FULL DEFINITION", dax.Value, StringComparison.Ordinal);
        // The canonical Definition still carries the complete saved expression.
        var html = HtmlReportRenderer.Render(inventory);
        var definition = html[html.IndexOf($"id=\"def-{card.Id[4..]}\"", StringComparison.Ordinal)..];
        Assert.Contains(System.Text.Encodings.Web.HtmlEncoder.Default.Encode(expression), definition, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(8, 5)]
    [InlineData(60, 60)]
    public void ConsumersAndLocationsUseThreeItemCapsWithAccurateTotals(int consumerCount, int locationCount)
    {
        static ProjectFileContent File(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));
        var files = new List<ProjectFileContent> {
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Sales.tmdl", "table Sales\n\tmeasure Hub = 1\n" + string.Join("\n", Enumerable.Range(0, consumerCount).Select(i => $"\tmeasure Consumer{i} = [Hub]"))),
            File("Model.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"),
            File("Model.Report/definition/pages/pages.json", "{\"pageOrder\":[\"p\"]}"),
            File("Model.Report/definition/pages/p/page.json", "{\"name\":\"p\",\"displayName\":\"Overview\"}") };
        files.AddRange(Enumerable.Range(0, locationCount).Select(i => File($"Model.Report/definition/pages/p/visuals/v{i}/visual.json", JsonSerializer.Serialize(new {
            name = $"v{i}", visual = new { visualType = "card", query = new { queryState = new { Values = new { projections = new[] {
                new { field = new { Measure = new { Expression = new { SourceRef = new { Entity = "Sales" } }, Property = "Hub" } }, queryRef = "Sales.Hub" } } } } } } }))));
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Preview caps", files));
        var inspect = Inspect(Row(inventory, "Hub"))!;
        var consumers = Section(inspect, "Used by")!; var locations = Section(inspect, "Used in the report")!;
        Assert.Equal(3, consumers.Descendants("li").Count()); Assert.Equal(3, locations.Descendants("li").Count());
        Assert.Equal(consumerCount > 50 ? $"Explore in Lineage ({consumerCount} objects)" : $"See all {consumerCount} in Lineage", consumers.Element("a")!.Value);
        Assert.Equal(locationCount > 50 ? $"Report usage in Lineage ({locationCount} locations)" : $"See all {locationCount} report locations", locations.Element("a")!.Value);
        Assert.StartsWith("#lin-", consumers.Element("a")!.Attribute("href")!.Value, StringComparison.Ordinal);
        Assert.All(locations.Descendants("li"), item => Assert.Contains("Used as: Values", item.Value, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("PageFilterColumn", "Page-level reference", "Page filter")]
    [InlineData("ReportFilterColumn", "Report-level reference", "Report filter")]
    public void PageAndReportReferencesAreNotPresentedAsVisualPlacements(string name, string label, string role)
    {
        var section = Section(Inspect(Row(Scan(), name)), "Used in the report")!;
        Assert.Contains(label, section.Value, StringComparison.Ordinal); Assert.Contains("Used as: " + role, section.Value, StringComparison.Ordinal);
        Assert.Empty(section.Descendants("li").Descendants("a"));
    }
}

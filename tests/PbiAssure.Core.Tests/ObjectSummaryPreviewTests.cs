using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ObjectSummaryPreviewTests
{
    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PbiAssure.slnx"))) root = root.Parent;
        return root!.FullName;
    }
    private static ProjectInventory Scan(string fixture = "pbi-assure-coverage") => ProjectScanner.Scan(Path.Combine(Root(), "tests", "fixtures", fixture));
    private static string Article(string html, LineageCard card)
    {
        var start = html.IndexOf($"<article id=\"{card.Id}\"", StringComparison.Ordinal);
        return html[start..(html.IndexOf("</article>", start, StringComparison.Ordinal) + 10)];
    }
    private static XElement Summary(string article) => XElement.Parse("<root>" + Regex.Match(article,
        "data-object-view=\"summary\">([\\s\\S]*?)</section>\\s*<section class=\"object-local-view\" data-object-view=\"lineage\"").Groups[1].Value + "</root>");
    private static XElement? Section(XElement summary, string heading) => summary.Elements("section").SingleOrDefault(section => section.Element("h4")?.Value == heading);
    private static LineageCard Card(ProjectInventory inventory, string name) => SemanticLineageProjection.Build(inventory).CardFor(inventory.SemanticObjectUsages.Single(usage => usage.ObjectName == name && usage.Table == "Fact"))!;

    [Theory]
    [InlineData("pbi-assure-coverage")]
    [InlineData("desktop-formatting-semantic-reference-sanitized")]
    [InlineData("desktop-dynamic-text-evidence")]
    public void SummaryUsesOnlyBoundedPublishedRelationsAndDoesNotMutateInventory(string fixture)
    {
        var inventory = Scan(fixture); var original = JsonSerializer.Serialize(inventory);
        var projection = SemanticLineageProjection.Build(inventory); var html = HtmlReportRenderer.Render(inventory);
        foreach (var card in projection.Cards.Where(card => card.Kind != LineageFocusKind.Visual))
        {
            var article = Article(html, card); var summary = Summary(article);
            var usedBy = Section(summary, "Used by");
            Assert.Equal(card.UsedBy.Items.Take(3).Select(item => item.Name), usedBy?.Descendants("li").Select(item => item.Elements().First().Value) ?? []);
            var locations = summary.Elements("section").Where(section => section.Element("h4")?.Value is "Used in the report" or "Page and report references").SelectMany(section => section.Descendants("li")).ToArray();
            Assert.Equal(Math.Min(3, card.UsedInReport.TotalCount), locations.Length);
            Assert.Equal(card.UsedInReport.Items.Take(3).Where(item => item.VisualCardId is not null).Select(item => "#visual-" + item.VisualCardId![4..] + "-summary"),
                locations.SelectMany(item => item.Descendants("a")).Select(link => link.Attribute("href")!.Value));
            var meaningfulPath = card.Path is { Endpoint: not null, Steps.Count: > 1 };
            Assert.Equal(meaningfulPath, article.Contains("data-lineage-path=", StringComparison.Ordinal));
            Assert.Equal(meaningfulPath, article.Contains("<h3>How this reaches the report</h3>", StringComparison.Ordinal));
            Assert.DoesNotContain("Path to report", article, StringComparison.Ordinal);
            Assert.DoesNotContain("safe to delete", summary.Value, StringComparison.OrdinalIgnoreCase);
            if (card.Usage?.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation)
                Assert.Contains("Checks limited", article, StringComparison.Ordinal);
        }
        Assert.Equal(original, JsonSerializer.Serialize(inventory));
    }

    [Theory]
    [InlineData("DirectlyUsedMeasure", SemanticUsageStates.DirectlyUsed, true, true, false)]
    [InlineData("IndirectlyUsedMeasure", SemanticUsageStates.IndirectlyUsed, true, false, true)]
    [InlineData("RelationshipKey", SemanticUsageStates.StructurallyRequired, false, false, false)]
    [InlineData("UnusedBranchColumn", SemanticUsageStates.UsedOnlyByUnusedBranch, true, false, false)]
    [InlineData("ApparentlyUnusedMeasure", SemanticUsageStates.ApparentlyUnused, false, false, false)]
    public void EveryStatePreservesItsExplanation(string name, string state, bool consumers, bool locations, bool path)
    {
        var inventory = Scan(); var card = Card(inventory, name); var article = Article(HtmlReportRenderer.Render(inventory), card); var summary = Summary(article);
        Assert.Equal(state, card.Usage!.UsageState);
        Assert.Equal(consumers, Section(summary, "Used by") is not null);
        Assert.Equal(locations, Section(summary, "Used in the report") is not null);
        Assert.Equal(path, article.Contains("data-lineage-path=", StringComparison.Ordinal));
        if (card.Reason is not null) Assert.Contains(System.Net.WebUtility.HtmlDecode(card.Reason), System.Net.WebUtility.HtmlDecode(article), StringComparison.Ordinal);
        if (state == SemanticUsageStates.StructurallyRequired)
        {
            Assert.Contains("Required by the model", summary.Value, StringComparison.Ordinal);
            Assert.Contains("Required by the model structure", article, StringComparison.Ordinal);
        }
        if (state == SemanticUsageStates.ApparentlyUnused) Assert.Contains("Check before removing it", summary.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectMeasureHasTypedTrustedVisualCardsAndSelectedDirectPathStaysHidden()
    {
        var inventory = Scan(); var card = Card(inventory, "DirectlyUsedMeasure");
        // It has model consumers too: don't seek an alternative indirect path to force a diagram.
        Assert.NotEmpty(card.UsedBy.Items); Assert.Single(card.Path!.Steps);
        var article = Article(HtmlReportRenderer.Render(inventory), card); var summary = Summary(article);
        var locations = Section(summary, "Used in the report")!;
        Assert.Contains("Table", locations.Value, StringComparison.Ordinal);
        Assert.Contains("Report PbiAssureCoverage", locations.Value, StringComparison.Ordinal);
        Assert.Contains("Page Core coverage", locations.Value, StringComparison.Ordinal);
        Assert.Contains("Used as: Values", locations.Value, StringComparison.Ordinal);
        Assert.NotNull(Section(summary, "DAX")); Assert.DoesNotContain("data-lineage-path=", article, StringComparison.Ordinal);
        Assert.All(locations.Descendants("li"), item => Assert.StartsWith("#visual-", item.Element("a")!.Attribute("href")!.Value, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("PageFilterColumn", "Page-level reference", "Page filter")]
    [InlineData("ReportFilterColumn", "Report-level reference", "Report filter")]
    public void ReferencesStaySeparateFromVisualCards(string name, string label, string role)
    {
        var inventory = Scan(); var summary = Summary(Article(HtmlReportRenderer.Render(inventory), Card(inventory, name)));
        Assert.Null(Section(summary, "Used in the report"));
        var references = Section(summary, "Page and report references")!;
        Assert.Contains(label, references.Value, StringComparison.Ordinal); Assert.Contains("Used as: " + role, references.Value, StringComparison.Ordinal);
        Assert.Empty(references.Descendants("a"));
    }

    [Fact]
    public void MixedVisualAndPageUseHasOneSharedThreeLocationBudget()
    {
        var inventory = Scan(); var summary = Summary(Article(HtmlReportRenderer.Render(inventory), Card(inventory, "VisualFilterOnlyColumn")));
        Assert.Contains("Used as: Visual filter", Section(summary, "Used in the report")!.Value, StringComparison.Ordinal);
        Assert.Contains("Page-level reference", Section(summary, "Page and report references")!.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1", "1", false)]
    [InlineData("1\n2\n3\n4\n5\n6\n7", "1\n2\n3\n4\n5\n6", true)]
    [InlineData("1\r\n2\r3\n4\n5\n6\n7", "1\n2\n3\n4\n5\n6", true)]
    [InlineData("<script>&\"", "<script>&\"", false)]
    public void DaxHasTheSameLineLimitAndEscapingAsInlineInspection(string expression, string expected, bool truncated) => CheckDax(expression, expected, truncated);

    [Fact]
    public void DaxHasTheSameCharacterAndUnicodeLimitWithoutHiddenRemainder()
    {
        CheckDax(new string('x', 600), new string('x', 600), false);
        CheckDax(new string('x', 600) + "FULL ONLY", new string('x', 600), true);
        CheckDax(new string('x', 599) + "😀tail", new string('x', 599), true);
    }
    private static void CheckDax(string expression, string expected, bool truncated)
    {
        var inventory = Scan();
        inventory = inventory with { SemanticModels = inventory.SemanticModels.Select(model => model with { Tables = model.Tables.Select(table => table with {
            Measures = table.Measures.Select(measure => measure.Name == "DirectlyUsedMeasure" ? measure with { Expression = expression } : measure).ToArray()
        }).ToArray() }).ToArray() };
        var card = Card(inventory, "DirectlyUsedMeasure"); var html = HtmlReportRenderer.Render(inventory); var article = Article(html, card); var summary = Summary(article);
        var dax = Section(summary, "DAX")!;
        Assert.Equal(expected, dax.Descendants("code").Single().Value);
        Assert.Equal(truncated, dax.Value.Contains("Preview — expression shortened.", StringComparison.Ordinal));
        Assert.Equal("#def-" + card.Id[4..], dax.Element("a")!.Attribute("href")!.Value);
        Assert.DoesNotContain("FULL ONLY", summary.Value, StringComparison.Ordinal);
        var definition = article[article.IndexOf("data-object-view=\"definition\"", StringComparison.Ordinal)..];
        Assert.Contains(System.Text.Encodings.Web.HtmlEncoder.Default.Encode(expression), definition, StringComparison.Ordinal);
    }

    [Fact]
    public void PhysicalColumnHasNoEmptyDaxAndPowerQueryContextRemainsBounded()
    {
        var inventory = Scan(); var summary = Summary(Article(HtmlReportRenderer.Render(inventory), Card(inventory, "IndirectlyUsedColumn")));
        Assert.Null(Section(summary, "DAX"));
        var query = Section(summary, "Power Query context")!;
        Assert.Contains("Table loaded by:", query.Value, StringComparison.Ordinal);
        Assert.Contains("doesn't follow the column through every query step", query.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportMeasureOwnershipExpressionAndDirectPathRuleRemainReportSpecific()
    {
        var inventory = Scan(); var card = SemanticLineageProjection.Build(inventory).CardForReportMeasure("PbiAssureCoverage", "Fact", "ActiveReportMeasure")!;
        var article = Article(HtmlReportRenderer.Render(inventory), card); var summary = Summary(article);
        Assert.Contains("Report · Measure", System.Net.WebUtility.HtmlDecode(article), StringComparison.Ordinal);
        Assert.Contains("Report PbiAssureCoverage", summary.Value, StringComparison.Ordinal);
        Assert.Equal(inventory.Reports.Single(report => report.Name == "PbiAssureCoverage").ReportMeasures.Single(measure => measure.Name == "ActiveReportMeasure").Expression,
            Section(summary, "DAX")!.Descendants("code").Single().Value);
        Assert.DoesNotContain("data-lineage-path=", article, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(8, 5)]
    [InlineData(60, 60)]
    public void ThreeItemCapsKeepAccurateTotalsAndCanonicalOverflow(int consumerCount, int locationCount)
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
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Summary caps", files));
        var card = SemanticLineageProjection.Build(inventory).CardFor(inventory.SemanticObjectUsages.Single(usage => usage.ObjectName == "Hub"))!;
        var summary = Summary(Article(HtmlReportRenderer.Render(inventory), card));
        Assert.Equal(3, Section(summary, "Used by")!.Descendants("li").Count());
        Assert.Equal(3, Section(summary, "Used in the report")!.Descendants("li").Count());
        Assert.Contains(consumerCount > 50 ? $"Explore in Lineage ({consumerCount} objects)" : $"See all {consumerCount} in Lineage", summary.Value, StringComparison.Ordinal);
        Assert.Contains(locationCount > 50 ? $"Report usage in Lineage ({locationCount} locations)" : $"See all {locationCount} report locations", summary.Value, StringComparison.Ordinal);
        Assert.All(summary.Descendants("a").Where(link => link.Value.StartsWith("See all", StringComparison.Ordinal) || link.Value.Contains("in Lineage", StringComparison.Ordinal)), link => Assert.Equal("#" + card.Id, link.Attribute("href")!.Value));
    }
}

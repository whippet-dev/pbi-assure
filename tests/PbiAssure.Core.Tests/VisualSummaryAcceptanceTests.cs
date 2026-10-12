using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class VisualSummaryAcceptanceTests
{
    private static ProjectInventory Scan(string fixture = "pbi-assure-coverage")
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PbiAssure.slnx"))) root = root.Parent;
        return ProjectScanner.Scan(Path.Combine(root!.FullName, "tests", "fixtures", fixture));
    }

    private static XElement Article(string html, string id) => XElement.Parse(Regex.Matches(html,
        "<article [^>]*data-report-context=\"Visual\".*?</article>", RegexOptions.Singleline)
        .Single(match => match.Value.StartsWith($"<article id=\"{id}\"", StringComparison.Ordinal)).Value.Replace("&#x1F;", "", StringComparison.Ordinal));
    private static XElement Summary(XElement article) => article.Elements("section").Single(section => (string?)section.Attribute("data-context-view") == "summary");
    private static XElement[] Tiles(XElement summary) => summary.Elements("ul").Where(list => (string?)list.Attribute("class") == "visual-use-preview").Elements("li").ToArray();
    private static string Fact(XElement scope, string name) => scope.Descendants("dt").Single(term => term.Value == name).Parent!.Element("dd")!.Value;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(65)]
    public void SummaryShowsFourTrustedTilesAndAnAccurateCompleteObjectsRoute(int count)
    {
        var inventory = ReportCollectionTests.WideVisual(count);
        var before = JsonSerializer.Serialize(inventory);
        var report = inventory.Reports.Single(); var page = report.Pages.Single();
        var card = SemanticLineageProjection.Build(inventory).CardForVisual(report.Name, page.Name, "wide");
        var html = HtmlReportRenderer.Render(inventory);
        var route = ReportCollectionTests.VisualSummaryRoute(html, page.Visuals.Single(visual => visual.Name == "wide").RelativePath);
        var row = ReportCollectionTests.Pages(html).SelectMany(page => page.Descendants("li"))
            .Single(row => row.Element("h4")?.Element("a")?.Attribute("href")?.Value == route);
        var id = row.Attribute("data-preview-visual")!.Value;
        var article = Article(html, id); var summary = Summary(article);
        var tiles = Tiles(summary);
        Assert.Equal(Math.Min(count, 4), tiles.Length);
        Assert.Equal(count.ToString(System.Globalization.CultureInfo.InvariantCulture), Fact(summary, "Model objects used"));
        Assert.Equal(card?.Uses.Items.Take(4).Select(use => use.Object.Name) ?? [], tiles.Select(tile => tile.Elements("span").First().Value));
        Assert.All(tiles, tile =>
        {
            Assert.Contains("Measure — Used as: Values", tile.Value, StringComparison.Ordinal);
            Assert.Single(tile.Descendants("a"));
            Assert.Null(tile.Attribute("onclick"));
        });
        Assert.DoesNotContain("PageOnly", summary.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Stale", summary.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Missing", summary.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("M04", summary.Value, StringComparison.Ordinal);
        var more = summary.Descendants("a").SingleOrDefault(link => link.Value.StartsWith("See all ", StringComparison.Ordinal));
        if (count > 4)
        {
            Assert.Equal($"See all {count} in Objects →", more!.Value);
            Assert.Equal("#visual-" + id[4..] + "-objects", more.Attribute("href")!.Value);
        }
        else Assert.Null(more);
        if (count == 0) Assert.Contains("No model objects found for this visual.", summary.Value, StringComparison.Ordinal);
        var objects = article.Elements("section").Single(section => (string?)section.Attribute("data-context-view") == "objects");
        Assert.Equal(card?.Uses.Items.Count ?? 0, objects.Descendants("li").Count());
        Assert.Equal(before, JsonSerializer.Serialize(inventory));
    }

    [Theory]
    [InlineData("pbi-assure-coverage")]
    [InlineData("desktop-formatting-semantic-reference-sanitized")]
    [InlineData("desktop-dynamic-text-evidence")]
    public void SummaryUsesTheSamePolicyFilteredTilesAndRolePresentationAsReportPages(string fixture)
    {
        var inventory = Scan(fixture); var html = HtmlReportRenderer.Render(inventory);
        var projection = SemanticLineageProjection.Build(inventory);
        var rows = ReportCollectionTests.Pages(html).SelectMany(page => page.Descendants("li"))
            .Where(row => (string?)row.Attribute("class") == "visual-preview").ToArray();
        foreach (var report in inventory.Reports)
        foreach (var page in report.Pages)
        foreach (var visual in page.Visuals)
        {
            var card = projection.CardForVisual(report.Name, page.Name, visual.Name);
            if (card is null) continue;
            var summary = Summary(Article(html, card.Id));
            var tiles = Tiles(summary);
            var rowTiles = rows.Single(row => (string?)row.Attribute("data-preview-visual") == card.Id)
                .Elements("ul").Where(list => (string?)list.Attribute("class") == "visual-use-preview").Elements("li").ToArray();
            Assert.Equal(Math.Min(card.Uses.TotalCount, 4), tiles.Length);
            Assert.Equal(rowTiles.Select(tile => tile.ToString()), tiles.Take(3).Select(tile => tile.ToString()));
            Assert.Equal(card.Uses.Items.Take(4).Select(use => "#sum-" + use.Object.CardId![4..]),
                tiles.Select(tile => tile.Descendants("a").Single().Attribute("href")!.Value));
        }
    }

    [Fact]
    public void SummaryRetainsIdentityReviewCountsAndSpecificQualifications()
    {
        var inventory = Scan(); var html = HtmlReportRenderer.Render(inventory);
        foreach (var card in SemanticLineageProjection.Build(inventory).Cards.Where(card => card.Kind == LineageFocusKind.Visual))
        {
            var article = Article(html, card.Id); var summary = Summary(article);
            var findings = inventory.Findings.Where(item => item.Report == card.Report!.Name && item.Page == card.Page!.Name && item.Visual == card.Visual!.Name).ToArray();
            Assert.Equal(findings.Count(item => item.Category != AssuranceCategories.Accessibility).ToString(System.Globalization.CultureInfo.InvariantCulture), Fact(summary, "Findings"));
            Assert.Equal(findings.Count(item => item.Category == AssuranceCategories.Accessibility).ToString(System.Globalization.CultureInfo.InvariantCulture), Fact(summary, "Accessibility observations"));
            var theme = card.Report!.ThemeReview;
            var themeCount = theme.Deviations.Count(item => item.PageName == card.Page!.Name && item.VisualName == card.Visual!.Name)
                + theme.ConsistencyObservations.Count(item => item.PageName == card.Page!.Name && item.VisualName == card.Visual!.Name)
                + theme.AccessibilityObservations.Count(item => item.PageName == card.Page!.Name && item.VisualName == card.Visual!.Name);
            Assert.Equal(themeCount.ToString(System.Globalization.CultureInfo.InvariantCulture), Fact(summary, "Theme review items"));
            Assert.Contains("Saved project evidence; runtime behaviour and external usage are outside this view.", summary.Value, StringComparison.Ordinal);
            Assert.NotEmpty(article.Element("header")!.Element("h2")!.Value);
        }
    }

    [Fact]
    public void CopyTeachesInlineInspectionAndDropsOnlyTheRedundantSummaryInstruction()
    {
        var html = HtmlReportRenderer.Render(Scan());
        Assert.Contains("Use More about this object for a quick look, or open an object for its Summary, Lineage and Definition.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Open an object name to explore", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Explore dependencies and all listed locations in Lineage. Supporting evidence is in Details.", html, StringComparison.Ordinal);
        Assert.Contains("Some checks were limited for this model", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingAndDynamicAltTextStayConsistentAcrossInlineSnapshotAndDetails(bool dynamic)
    {
        var inventory = Scan();
        inventory = inventory with { Reports = inventory.Reports.Select(report => report with { Pages = report.Pages.Select(page => page with {
            Visuals = page.Visuals.Select(visual => visual.Name == "access-missing-alt" ? visual with {
                Accessibility = visual.Accessibility with { AltTextIsDynamic = dynamic }
            } : visual).ToArray() }).ToArray() }).ToArray() };
        var report = inventory.Reports.Single(report => report.Pages.Any(page => page.Visuals.Any(visual => visual.Name == "access-missing-alt")));
        var page = report.Pages.Single(page => page.Visuals.Any(visual => visual.Name == "access-missing-alt"));
        var html = HtmlReportRenderer.Render(inventory);
        var route = ReportCollectionTests.VisualSummaryRoute(html, page.Visuals.Single(visual => visual.Name == "access-missing-alt").RelativePath);
        var row = ReportCollectionTests.Pages(html).SelectMany(page => page.Descendants("li"))
            .Single(row => row.Element("h4")?.Element("a")?.Attribute("href")?.Value == route);
        var article = Article(html, row.Attribute("data-preview-visual")!.Value);
        Assert.Equal(dynamic ? "Dynamic alt text" : "Not set", Fact(article, "Alt text"));
        Assert.Equal(dynamic ? "Dynamic alt text" : "Not set", Fact(article, "Saved alternative text"));
        var inspect = row.Element("details")!;
        Assert.Equal(dynamic ? "Dynamic alt text" : "Not set", Fact(inspect, "Alt text"));
        Assert.DoesNotContain("<dt>Alt text</dt><dd>Missing</dd>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Alt text</dt><dd>Not configured</dd>", html, StringComparison.Ordinal);
    }
}

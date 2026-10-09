using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class ReportCollectionTests
{
    private static ProjectInventory Scan(string name = "pbi-assure-coverage")
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PbiAssure.slnx"))) root = root.Parent;
        return ProjectScanner.Scan(Path.Combine(root!.FullName, "tests", "fixtures", name));
    }
    internal static XElement[] Pages(string html)
    {
        // Page cards contain native visual disclosures; match their balanced outer boundary.
        return Regex.Matches(html, "<details class=\"page-card\"").Select(start =>
        {
            var depth = 0;
            foreach (Match tag in Regex.Matches(html[start.Index..], "</?details\\b[^>]*>"))
            {
                depth += tag.Value.StartsWith("</", StringComparison.Ordinal) ? -1 : 1;
                if (depth == 0)
                    return XElement.Parse(html.Substring(start.Index, tag.Index + tag.Length).Replace("&#x1F;", "", StringComparison.Ordinal));
            }
            throw new InvalidOperationException("Unclosed page card.");
        }).ToArray();
    }
    private static XElement[] Rows(string html) => Pages(html).SelectMany(page => page.Descendants("li")
        .Where(row => (string?)row.Attribute("class") == "visual-preview")).ToArray();
    private static XElement[] Uses(XElement row) => row.Elements("ul").Where(list => (string?)list.Attribute("class") == "visual-use-preview").Elements("li").ToArray();
    private static string[] ObjectLinks(XElement row) => Uses(row).SelectMany(item => item.Descendants("a")).Select(link => (string)link.Attribute("href")!).ToArray();

    [Theory]
    [InlineData("pbi-assure-coverage")]
    [InlineData("desktop-formatting-semantic-reference-sanitized")]
    [InlineData("desktop-dynamic-text-evidence")]
    public void EachPreviewIsTheFirstThreeItemsOfThePublishedVisualObjectsRelation(string name)
    {
        var inventory = Scan(name); var before = JsonSerializer.Serialize(inventory);
        var projection = SemanticLineageProjection.Build(inventory);
        var html = HtmlReportRenderer.Render(inventory); var rows = Rows(html);
        Assert.Equal(inventory.VisualCount, rows.Length);
        foreach (var report in inventory.Reports)
        foreach (var page in report.Pages)
        foreach (var visual in page.Visuals)
        {
            var card = projection.CardForVisual(report.Name, page.Name, visual.Name);
            var row = card is not null ? rows.Single(row => (string?)row.Attribute("data-preview-visual") == card.Id)
                : rows.Single(row => row.Element("h4")!.Element("a")!.Attribute("href")!.Value == VisualSummaryRoute(html, visual.RelativePath));
            var expected = card?.Uses.Items.Take(3).ToArray() ?? [];
            Assert.Equal(expected.Select(use => "#sum-" + use.Object.CardId![4..]), ObjectLinks(row));
            Assert.Equal(expected.Select(use => use.Object.Name), Uses(row).Select(item => item.Element("span")!.Value));
            Assert.Equal(Math.Min(card?.Uses.TotalCount ?? 0, 3), Uses(row).Length);
            foreach (var (item, index) in Uses(row).Select((item, index) => (item, index)))
                Assert.StartsWith(SemanticLineageProjection.ObjectTypeLabel(expected[index].Object.ObjectType),
                    item.Elements("span").Single(span => (string?)span.Attribute("class") == "visual-use-meta").Value, StringComparison.Ordinal);
            if ((card?.Uses.TotalCount ?? 0) > 3)
            {
                var more = row.Elements("a").Single();
                Assert.Equal($"+{card!.Uses.TotalCount - 3} more", more.Value);
                Assert.Equal("#visual-" + card.Id[4..] + "-objects", more.Attribute("href")!.Value);
            }
            else Assert.DoesNotContain(row.Elements("a"), link => (string?)link.Attribute("class") == "visual-preview-more");
        }
        Assert.Equal(before, JsonSerializer.Serialize(inventory));
    }
    internal static string VisualSummaryRoute(string html, string path)
    {
        var article = Regex.Matches(html, "<article [^>]*data-report-context=\"Visual\".*?</article>", RegexOptions.Singleline)
            .Single(match => match.Value.Contains(System.Text.Encodings.Web.HtmlEncoder.Default.Encode(path), StringComparison.Ordinal)).Value;
        return Regex.Match(article, "href=\"(#[^\"]+)\" data-context-view-link=\"summary\"|data-context-view-link=\"summary\" href=\"(#[^\"]+)\"")
            .Groups.Cast<Group>().Skip(1).First(group => group.Success).Value;
    }

    internal static ProjectInventory WideVisual(int count = 65)
    {
        static ProjectFileContent File(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));
        static object Field(string property, string kind = "Measure") => new Dictionary<string, object> {
            [kind] = new { Expression = new { SourceRef = new { Entity = "Sales" } }, Property = property } };
        var projections = Enumerable.Range(0, count).Select(index => new { field = Field($"M{index:D2}"), queryRef = $"Sales.M{index:D2}" })
            .Append(new { field = Field("Missing"), queryRef = "Sales.Missing" }).ToArray();
        var visual = JsonSerializer.Serialize(new { name = "wide", position = new { x = 10, y = 10, width = 300, height = 200 }, visual = new {
            visualType = "tableEx", query = new { queryState = new { Values = new { projections } } },
            objects = new { lineStyles = new[] { new { properties = new { lineStyle = new { expr = new { Literal = new { Value = "'dashed'" } } } },
                selector = new { data = new[] { new { scopeId = new { Comparison = new { Left = Field("Stale", "Column"), Right = new { Literal = new { Value = "'B'" } } } } } } } } } } } });
        var second = JsonSerializer.Serialize(new { name = "overlap", visual = new { visualType = "card", query = new { queryState = new { Values = new {
            projections = Enumerable.Range(0, Math.Min(count, 2)).Select(index => new { field = Field($"M{index:D2}"), queryRef = $"Sales.M{index:D2}" }).ToArray() } } } } });
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Collection scanability", [
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Sales.tmdl", "table Sales\n\tcolumn PageOnly\n\t\tdataType: int64\n\tcolumn Stale\n\t\tdataType: int64\n" + string.Join("\n", Enumerable.Range(0, count).Select(index => $"\tmeasure M{index:D2} = 1"))),
            File("Model.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"),
            File("Model.Report/definition/pages/pages.json", "{\"pageOrder\":[\"p1\"]}"),
            File("Model.Report/definition/pages/p1/page.json", JsonSerializer.Serialize(new { name = "p1", displayName = "Collection page", width = 1280, height = 720,
                filterConfig = new { filters = new[] { new { name = "Page filter", field = Field("PageOnly", "Column") } } } })),
            File("Model.Report/definition/pages/p1/visuals/wide/visual.json", visual),
            File("Model.Report/definition/pages/p1/visuals/overlap/visual.json", second),
            File("Model.Report/definition/pages/p1/visuals/static/visual.json", "{\"name\":\"static\",\"visual\":{\"visualType\":\"image\"}}") ]));
    }

    [Fact]
    public void PageDistinctCountIsUncappedAndExcludesPageFiltersStaleSelectorsAndMissingReferences()
    {
        var inventory = WideVisual(); var projection = SemanticLineageProjection.Build(inventory);
        var report = inventory.Reports.Single(); var page = report.Pages.Single();
        Assert.Contains(page.FieldReferences, reference => reference.ObjectName == "PageOnly");
        Assert.Contains(page.Visuals.Single(visual => visual.Name == "wide").FieldReferences, reference => reference.ObjectName == "Stale");
        var card = projection.CardForVisual(report.Name, page.Name, "wide")!;
        Assert.Equal(65, card.Uses.TotalCount); Assert.Equal(50, card.Uses.Items.Count);
        Assert.Equal(65, projection.DirectVisualObjectCountForPage(report.Name, page.Name));
        var html = HtmlReportRenderer.Render(inventory); var collection = Pages(html).Single();
        Assert.Contains("3 visuals · 65 model objects used by visuals", collection.Element("summary")!.Value, StringComparison.Ordinal);
        var row = Rows(html).Single(row => (string?)row.Attribute("data-preview-visual") == card.Id);
        Assert.Equal(3, Uses(row).Length); Assert.Contains("+62 more", row.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("PageOnly", row.Value, StringComparison.Ordinal); Assert.DoesNotContain("Stale", row.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Missing", string.Join(' ', Uses(row).Select(use => use.Value)), StringComparison.Ordinal); Assert.DoesNotContain("M03", row.Value, StringComparison.Ordinal);
        Assert.Contains("Sales[PageOnly]", collection.Elements("div").Descendants("div").Single(element => (string?)element.Attribute("class") == "page-reference-preview").Value, StringComparison.Ordinal);
        Assert.Contains("No model objects used", Rows(html).Single(item => item.Element("h4")!.Value == "Image").Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PageCountsUseNaturalSingularAndZeroGrammar(int count)
    {
        var inventory = WideVisual(count); var summary = Pages(HtmlReportRenderer.Render(inventory)).Single().Element("summary")!.Value;
        Assert.Contains(count == 1 ? "1 model object used by visuals" : "0 model objects used by visuals", summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EmptyAndSingleVisualPagesKeepTheirVisualCount(int count)
    {
        var inventory = WideVisual(1); var report = inventory.Reports.Single(); var page = report.Pages.Single();
        inventory = inventory with { Reports = [report with { Pages = [page with {
            Visuals = page.Visuals.Where(visual => visual.Name == "wide").Take(count).ToArray() }] }] };
        var summary = Pages(HtmlReportRenderer.Render(inventory)).Single().Element("summary")!.Value;
        Assert.Contains(count == 1 ? "1 visual · 1 model object" : "0 visuals · 0 model objects", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void PageReferencePreviewIsSeparateBoundedAndRoutesOverflowToTheExistingPageSummary()
    {
        var inventory = WideVisual(4); var report = inventory.Reports.Single(); var page = report.Pages.Single();
        var template = page.FieldReferences.Single();
        inventory = inventory with { Reports = [report with { Pages = [page with {
            FieldReferences = Enumerable.Range(0, 5).Select(index => template with { ObjectName = $"PageOnly{index}" }).ToArray() }] }] };
        var html = HtmlReportRenderer.Render(inventory);
        var references = Pages(html).Single().Descendants("div").Single(element => (string?)element.Attribute("class") == "page-reference-preview");
        Assert.Equal(3, references.Descendants("li").Count());
        Assert.Contains("couldn't be matched", references.Value, StringComparison.Ordinal);
        var more = references.Element("p")!.ElementsAfterSelf("p").Single().Element("a")!;
        Assert.Equal("+2 more page-level references", more.Value);
        Assert.Contains("-summary", more.Attribute("href")!.Value, StringComparison.Ordinal);
        Assert.All(Rows(html), row => Assert.DoesNotContain("PageOnly", row.Value, StringComparison.Ordinal));
        Assert.Equal(4, SemanticLineageProjection.Build(inventory).DirectVisualObjectCountForPage(report.Name, page.Name));
    }

    [Fact]
    public void KnownRolesAreShownAndUnknownPathsAreOmittedWithoutChangingEvidence()
    {
        var inventory = WideVisual(3);
        inventory = inventory with { SemanticObjectUsages = inventory.SemanticObjectUsages.Select(usage => usage with {
            DirectReportReferences = usage.DirectReportReferences.Select(evidence => evidence with { Role = usage.ObjectName switch {
                "M00" => "Rows", "M01" => "Conditional formatting", _ => "$.visual.objects.implementationProperty" } }).ToArray()
        }).ToArray() };
        var before = JsonSerializer.Serialize(inventory);
        var row = Rows(HtmlReportRenderer.Render(inventory)).First();
        Assert.Contains("Rows", row.Value, StringComparison.Ordinal); Assert.Contains("Conditional formatting", row.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Implementation", row.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$.visual", row.Value, StringComparison.Ordinal);
        Assert.Equal(before, JsonSerializer.Serialize(inventory));
    }

    [Theory]
    [InlineData("projection-visual", "Measure — Used as: Values")]
    [InlineData("formatting-only-visual", "Column — Used as: Formatting")]
    [InlineData("filter-only-visual", "Column — Used as: Visual filter")]
    public void UsageTilesShowThePublishedObjectTypeAndPlainPowerBIRole(string visualName, string expected)
    {
        var inventory = Scan(); var visual = inventory.Reports.SelectMany(report => report.Pages).SelectMany(page => page.Visuals).Single(visual => visual.Name == visualName);
        var html = HtmlReportRenderer.Render(inventory); var route = VisualSummaryRoute(html, visual.RelativePath);
        var row = Rows(html).Single(row => row.Element("h4")!.Element("a")!.Attribute("href")!.Value == route);
        Assert.Contains("Objects used by this visual", row.Value, StringComparison.Ordinal);
        Assert.Contains(Uses(row), tile => tile.Elements("span").Any(span => span.Value == expected));
        Assert.All(Uses(row), tile => Assert.Single(tile.Descendants("a")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ReviewPreviewsOnlyShowNonzeroVisualSpecificFamilies(int count)
    {
        var inventory = Scan(); var card = SemanticLineageProjection.Build(inventory).Cards.First(item => item.Kind == LineageFocusKind.Visual);
        var template = inventory.Findings[0] with { Report = card.Report!.Name, Page = card.Page!.Name, Visual = card.Visual!.Name };
        inventory = inventory with { Findings = Enumerable.Range(0, count).Select(index => template with { Category = "Navigation" })
            .Concat(Enumerable.Range(0, count).Select(index => template with { Category = AssuranceCategories.Accessibility })).ToArray(),
            Reports = inventory.Reports.Select(report => report == card.Report ? report with { ThemeReview = report.ThemeReview with {
                Deviations = Enumerable.Range(0, count).Select(index => new ThemeDeviationInventory(card.Page.Name, card.Page.DisplayName, card.Visual.Name, card.Visual.VisualType, "font", "Font", "12", "10", null)).ToArray(),
                ConsistencyObservations = [], AccessibilityObservations = [] } } : report).ToArray() };
        var row = Rows(HtmlReportRenderer.Render(inventory)).Single(row => (string?)row.Attribute("data-preview-visual") == card.Id);
        var cue = row.Elements("p").SingleOrDefault(element => (string?)element.Attribute("class") == "visual-review-preview");
        if (count == 0) Assert.Null(cue);
        else
        {
            Assert.Contains($"{count} {(count == 1 ? "finding" : "findings")}", cue!.Value, StringComparison.Ordinal);
            Assert.Contains($"{count} {(count == 1 ? "accessibility observation" : "accessibility observations")}", cue.Value, StringComparison.Ordinal);
            Assert.Contains($"{count} {(count == 1 ? "theme review item" : "theme review items")}", cue.Value, StringComparison.Ordinal);
            Assert.Equal("#visual-" + card.Id[4..] + "-reviews", cue.Element("a")!.Attribute("href")!.Value);
        }
        if (count > 0) Assert.Contains("on this page, including visuals", Pages(HtmlReportRenderer.Render(inventory)).First(page => page.Attribute("data-page-name")!.Value == card.Page.DisplayName).Element("summary")!.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void IdentityMetadataAvoidsRepeatingTheTypeAndKeepsSameTitledVisualsDistinct()
    {
        var inventory = WideVisual(1); var report = inventory.Reports.Single(); var page = report.Pages.Single();
        inventory = inventory with { Reports = [report with { Pages = [page with { Visuals = page.Visuals.Select(visual => visual with {
            Accessibility = visual.Accessibility with { TitleText = "Image", TitleIsVisible = true, HasConfiguredTitleText = true }, VisualType = "image" }).ToArray() }] }] };
        var rows = Rows(HtmlReportRenderer.Render(inventory));
        Assert.Equal(3, rows.Select(row => row.Attribute("data-preview-visual")!.Value).Distinct().Count());
        Assert.All(rows, row => Assert.DoesNotContain("Image", row.Elements("p").First().Value, StringComparison.Ordinal));
        Assert.All(rows, row => Assert.Contains("Image", row.Element("h4")!.Value, StringComparison.Ordinal));
        // Even an image uses objects if the already-published evidence says it does.
        Assert.Equal(2, rows.Count(row => Uses(row).Length > 0));
        Assert.Single(rows, row => Uses(row).Length == 0);
        Assert.Contains("single-report-name", HtmlReportRenderer.Render(inventory), StringComparison.Ordinal);
    }
}

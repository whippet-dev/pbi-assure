using System.Text.Json;
using System.Xml.Linq;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

public sealed class VisualInlineInspectTests
{
    private static readonly string[] InspectHeadings = ["Behaviour", "Accessibility"];
    private static ProjectInventory Coverage()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PbiAssure.slnx"))) root = root.Parent;
        return ProjectScanner.Scan(Path.Combine(root!.FullName, "tests", "fixtures", "pbi-assure-coverage"));
    }

    private static XElement Row(ProjectInventory inventory, string visualName)
    {
        var visual = inventory.Reports.SelectMany(report => report.Pages).SelectMany(page => page.Visuals).Single(visual => visual.Name == visualName);
        var html = HtmlReportRenderer.Render(inventory);
        var route = ReportCollectionTests.VisualSummaryRoute(html, visual.RelativePath);
        return ReportCollectionTests.Pages(html).SelectMany(page => page.Descendants("li"))
            .Single(row => (string?)row.Attribute("class") == "visual-preview" && row.Element("h4")!.Element("a")!.Attribute("href")!.Value == route);
    }
    private static XElement Inspect(XElement row) => row.Elements("details").Single();
    private static string Fact(XElement inspect, string name) => inspect.Descendants("dt")
        .Single(term => (term.Element("span")?.Value ?? term.Value) == name).Parent!.Element("dd")!.Value;

    [Fact]
    public void TooltipAndActionsReuseSavedFriendlyTargetsAndPreserveQualifications()
    {
        var inventory = Coverage();
        var tooltip = Inspect(Row(inventory, "projection-visual"));
        Assert.Contains("Report-page tooltip: page “Coverage tooltip”", tooltip.Value, StringComparison.Ordinal);
        var actions = Inspect(Row(inventory, "valid-actions"));
        Assert.Contains("opens report page “Action destination”", actions.Value, StringComparison.Ordinal);
        Assert.Contains("applies bookmark “Coverage bookmark”", actions.Value, StringComparison.Ordinal);
        Assert.Contains("opens a web link", actions.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime behaviour has not been tested", actions.Value, StringComparison.Ordinal);
        var broken = Inspect(Row(inventory, "diagnostic-actions"));
        Assert.Contains("bookmark that is no longer present", broken.Value, StringComparison.Ordinal);
        Assert.Contains("missing page", broken.Value, StringComparison.Ordinal);
        Assert.Contains("destination is set dynamically", broken.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("MissingActionBookmark", broken.Value, StringComparison.Ordinal);
        var brokenTooltips = Inspect(Row(inventory, "tooltip-diagnostics"));
        Assert.Contains("page “missing page”", brokenTooltips.Value, StringComparison.Ordinal);
        Assert.Contains("dynamic or unspecified page", brokenTooltips.Value, StringComparison.Ordinal);
        Assert.Contains("destination is set dynamically", brokenTooltips.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void MinimalVisualShowsUsefulSavedDefaultsWithoutForensicPayload()
    {
        var inspect = Inspect(Row(Coverage(), "access-missing-alt"));
        Assert.Contains("No configured action or tooltip.", inspect.Value, StringComparison.Ordinal);
        Assert.Equal("Missing", Fact(inspect, "Alt text"));
        Assert.Contains("Included", Fact(inspect, "Tab order"), StringComparison.Ordinal);
        Assert.DoesNotContain("$.visual", inspect.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void SavedAltTextTitleAndFriendlyTabOrderAreAvailableInline()
    {
        var inspect = Inspect(Row(Coverage(), "projection-visual"));
        Assert.Equal("Core semantic projection coverage", Fact(inspect, "Alt text"));
        var tab = inspect.Descendants("dt").Single(term => term.Value.StartsWith("Tab order", StringComparison.Ordinal)).Parent!.Element("dd")!.Value;
        Assert.Contains("Included", tab, StringComparison.Ordinal);
        Assert.Contains("Position 8", tab, StringComparison.Ordinal);
        var excluded = Inspect(Row(Coverage(), "access-excluded"));
        Assert.Contains("Excluded", excluded.Value, StringComparison.Ordinal);
        Assert.Equal("Hidden", Fact(Inspect(Row(Coverage(), "access-title-disabled")), "Title"));
    }

    [Fact]
    public void ExistingDynamicStatesAreNotPresentedAsResolvedSavedTextOrTargets()
    {
        var inventory = Coverage();
        inventory = inventory with { Reports = inventory.Reports.Select(report => report with { Pages = report.Pages.Select(page => page with {
            Visuals = page.Visuals.Select(visual => visual.Name == "projection-visual" ? visual with {
                Accessibility = visual.Accessibility with { AltTextIsDynamic = true, TitleTextIsDynamic = true },
                TooltipBindings = visual.TooltipBindings.Select(binding => binding with { HasDynamicConfiguration = true }).ToArray()
            } : visual).ToArray() }).ToArray() }).ToArray() };
        var before = JsonSerializer.Serialize(inventory);
        var inspect = Inspect(Row(inventory, "projection-visual"));
        Assert.Equal("Dynamic alt text", Fact(inspect, "Alt text"));
        Assert.Equal("Dynamic title", Fact(inspect, "Title text"));
        Assert.Contains("Report-page tooltip: destination is set dynamically", inspect.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Coverage tooltip", inspect.Value, StringComparison.Ordinal);
        Assert.Equal(before, JsonSerializer.Serialize(inventory));
    }

    [Fact]
    public void EachVisualHasAnIndependentNamedDisclosureAndItsStableContextLink()
    {
        var inventory = Coverage();
        var rows = ReportCollectionTests.Pages(HtmlReportRenderer.Render(inventory)).SelectMany(page => page.Descendants("li"))
            .Where(row => (string?)row.Attribute("class") == "visual-preview").ToArray();
        Assert.Equal(inventory.VisualCount, rows.Length);
        Assert.Equal(rows.Length, rows.Select(row => Inspect(row).Attribute("id")!.Value).Distinct().Count());
        foreach (var row in rows)
        {
            var inspect = Inspect(row); var summary = inspect.Element("summary")!;
            Assert.Equal("More about this visual", summary.Value);
            Assert.StartsWith(summary.Value + ": " + row.Element("h4")!.Value + " · ", summary.Attribute("aria-label")!.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("Saved configuration; runtime behaviour has not been tested.", inspect.Value, StringComparison.Ordinal);
            Assert.Empty(summary.Elements());
            Assert.Null(inspect.Attribute("name")); Assert.Null(inspect.Attribute("open"));
            Assert.EndsWith("-summary", row.Element("h4")!.Element("a")!.Attribute("href")!.Value, StringComparison.Ordinal);
            Assert.Equal(InspectHeadings, inspect.Elements("h5").Select(heading => heading.Value));
            Assert.Empty(inspect.Descendants("details")); Assert.Empty(inspect.Descendants("pre"));
            Assert.DoesNotContain("Source file", inspect.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("All saved field references", inspect.Value, StringComparison.Ordinal);
        }
    }
}

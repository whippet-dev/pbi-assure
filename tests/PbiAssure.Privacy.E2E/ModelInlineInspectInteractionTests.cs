using System.Text.Json;
using Microsoft.Playwright;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class ModelInlineInspectInteractionTests(PrivacyE2EFixture fixture) : IAsyncLifetime
{
    private readonly List<string> errors = [];
    private string Output => Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-model-inspect");
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() { Assert.Empty(errors); return Task.CompletedTask; }
    private ProjectInventory Coverage() => ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "pbi-assure-coverage"));
    private static Task<JsonElement?> Settle(IPage page) => page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    private static ILocator Row(IPage page, string name) => page.Locator(".semantic-object").Filter(new() {
        Has = page.Locator(".object-name strong a").Filter(new() { HasTextRegex = new("^" + System.Text.RegularExpressions.Regex.Escape(name) + "$") }) });
    private static ILocator Section(ILocator row, string heading) => row.Locator(".model-inspect > section").Filter(new() {
        Has = row.Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });

    private async Task<IPage> Open(IBrowserContext context, string name, ProjectInventory? inventory = null, string? longObjectName = null)
    {
        Directory.CreateDirectory(Output);
        var path = Path.Combine(Output, name + ".html");
        var html = HtmlReportRenderer.Render(inventory ?? Coverage());
        if (longObjectName is not null) html = html.Replace(">DirectlyUsedMeasure</a>", ">" + System.Text.Encodings.Web.HtmlEncoder.Default.Encode(longObjectName) + "</a>", StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, html);
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await page.GotoAsync(new Uri(path).AbsoluteUri + "#semantic-usage"); await Settle(page);
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand all tables", Exact = true }).ClickAsync();
        return page;
    }

    [Fact]
    public async Task NativeDisclosureAllowsComparisonWithoutNavigationAndKeepsDeepDestinations()
    {
        await using var context = await fixture.Browser.NewContextAsync(); var page = await Open(context, "comparison");
        await page.Locator("#usage-search").FillAsync("Amount");
        var first = Row(page, "BaseAmount"); var firstSummary = first.Locator(".model-inspect > summary");
        var url = page.Url; var entries = await page.EvaluateAsync<int>("history.length");
        Assert.Equal("More about this object", await firstSummary.InnerTextAsync());
        Assert.Contains("Fact[BaseAmount]", await firstSummary.GetAttributeAsync("aria-label") ?? "", StringComparison.Ordinal);
        Assert.Equal(0, await firstSummary.Locator("a, button, input").CountAsync());
        await firstSummary.PressAsync("Enter"); await Settle(page);
        Assert.True(await firstSummary.EvaluateAsync<bool>("e => e.matches(':focus-visible')"));
        Assert.NotEqual("none", await firstSummary.EvaluateAsync<string>("e => getComputedStyle(e).outlineStyle"));
        Assert.Equal(3, await Section(first, "Used by").Locator("li").CountAsync());
        Assert.Contains("See all", await Section(first, "Used by").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(0, await first.GetByRole(AriaRole.Heading, new() { Name = "Used in the report", Exact = true }).CountAsync());
        await page.Locator("#usage-search").FillAsync("Measure");
        var second = Row(page, "DirectlyUsedMeasure");
        await second.Locator(".model-inspect > summary").PressAsync("Space"); await Settle(page);
        Assert.True(await first.Locator(".model-inspect").EvaluateAsync<bool>("e => e.open"));
        Assert.True(await second.Locator(".model-inspect").EvaluateAsync<bool>("e => e.open"));
        Assert.Equal(url, page.Url); Assert.Equal(entries, await page.EvaluateAsync<int>("history.length"));
        Assert.Contains("Used as: Values", await Section(second, "Used in the report").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains("IndirectlyUsedMeasure", await Section(second, "DAX").InnerTextAsync(), StringComparison.Ordinal);
        await page.Locator("#usage-search").FillAsync("Fact"); await Settle(page);
        await second.ScrollIntoViewIfNeededAsync();
        await second.ScreenshotAsync(new() { Path = Path.Combine(Output, "direct-measure.png") });
        await first.ScreenshotAsync(new() { Path = Path.Combine(Output, "indirect-column.png") });
        var definition = Section(second, "DAX").GetByRole(AriaRole.Link, new() { Name = "Full definition", Exact = true });
        await definition.PressAsync("Enter"); await Settle(page);
        Assert.Equal("definition", await page.Locator("[data-lineage-card]:not([hidden])").GetAttributeAsync("data-active-object-view"));
        await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page);
        Assert.True(await second.Locator(".model-inspect").EvaluateAsync<bool>("e => e.open"));
        await Section(second, "Used in the report").Locator("li a").First.PressAsync("Enter"); await Settle(page);
        Assert.Equal("summary", await page.Locator("[data-report-context]:not([hidden])").GetAttributeAsync("data-active-context-view"));
    }

    [Fact]
    public async Task TwoObjectsSummaryDefinitionAndLineageRestoreExistingModelState()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 720 }, ReducedMotion = ReducedMotion.Reduce });
        var page = await Open(context, "history");
        await page.Locator("#usage-search").FillAsync("Measure");
        await page.Locator("#semantic-usage summary").Filter(new() { HasText = "More filters" }).ClickAsync();
        await page.Locator("#usage-table").SelectOptionAsync("Fact");
        await page.Locator("#usage-object-type").SelectOptionAsync("Measure");
        var first = Row(page, "IndirectlyUsedMeasure"); var second = Row(page, "DirectlyUsedMeasure");
        await first.Locator(".model-inspect > summary").PressAsync("Enter");
        await second.Locator(".model-inspect > summary").PressAsync("Enter");
        var source = second.Locator(".object-name a"); await source.ScrollIntoViewIfNeededAsync(); await source.FocusAsync(); await Settle(page);
        var focus = await page.EvaluateAsync<string>("document.activeElement.outerHTML"); var scroll = await page.EvaluateAsync<double>("scrollY");
        await source.PressAsync("Enter"); await Settle(page); var summaryRoute = page.Url;
        var card = page.Locator("[data-lineage-card]:not([hidden])");
        await card.GetByRole(AriaRole.Link, new() { Name = "Definition", Exact = true }).PressAsync("Enter"); await Settle(page);
        await card.GetByRole(AriaRole.Link, new() { Name = "Lineage", Exact = true }).PressAsync("Enter"); await Settle(page);
        await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page); await AssertRestored();
        await page.GoForwardAsync(); await Settle(page); Assert.Equal(summaryRoute, page.Url);
        await page.GoBackAsync(); await Settle(page); await AssertRestored();

        async Task AssertRestored()
        {
            Assert.EndsWith("#semantic-usage", page.Url, StringComparison.Ordinal);
            Assert.Equal("Measure", await page.Locator("#usage-search").InputValueAsync());
            Assert.Equal("Fact", await page.Locator("#usage-table").InputValueAsync());
            Assert.Equal("Measure", await page.Locator("#usage-object-type").InputValueAsync());
            Assert.Equal("developer", await page.Locator("#usage-origin").InputValueAsync());
            Assert.True(await first.Locator(".model-inspect").EvaluateAsync<bool>("e => e.open"));
            Assert.True(await second.Locator(".model-inspect").EvaluateAsync<bool>("e => e.open"));
            Assert.Equal(focus, await page.EvaluateAsync<string>("document.activeElement.outerHTML"));
            Assert.InRange(Math.Abs(await page.EvaluateAsync<double>("scrollY") - scroll), 0, 3);
        }
    }

    [Theory]
    [InlineData(1280, 900, ColorScheme.Light, ForcedColors.None)]
    [InlineData(1280, 900, ColorScheme.Dark, ForcedColors.None)]
    [InlineData(320, 900, ColorScheme.Light, ForcedColors.None)]
    [InlineData(320, 225, ColorScheme.Dark, ForcedColors.None)]
    [InlineData(320, 900, ColorScheme.Light, ForcedColors.Active)]
    public async Task LongNamesLocationsAndDaxReflow(int width, int height, ColorScheme colors, ForcedColors forced)
    {
        var inventory = Coverage(); var longName = "Long identifier " + new string('x', 150);
        inventory = inventory with { SemanticModels = inventory.SemanticModels.Select(model => model with { Tables = model.Tables.Select(table => table with {
            Measures = table.Measures.Select(measure => measure.Name == "DirectlyUsedMeasure" ? measure with { Expression = new string('x', 700) + " FULL ONLY" } : measure).ToArray()
        }).ToArray() }).ToArray(), Reports = inventory.Reports.Select(report => report with { Pages = report.Pages.Select(savedPage => savedPage with {
            DisplayName = savedPage.Name == "main-page" ? longName : savedPage.DisplayName,
            Visuals = savedPage.Visuals.Select(visual => visual.Name == "projection-visual" ? visual with {
                Accessibility = visual.Accessibility with { TitleText = longName, TitleIsVisible = true, TitleTextIsDynamic = false }
            } : visual).ToArray() }).ToArray() }).ToArray() };
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height }, ColorScheme = colors, ForcedColors = forced });
        // Vary generated visible text only; canonical object identities remain intact.
        var page = await Open(context, $"reflow-{width}-{height}-{colors}-{forced}", inventory, longName);
        await page.Locator("#usage-search").FillAsync("DirectlyUsedMeasure");
        var row = Row(page, longName);
        var inspect = row.Locator(".model-inspect"); await inspect.Locator("summary").PressAsync("Enter"); await Settle(page);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        Assert.All(await inspect.Locator("summary, li, pre, a").EvaluateAllAsync<double[]>("elements => elements.map(e => e.getBoundingClientRect().right)"), right => Assert.True(right <= width));
        Assert.Equal(600, (await inspect.Locator("pre code").InnerTextAsync()).Length);
        Assert.Contains("Preview — expression shortened.", await inspect.InnerTextAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("FULL ONLY", await inspect.InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains(longName, await inspect.InnerTextAsync(), StringComparison.Ordinal);
        if (forced == ForcedColors.Active)
            Assert.All(await inspect.Locator("li").EvaluateAllAsync<string[]>("elements => elements.map(e => getComputedStyle(e).borderTopStyle)"), border => Assert.Equal("solid", border));
        await row.Locator(".object-name a").FocusAsync(); await Settle(page);
        Assert.True(await page.EvaluateAsync<bool>("document.activeElement.matches(':focus-visible')"));
        await page.ScreenshotAsync(new() { Path = Path.Combine(Output, $"model-{width}-{height}-{colors}-{forced}.png") });
    }
}

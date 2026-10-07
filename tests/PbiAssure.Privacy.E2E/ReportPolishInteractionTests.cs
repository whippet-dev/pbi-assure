using Microsoft.Playwright;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class ReportPolishInteractionTests(PrivacyE2EFixture fixture) : IAsyncLifetime
{
    private readonly List<string> errors = [];
    private string DirectoryPath => Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-polish");
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() { Assert.Empty(errors); return Task.CompletedTask; }
    private static Task<System.Text.Json.JsonElement?> Settle(IPage page) => page.EvaluateAsync(
        "() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    private async Task<string> SaveReport(bool longName = false)
    {
        Directory.CreateDirectory(DirectoryPath);
        var html = HtmlReportRenderer.Render(ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "pbi-assure-coverage")));
        if (longName) html = html.Replace("Fact[IndirectlyUsedColumn]", "Fact[A long object identifier with meaningful spaces and an_uninterrupted_suffix_" + new string('x', 60) + "]", StringComparison.Ordinal);
        var path = Path.Combine(DirectoryPath, longName ? "report-long.html" : "report-coverage.html");
        await File.WriteAllTextAsync(path, html);
        return new Uri(path).AbsoluteUri;
    }
    private async Task<IPage> Open(IBrowserContext context, string url, string fragment)
    {
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await page.GotoAsync(url + "#" + fragment);
        await Settle(page);
        return page;
    }
    private static ILocator Entity(IPage page) => page.Locator("[data-report-context]:not([hidden])");
    private static ILocator Object(IPage page) => page.Locator("[data-lineage-card]:not([hidden])");

    [Fact]
    public async Task BrokenActionsSeparatesPageSummaryFromItsVisualFindings()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context, await SaveReport(), "reports");
        var collection = page.Locator(".page-card").Filter(new() { HasText = "Broken actions" });
        Assert.Contains("4 findings", await collection.InnerTextAsync(), StringComparison.Ordinal);
        await collection.GetByRole(AriaRole.Link, new() { Name = "Broken actions", Exact = true }).First.PressAsync("Enter"); await Settle(page);
        Assert.Contains("No findings or accessibility observations for the page itself.", await Entity(page).InnerTextAsync(), StringComparison.Ordinal);
        await page.ScreenshotAsync(new() { Path = Path.Combine(DirectoryPath, "page-review-scope.png") });
        await Entity(page).GetByRole(AriaRole.Link, new() { Name = "Visuals", Exact = true }).PressAsync("Enter");
        await Entity(page).Locator("[data-context-view='visuals'] a").PressAsync("Enter");
        await Entity(page).Locator(".object-view-nav").GetByRole(AriaRole.Link, new() { Name = "Reviews", Exact = true }).PressAsync("Enter"); await Settle(page);
        Assert.Equal(4, await Entity(page).Locator("[data-context-view='reviews'] a[href^='#finding-']").CountAsync());
        Assert.Equal("reviews", await Entity(page).GetAttributeAsync("data-active-context-view"));
        await page.ScreenshotAsync(new() { Path = Path.Combine(DirectoryPath, "visual-local-reviews.png") });
    }

    [Theory]
    [InlineData("Report")]
    [InlineData("Page")]
    public async Task GlobalReviewLinksPreserveBookmarkSearchAndSeverityWhileLocalVisualReviewsStayLocal(string kind)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var url = await SaveReport(); var page = await Open(context, url, "findings");
        await page.Locator("#finding-search").FillAsync("bookmark");
        await page.Locator("#findings summary").Filter(new() { HasText = "More filters" }).ClickAsync();
        var severity = page.Locator("#finding-severity");
        await severity.SelectOptionAsync("Warning");
        var visibleIds = await page.Locator(".finding-card:not([hidden])").EvaluateAllAsync<string[]>("elements => elements.map(e => e.id)");
        await page.Locator(".section-nav a[href='#reports']").ClickAsync();
        var route = await page.Locator($"[data-report-context='{kind}'] [data-context-view-link='summary']").First.GetAttributeAsync("href");
        // Follow a real collection/context link without loading another document.
        await page.Locator($"a[href='{route}']").First.ClickAsync(); await Settle(page);
        var global = Entity(page).GetByRole(AriaRole.Link, new() { Name = "Open all findings", Exact = true });
        Assert.Equal("#findings", await global.GetAttributeAsync("href"));
        Assert.Equal("#theme-review", await Entity(page).GetByRole(AriaRole.Link, new() { Name = "Open theme review", Exact = true }).GetAttributeAsync("href"));
        await global.PressAsync("Enter"); await Settle(page);
        Assert.Equal("bookmark", await page.Locator("#finding-search").InputValueAsync());
        Assert.Equal("Warning", await severity.InputValueAsync());
        Assert.Equal(visibleIds, await page.Locator(".finding-card:not([hidden])").EvaluateAllAsync<string[]>("elements => elements.map(e => e.id)"));
        Assert.Equal("reviews", await page.Locator("main").GetAttributeAsync("data-active-area"));
        await page.GoBackAsync(); await Settle(page);
        Assert.Equal(kind, await Entity(page).GetAttributeAsync("data-report-context"));
        await page.GoForwardAsync(); await Settle(page);
        Assert.Equal("bookmark", await page.Locator("#finding-search").InputValueAsync());
        Assert.Equal("Warning", await severity.InputValueAsync());
        var visualRoute = await page.Locator("[data-report-context='Visual'] [data-context-view-link='reviews']").First.GetAttributeAsync("href");
        var visualPage = await Open(context, url, visualRoute![1..]);
        Assert.Equal("reviews", await Entity(visualPage).GetAttributeAsync("data-active-context-view"));
        Assert.True(await Entity(visualPage).GetByRole(AriaRole.Heading, new() { Name = "Findings", Exact = true }).IsVisibleAsync());
        Assert.Equal(0, await Entity(visualPage).GetByRole(AriaRole.Link, new() { Name = "Open all findings", Exact = true }).CountAsync());
        // Visual Reviews stays a local list; its theme links explicitly open the global collection.
        var themeRoute = await page.Locator("[data-report-context='Visual']:has(a[href='#theme-deviations-heading']) [data-context-view-link='reviews']").First.GetAttributeAsync("href");
        var themePage = await Open(context, url, themeRoute![1..]);
        var themeLink = Entity(themePage).Locator("a[href='#theme-deviations-heading']").First;
        Assert.EndsWith("Open theme review", await themeLink.InnerTextAsync(), StringComparison.Ordinal);
        await themeLink.PressAsync("Enter"); await Settle(themePage);
        Assert.Equal("theme-review", await themePage.Locator("main").GetAttributeAsync("data-active-section"));
    }

    [Theory]
    [InlineData(1280, 900, false)]
    [InlineData(1280, 900, true)]
    [InlineData(320, 900, false)]
    [InlineData(320, 900, true)]
    // CSS viewport produced by a 1280 x 900 desktop at 400% browser zoom.
    [InlineData(320, 225, false)]
    [InlineData(320, 225, true)]
    public async Task StickyReturnClearsFocusedObjectAndVisualHeadingsAtDesktopNarrowAndZoomReflow(int width, int height, bool longName)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height }, ReducedMotion = ReducedMotion.Reduce });
        var page = await Open(context, await SaveReport(longName), "semantic-usage");
        await page.Locator("#usage-search").FillAsync("IndirectlyUsedColumn");
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand all tables", Exact = true }).ClickAsync();
        await page.Locator(".semantic-object:not([hidden]) .object-name a").PressAsync("Enter"); await Settle(page);
        Assert.Equal("Summary", await page.EvaluateAsync<string>("document.activeElement.textContent"));
        await AssertFocusClear(page);
        await Object(page).Locator(".object-view-nav").GetByRole(AriaRole.Link, new() { Name = "Lineage", Exact = true }).PressAsync("Enter"); await Settle(page);
        Assert.Equal(await Object(page).Locator("h2").InnerTextAsync(), await page.EvaluateAsync<string>("document.activeElement.textContent"));
        await AssertFocusClear(page);
        await page.ScreenshotAsync(new() { Path = Path.Combine(DirectoryPath, $"lineage-{width}-{height}-{(longName ? "long" : "short")}.png") });
        await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page);
        Assert.EndsWith("#semantic-usage", page.Url, StringComparison.Ordinal);
        Assert.Equal("IndirectlyUsedColumn", await page.Locator("#usage-search").InputValueAsync());
        if (await page.Locator("#report-navigation-toggle").IsVisibleAsync())
            await page.Locator("#report-navigation-toggle").ClickAsync();
        await page.Locator(".section-nav a[href='#reports']").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand all pages", Exact = true }).ClickAsync();
        await page.Locator(".page-card:not([hidden]) a[href^='#visual-']").First.PressAsync("Enter"); await Settle(page);
        Assert.Equal("Visual", await Entity(page).GetAttributeAsync("data-report-context"));
        await AssertFocusClear(page);
    }

    private static async Task AssertFocusClear(IPage page)
    {
        Assert.True(await page.Locator("#investigation-return").IsVisibleAsync());
        var geometry = await page.EvaluateAsync<double[]>("""
            () => {
              const focus = document.activeElement.getBoundingClientRect();
              const back = document.getElementById('investigation-return-row').getBoundingClientRect();
              return [focus.top, focus.bottom, back.top, back.bottom, innerHeight, document.documentElement.scrollWidth, innerWidth];
            }
            """);
        Assert.True(geometry[0] >= geometry[3] + 2, $"Focused heading starts at {geometry[0]}, sticky Return ends at {geometry[3]}.");
        Assert.True(geometry[0] < geometry[4], $"Focused heading is below viewport: {geometry[0]} >= {geometry[4]}.");
        if (geometry[1] - geometry[0] <= geometry[4] - geometry[3] - 8)
            Assert.True(geometry[1] <= geometry[4] + 1, "A heading that fits must be fully in view.");
        Assert.True(geometry[2] >= 0 && geometry[3] <= geometry[4], "Return is outside viewport.");
        Assert.True(geometry[5] <= geometry[6], "Horizontal overflow.");
        Assert.True(await page.EvaluateAsync<bool>("document.activeElement.matches(':focus-visible')"));
    }

    [Theory]
    [InlineData("[data-lineage-card] [data-object-view='summary']", "semantic-usage", "summary", "Summary")]
    [InlineData("[data-lineage-card='semantic']", "semantic-usage", "lineage", "")]
    [InlineData("[data-report-context='Visual'] [data-context-view='summary']", "reports", "summary", "Summary")]
    [InlineData("#finding-1", "reviews", "findings", "")]
    [InlineData("#analysis-coverage", "technical", "analysis-coverage", "")]
    public async Task FreshStandaloneFileLinksRevealTheDestinationParentFocusAndNoInventedReturn(string selector, string area, string view, string focus)
    {
        await using var context = await fixture.Browser.NewContextAsync(); var url = await SaveReport();
        var discover = await Open(context, url, "summary");
        var id = await discover.Locator(selector).First.GetAttributeAsync("id");
        var page = await Open(context, url, id!);
        Assert.StartsWith("file://", page.Url, StringComparison.Ordinal);
        Assert.True(await page.Locator("#" + id).IsVisibleAsync());
        Assert.Equal(area, await page.Locator("main").GetAttributeAsync("data-active-area"));
        Assert.Equal(area, await page.Locator(".section-navigator [aria-current='page']").GetAttributeAsync("data-section-target"));
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
        if (area == "semantic-usage") Assert.Equal(view, await Object(page).GetAttributeAsync("data-active-object-view"));
        if (area == "reports") Assert.Equal(view, await Entity(page).GetAttributeAsync("data-active-context-view"));
        if (area is "reviews" or "technical") Assert.Equal(view, await page.Locator("main").GetAttributeAsync("data-active-section"));
        if (focus.Length > 0) Assert.Equal(focus, await page.EvaluateAsync<string>("document.activeElement.textContent"));
        else if (view == "lineage") Assert.Equal(await Object(page).Locator("h2").InnerTextAsync(), await page.EvaluateAsync<string>("document.activeElement.textContent"));
        else if (view == "findings") Assert.True(await page.Locator("#" + id + " > summary").EvaluateAsync<bool>("element => element === document.activeElement"));
        else Assert.Equal(id, await page.EvaluateAsync<string>("document.activeElement.id"));
        var box = await page.EvaluateAsync<double[]>("() => { const r = document.activeElement.getBoundingClientRect(); return [r.top, r.bottom, innerHeight]; }");
        Assert.True(box[0] >= 0 && box[0] < box[2]);
        Assert.Empty(errors);
    }
}

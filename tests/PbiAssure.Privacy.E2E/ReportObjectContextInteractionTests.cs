using Microsoft.Playwright;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class ReportObjectContextInteractionTests(PrivacyE2EFixture fixture) : IAsyncLifetime
{
    private readonly List<string> errors = [];
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() { Assert.Empty(errors); return Task.CompletedTask; }
    private async Task<IPage> Open(IBrowserContext context, string fragment = "", string? html = null)
    {
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        html ??= HtmlReportRenderer.Render(ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "pbi-assure-coverage")));
        await page.RouteAsync("http://report.test/object.html", route => route.FulfillAsync(new() { ContentType = "text/html", Body = html }));
        await page.GotoAsync("http://report.test/object.html" + fragment);
        await Settle(page);
        return page;
    }
    private static async Task Settle(IPage page) => await page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    private static ILocator Context(IPage page) => page.Locator("[data-lineage-card]:not([hidden])");
    private static ILocator LocalLink(IPage page, string name) => Context(page).Locator(".object-view-nav").GetByRole(AriaRole.Link, new() { Name = name, Exact = true });

    [Theory]
    [InlineData("sum", "summary", "Summary")]
    [InlineData("lin", "lineage", "Fact[IndirectlyUsedColumn]")]
    [InlineData("def", "definition", "Definition")]
    [InlineData("obj", "summary", "Summary")]
    public async Task FreshLinksSelectTheLocalViewFocusItAndNeverInventReturn(string prefix, string view, string focus)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context, $"#{prefix}-fact-indirectlyusedcolumn-122f1f2a724a");
        Assert.Equal(view, await Context(page).GetAttributeAsync("data-active-object-view"));
        Assert.Equal(focus, await page.EvaluateAsync<string>("document.activeElement.textContent"));
        Assert.Equal(view, await Context(page).Locator(".object-view-nav [aria-current='page']").GetAttributeAsync("data-object-view-link"));
        Assert.Equal("page", await page.Locator(".section-nav a[href='#semantic-usage']").GetAttributeAsync("aria-current"));
        foreach (var label in new[] { "Definition", "Lineage", "Summary" })
        {
            await LocalLink(page, label).ClickAsync();
            Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
            Assert.Single(await page.Locator("[data-lineage-card]:not([hidden]) h2").AllAsync());
            Assert.Equal(1, await Context(page).Locator("[data-object-view]:not([hidden])").CountAsync());
        }
    }

    [Fact]
    public async Task LocalViewsAndNeighbourHistoryKeepIdentityAndTheRootCollectionOrigin()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context, "#semantic-usage");
        await page.Locator("#usage-search").FillAsync("IndirectlyUsedColumn");
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand all tables", Exact = true }).ClickAsync();
        var source = page.Locator(".semantic-object:not([hidden]) .object-name a");
        await source.PressAsync("Enter");
        var summary = page.Url;
        var title = await Context(page).Locator("h2").InnerTextAsync();
        var screenshotDirectory = Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-slice3");
        Directory.CreateDirectory(screenshotDirectory);
        await page.ScreenshotAsync(new() { Path = Path.Combine(screenshotDirectory, "object-summary.png") });
        await LocalLink(page, "Lineage").ClickAsync();
        var lineage = page.Url;
        await LocalLink(page, "Definition").ClickAsync();
        Assert.Equal(title, await Context(page).Locator("h2").InnerTextAsync());
        await page.GoBackAsync(); await Settle(page);
        Assert.Equal(lineage, page.Url);
        await Context(page).GetByRole(AriaRole.Link, new() { Name = "Fact[DirectlyUsedMeasure]", Exact = true }).First.ClickAsync();
        await LocalLink(page, "Summary").ClickAsync();
        await page.GoBackAsync(); await Settle(page);
        Assert.Equal("lineage", await Context(page).GetAttributeAsync("data-active-object-view"));
        await page.GoBackAsync(); await Settle(page);
        Assert.Equal(lineage, page.Url);
        await page.GoBackAsync(); await Settle(page);
        Assert.Equal(summary, page.Url);
        await page.GoForwardAsync(); await Settle(page);
        Assert.Equal(lineage, page.Url);
        await page.Locator("#investigation-return").ClickAsync(); await Settle(page);
        Assert.EndsWith("#semantic-usage", page.Url, StringComparison.Ordinal);
        Assert.Equal("IndirectlyUsedColumn", await page.Locator("#usage-search").InputValueAsync());
        Assert.Contains("#sum-", await page.EvaluateAsync<string>("document.activeElement.getAttribute('href')"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("DirectlyUsed")]
    [InlineData("IndirectlyUsed")]
    [InlineData("StructurallyRequired")]
    [InlineData("UsedOnlyByUnusedBranch")]
    [InlineData("ApparentlyUnused")]
    public async Task EachStateOpensSummaryAndHasWorkingDefinitionAndDetails(string state)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context);
        var row = page.Locator($".semantic-object[data-usage-state='{state}']").First;
        var route = await row.GetAttributeAsync("data-object-summary");
        await page.GotoAsync("http://report.test/object.html#" + route); await Settle(page);
        Assert.Equal("summary", await Context(page).GetAttributeAsync("data-active-object-view"));
        Assert.True(await Context(page).Locator(".object-context-header .badge").IsVisibleAsync());
        await Context(page).Locator(".object-context-details > summary").PressAsync("Enter");
        Assert.True(await Context(page).GetByRole(AriaRole.Heading, new() { Name = "Evidence and provenance", Exact = true }).IsVisibleAsync());
        if (state == "ApparentlyUnused") Assert.Contains("Check before removing it", await Context(page).InnerTextAsync(), StringComparison.Ordinal);
        await LocalLink(page, "Definition").PressAsync("Enter");
        Assert.True(await Context(page).Locator("[data-object-view='definition']").IsVisibleAsync());
    }

    [Theory]
    [InlineData("function")]
    [InlineData("report-measure")]
    public async Task SpecialObjectsKeepReachabilityAndDefinitionsWithoutUsageBadges(string kind)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context);
        var id = await page.Locator($"[data-lineage-card='{kind}']").First.GetAttributeAsync("id");
        await page.GotoAsync("http://report.test/object.html#def-" + id![4..]); await Settle(page);
        Assert.Equal(0, await Context(page).Locator(".object-context-header .badge").CountAsync());
        Assert.Contains("reached from a report", (await Context(page).Locator(".object-context-header").InnerTextAsync()).ToLowerInvariant(), StringComparison.Ordinal);
        Assert.True(await Context(page).Locator("[data-object-view='definition'] pre code").IsVisibleAsync());
        Assert.Equal("page", await page.Locator(".section-nav a[href='#semantic-usage']").GetAttributeAsync("aria-current"));
    }

    [Theory]
    [InlineData(1280, "light")]
    [InlineData(1280, "dark")]
    [InlineData(320, "light")]
    [InlineData(320, "dark")]
    public async Task LongIdentityReflowsAndPrintKeepsOnlyTheActiveObjectView(int width, string theme)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = 900 }, ReducedMotion = ReducedMotion.Reduce });
        var html = HtmlReportRenderer.Render(ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "pbi-assure-coverage")));
        // Presentation-only stress input; Desktop-authored fixtures remain unchanged.
        var longName = "Fact[A deliberately long object identifier with meaningful spaces and an uninterrupted_suffix_" + new string('x', 100) + "]";
        html = html.Replace("Fact[IndirectlyUsedColumn]", longName, StringComparison.Ordinal);
        var page = await Open(context, "#sum-fact-indirectlyusedcolumn-122f1f2a724a", html);
        await page.GetByRole(AriaRole.Button, new() { Name = theme == "dark" ? "Dark appearance" : "Light appearance", Exact = true }).ClickAsync();
        Assert.Equal(longName, await Context(page).Locator("h2").InnerTextAsync());
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        var directory = Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-slice3");
        Directory.CreateDirectory(directory);
        await page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"object-{width}-{theme}.png"), FullPage = true });
        await LocalLink(page, "Lineage").ClickAsync();
        await page.EmulateMediaAsync(new() { Media = Media.Print });
        Assert.True(await Context(page).Locator("h2").IsVisibleAsync());
        Assert.True(await Context(page).Locator("[data-object-view='lineage']").IsVisibleAsync());
        Assert.False(await Context(page).Locator("[data-object-view='summary']").IsVisibleAsync());
        Assert.False(await Context(page).Locator("[data-object-view='definition']").IsVisibleAsync());
        Assert.False(await page.Locator("#semantic-usage").IsVisibleAsync());
        await page.EmulateMediaAsync(new() { Media = Media.Screen, ForcedColors = ForcedColors.Active });
        Assert.True(await LocalLink(page, "Definition").IsVisibleAsync());
    }
}

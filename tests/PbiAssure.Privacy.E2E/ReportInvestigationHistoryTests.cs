using Microsoft.Playwright;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class ReportInvestigationHistoryTests(PrivacyE2EFixture fixture) : IAsyncLifetime
{
    private readonly List<string> scriptErrors = [];
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync()
    {
        Assert.Empty(scriptErrors);
        return Task.CompletedTask;
    }
    private string Render() => HtmlReportRenderer.Render(ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "pbi-assure-coverage")));

    private async Task<IPage> OpenAsync(IBrowserContext context, string fragment = "")
    {
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => scriptErrors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") scriptErrors.Add(message.Text); };
        var html = Render();
        await page.RouteAsync("http://report.test/report.html", route => route.FulfillAsync(new() { ContentType = "text/html", Body = html }));
        await page.GotoAsync("http://report.test/report.html" + fragment);
        await SettleAsync(page);
        return page;
    }

    private static async Task SettleAsync(IPage page) => await page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    private static ILocator Card(IPage page) => page.Locator("[data-lineage-card]:not([hidden]), [data-report-context]:not([hidden])");
    private static async Task<string[]> ExpansionsAsync(IPage page) => await page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('main details')].map((detail, index) => `${index}:${detail.open}`)");
    private static async Task<string> FocusAsync(IPage page) => await page.EvaluateAsync<string>("document.activeElement.outerHTML");
    private static Task GoModelAsync(IPage page) => page.Locator(".section-nav a[href='#semantic-usage']").ClickAsync();

    private static async Task<ILocator> FilteredModelAsync(IPage page)
    {
        await GoModelAsync(page);
        await page.Locator("#usage-search").FillAsync("IndirectlyUsedColumn");
        await page.Locator("#semantic-usage summary").Filter(new() { HasText = "More filters" }).ClickAsync();
        await page.Locator("#usage-usage-state").SelectOptionAsync("IndirectlyUsed");
        await page.Locator("#usage-table").SelectOptionAsync("Fact");
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand all tables", Exact = true }).ClickAsync();
        var link = page.Locator(".semantic-object:not([hidden]) a[href^='#sum-']");
        await link.ScrollIntoViewIfNeededAsync();
        await link.FocusAsync();
        await SettleAsync(page);
        return link;
    }

    private static async Task AssertModelFiltersAsync(IPage page)
    {
        Assert.Equal("IndirectlyUsedColumn", await page.Locator("#usage-search").InputValueAsync());
        Assert.Equal("IndirectlyUsed", await page.Locator("#usage-usage-state").InputValueAsync());
        Assert.Equal("developer", await page.Locator("#usage-origin").InputValueAsync());
        Assert.Equal("Fact", await page.Locator("#usage-table").InputValueAsync());
    }

    [Fact]
    public async Task ModelConsumerDetailsReturnRestoresSearchFacetsExpansionsScrollAndInitiatingLink()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 720 } });
        var page = await OpenAsync(context);
        var source = await FilteredModelAsync(page);
        var focus = await FocusAsync(page);
        var expanded = await ExpansionsAsync(page);
        var scroll = await page.EvaluateAsync<double>("scrollY");
        var rowId = await source.EvaluateAsync<string>("link => link.closest('.semantic-object').id");
        await source.PressAsync("Enter");
        await Card(page).GetByRole(AriaRole.Link, new() { Name = "Lineage", Exact = true }).PressAsync("Enter");
        await Card(page).GetByRole(AriaRole.Link, new() { Name = "Fact[DirectlyUsedMeasure]", Exact = true }).First.PressAsync("Enter");
        await Card(page).GetByRole(AriaRole.Link, new() { Name = "Definition", Exact = true }).PressAsync("Enter");
        await SettleAsync(page);
        await AssertModelFiltersAsync(page);
        Assert.False(await page.Locator("#investigation-selection-note").IsVisibleAsync());
        Assert.Equal(1, await page.Locator(".semantic-object:not([hidden])").CountAsync()); // Context navigation does not reveal unrelated collection rows.
        Assert.Contains("search “IndirectlyUsedColumn”", await page.Locator("#investigation-return").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(rowId, await page.EvaluateAsync<string>("history.state.reportInvestigation.origin.view.initiatingId"));
        await page.Locator("#investigation-return").PressAsync("Enter");
        await page.WaitForURLAsync("**#semantic-usage");
        await SettleAsync(page);
        await AssertModelFiltersAsync(page);
        Assert.Equal(expanded, await ExpansionsAsync(page));
        Assert.Equal(focus, await FocusAsync(page));
        Assert.InRange(Math.Abs(await page.EvaluateAsync<double>("scrollY") - scroll), 0, 3);
        Assert.Equal(1, await page.Locator(".semantic-object:not([hidden])").CountAsync());
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
        var directory = Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-slice3");
        Directory.CreateDirectory(directory);
        await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "model-return-restored.png") });
    }

    [Fact]
    public async Task ModelLineagesVisualAndDetailsRetainOriginAcrossBackForwardAndReturn()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenAsync(context);
        var source = await FilteredModelAsync(page);
        await source.ClickAsync();
        await Card(page).GetByRole(AriaRole.Link, new() { Name = "Lineage", Exact = true }).ClickAsync();
        var lineageA = page.Url;
        await Card(page).GetByRole(AriaRole.Link, new() { Name = "Fact[DirectlyUsedMeasure]", Exact = true }).First.ClickAsync();
        var lineageB = page.Url;
        await Card(page).Locator("[data-object-view='lineage'] a[href^='#visual-']").First.ClickAsync();
        var visual = page.Url;
        Assert.Equal("page", await page.Locator(".section-nav a[href='#reports']").GetAttributeAsync("aria-current"));
        await Card(page).GetByRole(AriaRole.Link, new() { Name = "Reviews", Exact = true }).ClickAsync();
        var details = page.Url;
        foreach (var url in new[] { visual, lineageB, lineageA })
        {
            await page.GoBackAsync();
            await SettleAsync(page);
            Assert.Equal(url, page.Url);
            Assert.True(await page.Locator("[data-lineage-card]:not([hidden]), [data-report-context]:not([hidden])").IsVisibleAsync());
            await AssertModelFiltersAsync(page);
        }
        Assert.Equal("page", await page.Locator(".section-nav a[href='#semantic-usage']").GetAttributeAsync("aria-current"));
        foreach (var url in new[] { lineageB, visual, details })
        {
            await page.GoForwardAsync();
            await SettleAsync(page);
            Assert.Equal(url, page.Url);
            await AssertModelFiltersAsync(page);
        }
        await page.Locator("#investigation-return").ClickAsync();
        await page.WaitForURLAsync("**#semantic-usage");
        await SettleAsync(page);
        await AssertModelFiltersAsync(page);
        Assert.Contains("href=\"#sum-", await FocusAsync(page), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("developer")]
    [InlineData("system")]
    public async Task BothAuthoredAndSystemScopeSurviveTheRoundTrip(string origin)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenAsync(context);
        await GoModelAsync(page);
        await page.Locator("#semantic-usage summary").Filter(new() { HasText = "More filters" }).ClickAsync();
        await page.Locator("#usage-origin").SelectOptionAsync(origin);
        var row = page.Locator($".semantic-object[data-object-origin='{origin}']").First;
        var name = await row.Locator(".object-name strong").InnerTextAsync();
        await page.Locator("#usage-search").FillAsync(name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand all tables", Exact = true }).ClickAsync();
        await row.Locator("a[href^='#sum-']").ClickAsync();
        await Card(page).GetByRole(AriaRole.Link, new() { Name = "Definition", Exact = true }).ClickAsync();
        await page.Locator("#investigation-return").ClickAsync();
        await page.WaitForURLAsync("**#semantic-usage");
        await SettleAsync(page);
        Assert.Equal(origin, await page.Locator("#usage-origin").InputValueAsync());
        Assert.Equal(name, await page.Locator("#usage-search").InputValueAsync());
        Assert.True(await row.IsVisibleAsync());
    }

    [Fact]
    public async Task FindingToAffectedVisualReturnPreservesFindingSearchFacetsAndExpansion()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenAsync(context);
        await page.Locator(".section-nav a[href='#findings']").ClickAsync();
        await page.Locator("#finding-search").FillAsync("MissingLocalObject");
        await page.Locator("#findings summary").Filter(new() { HasText = "More filters" }).ClickAsync();
        await page.Locator("#finding-severity").SelectOptionAsync("Error");
        var finding = page.Locator(".finding-card:not([hidden])");
        await finding.Locator("summary").First.ClickAsync();
        var source = finding.Locator("a[href^='#visual-']").First;
        await source.FocusAsync();
        var expanded = await ExpansionsAsync(page);
        await source.PressAsync("Enter");
        Assert.True(await page.Locator("#investigation-return").IsVisibleAsync());
        await page.Locator("#investigation-return").PressAsync("Enter");
        await page.WaitForURLAsync("**#findings");
        await SettleAsync(page);
        Assert.Equal("MissingLocalObject", await page.Locator("#finding-search").InputValueAsync());
        Assert.Equal("Error", await page.Locator("#finding-severity").InputValueAsync());
        Assert.Equal(expanded, await ExpansionsAsync(page));
        Assert.Contains("href=\"#visual-", await FocusAsync(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsOriginSurvivesOutOfFilterVisualReveal()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenAsync(context);
        await page.Locator(".section-nav a[href='#reports']").ClickAsync();
        await page.Locator("#page-search").FillAsync("Core coverage");
        await page.Locator("#reports summary").Filter(new() { HasText = "More filters" }).ClickAsync();
        await page.Locator("#page-visibility").SelectOptionAsync("Visible");
        var source = page.Locator(".page-card:not([hidden])").Filter(new() { HasText = "Core coverage" }).First;
        await source.Locator("summary").First.ClickAsync();
        var link = source.Locator("a[href^='#visual-']").First;
        var initiatingLink = await link.EvaluateAsync<string>("element => element.outerHTML");
        await link.ClickAsync();
        var otherVisual = page.Locator("[data-report-context='Visual'][hidden] a[data-context-view-link='summary']").Last;
        var destination = await otherVisual.GetAttributeAsync("href");
        // An external fragment change is still an in-report route, without clearing page filters.
        await page.EvaluateAsync("fragment => { location.hash = fragment; }", destination);
        await SettleAsync(page);
        Assert.Equal("Core coverage", await page.Locator("#page-search").InputValueAsync());
        Assert.Equal("Visible", await page.Locator("#page-visibility").InputValueAsync());
        Assert.True(await page.Locator(destination!).IsVisibleAsync());
        await page.Locator("#investigation-return").ClickAsync();
        await page.WaitForURLAsync("**#reports");
        await SettleAsync(page);
        Assert.Equal("Core coverage", await page.Locator("#page-search").InputValueAsync());
        Assert.Equal(initiatingLink, await FocusAsync(page));
    }

    [Theory]
    [InlineData("semantic")]
    [InlineData("visual")]
    public async Task FreshLineageDeepLinksHaveNoReturnAndFocusTheirHeadingWithParentCurrent(string kind)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenAsync(context);
        var id = await page.Locator(kind == "visual" ? "[data-report-context='Visual']" : "[data-lineage-card='semantic']").First.GetAttributeAsync("id");
        var direct = await OpenAsync(context, "#" + id);
        Assert.True(await Card(direct).IsVisibleAsync());
        Assert.Equal(id + "-title", await direct.EvaluateAsync<string>("document.activeElement.id"));
        Assert.False(await direct.Locator("#investigation-return-row").IsVisibleAsync());
        var parent = kind == "visual" ? "reports" : "semantic-usage";
        Assert.Equal("page", await direct.Locator($".section-nav a[href='#{parent}']").GetAttributeAsync("aria-current"));
        Assert.True(await Card(direct).GetByRole(AriaRole.Link, new() { Name = "Summary", Exact = true }).IsVisibleAsync());
    }

    [Fact]
    public async Task ExplicitCollectionNavigationUsesFiltersEditedWhileViewingASelectedException()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenAsync(context);
        var source = await FilteredModelAsync(page);
        await source.ClickAsync();
        await Card(page).GetByRole(AriaRole.Link, new() { Name = "Fact[DirectlyUsedMeasure]", Exact = true }).First.ClickAsync();
        await Card(page).GetByRole(AriaRole.Link, new() { Name = "Definition", Exact = true }).ClickAsync();
        await GoModelAsync(page);
        await page.Locator("#usage-search").FillAsync("DirectlyUsedMeasure");
        await page.Locator("#usage-usage-state").SelectOptionAsync("DirectlyUsed");
        await GoModelAsync(page);
        await SettleAsync(page);
        Assert.Equal(1, await page.Locator(".semantic-object:not([hidden])").CountAsync());
        Assert.True(await page.Locator(".semantic-object:not([hidden])").IsVisibleAsync());
        Assert.Contains("DirectlyUsedMeasure", await page.Locator(".semantic-object:not([hidden])").InnerTextAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReloadIsAFreshContextAndEarlierSessionEntriesDoNotFabricateReturn()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenAsync(context);
        var source = await FilteredModelAsync(page);
        await source.ClickAsync();
        await Card(page).GetByRole(AriaRole.Link, new() { Name = "Fact[DirectlyUsedMeasure]", Exact = true }).First.ClickAsync();
        await page.ReloadAsync();
        await SettleAsync(page);
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
        Assert.Equal("", await page.Locator("#usage-search").InputValueAsync());
        await page.GoBackAsync();
        await SettleAsync(page);
        Assert.True(await Card(page).IsVisibleAsync());
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
    }

    [Fact]
    public async Task FreshSystemObjectDeepLinkRevealsOnlyItsDestinationWithoutChangingAuthoredScope()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenAsync(context);
        var id = await page.Locator(".semantic-object[data-object-origin='system']").First.GetAttributeAsync("id");
        var direct = await OpenAsync(context, "#" + id);
        Assert.True(await Card(direct).IsVisibleAsync());
        Assert.Equal("Summary", await direct.EvaluateAsync<string>("document.activeElement.textContent"));
        Assert.Equal("developer", await direct.Locator("#usage-origin").InputValueAsync());
        Assert.False(await direct.Locator("#investigation-return-row").IsVisibleAsync());
        Assert.False(await direct.Locator("#investigation-selection-note").IsVisibleAsync());
        Assert.Equal("page", await direct.Locator(".section-nav a[href='#semantic-usage']").GetAttributeAsync("aria-current"));
    }

    [Fact]
    public async Task CosmeticEvidenceAndOverflowDoNotCreateHistoryEntriesAndHashlessBackOpensSummary()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await OpenAsync(context);
        await page.Locator("#summary .overview-findings a").First.ClickAsync();
        var length = await page.EvaluateAsync<int>("history.length");
        var evidence = page.Locator(".finding-card:not([hidden])[open] details summary").First;
        await evidence.ClickAsync();
        await evidence.ClickAsync();
        Assert.Equal(length, await page.EvaluateAsync<int>("history.length"));
        await page.GoBackAsync();
        await SettleAsync(page);
        Assert.EndsWith("report.html", page.Url, StringComparison.Ordinal);
        Assert.True(await page.Locator("#summary").IsVisibleAsync());
        var richCard = await page.Locator("[data-lineage-card]:has(.lineage-overflow)").First.GetAttributeAsync("id");
        await page.EvaluateAsync("id => { location.hash = id; }", richCard);
        await SettleAsync(page);
        length = await page.EvaluateAsync<int>("history.length");
        await Card(page).Locator(".lineage-overflow summary").First.ClickAsync();
        Assert.Equal(length, await page.EvaluateAsync<int>("history.length"));
    }

    [Fact]
    public async Task UnknownAndMalformedFragmentsAndMissingFocusTargetsFallBackSafely()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        foreach (var fragment in new[] { "#unknown-destination", "#%E0%A4%A" })
        {
            var page = await OpenAsync(context, fragment);
            var errors = new List<string>();
            page.PageError += (_, error) => errors.Add(error);
            Assert.True(await page.Locator("#summary").IsVisibleAsync());
            var source = await FilteredModelAsync(page);
            await source.ClickAsync();
            // A missing initiating control must fall back to a meaningful heading, never the body.
            await source.EvaluateAsync("element => element.remove()");
            await page.Locator("#investigation-return").ClickAsync();
            await page.WaitForURLAsync("**#semantic-usage");
            await SettleAsync(page);
            Assert.Equal("semantic-usage-heading", await page.EvaluateAsync<string>("document.activeElement.id"));
            Assert.Empty(errors);
        }
    }
}

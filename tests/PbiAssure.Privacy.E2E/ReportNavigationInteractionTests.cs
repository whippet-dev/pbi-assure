using Microsoft.Playwright;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class ReportNavigationInteractionTests(PrivacyE2EFixture fixture) : IAsyncLifetime
{
    private readonly List<string> errors = [];
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() { Assert.Empty(errors); return Task.CompletedTask; }
    private string Render() => HtmlReportRenderer.Render(ProjectScanner.Scan(
        Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "pbi-assure-coverage")));
    private static Task<System.Text.Json.JsonElement?> Settle(IPage page) => page.EvaluateAsync(
        "() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    private async Task<IPage> Open(IBrowserContext context, string fragment = "", string? html = null, bool settle = true)
    {
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await page.RouteAsync("http://report.test/navigation.html", route => route.FulfillAsync(
            new() { ContentType = "text/html", Body = html ?? Render() }));
        await page.GotoAsync("http://report.test/navigation.html" + (fragment.Length > 0 ? "#" + fragment : ""));
        if (settle) await Settle(page);
        return page;
    }

    [Theory]
    [InlineData("summary", "summary")]
    [InlineData("semantic-usage", "semantic-usage")]
    [InlineData("reports", "reports")]
    [InlineData("findings", "reviews")]
    [InlineData("theme-review", "reviews")]
    [InlineData("accessibility-review", "reviews")]
    [InlineData("power-query", "technical")]
    [InlineData("relationships", "technical")]
    [InlineData("row-level-security", "technical")]
    [InlineData("analysis-coverage", "technical")]
    public async Task LegacyDestinationsResolveInTheirAreaWithOneActiveView(string fragment, string area)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await Open(context, fragment);
        Assert.True(await page.Locator("#" + fragment).IsVisibleAsync());
        Assert.Equal(area, await page.Locator("main").GetAttributeAsync("data-active-area"));
        Assert.Equal(area, await page.Locator(".section-navigator [aria-current='page']").GetAttributeAsync("data-section-target"));
        Assert.Equal(1, await page.Locator("[data-report-section]:not([hidden])").CountAsync());
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
        if (area is "reviews" or "technical")
        {
            Assert.Equal(fragment, await page.Locator($"#{area} [data-workspace-target][aria-current='page']").GetAttributeAsync("data-workspace-target"));
            Assert.True(await page.Locator($"#{area} .workspace-heading").IsVisibleAsync());
        }
    }

    [Theory]
    [InlineData("[data-lineage-card] [data-object-view='summary']", "semantic-usage")]
    [InlineData("[data-report-context='Visual'] [data-context-view='summary']", "reports")]
    [InlineData("#finding-1", "reviews")]
    [InlineData("#power-query [data-investigation-item='query']", "technical")]
    [InlineData("#analysis-coverage .coverage-model[id]", "technical")]
    public async Task FreshEntityEvidenceLinksSelectTheirParentAndDoNotInventReturn(string selector, string area)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var html = Render(); var discover = await Open(context, html: html);
        var id = await discover.Locator(selector).First.GetAttributeAsync("id");
        Assert.False(string.IsNullOrEmpty(id));
        var page = await Open(context, id!, html);
        Assert.True(await page.Locator("#" + id).IsVisibleAsync());
        Assert.Equal(area, await page.Locator(".section-navigator [aria-current='page']").GetAttributeAsync("data-section-target"));
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
    }

    [Fact]
    public async Task ShellHasFourPrimaryAreasSecondaryTechnicalAndAccessibleMetadata()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await Open(context);
        Assert.Equal(["Overview", "Model", "Report pages", "Reviews"], await page.Locator(".section-nav:not(.technical-nav) a").AllTextContentsAsync());
        Assert.Equal("Technical", await page.Locator(".technical-nav a").InnerTextAsync());
        Assert.Equal(1, await page.GetByRole(AriaRole.Navigation, new() { Name = "Report navigation", Exact = true }).CountAsync());
        Assert.False(await page.Locator(".project-details .report-meta").IsVisibleAsync());
        await page.Locator(".project-details > summary").PressAsync("Enter");
        Assert.Contains("Source project", await page.Locator(".project-details").InnerTextAsync());
        Assert.Contains("Output format", await page.Locator(".project-details").InnerTextAsync());
        Assert.True(await page.Locator(".project-details #scan-timestamp").IsVisibleAsync());
        Assert.DoesNotContain("Model intelligence", await page.Locator(".site-header").InnerTextAsync());
        Assert.Equal(0, await page.Locator(".site-header .brand-qualifier").CountAsync());
        Assert.True(await page.EvaluateAsync<bool>("() => { const ids = [...document.querySelectorAll('[id]')].map(e => e.id); return new Set(ids).size === ids.length; }"));
        foreach (var phrase in new[] { "Checks complete", "safe to delete", "combined review score", "accessibility certified" })
            Assert.DoesNotContain(phrase, await page.Locator("body").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("reviews", "findings", "Theme", "accessibility-review", "reviews")]
    [InlineData("technical", "power-query", "Relationships", "row-level-security", "technical")]
    public async Task FreshWorkspaceLocalViewsPreserveFiltersAndHaveNoInventedOrigin(
        string alias, string landing, string nextLabel, string last, string area)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await Open(context, alias);
        Assert.Equal(landing, await page.Locator("main").GetAttributeAsync("data-active-section"));
        var search = page.Locator($"#{landing} input[type='search']");
        await search.FillAsync("missing");
        await page.GetByRole(AriaRole.Navigation, new() { Name = area == "reviews" ? "Reviews views" : "Technical views" })
            .GetByRole(AriaRole.Link, new() { Name = nextLabel, Exact = true }).ClickAsync();
        await page.Locator($"#{area} [data-workspace-target='{last}']").ClickAsync();
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
        await page.Locator($"#{area} [data-workspace-target='{landing}']").ClickAsync();
        Assert.Equal("missing", await search.InputValueAsync());
        await page.GoBackAsync(); await Settle(page);
        Assert.Equal(last, await page.Locator("main").GetAttributeAsync("data-active-section"));
        await page.GoForwardAsync(); await Settle(page);
        Assert.Equal("missing", await search.InputValueAsync());
    }

    [Fact]
    public async Task VisualOriginSurvivesAllReviewFamiliesAndBackForward()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var html = Render(); var discover = await Open(context, "", html);
        var reviewId = await discover.Locator("[data-report-context='Visual'] [data-context-view='reviews']:has(a[href^='#finding-'])").First.GetAttributeAsync("id");
        var page = await Open(context, reviewId!, html);
        var origin = page.Url;
        await page.Locator("[data-report-context]:not([hidden]) [data-context-view='reviews'] a[href^='#finding-']").First.ClickAsync();
        Assert.Equal("reviews", await page.Locator("main").GetAttributeAsync("data-active-area"));
        await page.Locator("#reviews [data-workspace-target='theme-review']").ClickAsync();
        await page.Locator("#reviews [data-workspace-target='accessibility-review']").ClickAsync();
        await page.Locator("#reviews [data-workspace-target='findings']").ClickAsync();
        Assert.Contains("Return to Report pages", await page.Locator("#investigation-return").InnerTextAsync());
        await page.GoBackAsync(); await Settle(page);
        Assert.Equal("accessibility-review", await page.Locator("main").GetAttributeAsync("data-active-section"));
        await page.GoForwardAsync(); await Settle(page);
        await page.Locator("#investigation-return").ClickAsync(); await Settle(page);
        Assert.Equal(origin, page.Url);
        Assert.Equal("reports", await page.Locator("main").GetAttributeAsync("data-active-area"));
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
    }

    [Fact]
    public async Task OverviewReviewAndCoverageRoutesSelectParentsAndUsageShortcutStillFilters()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await Open(context);
        foreach (var (target, area) in new[] { ("findings", "reviews"), ("theme-review", "reviews"), ("accessibility-review", "reviews"), ("analysis-coverage", "technical") })
        {
            await page.Locator($"#summary a[href='#{target}']").First.ClickAsync();
            Assert.Equal(area, await page.Locator("main").GetAttributeAsync("data-active-area"));
            await page.Locator(".section-nav a[href='#summary']").ClickAsync();
        }
        await page.Locator("#summary [data-usage-shortcut='ApparentlyUnused']").ClickAsync();
        Assert.Equal("semantic-usage", await page.Locator("main").GetAttributeAsync("data-active-area"));
        Assert.Equal("ApparentlyUnused", await page.Locator("#usage-usage-state").InputValueAsync());
    }

    [Fact]
    public async Task OverviewInvestigationsReturnToTheirFilteredCollectionOrOriginalFindingLink()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await Open(context);
        await page.Locator("#summary [data-usage-shortcut='ApparentlyUnused']").ClickAsync();
        var collectionUrl = page.Url;
        var source = page.Locator(".semantic-object:not([hidden]) a").First;
        var sourceHref = await source.GetAttributeAsync("href");
        await source.ClickAsync();
        Assert.Equal("summary", await page.Locator("[data-lineage-card]:not([hidden])").GetAttributeAsync("data-active-object-view"));
        await page.Locator("[data-lineage-card]:not([hidden]) .object-view-nav").GetByRole(AriaRole.Link, new() { Name = "Lineage", Exact = true }).ClickAsync();
        await page.Locator("#investigation-return").ClickAsync();
        await page.WaitForURLAsync(collectionUrl); await Settle(page);
        Assert.Equal("ApparentlyUnused", await page.Locator("#usage-usage-state").InputValueAsync());
        Assert.Equal(sourceHref, await page.EvaluateAsync<string>("document.activeElement.getAttribute('href')"));
        await page.Locator(".section-nav a[href='#summary']").ClickAsync();
        var overviewUrl = page.Url;
        var findingLink = page.Locator("#summary .overview-findings a").First;
        var findingHref = await findingLink.GetAttributeAsync("href");
        await findingLink.ClickAsync();
        Assert.Equal("reviews", await page.Locator("main").GetAttributeAsync("data-active-area"));
        await page.Locator(findingHref! + " a[href^='#visual-']").First.ClickAsync();
        Assert.Equal("reports", await page.Locator("main").GetAttributeAsync("data-active-area"));
        await page.Locator("#investigation-return").ClickAsync();
        await page.WaitForURLAsync(overviewUrl); await Settle(page);
        Assert.Equal(findingHref, await page.EvaluateAsync<string>("document.activeElement.getAttribute('href')"));
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
    }

    [Fact]
    public async Task NarrowKeyboardNavigationReflowsAndClosesWithPredictableFocusAndPrint()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 320, Height = 900 } });
        var page = await Open(context);
        var toggle = page.Locator("#report-navigation-toggle");
        Assert.Equal("false", await toggle.GetAttributeAsync("aria-expanded"));
        Assert.False(await page.Locator("#report-navigation-links").IsVisibleAsync());
        await toggle.PressAsync("Enter");
        Assert.Equal("true", await toggle.GetAttributeAsync("aria-expanded"));
        await page.Locator(".section-nav a[href='#findings']").PressAsync("Escape");
        Assert.Equal("report-navigation-toggle", await page.EvaluateAsync<string>("document.activeElement.id"));
        await toggle.PressAsync("Enter");
        await page.Locator(".section-nav a[href='#findings']").PressAsync("Enter");
        Assert.Equal("false", await toggle.GetAttributeAsync("aria-expanded"));
        Assert.Equal("findings-heading", await page.EvaluateAsync<string>("document.activeElement.id"));
        Assert.Contains("Reviews", await toggle.InnerTextAsync());
        var route = page.Url;
        await page.Locator(".skip-link").PressAsync("Enter"); await Settle(page);
        Assert.Equal("main-content", await page.EvaluateAsync<string>("document.activeElement.id"));
        Assert.Equal("findings", await page.Locator("main").GetAttributeAsync("data-active-section"));
        Assert.Equal(route, page.Url);
        var directory = Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-slice5"); Directory.CreateDirectory(directory);
        foreach (var mode in new[] { "Light", "Dark" })
        {
            await page.GetByRole(AriaRole.Button, new() { Name = mode + " appearance", Exact = true }).ClickAsync();
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "reviews-320-" + mode.ToLowerInvariant() + ".png") });
        }
        await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active, ReducedMotion = ReducedMotion.Reduce });
        await toggle.FocusAsync();
        Assert.True(await toggle.EvaluateAsync<bool>("e => parseFloat(getComputedStyle(e).outlineWidth) >= 2"));
        await page.EmulateMediaAsync(new() { Media = Media.Print, ForcedColors = ForcedColors.None });
        Assert.False(await page.Locator(".section-navigator").IsVisibleAsync());
        Assert.False(await page.Locator("#summary").IsVisibleAsync());
        Assert.False(await page.Locator("#theme-review").IsVisibleAsync());
        Assert.True(await page.Locator("#findings").IsVisibleAsync());
        Assert.True(await page.Locator(".site-header h1").IsVisibleAsync());
        await page.EmulateMediaAsync(new() { Media = Media.Screen });
        await page.SetViewportSizeAsync(1280, 900);
        await page.GetByRole(AriaRole.Button, new() { Name = "Light appearance", Exact = true }).ClickAsync();
        Assert.True(await page.Locator(".section-nav a[href='#findings']").IsVisibleAsync());
        Assert.False(await toggle.IsVisibleAsync());
        await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "reviews-desktop.png") });
    }

    [Fact]
    public async Task WithoutScriptAllNavigationAndMetadataRemainAvailable()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { JavaScriptEnabled = false, ViewportSize = new() { Width = 320, Height = 900 } });
        var page = await Open(context, settle: false);
        Assert.True(await page.Locator(".section-nav a[href='#semantic-usage']").IsVisibleAsync());
        Assert.False(await page.Locator("#report-navigation-toggle").IsVisibleAsync());
        await page.Locator(".section-nav a[href='#findings']").ClickAsync();
        await page.Locator("#reviews [data-workspace-target='accessibility-review']").ClickAsync();
        Assert.EndsWith("#accessibility-review", page.Url, StringComparison.Ordinal);
        Assert.True(await page.Locator("#accessibility-review").IsVisibleAsync());
    }
}

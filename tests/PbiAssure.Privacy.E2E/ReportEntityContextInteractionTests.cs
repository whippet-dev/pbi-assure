using Microsoft.Playwright;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;
using System.Text;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class ReportEntityContextInteractionTests(PrivacyE2EFixture fixture) : IAsyncLifetime
{
    private readonly List<string> errors = [];
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() { Assert.Empty(errors); return Task.CompletedTask; }
    private static ILocator Entity(IPage page) => page.Locator("[data-report-context]:not([hidden])");
    private static Task<System.Text.Json.JsonElement?> Settle(IPage page) => page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    private async Task<IPage> Open(IBrowserContext context, string fragment = "reports", string? html = null)
    {
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        html ??= HtmlReportRenderer.Render(ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "pbi-assure-coverage")));
        await page.RouteAsync("http://report.test/entity.html", route => route.FulfillAsync(new() { ContentType = "text/html", Body = html }));
        await page.GotoAsync("http://report.test/entity.html#" + fragment); await Settle(page);
        return page;
    }
    private static Task Local(IPage page, string label) => Entity(page).Locator(".object-view-nav").GetByRole(AriaRole.Link, new() { Name = label, Exact = true }).ClickAsync();

    [Theory]
    [InlineData("Report", "summary", "Pages")]
    [InlineData("Report", "pages", "Summary")]
    [InlineData("Page", "summary", "Visuals")]
    [InlineData("Page", "visuals", "Summary")]
    public async Task FreshReportAndPageViewsKeepTheirIdentityAndNeverFabricateOrigin(string kind, string view, string other)
    {
        await using var context = await fixture.Browser.NewContextAsync(); var page = await Open(context);
        var route = await page.Locator($"[data-report-context='{kind}'] [data-context-view-link='{view}']").First.GetAttributeAsync("href");
        page = await Open(context, route![1..]);
        Assert.Equal(view, await Entity(page).GetAttributeAsync("data-active-context-view"));
        Assert.Equal(view, (await page.EvaluateAsync<string>("document.activeElement.textContent")).ToLowerInvariant());
        var title = await Entity(page).Locator("h2").InnerTextAsync();
        await Local(page, other);
        Assert.Equal(title, await Entity(page).Locator("h2").InnerTextAsync());
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("objects")]
    [InlineData("reviews")]
    public async Task FreshVisualViewsAndLocalSwitchesHaveOneIdentityCorrectFocusAndNoReturn(string view)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context);
        var id = await page.Locator("[data-report-context='Visual']").First.GetAttributeAsync("id");
        page = await Open(context, "visual-" + id![4..] + "-" + view);
        Assert.Equal(view, await Entity(page).GetAttributeAsync("data-active-context-view"));
        Assert.Equal(view, (await page.EvaluateAsync<string>("document.activeElement.textContent")).ToLowerInvariant());
        var heading = await Entity(page).Locator("h2").InnerTextAsync();
        foreach (var label in new[] { "Reviews", "Objects", "Summary" })
        {
            await Local(page, label);
            Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
            Assert.Equal(heading, await Entity(page).Locator("h2").InnerTextAsync());
            Assert.Equal(label.ToLowerInvariant(), await Entity(page).Locator("[aria-current='page']").GetAttributeAsync("data-context-view-link"));
            Assert.Single(await Entity(page).Locator("[data-context-view]:not([hidden])").AllAsync());
        }
        Assert.Equal("page", await page.Locator(".section-nav a[href='#reports']").GetAttributeAsync("aria-current"));
    }

    [Fact]
    public async Task ReportsReportPageVisualObjectsAndObjectReturnRestoreTheCollection()
    {
        await using var context = await fixture.Browser.NewContextAsync(); var page = await Open(context);
        await page.Locator("#page-search").FillAsync("Core coverage");
        var report = page.Locator("#page-list > h3 a").Filter(new() { HasText = "PbiAssureCoverage" });
        var initiating = await report.GetAttributeAsync("href"); await report.PressAsync("Enter");
        Assert.Equal("Report", await Entity(page).GetAttributeAsync("data-report-context"));
        await Local(page, "Pages"); await Entity(page).Locator("[data-context-view='pages'] a").First.ClickAsync();
        Assert.Equal("Page", await Entity(page).GetAttributeAsync("data-report-context"));
        await Local(page, "Visuals"); await Entity(page).Locator("[data-context-view='visuals'] a").First.ClickAsync();
        Assert.Equal("Visual", await Entity(page).GetAttributeAsync("data-report-context"));
        await Local(page, "Objects"); await Entity(page).Locator("[data-context-view='objects'] a[href^='#sum-']").First.ClickAsync();
        Assert.Equal("summary", await page.Locator("[data-lineage-card]:not([hidden])").GetAttributeAsync("data-active-object-view"));
        await page.Locator("#investigation-return").ClickAsync(); await Settle(page);
        Assert.EndsWith("#reports", page.Url, StringComparison.Ordinal);
        Assert.Equal("Core coverage", await page.Locator("#page-search").InputValueAsync());
        Assert.Equal(initiating, await page.EvaluateAsync<string>("document.activeElement.getAttribute('href')"));
    }

    [Fact]
    public async Task FilteredFindingsOpenVisualReviewsAndReturnPreservesFocusAndFilters()
    {
        await using var context = await fixture.Browser.NewContextAsync(); var page = await Open(context, "findings");
        var errors = new List<string>(); page.PageError += (_, error) => errors.Add(error);
        await page.Locator("#finding-search").FillAsync("visual");
        var card = page.Locator(".finding-card:not([hidden])").Filter(new() { Has = page.Locator("a.inventory-link[href$='-reviews']") }).First;
        await card.Locator("summary").First.ClickAsync();
        var link = card.Locator("a.inventory-link[href$='-reviews']"); var initiating = await link.GetAttributeAsync("href"); await link.ClickAsync();
        Assert.Equal("reviews", await Entity(page).GetAttributeAsync("data-active-context-view"));
        await Local(page, "Objects"); await Local(page, "Summary");
        await page.Locator("#investigation-return").ClickAsync(); await Settle(page);
        Assert.EndsWith("#findings", page.Url, StringComparison.Ordinal);
        Assert.Equal("visual", await page.Locator("#finding-search").InputValueAsync());
        Assert.Equal(initiating, await page.EvaluateAsync<string>("document.activeElement.getAttribute('href')"));
        Assert.True(await card.EvaluateAsync<bool>("element => element.open")); Assert.Empty(errors);
    }

    [Fact]
    public async Task VisualObjectLineageAnotherVisualBackForwardAndReturnKeepRootEntity()
    {
        static ProjectFileContent File(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));
        static string Visual(string name) => "{\"name\":\"" + name + "\",\"position\":{\"x\":20,\"y\":20,\"width\":200,\"height\":100},\"visual\":{\"visualType\":\"card\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[{\"field\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Sales\"}},\"Property\":\"Shared\"}},\"queryRef\":\"Sales.Shared\"}]}}}}}";
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Shared visual object", [
            File("Model.SemanticModel/definition.pbism", "{}"), File("Model.SemanticModel/definition/tables/Sales.tmdl", "table Sales\n\tmeasure Shared = 1\n"),
            File("Model.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"),
            File("Model.Report/definition/pages/pages.json", "{\"pageOrder\":[\"p1\"]}"),
            File("Model.Report/definition/pages/p1/page.json", "{\"name\":\"p1\",\"displayName\":\"Overview\",\"width\":1280,\"height\":720}"),
            File("Model.Report/definition/pages/p1/visuals/v1/visual.json", Visual("v1")), File("Model.Report/definition/pages/p1/visuals/v2/visual.json", Visual("v2"))]));
        var html = HtmlReportRenderer.Render(inventory);
        await using var context = await fixture.Browser.NewContextAsync(); var page = await Open(context, html: html);
        var route = await page.Locator("[data-report-context='Visual']:has([data-context-view='objects'] a[href^='#sum-']) [data-context-view-link='objects']").First.GetAttributeAsync("href");
        page = await Open(context, route![1..], html);
        var originalId = await Entity(page).GetAttributeAsync("id");
        await Entity(page).Locator("[data-context-view='objects'] a[href^='#sum-']").First.ClickAsync();
        var objectSummary = page.Url;
        await page.Locator("[data-lineage-card]:not([hidden]) .object-view-nav").GetByRole(AriaRole.Link, new() { Name = "Lineage", Exact = true }).ClickAsync();
        var objectLineage = page.Url;
        await page.Locator("[data-lineage-card]:not([hidden]) [data-lineage-group='report'] .lineage-overflow > summary").ClickAsync();
        await page.Locator("[data-lineage-card]:not([hidden]) [data-lineage-group='report'] a[href^='#visual-']").Last.ClickAsync();
        Assert.NotEqual(originalId, await Entity(page).GetAttributeAsync("id"));
        var visual = page.Url;
        await Local(page, "Reviews"); await page.GoBackAsync(); await Settle(page); Assert.Equal(visual, page.Url);
        await page.GoBackAsync(); await Settle(page); Assert.Equal(objectLineage, page.Url);
        await page.GoBackAsync(); await Settle(page); Assert.Equal(objectSummary, page.Url);
        await page.GoForwardAsync(); await Settle(page); await page.GoForwardAsync(); await Settle(page); Assert.Equal(visual, page.Url);
        await page.Locator("#investigation-return").ClickAsync(); await Settle(page);
        Assert.EndsWith(route!, page.Url, StringComparison.Ordinal); Assert.Equal("objects", await Entity(page).GetAttributeAsync("data-active-context-view"));
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
    }

    [Theory]
    [InlineData("lineage", "objects")]
    [InlineData("details", "summary")]
    public async Task LegacyFragmentsResolveToTheSameVisualContext(string legacy, string view)
    {
        await using var context = await fixture.Browser.NewContextAsync(); var page = await Open(context);
        var id = legacy == "lineage" ? await page.Locator("[data-report-context='Visual']").First.GetAttributeAsync("id")
            : await page.Locator("[data-report-context='Visual'] [data-context-route]").First.GetAttributeAsync("id");
        page = await Open(context, id!);
        Assert.Equal(view, await Entity(page).GetAttributeAsync("data-active-context-view"));
        Assert.False(await page.Locator("#investigation-return-row").IsVisibleAsync());
    }

    [Theory]
    [InlineData(1280, false)]
    [InlineData(320, true)]
    public async Task ContextsWrapSupportKeyboardAppearanceAndPrintOnlyTheActiveEntity(int width, bool dark)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = 900 }, ColorScheme = dark ? ColorScheme.Dark : ColorScheme.Light });
        var page = await Open(context);
        var route = await page.Locator("[data-report-context='Visual']:has([data-context-view='objects'] a[href^='#sum-']) [data-context-view-link='objects']").First.GetAttributeAsync("href");
        page = await Open(context, route![1..]);
        await Entity(page).Locator(".object-view-nav").GetByRole(AriaRole.Link, new() { Name = "Reviews", Exact = true }).PressAsync("Enter");
        Assert.Equal("reviews", await Entity(page).GetAttributeAsync("data-active-context-view"));
        await Entity(page).Locator(".object-context-details > summary").PressAsync("Enter");
        Assert.True(await Entity(page).Locator(".object-context-details").EvaluateAsync<bool>("element => element.open"));
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        var directory = Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-slice4"); Directory.CreateDirectory(directory);
        await page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"visual-reviews-{width}.png") });
        await Entity(page).Locator(".object-context-details > summary").PressAsync("Enter");
        await Local(page, "Summary");
        await page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"visual-summary-{width}.png") });
        await Local(page, "Reviews");
        await page.EmulateMediaAsync(new() { Media = Media.Print });
        Assert.Single(await page.Locator("[data-report-context]:visible").AllAsync());
        Assert.Single(await Entity(page).Locator("[data-context-view]:visible").AllAsync());
        Assert.False(await page.Locator("#reports").IsVisibleAsync());
    }

    [Fact]
    public async Task LongNamesForcedColorsAndReducedMotionKeepOrdinaryLinksUsableAt320Pixels()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 320, Height = 900 }, ForcedColors = ForcedColors.Active, ReducedMotion = ReducedMotion.Reduce });
        var page = await Open(context);
        var route = await page.Locator("[data-report-context='Visual'] [data-context-view-link='reviews']").First.GetAttributeAsync("href");
        page = await Open(context, route![1..]);
        await Entity(page).Locator("h2").EvaluateAsync("element => { element.textContent = 'Long_visual_title_'.repeat(25); }");
        await Entity(page).Locator(".object-view-nav").GetByRole(AriaRole.Link, new() { Name = "Objects", Exact = true }).PressAsync("Enter");
        Assert.Equal("objects", await Entity(page).GetAttributeAsync("data-active-context-view"));
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        Assert.Equal(0, await Entity(page).Locator("[role='tab'], [role='tablist']").CountAsync());
        Assert.Equal("active", await page.EvaluateAsync<string>("matchMedia('(forced-colors: active)').matches ? 'active' : 'inactive'"));
    }
}

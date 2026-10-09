using System.Text;
using System.Text.Json;
using Microsoft.Playwright;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class ReportCollectionInteractionTests(PrivacyE2EFixture fixture) : IAsyncLifetime
{
    private readonly List<string> errors = [];
    private static readonly string[] StaticTypes = ["image", "shape", "textbox", "actionButton"];
    private static readonly string[] PreviewNames = ["Sales[M0]", "Sales[M1]", "Sales[M2]"];
    private static readonly string[] PreviewRoles = ["Values", "Values", "Values"];
    private string Output => Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-visual-polish");
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() { Assert.Empty(errors); return Task.CompletedTask; }
    private static Task<JsonElement?> Settle(IPage page) => page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    private static ILocator Entity(IPage page) => page.Locator("[data-report-context]:not([hidden])");
    private static ILocator Collection(IPage page) => page.Locator(".page-card:not([hidden])");

    private static ProjectInventory Example()
    {
        static ProjectFileContent File(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));
        static object Field(string name, string kind = "Measure") => new Dictionary<string, object> {
            [kind] = new { Expression = new { SourceRef = new { Entity = "Sales" } }, Property = name } };
        static string Visual(string name, int count) => JsonSerializer.Serialize(new { name,
            position = new { x = 50, y = 20, width = 300, height = 150 }, visual = new { visualType = "tableEx",
                query = new { queryState = new { Values = new { projections = Enumerable.Range(0, count).Select(index => new {
                    field = Field($"M{index}"), queryRef = $"Sales.M{index}" }).ToArray() } } } } });
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Collection example", [
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Sales.tmdl", "table Sales\n\tcolumn PageOnly\n\t\tdataType: int64\n" + string.Join("\n", Enumerable.Range(0, 7).Select(index => $"\tmeasure M{index} = 1"))),
            File("Model.Report/definition.pbir", "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"),
            File("Model.Report/definition/pages/pages.json", "{\"pageOrder\":[\"p1\"]}"),
            File("Model.Report/definition/pages/p1/page.json", JsonSerializer.Serialize(new { name = "p1", displayName = "Collection page", width = 1280, height = 720,
                filterConfig = new { filters = new[] { new { name = "Page filter", field = Field("PageOnly", "Column") } } } })),
            File("Model.Report/definition/pages/p1/visuals/wide/visual.json", Visual("wide", 7)),
            File("Model.Report/definition/pages/p1/visuals/overlap/visual.json", Visual("overlap", 2)),
            .. StaticTypes.Select(type => File($"Model.Report/definition/pages/p1/visuals/{type}/visual.json", JsonSerializer.Serialize(new { name = type, visual = new { visualType = type } }))) ]));
        var report = inventory.Reports.Single(); var savedPage = report.Pages.Single();
        return inventory with {
            Findings = [new AssuranceFinding("TEST-1", "1", "Navigation", "Warning", "Inspect this visual", "Review its saved action.", report.Name, savedPage.Name, savedPage.DisplayName, "wide", null, null, null, "Model.Report", [], "Static", null),
                new AssuranceFinding("TEST-2", "1", AssuranceCategories.Accessibility, "Warning", "Inspect accessibility", "Review saved accessibility.", report.Name, savedPage.Name, savedPage.DisplayName, "wide", null, null, null, "Model.Report", [], "Static", null)],
            Reports = [report with { Pages = [savedPage with { Visuals = savedPage.Visuals.Select(visual => visual.Name == "wide" ? visual with {
                Accessibility = visual.Accessibility with { TitleText = "Revenue by region", TitleIsVisible = true, HasConfiguredTitleText = true } } : visual).ToArray() }],
                ThemeReview = report.ThemeReview with { Deviations = [new ThemeDeviationInventory(savedPage.Name, savedPage.DisplayName, "wide", "tableEx", "font", "Font", "12", "10", null)],
                    ConsistencyObservations = [], AccessibilityObservations = [] } }] };
    }

    private async Task<IPage> Open(IBrowserContext context, string name = "collection-example", ProjectInventory? inventory = null, bool longName = false)
    {
        Directory.CreateDirectory(Output);
        var source = inventory ?? Example();
        if (longName)
            source = source with { SemanticObjectUsages = source.SemanticObjectUsages.Select(usage => usage with {
                DirectReportReferences = usage.DirectReportReferences.Select(reference => reference with {
                    Role = "Conditional formatting", UsageContext = UsageContexts.Formatting
                }).ToArray() }).ToArray() };
        var html = HtmlReportRenderer.Render(source);
        if (longName) html = html.Replace("Sales[M0]", "Sales[A long identifier with spaces and_" + new string('x', 90) + "]", StringComparison.Ordinal);
        var path = Path.Combine(Output, name + (longName ? "-long" : "") + ".html");
        await System.IO.File.WriteAllTextAsync(path, html);
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await page.GotoAsync(new Uri(path).AbsoluteUri + "#reports"); await Settle(page);
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand all pages", Exact = true }).ClickAsync();
        return page;
    }

    [Fact]
    public async Task CollectionShowsUsefulUsagePageReferencesStaticRowsAndLocalReviewCues()
    {
        await using var context = await fixture.Browser.NewContextAsync(); var page = await Open(context);
        Assert.Contains("6 visuals · 7 model objects used by visuals", await Collection(page).Locator(":scope > summary").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains("on this page, including visuals", await Collection(page).Locator(":scope > summary").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(6, await Collection(page).Locator(".visual-preview").CountAsync());
        var wide = Collection(page).Locator(".visual-preview:has(.visual-preview-more)");
        Assert.Equal(3, await wide.Locator(".visual-use-preview li").CountAsync());
        Assert.Equal(PreviewNames, await wide.Locator(".visual-use-preview a").AllTextContentsAsync());
        Assert.Equal(PreviewRoles, await wide.Locator(".visual-use-role").AllTextContentsAsync());
        Assert.All(await wide.Locator(".visual-use-meta").AllTextContentsAsync(), text => Assert.Equal("Measure — Used as: Values", text));
        Assert.Equal("Objects used by this visual", await wide.Locator(".visual-preview-label").InnerTextAsync());
        Assert.Contains("Table", await wide.Locator(".visual-preview-meta").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal("+4 more", await wide.Locator(".visual-preview-more").InnerTextAsync());
        Assert.DoesNotContain("PageOnly", await wide.InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains("Sales[PageOnly]", await Collection(page).Locator(".page-reference-preview").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(4, await Collection(page).Locator(".visual-preview-empty").CountAsync());
        Assert.All(await Collection(page).Locator(".visual-preview:has(.visual-preview-empty) .visual-preview-meta").AllTextContentsAsync(), text => Assert.DoesNotContain(" · ", text, StringComparison.Ordinal));
        Assert.Equal("1 finding · 1 accessibility observation · 1 theme review item", await wide.Locator(".visual-review-preview").InnerTextAsync());
        Assert.Equal(1, await Collection(page).Locator(".visual-review-preview").CountAsync());
        await wide.Locator(".visual-preview-more").PressAsync("Enter"); await Settle(page);
        Assert.Equal("objects", await Entity(page).GetAttributeAsync("data-active-context-view"));
        Assert.Equal(7, await Entity(page).Locator("[data-context-view='objects'] a[href^='#sum-']").CountAsync());
        var visualId = await Entity(page).GetAttributeAsync("id");
        await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page);
        await wide.Locator(".visual-review-preview a").PressAsync("Enter"); await Settle(page);
        Assert.Equal(visualId, await Entity(page).GetAttributeAsync("id"));
        Assert.Equal("reviews", await Entity(page).GetAttributeAsync("data-active-context-view"));
        Assert.Equal(1, await Entity(page).Locator("[data-context-view='reviews'] a[href^='#finding-']").CountAsync());
        Assert.Equal(1, await Entity(page).Locator("[data-context-view='reviews'] a[href^='#accessibility-finding-']").CountAsync());
        await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page);
        await Collection(page).Locator(".page-reference-preview a").PressAsync("Enter"); await Settle(page);
        Assert.Equal("summary", await page.Locator("[data-lineage-card]:not([hidden])").GetAttributeAsync("data-active-object-view"));
    }

    [Fact]
    public async Task RealCoverageOffersDataPreviewsAndDirectVisualDestinations()
    {
        var inventory = ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "pbi-assure-coverage"));
        await using var context = await fixture.Browser.NewContextAsync(); var page = await Open(context, "collection-coverage", inventory);
        await page.Locator("#page-search").FillAsync("Core coverage");
        Assert.Contains("8 visuals · 16 model objects used by visuals", await Collection(page).Locator(":scope > summary").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(5, await Collection(page).Locator(".visual-use-preview").CountAsync());
        Assert.Contains("Visual filter", await Collection(page).InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains("Formatting", await Collection(page).InnerTextAsync(), StringComparison.Ordinal);
        var preview = Collection(page).Locator(".visual-preview:has(.visual-preview-more)");
        Assert.Equal("+8 more", await preview.Locator(".visual-preview-more").InnerTextAsync());
        await preview.Locator("h4 a").FocusAsync(); await Settle(page);
        await page.ScreenshotAsync(new() { Path = Path.Combine(Output, "coverage-data-visuals.png") });
        await Collection(page).ScreenshotAsync(new() { Path = Path.Combine(Output, "coverage-page.png") });
        await preview.Locator(".visual-preview-more").PressAsync("Enter"); await Settle(page);
        Assert.Equal(11, await Entity(page).Locator("[data-context-view='objects'] a[href^='#sum-']").CountAsync());
        await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page);
        Assert.Equal("Core coverage", await page.Locator("#page-search").InputValueAsync());
    }

    [Fact]
    public async Task PreviewObjectReturnBackAndForwardRestoreSearchFacetsExpansionScrollAndFocus()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 720 } });
        var page = await Open(context, "collection-history");
        await page.Locator("#page-search").FillAsync("M0");
        await page.Locator("#reports summary").Filter(new() { HasText = "More filters" }).ClickAsync();
        await page.Locator("#page-visual-type").SelectOptionAsync("tableEx");
        await page.Locator("#page-visibility").SelectOptionAsync("Visible");
        var link = Collection(page).Locator(".visual-use-preview a").First;
        await link.ScrollIntoViewIfNeededAsync(); await link.FocusAsync(); await Settle(page);
        var route = await link.GetAttributeAsync("href"); var focus = await page.EvaluateAsync<string>("document.activeElement.outerHTML");
        var expansions = await page.Locator("#page-list details").EvaluateAllAsync<bool[]>("elements => elements.map(e => e.open)");
        var scroll = await page.EvaluateAsync<double>("scrollY");
        await link.PressAsync("Enter"); await Settle(page);
        Assert.EndsWith(route!, page.Url, StringComparison.Ordinal);
        Assert.Equal("summary", await page.Locator("[data-lineage-card]:not([hidden])").GetAttributeAsync("data-active-object-view"));
        await page.GoBackAsync(); await Settle(page);
        Assert.EndsWith("#reports", page.Url, StringComparison.Ordinal);
        Assert.Equal(focus, await page.EvaluateAsync<string>("document.activeElement.outerHTML"));
        await page.GoForwardAsync(); await Settle(page);
        Assert.EndsWith(route!, page.Url, StringComparison.Ordinal);
        await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page);
        Assert.Equal("M0", await page.Locator("#page-search").InputValueAsync());
        Assert.Equal("tableEx", await page.Locator("#page-visual-type").InputValueAsync());
        Assert.Equal("Visible", await page.Locator("#page-visibility").InputValueAsync());
        Assert.Equal(expansions, await page.Locator("#page-list details").EvaluateAllAsync<bool[]>("elements => elements.map(e => e.open)"));
        Assert.Equal(focus, await page.EvaluateAsync<string>("document.activeElement.outerHTML"));
        Assert.InRange(Math.Abs(await page.EvaluateAsync<double>("scrollY") - scroll), 0, 3);
        Assert.Single(await Collection(page).AllAsync());
    }

    [Fact]
    public async Task RealDynamicTextUsesTheSameObjectsAsItsExistingVisualObjectsView()
    {
        var inventory = ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "desktop-dynamic-text-evidence"));
        await using var context = await fixture.Browser.NewContextAsync(); var page = await Open(context, "collection-dynamic-text", inventory);
        var rows = Collection(page).Locator(".visual-preview:has(.visual-use-preview)");
        Assert.True(await rows.CountAsync() > 0);
        foreach (var row in await rows.AllAsync())
        {
            Assert.Contains("Text box", await row.InnerTextAsync(), StringComparison.Ordinal);
            var preview = await row.Locator(".visual-use-preview a").AllTextContentsAsync();
            await row.Locator("h4 a").PressAsync("Enter");
            await Entity(page).GetByRole(AriaRole.Link, new() { Name = "Objects", Exact = true }).PressAsync("Enter");
            Assert.Equal(preview, (await Entity(page).Locator("[data-context-view='objects'] a[href^='#sum-']").AllTextContentsAsync()).Take(3));
            await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page);
        }
    }

    [Theory]
    [InlineData(1280, 900, ColorScheme.Light, ForcedColors.None)]
    [InlineData(1280, 900, ColorScheme.Dark, ForcedColors.None)]
    [InlineData(320, 900, ColorScheme.Light, ForcedColors.None)]
    [InlineData(320, 225, ColorScheme.Dark, ForcedColors.None)]
    [InlineData(320, 900, ColorScheme.Light, ForcedColors.Active)]
    public async Task RowsReflowAndKeepSeparateKeyboardTargetsWithLongNames(int width, int height, ColorScheme colors, ForcedColors forced)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height }, ColorScheme = colors, ForcedColors = forced, ReducedMotion = ReducedMotion.Reduce });
        var page = await Open(context, "collection-example", longName: true);
        var row = Collection(page).Locator(".visual-preview:has(.visual-preview-more)");
        var title = row.Locator("h4 a");
        await title.FocusAsync(); await title.PressAsync("Tab");
        Assert.True(await row.Locator(".visual-use-preview a").First.EvaluateAsync<bool>("element => element === document.activeElement"));
        Assert.True(await page.EvaluateAsync<bool>("document.activeElement.matches(':focus-visible')"));
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        Assert.All(await row.Locator(".visual-use-preview li, .visual-use-preview a, .visual-use-meta, .visual-use-role").EvaluateAllAsync<double[]>("elements => elements.map(e => e.getBoundingClientRect().right)"), right => Assert.True(right <= width));
        Assert.All(await row.Locator(".visual-use-meta").AllTextContentsAsync(), text => Assert.Equal("Measure — Used as: Conditional formatting", text));
        if (width == 320)
        {
            var cards = await row.Locator(".visual-use-preview li").EvaluateAllAsync<double[]>("elements => elements.map(e => e.getBoundingClientRect().x)");
            Assert.Single(cards.Distinct());
        }
        if (forced == ForcedColors.Active)
            Assert.All(await row.Locator(".visual-use-preview li").EvaluateAllAsync<string[]>("elements => elements.map(e => getComputedStyle(e).borderTopStyle)"), style => Assert.Equal("solid", style));
        await page.ScreenshotAsync(new() { Path = Path.Combine(Output, $"collection-{width}-{height}-{colors}-{forced}.png") });
        await row.Locator(".visual-preview-more").PressAsync("Enter"); await Settle(page);
        Assert.Equal("objects", await Entity(page).GetAttributeAsync("data-active-context-view"));
        await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page);
        Assert.True(await row.Locator(".visual-preview-more").EvaluateAsync<bool>("element => element === document.activeElement"));
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
    }
}

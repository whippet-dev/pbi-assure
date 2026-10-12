using Microsoft.Playwright;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class ObjectSummaryPreviewInteractionTests(PrivacyE2EFixture fixture) : IAsyncLifetime
{
    private readonly List<string> errors = [];
    private string Output => Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-object-summary");
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() { Assert.Empty(errors); return Task.CompletedTask; }
    private ProjectInventory Coverage() => ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "pbi-assure-coverage"));
    private static Task<System.Text.Json.JsonElement?> Settle(IPage page) => page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    private static ILocator Card(IPage page) => page.Locator("[data-lineage-card]:not([hidden])");
    private static ILocator Summary(IPage page) => Card(page).Locator("[data-object-view='summary']");
    private static ILocator Row(IPage page, string name) => page.Locator(".semantic-object").Filter(new() {
        Has = page.Locator(".object-name a").Filter(new() { HasTextRegex = new("^" + System.Text.RegularExpressions.Regex.Escape(name) + "$") }) });
    private static ILocator Group(IPage page, string name) => Summary(page).Locator(".object-summary-group").Filter(new() {
        Has = page.GetByRole(AriaRole.Heading, new() { Name = name, Exact = true }) });
    private async Task<IPage> Open(IBrowserContext context, string name, string fragment = "semantic-usage", ProjectInventory? inventory = null, string? longObjectName = null)
    {
        Directory.CreateDirectory(Output);
        var html = HtmlReportRenderer.Render(inventory ?? Coverage());
        if (longObjectName is not null)
        {
            // Stress visible text after projection, preserving the fixture's canonical ownership.
            html = html.Replace("Fact[DirectlyUsedMeasure]", System.Text.Encodings.Web.HtmlEncoder.Default.Encode(longObjectName), StringComparison.Ordinal)
                .Replace("PbiAssureCoverage", System.Text.Encodings.Web.HtmlEncoder.Default.Encode(longObjectName), StringComparison.Ordinal);
        }
        var path = Path.Combine(Output, name + ".html"); await File.WriteAllTextAsync(path, html);
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await page.GotoAsync(new Uri(path).AbsoluteUri + "#" + fragment); await Settle(page);
        return page;
    }

    [Fact]
    public async Task SummaryLocationsConsumersAndDefinitionKeepRootOriginAndBackForward()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 }, ReducedMotion = ReducedMotion.Reduce });
        var page = await Open(context, "history");
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand all tables", Exact = true }).ClickAsync();
        await page.Locator("#usage-search").FillAsync("DirectlyUsedMeasure");
        await page.Locator("#semantic-usage summary").Filter(new() { HasText = "More filters" }).ClickAsync();
        await page.Locator("#usage-table").SelectOptionAsync("Fact");
        await page.Locator("#usage-object-type").SelectOptionAsync("Measure");
        await Settle(page);
        var row = Row(page, "DirectlyUsedMeasure");
        await row.Locator(".model-inspect > summary").PressAsync("Enter");
        var source = row.Locator(".object-name a"); await source.ScrollIntoViewIfNeededAsync(); await source.FocusAsync(); await Settle(page);
        var scroll = await page.EvaluateAsync<double>("scrollY"); var focus = await source.EvaluateAsync<string>("e => e.outerHTML");
        await source.PressAsync("Enter"); await Settle(page); var route = page.Url;
        Assert.Equal(1, await page.Locator("[data-lineage-card]:not([hidden])").CountAsync());
        Assert.Contains("Used as: Values", await Group(page, "Used in the report").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(0, await Card(page).Locator(".lineage-path").CountAsync());
        await page.ScreenshotAsync(new() { Path = Path.Combine(Output, "direct-summary.png"), FullPage = true });

        var consumer = Group(page, "Used by").Locator("li a").Last;
        var consumerTitle = await consumer.InnerTextAsync(); await consumer.PressAsync("Enter"); await Settle(page);
        Assert.Equal(consumerTitle, await Card(page).Locator("h2").InnerTextAsync());
        await page.GoBackAsync(); await Settle(page); Assert.Equal(route, page.Url);

        await Group(page, "DAX").GetByRole(AriaRole.Link, new() { Name = "Full definition", Exact = true }).PressAsync("Enter"); await Settle(page);
        Assert.Equal("definition", await Card(page).GetAttributeAsync("data-active-object-view"));
        await page.GoBackAsync(); await Settle(page); Assert.Equal(route, page.Url);

        var visual = Group(page, "Used in the report").Locator("li a").First; await visual.PressAsync("Enter"); await Settle(page);
        Assert.True(await page.Locator("[data-report-context]:not([hidden])").IsVisibleAsync());
        await page.GoBackAsync(); await Settle(page); Assert.Equal(route, page.Url);
        Assert.True(await visual.EvaluateAsync<bool>("e => e === document.activeElement"));
        await page.GoForwardAsync(); await Settle(page);
        await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page);
        Assert.EndsWith("#semantic-usage", page.Url, StringComparison.Ordinal);
        Assert.Equal("DirectlyUsedMeasure", await page.Locator("#usage-search").InputValueAsync());
        Assert.Equal("Fact", await page.Locator("#usage-table").InputValueAsync());
        Assert.Equal("Measure", await page.Locator("#usage-object-type").InputValueAsync());
        Assert.Equal("developer", await page.Locator("#usage-origin").InputValueAsync());
        Assert.True(await row.Locator(".model-inspect").EvaluateAsync<bool>("e => e.open"));
        Assert.Equal(focus, await page.EvaluateAsync<string>("document.activeElement.outerHTML"));
        Assert.InRange(Math.Abs(await page.EvaluateAsync<double>("scrollY") - scroll), 0, 3);
    }

    [Fact]
    public async Task MeaningfulIndirectPathAndStructuralExplanationsRemainReachable()
    {
        await using var context = await fixture.Browser.NewContextAsync(); var page = await Open(context, "states");
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand all tables", Exact = true }).ClickAsync();
        foreach (var name in new[] { "IndirectlyUsedMeasure", "RelationshipKey", "ApparentlyUnusedMeasure", "UnusedBranchColumn" })
        {
            await page.Locator("#usage-search").FillAsync(name);
            await Row(page, name).Locator(".object-name a").PressAsync("Enter"); await Settle(page);
            var card = Card(page);
            if (Coverage().SemanticObjectUsages.Single(usage => usage.ObjectName == name && usage.Table == "Fact").ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation)
                Assert.True(await card.Locator(".object-context-header .confidence-flag").IsVisibleAsync());
            if (name == "IndirectlyUsedMeasure")
            {
                Assert.Equal(0, await Summary(page).GetByRole(AriaRole.Heading, new() { Name = "Used in the report", Exact = true }).CountAsync());
                await card.GetByRole(AriaRole.Link, new() { Name = "Lineage", Exact = true }).PressAsync("Enter"); await Settle(page);
                Assert.True(await card.GetByRole(AriaRole.Heading, new() { Name = "How this reaches the report", Exact = true }).IsVisibleAsync());
                await page.ScreenshotAsync(new() { Path = Path.Combine(Output, "indirect-lineage.png"), FullPage = true });
            }
            else
            {
                Assert.Equal(0, await card.Locator(".lineage-path").CountAsync());
                if (name == "RelationshipKey") Assert.Contains("Required by the model", await Summary(page).InnerTextAsync(), StringComparison.Ordinal);
                if (name == "ApparentlyUnusedMeasure") Assert.Contains("Check before removing it", await Summary(page).InnerTextAsync(), StringComparison.Ordinal);
            }
            await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page);
        }
    }

    [Theory]
    [InlineData(1280, 900, ColorScheme.Light, ForcedColors.None)]
    [InlineData(1280, 900, ColorScheme.Dark, ForcedColors.None)]
    [InlineData(320, 900, ColorScheme.Light, ForcedColors.None)]
    [InlineData(320, 225, ColorScheme.Dark, ForcedColors.None)]
    [InlineData(320, 900, ColorScheme.Light, ForcedColors.Active)]
    public async Task SummaryLongNamesRolesAndBoundedDaxReflow(int width, int height, ColorScheme colors, ForcedColors forced)
    {
        var inventory = Coverage(); var longName = "Long identifier " + new string('x', 150);
        inventory = inventory with { SemanticModels = inventory.SemanticModels.Select(model => model with { Tables = model.Tables.Select(table => table with {
            Measures = table.Measures.Select(measure => measure.Name == "DirectlyUsedMeasure" ? measure with { Expression = new string('x', 700) + " FULL ONLY" } : measure).ToArray()
        }).ToArray() }).ToArray(), Reports = inventory.Reports.Select(report => report with { Pages = report.Pages.Select(savedPage => savedPage with {
            DisplayName = longName, Visuals = savedPage.Visuals.Select(visual => visual.Name == "projection-visual" ? visual with {
                Accessibility = visual.Accessibility with { TitleText = longName, TitleIsVisible = true, TitleTextIsDynamic = false }
            } : visual).ToArray() }).ToArray() }).ToArray() };
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height }, ColorScheme = colors, ForcedColors = forced });
        var page = await Open(context, $"reflow-{width}-{height}-{colors}-{forced}", inventory: inventory, longObjectName: longName);
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand all tables", Exact = true }).ClickAsync();
        await page.Locator("#usage-search").FillAsync("DirectlyUsedMeasure");
        await Row(page, "DirectlyUsedMeasure").Locator(".object-name a").PressAsync("Enter"); await Settle(page);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        Assert.All(await Summary(page).Locator("li, pre, a").EvaluateAllAsync<double[]>("elements => elements.map(e => e.getBoundingClientRect().right)"), right => Assert.True(right <= width));
        Assert.Equal(600, (await Group(page, "DAX").Locator("code").InnerTextAsync()).Length);
        Assert.Contains("Preview — expression shortened.", await Summary(page).InnerTextAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("FULL ONLY", await Summary(page).InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains(longName, await Group(page, "Used in the report").InnerTextAsync(), StringComparison.Ordinal);
        var definition = Group(page, "DAX").GetByRole(AriaRole.Link, new() { Name = "Full definition", Exact = true });
        await definition.FocusAsync(); await Settle(page);
        Assert.True(await definition.EvaluateAsync<bool>("e => e.matches(':focus-visible')"));
        Assert.NotEqual("none", await definition.EvaluateAsync<string>("e => getComputedStyle(e).outlineStyle"));
        if (forced == ForcedColors.Active)
            Assert.All(await Summary(page).Locator(".object-summary-preview li").EvaluateAllAsync<string[]>("elements => elements.map(e => getComputedStyle(e).borderTopStyle)"), border => Assert.Equal("solid", border));
        await page.ScreenshotAsync(new() { Path = Path.Combine(Output, $"summary-{width}-{height}-{colors}-{forced}.png"), FullPage = true });
        await definition.PressAsync("Enter"); await Settle(page);
        Assert.Contains("FULL ONLY", await Card(page).Locator("[data-object-view='definition']").InnerTextAsync(), StringComparison.Ordinal);
    }
}

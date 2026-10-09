using System.Text.Json;
using Microsoft.Playwright;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class VisualInlineInspectInteractionTests(PrivacyE2EFixture fixture) : IAsyncLifetime
{
    private readonly List<string> errors = [];
    private string Output => Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-hybrid-inspect");
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() { Assert.Empty(errors); return Task.CompletedTask; }
    private static Task<JsonElement?> Settle(IPage page) => page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    private ProjectInventory Coverage() => ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "pbi-assure-coverage"));
    private static ILocator Row(IPage page, string visual) => page.Locator($".visual-preview[data-preview-visual*='-{visual}-']");

    private async Task<IPage> Open(IBrowserContext context, ProjectInventory? inventory = null, string name = "coverage")
    {
        Directory.CreateDirectory(Output);
        var path = Path.Combine(Output, name + ".html");
        await File.WriteAllTextAsync(path, HtmlReportRenderer.Render(inventory ?? Coverage()));
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await page.GotoAsync(new Uri(path).AbsoluteUri + "#reports"); await Settle(page);
        await page.Locator("#page-search").FillAsync("Core coverage");
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand all pages", Exact = true }).ClickAsync();
        return page;
    }

    [Fact]
    public async Task NativeInspectProvidesSavedBehaviourAndAccessibilityWithoutNavigation()
    {
        await using var context = await fixture.Browser.NewContextAsync(); var page = await Open(context);
        var projection = Row(page, "projection-visual"); var actions = Row(page, "valid-actions");
        var inspect = projection.Locator(".visual-inspect"); var summary = inspect.Locator("summary");
        Assert.Equal("Inspect " + await projection.Locator("h4").InnerTextAsync(), await summary.InnerTextAsync());
        Assert.Equal(0, await summary.Locator("a, button, input").CountAsync());
        var route = page.Url; var length = await page.EvaluateAsync<int>("history.length");
        await summary.FocusAsync(); await summary.PressAsync("Enter"); await Settle(page);
        Assert.True(await inspect.EvaluateAsync<bool>("element => element.open"));
        Assert.True(await summary.EvaluateAsync<bool>("element => element.matches(':focus-visible')"));
        Assert.NotEqual("none", await summary.EvaluateAsync<string>("element => getComputedStyle(element).outlineStyle"));
        Assert.Contains("Report-page tooltip: page “Coverage tooltip”", await inspect.InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains("Core semantic projection coverage", await inspect.InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains("Position 8", await inspect.InnerTextAsync(), StringComparison.Ordinal);
        await actions.Locator(".visual-inspect > summary").PressAsync("Space"); await Settle(page);
        Assert.Equal(2, await page.Locator(".page-card:not([hidden]) .visual-inspect[open]").CountAsync());
        Assert.Contains("Action destination", await actions.Locator(".visual-inspect").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains("Coverage bookmark", await actions.Locator(".visual-inspect").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains("web link", await actions.Locator(".visual-inspect").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(route, page.Url); Assert.Equal(length, await page.EvaluateAsync<int>("history.length"));
        await page.Locator(".page-card:not([hidden])").ScreenshotAsync(new() { Path = Path.Combine(Output, "inline-comparison.png") });
        await summary.PressAsync("Enter"); await Settle(page);
        Assert.False(await inspect.EvaluateAsync<bool>("element => element.open"));
        Assert.True(await actions.Locator(".visual-inspect").EvaluateAsync<bool>("element => element.open"));
        var minimal = Row(page, "selector-control-visual").Locator(".visual-inspect");
        await minimal.Locator("summary").PressAsync("Enter");
        Assert.Contains("No configured action or tooltip.", await minimal.InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(3, await projection.Locator(".visual-use-preview li").CountAsync());
        Assert.Equal("+8 more", await projection.Locator(".visual-preview-more").InnerTextAsync());
    }

    [Fact]
    public async Task TwoInspectDisclosuresFiltersFocusAndScrollSurviveContextReturnBackAndForward()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 720 }, ReducedMotion = ReducedMotion.Reduce });
        var page = await Open(context, name: "history");
        await page.Locator("#reports summary").Filter(new() { HasText = "More filters" }).ClickAsync();
        await page.Locator("#page-visibility").SelectOptionAsync("Visible");
        var first = Row(page, "projection-visual").Locator(".visual-inspect"); var second = Row(page, "valid-actions").Locator(".visual-inspect");
        await first.Locator("summary").PressAsync("Enter"); await second.Locator("summary").PressAsync("Enter");
        var link = Row(page, "valid-actions").Locator("h4 a");
        await link.ScrollIntoViewIfNeededAsync(); await link.FocusAsync(); await Settle(page);
        var focus = await page.EvaluateAsync<string>("document.activeElement.outerHTML"); var scroll = await page.EvaluateAsync<double>("scrollY");
        await link.PressAsync("Enter"); await Settle(page);
        Assert.Equal("summary", await page.Locator("[data-report-context]:not([hidden])").GetAttributeAsync("data-active-context-view"));
        var route = page.Url;
        await page.GoBackAsync(); await Settle(page); await AssertRestored();
        await page.GoForwardAsync(); await Settle(page); Assert.Equal(route, page.Url);
        await page.Locator("#investigation-return").PressAsync("Enter"); await Settle(page); await AssertRestored();

        async Task AssertRestored()
        {
            Assert.EndsWith("#reports", page.Url, StringComparison.Ordinal);
            Assert.Equal("Core coverage", await page.Locator("#page-search").InputValueAsync());
            Assert.Equal("Visible", await page.Locator("#page-visibility").InputValueAsync());
            Assert.True(await first.EvaluateAsync<bool>("element => element.open")); Assert.True(await second.EvaluateAsync<bool>("element => element.open"));
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
    public async Task ExpandedSavedValuesAndNamesReflow(int width, int height, ColorScheme colors, ForcedColors forced)
    {
        var inventory = Coverage(); var longText = "Revenue comparison " + new string('x', 180);
        inventory = inventory with { Reports = inventory.Reports.Select(report => report with { Pages = report.Pages.Select(savedPage => savedPage with {
            DisplayName = savedPage.Name == "tooltip-page" ? longText : savedPage.DisplayName,
            Visuals = savedPage.Visuals.Select(visual => visual.Name == "projection-visual" ? visual with {
                Accessibility = visual.Accessibility with { TitleText = longText, HasConfiguredTitleText = true, TitleIsVisible = true, AltText = longText }
            } : visual).ToArray() }).ToArray() }).ToArray() };
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height }, ColorScheme = colors, ForcedColors = forced });
        var page = await Open(context, inventory, $"reflow-{width}-{height}-{colors}-{forced}");
        var row = Row(page, "projection-visual"); var inspect = row.Locator(".visual-inspect");
        await inspect.Locator("summary").PressAsync("Enter");
        await Row(page, "valid-actions").Locator(".visual-inspect > summary").PressAsync("Enter");
        await inspect.Locator("summary").FocusAsync(); await Settle(page);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        Assert.All(await inspect.Locator("summary, h5, p, li, dd").EvaluateAllAsync<double[]>("elements => elements.map(element => element.getBoundingClientRect().right)"), right => Assert.True(right <= width));
        Assert.Contains(longText, await inspect.InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains("Title text", await inspect.InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(2, await page.Locator(".page-card:not([hidden]) .visual-inspect[open]").CountAsync());
        await page.ScreenshotAsync(new() { Path = Path.Combine(Output, $"inspect-{width}-{height}-{colors}-{forced}.png") });
    }
}

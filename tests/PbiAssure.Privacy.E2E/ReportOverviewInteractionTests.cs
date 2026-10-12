using Microsoft.Playwright;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class ReportOverviewInteractionTests(PrivacyE2EFixture fixture)
{
    private static readonly string[] OverviewHeadings = ["Needs attention", "Model usage", "Checks", "Project snapshot", "Other reviews"];
    [Theory]
    [InlineData("DirectlyUsed", 18)]
    [InlineData("IndirectlyUsed", 23)]
    [InlineData("StructurallyRequired", 15)]
    [InlineData("UsedOnlyByUnusedBranch", 5)]
    [InlineData("ApparentlyUnused", 26)]
    public async Task UsageShortcutAppliesAuthoredScopeAndOpensOnlyMatchingObjects(string state, int expectedCount)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync(Render("pbi-assure-coverage"));
        await page.Locator($"#summary [data-usage-shortcut='{state}']").ClickAsync();
        Assert.True(await page.Locator("#semantic-usage").IsVisibleAsync());
        Assert.Equal(state, await page.Locator("#usage-usage-state").InputValueAsync());
        Assert.Equal("developer", await page.Locator("#usage-origin").InputValueAsync());
        var results = page.Locator(".semantic-object:not([hidden])");
        Assert.Equal(expectedCount, await results.CountAsync());
        Assert.Equal(expectedCount, await results.Filter(new() { Visible = true }).CountAsync());
        Assert.All(await results.EvaluateAllAsync<string[]>("items => items.map(item => item.dataset.usageState)"), value => Assert.Equal(state, value));
        Assert.EndsWith($"#semantic-usage?state={state}", page.Url, StringComparison.Ordinal);
        Assert.Equal("semantic-usage-heading", await page.EvaluateAsync<string>("document.activeElement.id"));
        await page.GoBackAsync();
        Assert.True(await page.Locator("#summary").IsVisibleAsync());
        await page.GoForwardAsync();
        Assert.Equal(state, await page.Locator("#usage-usage-state").InputValueAsync());
    }

    [Fact]
    public async Task ShortcutClearsConflictingSearchAndFacetsAndZeroCandidatesRemainEmpty()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync(Render("model-reference-context"));
        await page.GetByRole(AriaRole.Link, new() { Name = "Model", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Searchbox, new() { Name = "Search model objects" }).FillAsync("Category");
        await page.Locator("#semantic-usage summary").Filter(new() { HasText = "More filters" }).ClickAsync();
        await page.Locator("#usage-origin").SelectOptionAsync("system");
        await page.GetByRole(AriaRole.Link, new() { Name = "Overview", Exact = true }).ClickAsync();
        await page.Locator("#summary [data-usage-shortcut='ApparentlyUnused']").ClickAsync();
        Assert.Equal("", await page.Locator("#usage-search").InputValueAsync());
        Assert.Equal("developer", await page.Locator("#usage-origin").InputValueAsync());
        Assert.Equal("ApparentlyUnused", await page.Locator("#usage-usage-state").InputValueAsync());
        Assert.Equal(0, await page.Locator(".semantic-object:not([hidden])").CountAsync());
        Assert.True(await page.Locator("#usage-empty-state").IsVisibleAsync());
    }

    [Fact]
    public async Task KeyboardCanOpenCanonicalFindingsAndSecondaryReviewsWithVisibleFocus()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync(Render("pbi-assure-coverage"));
        Assert.Equal(0, await page.Locator("#summary a[href^='#finding-']").CountAsync());
        var finding = page.Locator("#summary").GetByRole(AriaRole.Link, new() { Name = "Review 28 findings", Exact = true });
        await finding.FocusAsync();
        Assert.Equal("2px", await finding.EvaluateAsync<string>("element => getComputedStyle(element).outlineWidth"));
        await page.Keyboard.PressAsync("Enter");
        Assert.True(await page.Locator("#findings").IsVisibleAsync());
        Assert.Equal("findings-heading", await page.EvaluateAsync<string>("document.activeElement.id"));
        foreach (var (label, section) in new[] { ("Accessibility review", "accessibility-review"), ("Theme review", "theme-review") })
        {
            await page.GetByRole(AriaRole.Link, new() { Name = "Overview", Exact = true }).ClickAsync();
            await page.Locator("#summary").GetByRole(AriaRole.Link, new() { Name = label, Exact = true }).FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            Assert.True(await page.Locator($"#{section}").IsVisibleAsync());
            Assert.Equal(section + "-heading", await page.EvaluateAsync<string>("document.activeElement.id"));
        }
    }

    [Theory]
    [InlineData("pbi-assure-coverage")]
    [InlineData("model-reference-context")]
    public async Task OverviewReflowsInBothThemesAndForcedColoursWithoutScriptErrors(string name)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await page.SetContentAsync(Render(name));
        var directory = Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-overview-rebalance");
        Directory.CreateDirectory(directory);
        Assert.Equal(OverviewHeadings,
            await page.Locator("#summary .summary-groups > section > h3").AllTextContentsAsync());
        foreach (var appearance in new[] { "Light", "Dark" })
        {
            await page.GetByRole(AriaRole.Button, new() { Name = appearance + " appearance", Exact = true }).ClickAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{name}-{appearance.ToLowerInvariant()}.png"), FullPage = true });
            await page.SetViewportSizeAsync(320, 900);
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            Assert.True(await page.Locator("#summary .summary-group-assurance").IsVisibleAsync());
            var tops = await page.Locator("#summary .summary-groups > section").EvaluateAllAsync<double[]>("sections => sections.map(section => section.getBoundingClientRect().top)");
            Assert.Equal(tops.Order().ToArray(), tops);
            var metrics = await page.Locator("#summary .overview-attention-metrics > div").EvaluateAllAsync<double[]>("items => items.map(item => item.getBoundingClientRect().top)");
            if (metrics.Length > 0) Assert.Equal(3, metrics.Distinct().Count());
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{name}-{appearance.ToLowerInvariant()}-320.png"), FullPage = true });
            await page.SetViewportSizeAsync(320, 225); // 1280 × 900 at 400% equivalent reflow.
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await page.SetViewportSizeAsync(1280, 1000);
        }
        await page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });
        Assert.True(await page.Locator("#summary .summary-group-assurance").IsVisibleAsync());
        await page.Locator("#summary [data-usage-shortcut='ApparentlyUnused']").FocusAsync();
        Assert.True(await page.Locator("#summary [data-usage-shortcut='ApparentlyUnused']").EvaluateAsync<bool>("element => parseFloat(getComputedStyle(element).outlineWidth) >= 2"));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZeroStatesKeepAccessibilityAndThemeAvailableInReviews(bool clearAccessibility)
    {
        var inventory = ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "model-reference-context"));
        if (clearAccessibility) inventory = inventory with { Findings = [] };
        var directory = Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-overview-rebalance");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, clearAccessibility ? "zero-observations.html" : "no-primary-findings.html");
        await File.WriteAllTextAsync(path, HtmlReportRenderer.Render(inventory));
        await using var context = await fixture.Browser.NewContextAsync(); var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(path).AbsoluteUri);
        Assert.Contains("No findings.", await page.Locator("#summary").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(0, await page.Locator("#summary").GetByRole(AriaRole.Link, new() { Name = "Review 0 findings", Exact = true }).CountAsync());
        Assert.Equal(0, await page.Locator("#summary a[href='#theme-review']").CountAsync());
        Assert.Contains("No identified limitations", await page.Locator("#summary .summary-group-confidence").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains(clearAccessibility ? "No accessibility observations found." : "5 accessibility observations",
            await page.Locator("#summary .summary-group-reviews").InnerTextAsync(), StringComparison.Ordinal);
        await page.GetByRole(AriaRole.Link, new() { Name = "Reviews", Exact = true }).ClickAsync();
        await page.Locator(".workspace-heading").GetByRole(AriaRole.Link, new() { Name = "Theme", Exact = true }).ClickAsync();
        Assert.True(await page.Locator("#theme-review").IsVisibleAsync());
    }

    private string Render(string name)
    {
        var html = HtmlReportRenderer.Render(ProjectScanner.Scan(Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", name)));
        var directory = Path.Combine(fixture.RepositoryRoot, "artifacts", "ux-overview-rebalance");
        Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, name + ".html"), html);
        return html;
    }
}

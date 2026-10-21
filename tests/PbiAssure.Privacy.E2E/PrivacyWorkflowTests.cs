using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class PrivacyWorkflowTests(PrivacyE2EFixture fixture)
{
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new() { WriteIndented = true };

    [Fact(Timeout = 240_000)]
    public async Task InformationRouteAndKeyboardNavigationWorkWithoutProjectState()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await LoadUntilReadyAsync(page);
        await page.WaitForFunctionAsync("document.activeElement?.id === 'page-title'");
        Assert.Equal("none", await page.Locator("#page-title").EvaluateAsync<string>("element => getComputedStyle(element).outlineStyle"));
        // Selection is the first task after the introductory heading.
        await page.Keyboard.PressAsync("Tab");
        var picker = page.GetByRole(AriaRole.Button, new() { Name = "Choose project folder", Exact = true });
        Assert.Equal("Choose project folder", await page.EvaluateAsync<string>("() => document.activeElement?.textContent?.trim()"));
        Assert.True(await picker.EvaluateAsync<bool>("element => element === document.activeElement && element.matches(':focus-visible')"));
        Assert.Equal("2px", await picker.EvaluateAsync<string>("element => getComputedStyle(element).outlineWidth"));
        await page.Locator("details.guidance-panel > summary").FocusAsync();
        Assert.Equal("2px", await page.Locator("details.guidance-panel > summary").EvaluateAsync<string>("element => getComputedStyle(element).outlineWidth"));
        await page.GotoAsync(fixture.BaseUrl + "/about");
        var heading = page.GetByRole(AriaRole.Heading, new() { Name = "What PBI Project Lens does", Exact = true });
        await heading.WaitForAsync();
        Assert.Equal("What PBI Project Lens does — PBI Project Lens", await page.TitleAsync());
        var navigation = page.GetByRole(AriaRole.Navigation, new() { Name = "Application" });
        Assert.Equal("page", await navigation.GetByRole(AriaRole.Link, new() { Name = "About", Exact = true }).GetAttributeAsync("aria-current"));
        await navigation.GetByRole(AriaRole.Link, new() { Name = "Analyse", Exact = true }).FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForFunctionAsync("document.activeElement?.id === 'page-title'");
        Assert.Equal("page", await navigation.GetByRole(AriaRole.Link, new() { Name = "Analyse", Exact = true }).GetAttributeAsync("aria-current"));
        var info = navigation.GetByRole(AriaRole.Link, new() { Name = "About", Exact = true });
        Assert.Null(await info.GetAttributeAsync("target"));
        await info.FocusAsync();
        Assert.Equal("solid", await info.EvaluateAsync<string>("element => getComputedStyle(element).outlineStyle"));
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForFunctionAsync("document.activeElement?.id === 'about-title'");
        Assert.Equal("none", await heading.EvaluateAsync<string>("element => getComputedStyle(element).outlineStyle"));
        Assert.Single(context.Pages);
    }

    [Fact(Timeout = 240_000)]
    public async Task InformationOpensSeparatelyAndPreservesSelectedProjectAndScan()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var monitor = new PrivacyNetworkMonitor(context, fixture.BaseUrl);
        await LoadUntilReadyAsync(page);
        await SelectFixtureAsync(page);
        monitor.Begin("Information");
        await AssertInformationPopupPreservesPageAsync(page);
        monitor.Begin("Scan");
        await page.GetByRole(AriaRole.Button, new() { Name = "Run analysis", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Analysis complete", Exact = true }).WaitForAsync();
        var counts = await ReadCompletionAsync(page);
        monitor.Begin("Information");
        await AssertInformationPopupPreservesPageAsync(page);
        Assert.Equal(counts, await ReadCompletionAsync(page));
        Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = "Open investigation", Exact = true }).IsVisibleAsync());
        Assert.Empty(monitor.ExternalEvents());
        Assert.Empty(monitor.CanaryLeaks());
        Assert.Empty(monitor.UnexpectedEvents());
        Assert.All(monitor.Events.Where(item => item.Phase == "Information"), item =>
        {
            Assert.Equal("GET", item.Method);
            var path = new Uri(item.Url).AbsolutePath;
            Assert.True(path is "/about" or "/css/core.css" or "/css/app.css" or "/favicon.svg" or
                "/appearance.js" or "/project-picker.js" or "/download.js" or "/analysis-focus.js" or "/investigation.js" or "/investigation/mark.svg" or "/css/site.css" ||
                path.StartsWith("/_framework/", StringComparison.Ordinal), item.Url);
        });
    }

    private async Task AssertInformationPopupPreservesPageAsync(IPage page)
    {
        var link = page.Locator(".app-navigation a[href='about']");
        Assert.Equal("_blank", await link.GetAttributeAsync("target"));
        Assert.Equal("noopener noreferrer", await link.GetAttributeAsync("rel"));
        await link.FocusAsync();
        var popup = await page.RunAndWaitForPopupAsync(() => page.Keyboard.PressAsync("Enter"));
        await popup.GetByRole(AriaRole.Heading, new() { Name = "What PBI Project Lens does", Exact = true }).WaitForAsync();
        await popup.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Equal(fixture.BaseUrl + "/about", popup.Url);
        Assert.True(await popup.EvaluateAsync<bool>("window.opener === null"));
        Assert.DoesNotContain(PrivacyCanaries.ProjectName, await popup.Locator("body").InnerTextAsync(), StringComparison.Ordinal);
        await popup.CloseAsync();
        Assert.Equal(fixture.BaseUrl + "/", page.Url);
        Assert.True(await page.GetByRole(AriaRole.Heading, new() { Name = PrivacyCanaries.ProjectName, Exact = true }).IsVisibleAsync());
    }

    [Fact(Timeout = 240_000)]
    public async Task OnlineWorkflowProducesNoUnexpectedPostStartupNetworkTraffic()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true,
        });
        var page = await context.NewPageAsync();
        var monitor = new PrivacyNetworkMonitor(context, fixture.BaseUrl);
        await LoadUntilReadyAsync(page);

        monitor.Begin("Scan");
        await SelectFixtureAndScanAsync(page);
        Assert.Empty(monitor.Events);

        monitor.Begin("Viewer");
        await OpenAndExerciseReportAsync(page);
        await OpenAndExerciseUnusedReviewAsync(page);

        await AssertViewerHeadersAsync();
        Assert.Empty(monitor.UnexpectedEvents());

        monitor.Begin("Exports");
        var outputRoot = PrepareOutputDirectory("online");
        await page.Locator("details.download-options > summary").ClickAsync();
        var htmlDownload = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Download report", Exact = true }).ClickAsync());
        var htmlPath = Path.Combine(outputRoot, htmlDownload.SuggestedFilename);
        await htmlDownload.SaveAsAsync(htmlPath);
        var unusedReviewPath = await DownloadUnusedReviewAsync(page, outputRoot);
        await page.Locator("details.legacy-output > summary").ClickAsync();
        var csvDownload = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Download semantic usage CSV", Exact = true }).ClickAsync());
        var csvPath = Path.Combine(outputRoot, csvDownload.SuggestedFilename);
        await csvDownload.SaveAsAsync(csvPath);
        var dataCataloguePath = await DownloadExportAsync(page, outputRoot, "Data catalogue", "data-catalogue");
        var usageMappingPath = await DownloadExportAsync(page, outputRoot, "Usage mapping", "usage-mapping");

        Assert.Contains(PrivacyCanaries.ProjectName, await File.ReadAllTextAsync(htmlPath), StringComparison.Ordinal);
        Assert.Contains(PrivacyCanaries.ModelName, await File.ReadAllTextAsync(csvPath), StringComparison.Ordinal);
        await AssertBrowserCsvAsync(dataCataloguePath);
        await AssertBrowserCsvAsync(usageMappingPath);
        await AssertProjectStorageIsEmptyAsync(context, page);
        Assert.Empty(monitor.ExternalEvents());
        Assert.Empty(monitor.CanaryLeaks());
        Assert.Empty(monitor.UnexpectedEvents());

        var counts = await ReadCompletionAsync(page);
        await WriteEvidenceAsync("online.json", new
        {
            fixture.SourceRevision,
            fixture.VerificationSourceHasChanges,
            DisplayedBuild = (await page.Locator("footer.app-footer").InnerTextAsync()).Trim(),
            Browser = fixture.Browser.Version,
            Fixture = PrivacyCanaries.ProjectName,
            Counts = counts,
            Mode = fixture.IsDeployedSmoke ? "deployed" : "local",
            ScanAndExportNetworkRequests = monitor.Events.Count(item => item.Phase is "Scan" or "Exports"),
            ViewerStaticRequests = monitor.Events.Count(item => item.Phase == "Viewer"),
            UnexpectedRequests = monitor.UnexpectedEvents().Count,
            ExternalRequests = monitor.ExternalEvents().Count,
            CanaryLeaks = monitor.CanaryLeaks().Count,
            Scan = "passed",
            Viewer = "passed",
            Html = Path.GetFileName(htmlPath),
            ApparentlyUnusedHtml = Path.GetFileName(unusedReviewPath),
            ProjectStorage = "no cookies, project storage or IndexedDB databases; appearance preference only",
            Csv = Path.GetFileName(csvPath),
            DataCatalogueCsv = Path.GetFileName(dataCataloguePath),
            UsageMappingCsv = Path.GetFileName(usageMappingPath),
            TimestampUtc = DateTimeOffset.UtcNow,
        });
    }

    [Fact(Timeout = 240_000)]
    public async Task OfflineWorkflowCompletesAfterApplicationStartup()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true,
        });
        var page = await context.NewPageAsync();
        var monitor = new PrivacyNetworkMonitor(context, fixture.BaseUrl);
        await LoadUntilReadyAsync(page);

        await context.SetOfflineAsync(true);
        monitor.Begin("Scan");
        await SelectFixtureAndScanAsync(page);

        monitor.Begin("Exports");
        var outputRoot = PrepareOutputDirectory("offline");
        await page.Locator("details.download-options > summary").ClickAsync();
        var htmlDownload = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Download report", Exact = true }).ClickAsync());
        var htmlPath = Path.Combine(outputRoot, htmlDownload.SuggestedFilename);
        await htmlDownload.SaveAsAsync(htmlPath);
        var unusedReviewPath = await DownloadUnusedReviewAsync(page, outputRoot);
        await page.Locator("details.legacy-output > summary").ClickAsync();
        var csvDownload = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Download semantic usage CSV", Exact = true }).ClickAsync());
        var csvPath = Path.Combine(outputRoot, csvDownload.SuggestedFilename);
        await csvDownload.SaveAsAsync(csvPath);
        var dataCataloguePath = await DownloadExportAsync(page, outputRoot, "Data catalogue", "data-catalogue");
        var usageMappingPath = await DownloadExportAsync(page, outputRoot, "Usage mapping", "usage-mapping");

        Assert.True(File.Exists(htmlPath));
        Assert.True(File.Exists(csvPath));
        Assert.Contains(PrivacyCanaries.ProjectName, await File.ReadAllTextAsync(htmlPath), StringComparison.Ordinal);
        Assert.Contains(PrivacyCanaries.ModelName, await File.ReadAllTextAsync(csvPath), StringComparison.Ordinal);
        await AssertBrowserCsvAsync(dataCataloguePath);
        await AssertBrowserCsvAsync(usageMappingPath);
        await AssertProjectStorageIsEmptyAsync(context, page);

        monitor.Begin("Offline report");
        var standaloneReport = await context.NewPageAsync();
        await standaloneReport.GotoAsync(new Uri(htmlPath).AbsoluteUri);
        await ExerciseReportContentAsync(standaloneReport);
        await standaloneReport.CloseAsync();
        var standaloneReview = await context.NewPageAsync();
        await standaloneReview.GotoAsync(new Uri(unusedReviewPath).AbsoluteUri);
        await ExerciseUnusedReviewAsync(standaloneReview);
        await standaloneReview.CloseAsync();

        Assert.Empty(monitor.ExternalEvents());
        Assert.Empty(monitor.CanaryLeaks());
        Assert.DoesNotContain(monitor.Events, item => item.Phase is "Scan" or "Exports");
        Assert.Empty(monitor.UnexpectedEvents());

        var counts = await ReadCompletionAsync(page);
        await WriteEvidenceAsync("offline.json", new
        {
            fixture.SourceRevision,
            fixture.VerificationSourceHasChanges,
            DisplayedBuild = (await page.Locator("footer.app-footer").InnerTextAsync()).Trim(),
            Browser = fixture.Browser.Version,
            Fixture = PrivacyCanaries.ProjectName,
            Counts = counts,
            Mode = fixture.IsDeployedSmoke ? "deployed" : "local",
            NetworkDisabledAfterReady = true,
            ExternalRequests = monitor.ExternalEvents().Count,
            CanaryLeaks = monitor.CanaryLeaks().Count,
            Scan = "passed",
            Html = Path.GetFileName(htmlPath),
            ApparentlyUnusedHtml = Path.GetFileName(unusedReviewPath),
            ProjectStorage = "no cookies, project storage or IndexedDB databases; appearance preference only",
            Csv = Path.GetFileName(csvPath),
            DataCatalogueCsv = Path.GetFileName(dataCataloguePath),
            UsageMappingCsv = Path.GetFileName(usageMappingPath),
            Viewer = "downloaded standalone report opened and exercised locally while offline",
            TimestampUtc = DateTimeOffset.UtcNow,
        });
    }

    private async Task LoadUntilReadyAsync(IPage page)
    {
        await page.GotoAsync(fixture.BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.Locator("[data-pbiassure-app-ready='true']")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new() { Name = "Export data", Exact = true }).CountAsync());
    }

    private async Task SelectFixtureAndScanAsync(IPage page)
    {
        await SelectFixtureAsync(page);
        await page.GetByRole(AriaRole.Button, new() { Name = "Run analysis", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Analysis complete", Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = "Open investigation", Exact = true }).IsVisibleAsync());
    }

    private async Task SelectFixtureAsync(IPage page)
    {
        await page.Locator("details.picker-help > summary").ClickAsync();
        var chooser = await page.RunAndWaitForFileChooserAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Use alternative folder picker", Exact = true }).ClickAsync());
        await chooser.SetFilesAsync(fixture.FixtureDirectory);
        await page.GetByRole(AriaRole.Heading, new() { Name = PrivacyCanaries.ProjectName, Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    private static async Task OpenAndExerciseReportAsync(IPage page)
    {
        var popup = await page.RunAndWaitForPopupAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Open investigation", Exact = true }).ClickAsync());
        await popup.Locator("#workspace-heading").WaitForAsync();
        Assert.Equal("Overview", await popup.Locator("#workspace-heading").InnerTextAsync());
        Assert.Contains("/investigation/", popup.Url, StringComparison.Ordinal);
        await popup.Locator(".primary-nav [data-view='objects']").ClickAsync();
        await popup.Locator("#search").FillAsync("PBIASSURE_NO_MATCH_7F3C2A");
        Assert.Equal(0, await popup.Locator("[data-object]").CountAsync());
        await popup.Locator("#search").FillAsync(string.Empty);
        await popup.CloseAsync();
    }

    private static async Task<string> DownloadExportAsync(IPage page, string outputRoot, string preset, string filenameSuffix)
    {
        if (!await page.GetByRole(AriaRole.Heading, new() { Name = "Export CSV", Exact = true }).IsVisibleAsync())
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Export data", Exact = true }).ClickAsync();
        }

        await page.GetByRole(AriaRole.Radio, new() { Name = preset, Exact = true }).CheckAsync();
        await page.GetByText(preset == "Data catalogue"
                ? "One row per model column or measure, including usage state, user-facing evidence and report/page/visual counts."
                : "One row per logical direct report usage, showing where and how a model column or measure is used.",
            new() { Exact = true }).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        if (preset == "Data catalogue")
        {
            var optional = page.GetByRole(AriaRole.Checkbox, new() { Name = "ReportNames", Exact = true });
            Assert.False(await optional.IsCheckedAsync());
            await optional.CheckAsync();
            Assert.True(await optional.IsCheckedAsync());
            await optional.UncheckAsync();
            Assert.False(await optional.IsCheckedAsync());
        }
        else
        {
            Assert.True(await page.Locator("#export-column-Report").IsCheckedAsync());
            Assert.Equal(0, await page.Locator("#export-column-DirectUsageCount").CountAsync());
        }
        var download = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Download CSV", Exact = true }).ClickAsync());
        Assert.EndsWith($".{filenameSuffix}.csv", download.SuggestedFilename, StringComparison.Ordinal);
        var path = Path.Combine(outputRoot, download.SuggestedFilename);
        await download.SaveAsAsync(path);
        return path;
    }

    private static async Task OpenAndExerciseUnusedReviewAsync(IPage page)
    {
        var popup = await page.RunAndWaitForPopupAsync(() =>
            page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Review apparently unused ") }).ClickAsync());
        await popup.Locator("#usage-filter").WaitForAsync();
        Assert.Equal("ApparentlyUnused", await popup.Locator("#usage-filter").InputValueAsync());
        Assert.Contains("/investigation/", popup.Url, StringComparison.Ordinal);
        Assert.True(await popup.EvaluateAsync<bool>("window.opener !== null"));
        await popup.CloseAsync();
    }

    private static async Task<string> DownloadUnusedReviewAsync(IPage page, string outputRoot)
    {
        var download = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Download apparently unused review", Exact = true }).ClickAsync());
        Assert.EndsWith(".apparently-unused.html", download.SuggestedFilename, StringComparison.Ordinal);
        var path = Path.Combine(outputRoot, download.SuggestedFilename);
        await download.SaveAsAsync(path);
        var html = await File.ReadAllTextAsync(path);
        Assert.Contains(PrivacyCanaries.ProjectName, html, StringComparison.Ordinal);
        Assert.Contains("CanaryValue", html, StringComparison.Ordinal);
        return path;
    }

    private static async Task ExerciseUnusedReviewAsync(IPage review)
    {
        await review.Locator("body.unused-review").WaitForAsync();
        Assert.Contains(PrivacyCanaries.ProjectName, await review.Locator("body").InnerTextAsync(), StringComparison.Ordinal);
        Assert.True(await review.GetByText("CanaryValue", new() { Exact = true }).IsVisibleAsync());
        await review.Locator("#review-search").FillAsync("PBIASSURE_NO_MATCH_7F3C2A");
        await review.WaitForFunctionAsync("document.querySelectorAll('.review-row:not([hidden])').length === 0");
        await review.Locator("#review-search").FillAsync(string.Empty);
        Assert.True(await review.GetByText("CanaryValue", new() { Exact = true }).IsVisibleAsync());
        // Exercise the shared appearance path too: only this non-project preference may persist.
        await review.GetByRole(AriaRole.Button, new() { Name = "Dark appearance", Exact = true }).ClickAsync();
        await review.GetByRole(AriaRole.Button, new() { Name = "Match system appearance", Exact = true }).ClickAsync();
    }

    private static async Task AssertProjectStorageIsEmptyAsync(IBrowserContext context, IPage page)
    {
        Assert.Empty(await context.CookiesAsync());
        var unexpectedStorage = await page.EvaluateAsync<string[]>("""
            () => [
              ...Object.keys(localStorage).filter(key => key !== 'pbiassure-appearance'),
              ...Object.keys(sessionStorage)
            ]
            """);
        Assert.Empty(unexpectedStorage);
        var appearance = await page.EvaluateAsync<string?>("localStorage.getItem('pbiassure-appearance')");
        Assert.True(appearance is null or "light" or "dark");
        Assert.Empty(await page.EvaluateAsync<string[]>("async () => (await indexedDB.databases()).map(database => database.name)"));
        Assert.Empty(await page.EvaluateAsync<string[]>("async () => (await navigator.serviceWorker.getRegistrations()).map(registration => registration.scope)"));
    }

    private static async Task AssertBrowserCsvAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Contains(PrivacyCanaries.ModelName, await File.ReadAllTextAsync(path), StringComparison.Ordinal);
    }

    private static async Task ExerciseReportContentAsync(IPage report)
    {
        await report.Locator(".section-nav a[href='#summary']").ClickAsync();
        await report.GetByRole(AriaRole.Heading, new() { Name = "Overview", Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var limits = report.Locator("#summary details.scope");
        Assert.Null(await limits.GetAttributeAsync("open"));
        await limits.Locator("summary").PressAsync("Enter");
        Assert.NotNull(await limits.GetAttributeAsync("open"));
        Assert.Contains("It requires review before any removal.", await limits.InnerTextAsync(), StringComparison.Ordinal);
        await limits.Locator("summary").PressAsync("Enter");
        Assert.Null(await limits.GetAttributeAsync("open"));
        var fontFamily = await report.EvaluateAsync<string>("() => getComputedStyle(document.body).fontFamily");
        Assert.Contains("Segoe UI", fontFamily, StringComparison.OrdinalIgnoreCase);
        await report.Locator(".section-nav a[href='#findings']").ClickAsync();
        var findingSearch = report.Locator("#finding-search");
        if (await findingSearch.CountAsync() > 0)
        {
            await findingSearch.FillAsync("PBIASSURE_NO_MATCH_7F3C2A");
            await report.Locator("#finding-empty-state:not([hidden])")
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await findingSearch.FillAsync(string.Empty);
            return;
        }

        await report.Locator(".object-view-nav a[href='#accessibility-review']").ClickAsync();
        await report.Locator("#accessibility-review-heading")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await report.Locator(
            "#accessibility-review .accessibility-summary, #accessibility-review .section-empty-state").CountAsync() > 0);
    }

    private async Task AssertViewerHeadersAsync()
    {
        await using var api = await fixture.Playwright.APIRequest.NewContextAsync();
        var appResponse = await api.GetAsync(fixture.BaseUrl);
        Assert.True(appResponse.Ok);
        Assert.Contains("connect-src 'self'", appResponse.Headers["content-security-policy"], StringComparison.Ordinal);
        Assert.Equal("no-referrer", appResponse.Headers["referrer-policy"]);
        Assert.Equal("nosniff", appResponse.Headers["x-content-type-options"]);
        // Catch hosting-injected analytics as well as repository script changes.
        var appHtml = await appResponse.TextAsync();
        Assert.DoesNotContain("cloudflareinsights", appHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("zaraz", appHtml, StringComparison.OrdinalIgnoreCase);
        var viewerResponse = await api.GetAsync($"{fixture.BaseUrl}/investigation/index.html");
        Assert.True(viewerResponse.Ok);
        Assert.Equal("no-cache", viewerResponse.Headers["cache-control"]);
        var csp = viewerResponse.Headers["content-security-policy"];
        Assert.Contains("script-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("connect-src 'self'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-inline'", csp, StringComparison.Ordinal);

    }

    private static async Task<Dictionary<string, string>> ReadCompletionAsync(IPage page)
    {
        var counts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Project"] = (await page.Locator("#page-title").InnerTextAsync()).Trim(),
            ["Report"] = (await page.GetByRole(AriaRole.Button, new() { Name = "Open investigation", Exact = true }).InnerTextAsync()).Trim(),
        };

        return counts;
    }

    private string PrepareOutputDirectory(string name)
    {
        var path = Path.Combine(fixture.OutputDirectory, name);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
        return path;
    }

    private async Task WriteEvidenceAsync(string fileName, object evidence)
    {
        var mode = fixture.IsDeployedSmoke ? "deployed" : "local";
        var path = Path.Combine(fixture.EvidenceDirectory, $"{mode}-{fileName}");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(evidence, EvidenceJsonOptions));
    }
}

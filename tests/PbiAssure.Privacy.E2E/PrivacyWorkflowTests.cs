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
        await page.Keyboard.PressAsync("Tab");
        Assert.True(await page.Locator("details.guidance-panel > summary").EvaluateAsync<bool>("element => element.matches(':focus-visible')"));
        Assert.Equal("2px", await page.Locator("details.guidance-panel > summary").EvaluateAsync<string>("element => getComputedStyle(element).outlineWidth"));
        await page.Keyboard.PressAsync("Tab");
        var picker = page.GetByRole(AriaRole.Button, new() { Name = "Choose Power BI project", Exact = true });
        Assert.True(await picker.EvaluateAsync<bool>("element => element.matches(':focus-visible')"));
        Assert.Equal("2px", await picker.EvaluateAsync<string>("element => getComputedStyle(element).outlineWidth"));
        await page.GotoAsync(fixture.BaseUrl + "/about");
        var heading = page.GetByRole(AriaRole.Heading, new() { Name = "What PBI Assure does", Exact = true });
        await heading.WaitForAsync();
        Assert.Equal("What PBI Assure does — PBI Assure", await page.TitleAsync());
        var navigation = page.GetByRole(AriaRole.Navigation, new() { Name = "Application" });
        Assert.Equal("page", await navigation.GetByRole(AriaRole.Link, new() { Name = "What PBI Assure does", Exact = true }).GetAttributeAsync("aria-current"));
        await navigation.GetByRole(AriaRole.Link, new() { Name = "Analyse", Exact = true }).FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForFunctionAsync("document.activeElement?.id === 'page-title'");
        Assert.Equal("page", await navigation.GetByRole(AriaRole.Link, new() { Name = "Analyse", Exact = true }).GetAttributeAsync("aria-current"));
        var info = navigation.GetByRole(AriaRole.Link, new() { Name = "What PBI Assure does", Exact = true });
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
        await page.GetByRole(AriaRole.Button, new() { Name = "Run assurance", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Assurance summary", Exact = true }).WaitForAsync();
        var counts = await ReadAssuranceCountsAsync(page);
        monitor.Begin("Information");
        await AssertInformationPopupPreservesPageAsync(page);
        Assert.Equal(counts, await ReadAssuranceCountsAsync(page));
        Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = "Export data", Exact = true }).IsVisibleAsync());
        Assert.Empty(monitor.ExternalEvents());
        Assert.Empty(monitor.CanaryLeaks());
        Assert.Empty(monitor.UnexpectedEvents());
        Assert.All(monitor.Events.Where(item => item.Phase == "Information"), item =>
        {
            Assert.Equal("GET", item.Method);
            var path = new Uri(item.Url).AbsolutePath;
            Assert.True(path is "/about" or "/css/core.css" or "/css/app.css" or "/favicon.svg" or
                "/appearance.js" or "/project-picker.js" or "/download.js" ||
                path.StartsWith("/_framework/", StringComparison.Ordinal), item.Url);
        });
    }

    private async Task AssertInformationPopupPreservesPageAsync(IPage page)
    {
        var link = page.GetByRole(AriaRole.Link, new() { Name = "What PBI Assure does (opens in new tab)", Exact = true });
        Assert.Equal("_blank", await link.GetAttributeAsync("target"));
        Assert.Equal("noopener noreferrer", await link.GetAttributeAsync("rel"));
        await link.FocusAsync();
        var popup = await page.RunAndWaitForPopupAsync(() => page.Keyboard.PressAsync("Enter"));
        await popup.GetByRole(AriaRole.Heading, new() { Name = "What PBI Assure does", Exact = true }).WaitForAsync();
        await popup.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Equal(fixture.BaseUrl + "/about", popup.Url);
        Assert.True(await popup.EvaluateAsync<bool>("window.opener === null"));
        Assert.DoesNotContain(PrivacyCanaries.ProjectName, await popup.Locator("body").InnerTextAsync(), StringComparison.Ordinal);
        await popup.CloseAsync();
        Assert.Equal(fixture.BaseUrl + "/", page.Url);
        Assert.True(await page.GetByText($"Selected project: {PrivacyCanaries.ProjectName}", new() { Exact = true }).IsVisibleAsync());
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

        await AssertViewerHeadersAsync();
        Assert.Empty(monitor.UnexpectedEvents());

        monitor.Begin("Exports");
        var outputRoot = PrepareOutputDirectory("online");
        var htmlDownload = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Download HTML report", Exact = true }).ClickAsync());
        var htmlPath = Path.Combine(outputRoot, htmlDownload.SuggestedFilename);
        await htmlDownload.SaveAsAsync(htmlPath);
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
        Assert.Empty(monitor.ExternalEvents());
        Assert.Empty(monitor.CanaryLeaks());
        Assert.Empty(monitor.UnexpectedEvents());

        var counts = await ReadAssuranceCountsAsync(page);
        await WriteEvidenceAsync("online.json", new
        {
            fixture.SourceRevision,
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
        var htmlDownload = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Download HTML report", Exact = true }).ClickAsync());
        var htmlPath = Path.Combine(outputRoot, htmlDownload.SuggestedFilename);
        await htmlDownload.SaveAsAsync(htmlPath);
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

        monitor.Begin("Offline report");
        var standaloneReport = await context.NewPageAsync();
        await standaloneReport.GotoAsync(new Uri(htmlPath).AbsoluteUri);
        await ExerciseReportContentAsync(standaloneReport);
        await standaloneReport.CloseAsync();

        Assert.Empty(monitor.ExternalEvents());
        Assert.Empty(monitor.CanaryLeaks());
        Assert.DoesNotContain(monitor.Events, item => item.Phase is "Scan" or "Exports");
        Assert.Empty(monitor.UnexpectedEvents());

        var counts = await ReadAssuranceCountsAsync(page);
        await WriteEvidenceAsync("offline.json", new
        {
            fixture.SourceRevision,
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
        await page.GetByRole(AriaRole.Button, new() { Name = "Run assurance", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Assurance summary", Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Contains("Your model objects", await page.Locator("main").InnerTextAsync(), StringComparison.Ordinal);
    }

    private async Task SelectFixtureAsync(IPage page)
    {
        await page.Locator("details.picker-help > summary").ClickAsync();
        var chooser = await page.RunAndWaitForFileChooserAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Use alternative folder picker", Exact = true }).ClickAsync());
        await chooser.SetFilesAsync(fixture.FixtureDirectory);
        await page.GetByText($"Selected project: {PrivacyCanaries.ProjectName}", new() { Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    private static async Task OpenAndExerciseReportAsync(IPage page)
    {
        var popup = await page.RunAndWaitForPopupAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Open interactive report", Exact = true }).ClickAsync());
        await popup.GetByRole(AriaRole.Heading, new() { Name = PrivacyCanaries.ProjectName, Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Contains("/report-viewer", popup.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("report-viewer.html", popup.Url, StringComparison.Ordinal);
        await ExerciseReportContentAsync(popup);
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

    private static async Task AssertBrowserCsvAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Contains(PrivacyCanaries.ModelName, await File.ReadAllTextAsync(path), StringComparison.Ordinal);
    }

    private static async Task ExerciseReportContentAsync(IPage report)
    {
        await report.Locator("a[href='#summary']").ClickAsync();
        await report.GetByRole(AriaRole.Heading, new() { Name = "Summary", Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var limits = report.Locator("#summary details.scope");
        Assert.Null(await limits.GetAttributeAsync("open"));
        await limits.Locator("summary").PressAsync("Enter");
        Assert.NotNull(await limits.GetAttributeAsync("open"));
        Assert.Contains("It does not mean the object is safe to delete.", await limits.InnerTextAsync(), StringComparison.Ordinal);
        await limits.Locator("summary").PressAsync("Enter");
        Assert.Null(await limits.GetAttributeAsync("open"));
        var fontFamily = await report.EvaluateAsync<string>("() => getComputedStyle(document.body).fontFamily");
        Assert.Contains("Segoe UI", fontFamily, StringComparison.OrdinalIgnoreCase);
        await report.Locator("a[href='#findings']").ClickAsync();
        var findingSearch = report.Locator("#finding-search");
        if (await findingSearch.CountAsync() > 0)
        {
            await findingSearch.FillAsync("PBIASSURE_NO_MATCH_7F3C2A");
            await report.Locator("#finding-empty-state:not([hidden])")
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await findingSearch.FillAsync(string.Empty);
            return;
        }

        await report.Locator("a[href='#accessibility-review']").ClickAsync();
        await report.Locator("#accessibility-review-heading")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await report.Locator(
            "#accessibility-review .accessibility-summary, #accessibility-review .section-empty-state").CountAsync() > 0);
    }

    private async Task AssertViewerHeadersAsync()
    {
        await using var api = await fixture.Playwright.APIRequest.NewContextAsync();
        var htmlResponse = await api.GetAsync(
            $"{fixture.BaseUrl}/report-viewer.html",
            new APIRequestContextOptions { MaxRedirects = 0 });
        Assert.Equal(308, htmlResponse.Status);
        Assert.EndsWith("/report-viewer", htmlResponse.Headers["location"], StringComparison.Ordinal);

        var viewerResponse = await api.GetAsync($"{fixture.BaseUrl}/report-viewer");
        Assert.True(viewerResponse.Ok);
        var csp = viewerResponse.Headers["content-security-policy"];
        Assert.Contains("default-src 'none'", csp, StringComparison.Ordinal);
        Assert.Contains("connect-src 'none'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("connect-src 'self'", csp, StringComparison.Ordinal);
    }

    private static async Task<Dictionary<string, string>> ReadAssuranceCountsAsync(IPage page)
    {
        var counts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var label in new[] { "Errors", "Warnings", "Review required", "Total findings" })
        {
            var metric = page.Locator("article.metric").Filter(new LocatorFilterOptions
            {
                Has = page.GetByText(label, new() { Exact = true }),
            });
            counts[label] = (await metric.First.Locator("strong").InnerTextAsync()).Trim();
        }

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
        var path = Path.Combine(fixture.EvidenceDirectory, fileName);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(evidence, EvidenceJsonOptions));
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class PrivacyWorkflowTests(PrivacyE2EFixture fixture)
{
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new() { WriteIndented = true };

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
        var csvDownload = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Download semantic CSV", Exact = true }).ClickAsync());
        var csvPath = Path.Combine(outputRoot, csvDownload.SuggestedFilename);
        await csvDownload.SaveAsAsync(csvPath);

        Assert.Contains(PrivacyCanaries.ProjectName, await File.ReadAllTextAsync(htmlPath), StringComparison.Ordinal);
        Assert.Contains(PrivacyCanaries.ModelName, await File.ReadAllTextAsync(csvPath), StringComparison.Ordinal);
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
        var csvDownload = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Download semantic CSV", Exact = true }).ClickAsync());
        var csvPath = Path.Combine(outputRoot, csvDownload.SuggestedFilename);
        await csvDownload.SaveAsAsync(csvPath);

        Assert.True(File.Exists(htmlPath));
        Assert.True(File.Exists(csvPath));
        Assert.Contains(PrivacyCanaries.ProjectName, await File.ReadAllTextAsync(htmlPath), StringComparison.Ordinal);
        Assert.Contains(PrivacyCanaries.ModelName, await File.ReadAllTextAsync(csvPath), StringComparison.Ordinal);

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
    }

    private async Task SelectFixtureAndScanAsync(IPage page)
    {
        await page.Locator("details.picker-help > summary").ClickAsync();
        var chooser = await page.RunAndWaitForFileChooserAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Use alternative folder picker", Exact = true }).ClickAsync());
        await chooser.SetFilesAsync(fixture.FixtureDirectory);
        await page.GetByText($"Selected project: {PrivacyCanaries.ProjectName}", new() { Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.GetByRole(AriaRole.Button, new() { Name = "Run assurance", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Assurance summary", Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Contains("Your model objects", await page.Locator("main").InnerTextAsync(), StringComparison.Ordinal);
    }

    private static async Task OpenAndExerciseReportAsync(IPage page)
    {
        var popup = await page.RunAndWaitForPopupAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Open HTML report", Exact = true }).ClickAsync());
        await popup.GetByRole(AriaRole.Heading, new() { Name = PrivacyCanaries.ProjectName, Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Contains("/report-viewer", popup.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("report-viewer.html", popup.Url, StringComparison.Ordinal);
        await ExerciseReportContentAsync(popup);
        await popup.CloseAsync();
    }

    private static async Task ExerciseReportContentAsync(IPage report)
    {
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

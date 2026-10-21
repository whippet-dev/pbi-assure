using Microsoft.Playwright;

namespace PbiAssure.Privacy.E2E;

[Collection(PrivacyE2EGroup.Name)]
public sealed class FrontDoorJourneyTests(PrivacyE2EFixture fixture)
{
    [Theory(Timeout = 240_000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletionDeliversRealAnalysisToModernViewer(bool scope)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1366, Height = 768 } });
        var page = await Open(context);
        await SelectSnapshot(page, Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "pbi-assure-coverage"));
        await Run(page);
        var errors = new List<string>();
        var requests = new List<string>();
        context.Page += (_, opened) => opened.PageError += (_, error) => errors.Add(error);
        context.Request += (_, request) => requests.Add(request.Url);
        var viewer = await page.RunAndWaitForPopupAsync(() => page.GetByRole(AriaRole.Button,
            new() { Name = scope ? "Review analysis scope" : "Open investigation", Exact = true }).ClickAsync());
        await viewer.Locator("#workspace-heading").WaitForAsync();
        await page.GetByText("Investigation opened in a new tab.", new() { Exact = false }).WaitForAsync();
        Assert.Contains(scope ? "Analysis scope" : "Overview", await viewer.Locator("#workspace-heading").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pbi-assure-coverage", await viewer.Locator(".project-name").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fixture", await viewer.Locator(".sidebar > .source-label").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("/investigation/index.html", new Uri(viewer.Url).AbsolutePath);
        Assert.True(await viewer.GetByRole(AriaRole.Link, new() { Name = "Return to project", Exact = true }).Last.IsVisibleAsync());
        Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = "Choose folder again to analyse", Exact = true }).IsEnabledAsync());
        Assert.Empty(errors);
        Assert.All(requests, url => Assert.StartsWith(new Uri(fixture.BaseUrl).GetLeftPart(UriPartial.Authority), url));
        Assert.DoesNotContain(requests, url => url.Contains("coverage.json") || url.Contains("report-viewer"));
        Assert.Equal(0, await viewer.EvaluateAsync<int>("localStorage.length + sessionStorage.length"));
        Assert.True(await viewer.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        await Screenshot(viewer, scope ? "handoff-scope.png" : "handoff-overview.png", false);
    }

    [Fact(Timeout = 240_000)]
    public async Task DirectViewerUrlExplainsMissingAnalysisAndBlockedPopupKeepsProject()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context);
        var direct = await context.NewPageAsync();
        await direct.GotoAsync(fixture.BaseUrl + "/investigation/index.html");
        await direct.GetByRole(AriaRole.Heading, new() { Name = "Could not open the investigation" }).WaitForAsync();
        Assert.Contains("does not contain a saved analysis", await direct.Locator("body").InnerTextAsync());
        await SelectSnapshot(page, fixture.FixtureDirectory);
        await Run(page);
        await page.EvaluateAsync("window.open = () => null");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open investigation", Exact = true }).ClickAsync();
        await page.GetByText("Your browser blocked the investigation tab.", new() { Exact = false }).WaitForAsync();
        Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = "Open investigation", Exact = true }).IsEnabledAsync());
    }

    [Fact(Timeout = 240_000)]
    public async Task FirstArrivalPutsSelectionBeforeHelpAndExplainsOutput()
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 900 } });
        var page = await Open(context);
        var picker = page.GetByRole(AriaRole.Button, new() { Name = "Choose project folder", Exact = true });
        Assert.Equal("folder-selection-help", await picker.GetAttributeAsync("aria-describedby"));
        Assert.Contains("then run the analysis", await page.Locator("#folder-selection-help").InnerTextAsync());
        Assert.Contains("Your project is never modified or uploaded", await page.Locator(".privacy").InnerTextAsync());
        Assert.True(await picker.EvaluateAsync<bool>("e => e.getBoundingClientRect().top < 400"));
        await page.Keyboard.PressAsync("Tab");
        Assert.True(await picker.EvaluateAsync<bool>("e => e === document.activeElement"));
        await Screenshot(page, "front-door-1440.png", false);
        await page.Locator("details.guidance-panel > summary").FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        Assert.True(await page.Locator("details.guidance-panel").EvaluateAsync<bool>("e => e.open"));
    }

    [Theory(Timeout = 240_000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunningStatusAndCompletionRespectFocus(bool moveElsewhere)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context);
        await SelectSnapshot(page, fixture.FixtureDirectory);
        await page.EvaluateAsync("""
            () => { const read=window.pbiAssureProjectPicker.read; let first=true;
                window.pbiAssureProjectPicker.read=async path=>{
                    if(first){first=false;await new Promise(resolve=>window.releaseAnalysisRead=resolve);}
                    return read(path);
                };
            }
            """);
        await page.GetByRole(AriaRole.Button, new() { Name = "Run analysis", Exact = true }).FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => window.releaseAnalysisRead !== undefined");
        await page.GetByText("Analysing your project…", new() { Exact = true }).WaitForAsync();
        Assert.Equal("Analysing your project…", await page.Locator("#analysis-status").InnerTextAsync());
        Assert.Equal("status", await page.Locator("#analysis-status").GetAttributeAsync("role"));
        Assert.Equal("true", await page.Locator("#analysis-status").GetAttributeAsync("aria-atomic"));
        foreach (var name in new[] { "Analysing…", "Change project", "Use alternative folder picker" })
            Assert.False(await page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true }).IsEnabledAsync());
        Assert.DoesNotContain("%", await page.Locator("#analysis-status").InnerTextAsync());
        if (moveElsewhere) await page.Locator("button[data-appearance='system']").FocusAsync();
        await page.EvaluateAsync("window.releaseAnalysisRead()");
        await Completed(page);
        await page.WaitForFunctionAsync(moveElsewhere
            ? "() => document.activeElement?.dataset.appearance === 'system'"
            : "() => document.activeElement?.id === 'output-actions-heading'");
        if (!moveElsewhere)
        {
            await page.Keyboard.PressAsync("Tab");
            Assert.Equal("Open investigation", await page.EvaluateAsync<string>("document.activeElement.textContent"));
        }
    }

    [Fact(Timeout = 240_000)]
    public async Task SuccessLeadsWithReportAndKeepsSecondaryOutputsAndSnapshotReselection()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context);
        await SelectSnapshot(page, fixture.FixtureDirectory);
        await Run(page);
        Assert.Equal(0, await page.Locator("article.metric, .results, .report-cards").CountAsync());
        Assert.Equal("Open investigation", await page.Locator(".output-actions button:not(.secondary-button)").First.InnerTextAsync());
        Assert.False(await page.Locator("details.download-options").EvaluateAsync<bool>("e => e.open"));
        await page.Locator("details.download-options > summary").ClickAsync();
        foreach (var name in new[] { "Download report", "Export data" })
            Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true }).IsVisibleAsync());
        Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = "Review apparently unused", Exact = false }).IsVisibleAsync());
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new() { Name = "Analyse again", Exact = true }).CountAsync());
        var before = await page.Locator("#page-title").InnerTextAsync();
        var chooser = await page.RunAndWaitForFileChooserAsync(() => page.GetByRole(AriaRole.Button,
            new() { Name = "Choose folder again to analyse", Exact = true }).ClickAsync());
        await chooser.SetFilesAsync(fixture.FixtureDirectory);
        await Completed(page);
        Assert.Equal(before, await page.Locator("#page-title").InnerTextAsync());
        await page.Locator("details.download-options > summary").ClickAsync();
        Assert.Contains("Keep a standalone HTML report", await page.Locator(".download-report").InnerTextAsync());
        Assert.Contains("URLs do not preserve this analysis", await page.Locator(".output-note").InnerTextAsync());
        Assert.Contains("Review before sharing", await page.Locator(".sensitivity").InnerTextAsync());
        await Screenshot(page, "success-desktop.png", true);
    }

    [Theory(Timeout = 240_000)]
    [InlineData("cancel")]
    [InlineData("blocked")]
    [InlineData("invalid")]
    public async Task FailedReselectionKeepsPreviousReportAndExportConfiguration(string failure)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context);
        await SelectSnapshot(page, fixture.FixtureDirectory);
        await Run(page);
        await page.Locator("details.download-options > summary").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Export data", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Radio, new() { Name = "Usage mapping", Exact = true }).CheckAsync();
        var before = await page.Locator("#page-title").InnerTextAsync();
        // Existing picker abstraction's failure/cancel boundary, not a new browser capability.
        await page.EvaluateAsync("""
            failure => {
                window.pbiAssureProjectPicker.choose = async () => {
                    if (failure === 'invalid') return {displayName:'Invalid',files:[],visitedEntries:0,maximumDepth:0};
                    throw new Error(failure === 'cancel' ? '[PBIASSURE:CANCELLED] cancelled' : '[PBIASSURE:BLOCKED] blocked');
                };
            }
            """, failure);
        await page.GetByRole(AriaRole.Button, new() { Name = "Choose folder again to analyse", Exact = true }).ClickAsync();
        await page.Locator("#project-message").WaitForAsync();
        Assert.Equal(before, await page.Locator("#page-title").InnerTextAsync());
        Assert.True(await page.GetByRole(AriaRole.Radio, new() { Name = "Usage mapping", Exact = true }).IsCheckedAsync());
        Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = "Open investigation", Exact = true }).IsEnabledAsync());
        Assert.Equal("project-message", await page.GetByRole(AriaRole.Button,
            new() { Name = "Choose folder again to analyse", Exact = true }).GetAttributeAsync("aria-describedby"));
        Assert.DoesNotContain("Analysing", await page.Locator("#analysis-status").InnerTextAsync());
    }

    [Fact(Timeout = 240_000)]
    public async Task RetainedHandleReenumeratesCurrentFilesOnAnalyseAgain()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context);
        var files = Directory.EnumerateFiles(fixture.FixtureDirectory, "*", SearchOption.AllDirectories)
            .Select(path => new { path = Path.GetRelativePath(fixture.FixtureDirectory, path).Replace('\\', '/'), text = File.ReadAllText(path) });
        // Standard directory-handle methods used by the real picker, backed by existing fixture files.
        await page.EvaluateAsync("""
            files => {
                window.directoryEnumerations = 0;
                const build = (name, files) => {
                    const groups = new Map(), entries = [];
                    for(const file of files) {
                        const slash = file.path.indexOf('/');
                        if(slash < 0) entries.push([file.path,{kind:'file',getFile:async()=>new File([file.text],file.path)}]);
                        else { const top=file.path.slice(0,slash); if(!groups.has(top)) groups.set(top,[]);
                            groups.get(top).push({path:file.path.slice(slash+1),text:file.text}); }
                    }
                    for(const [top,children] of groups) entries.push([top,build(top,children)]);
                    return {name,kind:'directory',async *entries(){window.directoryEnumerations++;for(const item of entries)yield item;}};
                };
                window.showDirectoryPicker = async options => {
                    if(options.mode !== 'read') throw new Error('Read-only access required');
                    return build('Retained project',files);
                };
            }
            """, files);
        await page.GetByRole(AriaRole.Button, new() { Name = "Choose project folder", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Run analysis", Exact = true }).WaitForAsync();
        await Run(page);
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new() { Name = "Choose folder again to analyse", Exact = true }).CountAsync());
        var enumerations = await page.EvaluateAsync<int>("window.directoryEnumerations");
        await page.GetByRole(AriaRole.Button, new() { Name = "Analyse again", Exact = true }).ClickAsync();
        await Completed(page);
        Assert.True(await page.EvaluateAsync<int>("window.directoryEnumerations") > enumerations);
    }

    [Theory(Timeout = 240_000)]
    [InlineData("pbi-assure-coverage", true)]
    // This fixture has limitation records even without qualified absence results.
    [InlineData("desktop-landing-page", true)]
    public async Task ChecksNoticeUsesExistingQualificationsWithoutCompletenessClaims(string directory, bool limited)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context);
        await SelectSnapshot(page, Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", directory));
        await Run(page);
        Assert.Equal(limited ? 1 : 0, await page.Locator(".confidence-notice").CountAsync());
        if (limited) Assert.Contains("Review analysis scope", await page.Locator(".confidence-notice").InnerTextAsync());
        var text = await page.Locator("main").InnerTextAsync();
        foreach (var retired in new[] { "Everything was checked", "Analysis coverage", "Assurance" }) Assert.DoesNotContain(retired, text);
    }

    [Theory(Timeout = 240_000)]
    [InlineData(1366, 768)]
    [InlineData(1440, 900)]
    [InlineData(320, 900)]
    [InlineData(320, 225)]
    public async Task DesktopAndReflowKeepReportAndKeyboardActionsReachable(int width, int height)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        var page = await Open(context);
        await SelectSnapshot(page, fixture.FixtureDirectory);
        await Run(page);
        await page.Locator("#output-actions-heading").FocusAsync();
        await page.Keyboard.PressAsync("Tab");
        var open = page.GetByRole(AriaRole.Button, new() { Name = "Open investigation", Exact = true });
        Assert.True(await open.EvaluateAsync<bool>("e => e === document.activeElement && e.matches(':focus-visible')"));
        Assert.True(await open.EvaluateAsync<bool>("e => {const r=e.getBoundingClientRect(),h=document.querySelector('.app-bar').getBoundingClientRect();return r.top >= h.bottom && r.bottom <= innerHeight;}"));
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        await Screenshot(page, $"success-{width}-{height}.png", false);
    }

    [Fact(Timeout = 240_000)]
    public async Task UnsupportedInputKeepsActionableGuidanceWithoutOldAssuranceTerminology()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await Open(context);
        await SelectSnapshot(page, Path.Combine(fixture.RepositoryRoot, "tests", "fixtures", "desktop-tmsl-model-bim-evidence"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Run analysis", Exact = true }).ClickAsync();
        var error = page.Locator("#project-message");
        await error.WaitForAsync();
        var text = await error.InnerTextAsync();
        Assert.Contains("No investigation was generated", text);
        Assert.Contains("Keep a backup", text);
        Assert.DoesNotContain("assurance output", text);
        Assert.Equal("alert", await error.GetAttributeAsync("role"));
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new() { Name = "Open investigation", Exact = true }).CountAsync());
    }

    private async Task<IPage> Open(IBrowserContext context)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl);
        await page.Locator("[data-pbiassure-app-ready]").WaitForAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        return page;
    }

    private static async Task SelectSnapshot(IPage page, string path)
    {
        await page.BringToFrontAsync();
        if (!await page.Locator("details.picker-help").EvaluateAsync<bool>("e=>e.open"))
            await page.Locator("details.picker-help > summary").ClickAsync();
        var chooser = await page.RunAndWaitForFileChooserAsync(() => page.GetByRole(AriaRole.Button,
            new() { Name = "Use alternative folder picker", Exact = true }).ClickAsync());
        await chooser.SetFilesAsync(path);
        await page.GetByRole(AriaRole.Button, new() { Name = "Run analysis", Exact = true }).WaitForAsync();
    }

    private static async Task Run(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Run analysis", Exact = true }).ClickAsync();
        await Completed(page);
    }

    private static Task<IJSHandle> Completed(IPage page) => page.WaitForFunctionAsync("() => document.querySelector('#analysis-status')?.textContent.startsWith('Analysis complete for') && !document.querySelector('.output-actions button')?.disabled");

    private async Task Screenshot(IPage page, string name, bool fullPage)
    {
        var output = Path.Combine(fixture.RepositoryRoot, "artifacts", "front-door-polish");
        Directory.CreateDirectory(output);
        await page.ScreenshotAsync(new() { Path = Path.Combine(output, name), FullPage = fullPage });
    }
}

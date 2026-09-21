using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

/// <summary>
/// One analysis, two report experiences, one export area. After a scan the browser application offers
/// the interactive report and the apparently unused review as two named cards, each with an open
/// action and a download, and keeps export work visibly apart from report delivery.
/// </summary>
public sealed class WebReportActionsSurfaceTests
{
    [Fact]
    public void BothReportsHaveOpenAndDownloadActionsInTheirOwnCards()
    {
        var actions = NextSteps(ReadHomePage());

        var interactive = Card(actions, "interactive-report-heading");
        Assert.Contains("<h3 id=\"interactive-report-heading\">Interactive report</h3>", interactive, StringComparison.Ordinal);
        Assert.Contains("<p id=\"interactive-report-help\">Explore model usage, dependencies, report structure and findings.</p>", interactive, StringComparison.Ordinal);
        Assert.Contains("<button @onclick=\"OpenHtmlAsync\" disabled=\"@isBusy\" aria-describedby=\"interactive-report-help\">Open interactive report</button>", interactive, StringComparison.Ordinal);
        Assert.Contains("<button class=\"secondary-button\" @onclick=\"DownloadHtmlAsync\" disabled=\"@isBusy\">Download <span class=\"visually-hidden\">interactive </span>report</button>", interactive, StringComparison.Ordinal);

        var review = Card(actions, "unused-review-heading");
        Assert.Contains("<h3 id=\"unused-review-heading\">Apparently unused review</h3>", review, StringComparison.Ordinal);
        Assert.Contains("<p id=\"unused-review-help\">Review model items with no report or semantic-model usage found in this project.</p>", review, StringComparison.Ordinal);
        Assert.Contains("<button @onclick=\"OpenUnusedReviewAsync\" disabled=\"@isBusy\" aria-describedby=\"unused-review-help\">Review apparently unused (@inventory.DeveloperApparentlyUnusedSemanticObjectCount)</button>", review, StringComparison.Ordinal);
        Assert.Contains("<button class=\"secondary-button\" @onclick=\"DownloadUnusedReviewAsync\" disabled=\"@isBusy\">Download <span class=\"visually-hidden\">apparently unused </span>review</button>", review, StringComparison.Ordinal);

        // The interactive report is first in reading order; the review is never disabled at zero.
        Assert.True(actions.IndexOf("interactive-report-heading", StringComparison.Ordinal) < actions.IndexOf("unused-review-heading", StringComparison.Ordinal));
        Assert.DoesNotContain("DeveloperApparentlyUnusedSemanticObjectCount == 0", actions, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNextStepsCopyIsExactlyTheProductJourney()
    {
        var actions = NextSteps(ReadHomePage());

        Assert.Contains("<h2 id=\"output-actions-heading\">Analysis complete</h2>", actions, StringComparison.Ordinal);
        Assert.Contains("Explore your project, review apparently unused items, or export metadata for further work.", actions, StringComparison.Ordinal);
        Assert.Contains("<h3 id=\"export-data-heading\">Export data</h3>", actions, StringComparison.Ordinal);
        Assert.Contains("Export a Data Catalogue or Usage Mapping CSV for further work.", actions, StringComparison.Ordinal);
        Assert.Contains("Reports open in a new tab. Downloads are self-contained HTML files.", actions, StringComparison.Ordinal);
        // Retired wording from the single-report era.
        foreach (var retired in new[] { "Next steps", "Download HTML report", "Start here", "no usage evidence", "review list" })
        {
            Assert.DoesNotContain(retired, actions, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A screen reader hears the whole name; a sighted reader sees the short one. The visible text is a
    /// prefix and suffix of the accessible name, so the label-in-name relationship holds either way.
    /// </summary>
    [Fact]
    public void DownloadButtonsHaveUnambiguousAccessibleNames()
    {
        var actions = NextSteps(ReadHomePage());

        Assert.Equal("Download interactive report", AccessibleName(actions, "DownloadHtmlAsync"));
        Assert.Equal("Download apparently unused review", AccessibleName(actions, "DownloadUnusedReviewAsync"));
        Assert.Equal("Download report", VisibleText(actions, "DownloadHtmlAsync"));
        Assert.Equal("Download review", VisibleText(actions, "DownloadUnusedReviewAsync"));
        Assert.DoesNotContain("aria-label=\"Download", actions, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportDataIsASeparateAreaWithTheLegacyCsvStillCollapsed()
    {
        var actions = NextSteps(ReadHomePage());
        var export = actions[actions.IndexOf("<section class=\"export-area\"", StringComparison.Ordinal)..];

        Assert.Contains("aria-labelledby=\"export-data-heading\"", export, StringComparison.Ordinal);
        Assert.Contains("<button class=\"secondary-button\" @ref=\"exportToggle\" @onclick=\"ToggleExportPanel\" disabled=\"@isBusy\" aria-expanded=\"@(exportPanelOpen ? \"true\" : \"false\")\" aria-describedby=\"export-data-help\">Export data</button>", export, StringComparison.Ordinal);
        // A bool bound straight to aria-expanded renders as an empty attribute when true and vanishes when
        // false, which is not valid ARIA; the state is written as the literal strings.
        Assert.DoesNotContain("aria-expanded=\"@exportPanelOpen\"", export, StringComparison.Ordinal);
        Assert.Contains("@if (exportPanelOpen && selection is not null)", export, StringComparison.Ordinal);
        Assert.Contains("OnClose=\"CloseExportPanelAsync\"", export, StringComparison.Ordinal);
        var legacy = export[export.IndexOf("<details class=\"summary-help legacy-output\">", StringComparison.Ordinal)..];
        Assert.Contains(">Download semantic usage CSV</button>", legacy, StringComparison.Ordinal);
        Assert.Contains("existing fixed semantic-usage CSV", legacy, StringComparison.Ordinal);
        // Export work sits outside both report cards and after them.
        Assert.True(actions.IndexOf("<section class=\"export-area\"", StringComparison.Ordinal) > actions.IndexOf("DownloadUnusedReviewAsync", StringComparison.Ordinal));
        Assert.DoesNotContain("export-area", actions[..actions.IndexOf("<p class=\"output-note\">", StringComparison.Ordinal)], StringComparison.Ordinal);
        // The sharing warning still closes the section.
        Assert.Contains("HTML and CSV files may contain sensitive project metadata, including paths, expressions, source details and model/report metadata.", export, StringComparison.Ordinal);
        Assert.Contains("outputs are not automatically redacted", export, StringComparison.Ordinal);
    }

    /// <summary>Closing the export workspace hands focus back to the control that opened it.</summary>
    [Fact]
    public void ClosingTheExportPanelReturnsFocusToItsToggle()
    {
        var markup = ReadHomePage();
        var close = markup[markup.IndexOf("private async Task CloseExportPanelAsync()", StringComparison.Ordinal)..];
        close = close[..close.IndexOf('}', StringComparison.Ordinal)];

        Assert.Contains("exportPanelOpen = false;", close, StringComparison.Ordinal);
        Assert.Contains("await exportToggle.FocusAsync();", close, StringComparison.Ordinal);
        // Nothing moves focus when analysis completes.
        var run = markup[markup.IndexOf("private async Task RunAssuranceAsync()", StringComparison.Ordinal)..markup.IndexOf("private sealed class JsFileAccess", StringComparison.Ordinal)];
        Assert.DoesNotContain("FocusAsync", run, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTwoDownloadsUseTheSameSavePathWithDistinctFileNames()
    {
        var markup = ReadHomePage();
        var interactive = Method(markup, "DownloadHtmlAsync");
        var review = Method(markup, "DownloadUnusedReviewAsync");

        Assert.Contains("HtmlReportRenderer.Render(inventory)", interactive, StringComparison.Ordinal);
        Assert.Contains("BrowserDownloadFileNames.Html(selection.DisplayName)", interactive, StringComparison.Ordinal);
        Assert.Contains("ApparentlyUnusedReportRenderer.Render(inventory)", review, StringComparison.Ordinal);
        Assert.DoesNotContain("HtmlReportRenderer.Render", review, StringComparison.Ordinal);
        Assert.Contains("BrowserDownloadFileNames.ApparentlyUnusedHtml(selection.DisplayName)", review, StringComparison.Ordinal);
        foreach (var body in new[] { interactive, review })
        {
            Assert.Contains("JS.InvokeVoidAsync(\"pbiAssureDownload.save\"", body, StringComparison.Ordinal);
            Assert.Contains("\"text/html;charset=utf-8\"", body, StringComparison.Ordinal);
        }

        Assert.Equal("Sales Returns.pbiassure.html", BrowserDownloadFileNames.Html("Sales: Returns"));
        Assert.Equal("Sales Returns.apparently-unused.html", BrowserDownloadFileNames.ApparentlyUnusedHtml("Sales: Returns"));
        Assert.NotEqual(BrowserDownloadFileNames.Html("Any"), BrowserDownloadFileNames.ApparentlyUnusedHtml("Any"));
    }

    [Fact]
    public void ReportCardsSitSideBySideAndStackAtNarrowWidths()
    {
        var styles = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "PbiAssure.Web", "wwwroot", "css", "app.css"));

        Assert.Contains(".report-cards { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr));", styles, StringComparison.Ordinal);
        var narrow = styles[styles.IndexOf("@media (max-width: 46rem)", StringComparison.Ordinal)..];
        narrow = narrow[..narrow.IndexOf("@media", 1, StringComparison.Ordinal)];
        Assert.Contains(".report-cards { grid-template-columns: minmax(0, 1fr); }", narrow, StringComparison.Ordinal);
        var phone = styles[styles.IndexOf("@media (max-width: 34rem)", StringComparison.Ordinal)..];
        Assert.Contains(".report-card-actions { flex-direction: column; align-items: stretch; }", phone, StringComparison.Ordinal);
        // The export area is not a third accent card.
        var exportRule = styles[styles.IndexOf(".export-area {", StringComparison.Ordinal)..];
        exportRule = exportRule[..exportRule.IndexOf('}', StringComparison.Ordinal)];
        Assert.Contains("border: 1px solid var(--pa-line);", exportRule, StringComparison.Ordinal);
        Assert.Contains("background: var(--pa-surface);", exportRule, StringComparison.Ordinal);
        var cardRule = styles[styles.IndexOf(".report-card {", StringComparison.Ordinal)..];
        cardRule = cardRule[..cardRule.IndexOf('}', StringComparison.Ordinal)];
        Assert.Contains("var(--pa-accent-line)", cardRule, StringComparison.Ordinal);
        // The status line is always in the document, so a live region exists before it is filled.
        Assert.Contains(".report-card-status:empty { padding: 0; border: 0; }", styles, StringComparison.Ordinal);
        // Nothing from the retired banner layout survives.
        Assert.DoesNotContain(".output-primary", styles, StringComparison.Ordinal);
        Assert.DoesNotContain(".output-secondary", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void TheViewerHandshakeStillGuardsBothDeliveries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var markup = ReadHomePage();
        var script = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "wwwroot", "download.js"));
        var viewer = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "wwwroot", "report-viewer.js"));

        Assert.Equal(2, CountOccurrences(markup, "JS.InvokeAsync<string>(\"pbiAssureDownload.open\", \"text/html;charset=utf-8\", html)"));
        Assert.DoesNotContain("window.open", markup, StringComparison.Ordinal);
        Assert.Contains("report-viewer.html?v=__PBIASSURE_ASSET_VERSION__", script, StringComparison.Ordinal);
        Assert.Contains("window.open(viewerUrl.href, \"_blank\")", script, StringComparison.Ordinal);
        Assert.Contains("event.source !== reportWindow", script, StringComparison.Ordinal);
        Assert.Contains("reportWindow.postMessage", script, StringComparison.Ordinal);
        Assert.Contains("event.origin !== window.location.origin", viewer, StringComparison.Ordinal);
        Assert.Contains("event.source !== sourceWindow", viewer, StringComparison.Ordinal);
        Assert.Contains("window.opener = null", viewer, StringComparison.Ordinal);
        Assert.Contains("document.write(event.data.content)", viewer, StringComparison.Ordinal);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string ReadHomePage() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "PbiAssure.Web", "Pages", "Home.razor"));

    private static string NextSteps(string markup)
    {
        var start = markup.IndexOf("<section class=\"output-actions\"", StringComparison.Ordinal);
        Assert.True(start > markup.IndexOf("@if (inventory is not null)", StringComparison.Ordinal));
        var end = markup.IndexOf("<section class=\"results\"", start, StringComparison.Ordinal);
        return markup[start..end];
    }

    private static string Card(string actions, string headingId)
    {
        var start = actions.IndexOf($"<section class=\"report-card\" aria-labelledby=\"{headingId}\">", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected a report card labelled by {headingId}.");
        return actions[start..actions.IndexOf("</section>", start, StringComparison.Ordinal)];
    }

    private static string Method(string markup, string name)
    {
        var start = markup.IndexOf($"private async Task {name}()", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected {name}.");
        var end = markup.IndexOf("private async Task ", start + 1, StringComparison.Ordinal);
        return end < 0 ? markup[start..] : markup[start..end];
    }

    private static string Button(string actions, string handler)
    {
        var start = actions.IndexOf($"@onclick=\"{handler}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected a button bound to {handler}.");
        var open = actions.IndexOf('>', start);
        var close = actions.IndexOf("</button>", open, StringComparison.Ordinal);
        return actions[(open + 1)..close];
    }

    private static string AccessibleName(string actions, string handler) =>
        System.Text.RegularExpressions.Regex.Replace(Button(actions, handler), "<[^>]+>", string.Empty).Trim();

    private static string VisibleText(string actions, string handler) =>
        System.Text.RegularExpressions.Regex.Replace(
            Button(actions, handler), "<span class=\"visually-hidden\">[^<]*</span>", string.Empty).Trim();

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

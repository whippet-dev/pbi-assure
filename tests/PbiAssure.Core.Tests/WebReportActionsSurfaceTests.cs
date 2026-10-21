using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

/// <summary>One investigation application, with secondary portable downloads.</summary>
public sealed class WebReportActionsSurfaceTests
{
    [Fact]
    public void ReportLeadsAndFocusedReviewRemainsSecondary()
    {
        var actions = NextSteps(ReadHomePage());
        Assert.Contains(">Open investigation</button>", actions, StringComparison.Ordinal);
        Assert.Contains(">Download report</button>", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenUnusedReviewAsync", actions, StringComparison.Ordinal);
        Assert.Contains("Review apparently unused in Model Objects", actions, StringComparison.Ordinal);
        Assert.True(actions.IndexOf("OpenInvestigationAsync", StringComparison.Ordinal) < actions.IndexOf("other-outputs-heading", StringComparison.Ordinal));
        Assert.DoesNotContain("report-cards", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("DeveloperApparentlyUnusedSemanticObjectCount == 0", actions, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNextStepsCopyIsExactlyTheProductJourney()
    {
        var actions = NextSteps(ReadHomePage());

        Assert.Contains("<h2 id=\"output-actions-heading\" tabindex=\"-1\" @ref=\"completionHeading\">Analysis complete</h2>", actions, StringComparison.Ordinal);
        Assert.Contains("Your project is ready to explore.", actions, StringComparison.Ordinal);
        Assert.Contains("<h4 id=\"export-data-heading\">Export data</h4>", actions, StringComparison.Ordinal);
        Assert.Contains("Export a Data Catalogue or Usage Mapping CSV for further work.", actions, StringComparison.Ordinal);
        Assert.Contains("Keep a standalone HTML report of this analysis.", actions, StringComparison.Ordinal);
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

        Assert.Equal("Download report", AccessibleName(actions, "DownloadHtmlAsync"));
        Assert.Equal("Download apparently unused review", AccessibleName(actions, "DownloadUnusedReviewAsync"));
        Assert.Equal("Download report", VisibleText(actions, "DownloadHtmlAsync"));
        Assert.Equal("Download apparently unused review", VisibleText(actions, "DownloadUnusedReviewAsync"));
        Assert.DoesNotContain("aria-label=\"Download", actions, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportDataIsASeparateAreaWithTheLegacyCsvStillCollapsed()
    {
        var actions = NextSteps(ReadHomePage());
        var export = actions[actions.IndexOf("<section class=\"export-area\"", StringComparison.Ordinal)..];

        Assert.Contains("aria-labelledby=\"export-data-heading\"", export, StringComparison.Ordinal);
        Assert.Contains("<button class=\"secondary-button\" @ref=\"exportToggle\" @onclick=\"ToggleExportPanel\" disabled=\"@ActionsDisabled\" aria-expanded=\"@(exportPanelOpen ? \"true\" : \"false\")\" aria-describedby=\"export-data-help\">Export data</button>", export, StringComparison.Ordinal);
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
        Assert.Contains("Downloads can contain sensitive project metadata.", actions, StringComparison.Ordinal);
        Assert.Contains("outputs are not automatically redacted", actions, StringComparison.Ordinal);
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
        // Completion uses a guarded handoff rather than unconditional focus.
        var run = markup[markup.IndexOf("private async Task RunAssuranceAsync()", StringComparison.Ordinal)..markup.IndexOf("private sealed class JsFileAccess", StringComparison.Ordinal)];
        Assert.Contains("pbiAssureAnalysisFocus.begin", run, StringComparison.Ordinal);
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
    public void SecondaryOutputsReflowAndStatusesRemainAvailable()
    {
        var styles = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "PbiAssure.Web", "wwwroot", "css", "app.css"));
        Assert.Contains(".other-outputs { grid-template-columns: minmax(0, 1fr); }", styles, StringComparison.Ordinal);
        Assert.Contains(".report-card-actions { flex-direction: column; align-items: stretch; }", styles, StringComparison.Ordinal);
        Assert.Contains(".report-card-status:empty { padding: 0; border: 0; }", styles, StringComparison.Ordinal);
        Assert.DoesNotContain(".report-card {", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheSnapshotViewerCanOpenAnInvestigation()
    {
        var markup = ReadHomePage();
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "PbiAssure.Web", "wwwroot", "download.js"));
        Assert.Contains("pbiAssureInvestigation.open", markup);
        Assert.DoesNotContain("pbiAssureDownload.open", markup);
        Assert.DoesNotContain("window.open", script);
        Assert.DoesNotContain("postMessage", script);
        Assert.Contains("save(filename, mimeType, content)", script);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string ReadHomePage() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "PbiAssure.Web", "Pages", "Home.razor"));

    private static string NextSteps(string markup)
    {
        var start = markup.IndexOf("<section class=\"output-actions\"", StringComparison.Ordinal);
        Assert.True(start > markup.IndexOf("@if (inventory is not null)", StringComparison.Ordinal));
        var end = markup.IndexOf("<div class=\"setup-support", start, StringComparison.Ordinal);
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

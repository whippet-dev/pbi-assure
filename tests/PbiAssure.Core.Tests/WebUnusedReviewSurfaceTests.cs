using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// After a successful analysis the browser application offers two report experiences: the interactive
/// report and the apparently unused review. The review travels through exactly the same acknowledged
/// viewer handshake as the interactive report, and is saved through exactly the same browser save path,
/// with its own file name.
/// </summary>
public sealed class WebUnusedReviewSurfaceTests
{
    [Fact]
    public void BothReportsAreOfferedAsCardsAfterAnalysis()
    {
        var markup = ReadHomePage();
        var cards = markup[markup.IndexOf("<div class=\"report-cards\">", StringComparison.Ordinal)..markup.IndexOf("<p class=\"output-note\">", StringComparison.Ordinal)];

        var open = cards.IndexOf("<button @onclick=\"OpenHtmlAsync\" disabled=\"@isBusy\"", StringComparison.Ordinal);
        var review = cards.IndexOf("<button @onclick=\"OpenUnusedReviewAsync\" disabled=\"@isBusy\"", StringComparison.Ordinal);
        Assert.True(open >= 0 && review > open, "both reports have a card, interactive report first");
        Assert.Contains(">Open interactive report</button>", cards, StringComparison.Ordinal);
        Assert.Contains(">Review apparently unused (@inventory.DeveloperApparentlyUnusedSemanticObjectCount)</button>", cards, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"unused-review-help\"", cards, StringComparison.Ordinal);
        Assert.Contains("<p id=\"unused-review-help\">Review model items with no report or semantic-model usage found in this project.</p>", cards, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(cards, "<section class=\"report-card\""));
        // The review card is a real region: heading-labelled, with its own status line.
        Assert.Contains("<section class=\"report-card\" aria-labelledby=\"unused-review-heading\">", cards, StringComparison.Ordinal);
        Assert.Contains("<p class=\"report-card-status\" role=\"status\">@unusedReviewMessage</p>", cards, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReviewUsesTheAcknowledgedViewerPath()
    {
        var markup = ReadHomePage();
        var body = Method(markup, "OpenUnusedReviewAsync");

        Assert.Contains("ApparentlyUnusedReportRenderer.Render(inventory)", body, StringComparison.Ordinal);
        Assert.Contains("JS.InvokeAsync<string>(\"pbiAssureDownload.open\", \"text/html;charset=utf-8\", html)", body, StringComparison.Ordinal);
        Assert.Contains("WebReportViewerStatus.Describe(status, WebReportKind.ApparentlyUnusedReview)", body, StringComparison.Ordinal);
        Assert.Contains("WebReportViewerStatus.Opening(WebReportKind.ApparentlyUnusedReview)", body, StringComparison.Ordinal);
        // No second delivery mechanism: the only opener in the page is the one download.js provides.
        Assert.DoesNotContain("window.open", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("pbiAssureDownload.save", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Download review saves the review, not the interactive report, through the same save function
    /// the interactive report uses, under its own name.
    /// </summary>
    [Fact]
    public void DownloadReviewSavesTheReviewThroughTheSharedSavePath()
    {
        var markup = ReadHomePage();
        var body = Method(markup, "DownloadUnusedReviewAsync");

        Assert.Contains("ApparentlyUnusedReportRenderer.Render(inventory)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("HtmlReportRenderer.Render", body, StringComparison.Ordinal);
        Assert.Contains("JS.InvokeVoidAsync(\"pbiAssureDownload.save\", BrowserDownloadFileNames.ApparentlyUnusedHtml(selection.DisplayName), \"text/html;charset=utf-8\", html)", body, StringComparison.Ordinal);
        Assert.Contains("WebReportViewerStatus.DownloadStarted(WebReportKind.ApparentlyUnusedReview)", body, StringComparison.Ordinal);
        Assert.Contains("WebReportViewerStatus.DownloadFailed(WebReportKind.ApparentlyUnusedReview)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("pbiAssureDownload.open", body, StringComparison.Ordinal);
    }

    /// <summary>What the review download contains is the review page, with its own title and lede.</summary>
    [Fact]
    public void TheReviewRendererProducesTheReviewNotTheInteractiveReport()
    {
        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Review sample",
        [
            new ProjectFileContent("Model.pbip", "{}"u8.ToArray()),
            new ProjectFileContent("Model.SemanticModel/definition.pbism", "{}"u8.ToArray()),
            new ProjectFileContent("Model.SemanticModel/definition/tables/Sales.tmdl",
                "table Sales\n\tmeasure Idle = 1\n\tcolumn Amount\n\t\tdataType: int64\n\t\tsourceColumn: Amount\n"u8.ToArray()),
        ]));

        var review = ApparentlyUnusedReportRenderer.Render(inventory);
        var interactive = HtmlReportRenderer.Render(inventory);

        Assert.Contains(ApparentlyUnusedReportRenderer.Lede, review, StringComparison.Ordinal);
        Assert.DoesNotContain(ApparentlyUnusedReportRenderer.Lede, interactive, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"findings\"", review, StringComparison.Ordinal);
        Assert.Contains("id=\"findings\"", interactive, StringComparison.Ordinal);
    }

    [Fact]
    public void TheInteractiveReportRouteIsUnchanged()
    {
        var markup = ReadHomePage();
        var body = Method(markup, "OpenHtmlAsync");

        Assert.Contains("HtmlReportRenderer.Render(inventory)", body, StringComparison.Ordinal);
        Assert.Contains("JS.InvokeAsync<string>(\"pbiAssureDownload.open\", \"text/html;charset=utf-8\", html)", body, StringComparison.Ordinal);
        Assert.Contains("<button class=\"secondary-button\" @onclick=\"DownloadHtmlAsync\"", markup, StringComparison.Ordinal);
    }

    private static string ReadHomePage() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "PbiAssure.Web", "Pages", "Home.razor"));

    private static string Method(string markup, string name)
    {
        var start = markup.IndexOf($"private async Task {name}()", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected {name}.");
        var end = markup.IndexOf("private async Task ", start + 1, StringComparison.Ordinal);
        return end < 0 ? markup[start..] : markup[start..end];
    }

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

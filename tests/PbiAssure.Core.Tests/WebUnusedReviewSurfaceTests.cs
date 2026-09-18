namespace PbiAssure.Core.Tests;

/// <summary>
/// After a successful analysis the browser application offers two routes in: the full interactive
/// report and the lighter "Apparently unused" review list. The second one travels through exactly
/// the same acknowledged viewer handshake as the first.
/// </summary>
public sealed class WebUnusedReviewSurfaceTests
{
    [Fact]
    public void BothReportRoutesAreOfferedTogetherAfterAnalysis()
    {
        var markup = ReadHomePage();
        var primary = markup.IndexOf("<div class=\"output-primary\">", StringComparison.Ordinal);
        var primaryEnd = markup.IndexOf("<div class=\"output-secondary\">", primary, StringComparison.Ordinal);
        var banner = markup[primary..primaryEnd];

        var open = banner.IndexOf("<button @onclick=\"OpenHtmlAsync\" disabled=\"@isBusy\"", StringComparison.Ordinal);
        var review = banner.IndexOf("<button @onclick=\"OpenUnusedReviewAsync\" disabled=\"@isBusy\"", StringComparison.Ordinal);
        Assert.True(open >= 0 && review > open, "both routes sit in the primary banner, full report first");
        Assert.Contains(">Open interactive report</button>", banner, StringComparison.Ordinal);
        Assert.Contains(">Review apparently unused (@inventory.DeveloperApparentlyUnusedSemanticObjectCount)</button>", banner, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"review-unused-help\"", banner, StringComparison.Ordinal);
        Assert.Contains("<p id=\"review-unused-help\">", banner, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(banner, "<div class=\"output-route\">"));
    }

    [Fact]
    public void TheReviewListUsesTheAcknowledgedViewerPath()
    {
        var markup = ReadHomePage();
        var method = markup.IndexOf("private async Task OpenUnusedReviewAsync()", StringComparison.Ordinal);
        Assert.True(method >= 0);
        var body = markup[method..markup.IndexOf("private async Task DownloadCsvAsync()", method, StringComparison.Ordinal)];

        Assert.Contains("ApparentlyUnusedReportRenderer.Render(inventory)", body, StringComparison.Ordinal);
        Assert.Contains("JS.InvokeAsync<string>(\"pbiAssureDownload.open\", \"text/html;charset=utf-8\", html)", body, StringComparison.Ordinal);
        Assert.Contains("WebReportViewerStatus.Describe(status)", body, StringComparison.Ordinal);
        // No second delivery mechanism: the only opener in the page is the one download.js provides.
        Assert.DoesNotContain("window.open", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("pbiAssureDownload.save", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFullReportRouteIsUnchanged()
    {
        var markup = ReadHomePage();
        var method = markup.IndexOf("private async Task OpenHtmlAsync()", StringComparison.Ordinal);
        var body = markup[method..markup.IndexOf("private async Task DownloadCsvAsync()", method, StringComparison.Ordinal)];

        Assert.Contains("HtmlReportRenderer.Render(inventory)", body, StringComparison.Ordinal);
        Assert.Contains("JS.InvokeAsync<string>(\"pbiAssureDownload.open\", \"text/html;charset=utf-8\", html)", body, StringComparison.Ordinal);
        Assert.Contains("<button @onclick=\"DownloadHtmlAsync\" class=\"secondary-button\"", markup, StringComparison.Ordinal);
    }

    private static string ReadHomePage() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "PbiAssure.Web", "Pages", "Home.razor"));

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

using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>The focused workflow uses Model Objects; portable HTML remains an export.</summary>
public sealed class WebUnusedReviewSurfaceTests
{
    [Fact]
    public void FocusedReviewOpensThePrimaryViewerAndRetainsADownload()
    {
        var markup = ReadHomePage();
        Assert.Contains("OpenInvestigationAsync(\"unused\")", markup);
        Assert.DoesNotContain("OpenUnusedReviewAsync", markup);
        Assert.Contains("<details class=\"download-options\">", markup);
        Assert.True(markup.IndexOf("other-outputs-heading", StringComparison.Ordinal) < markup.IndexOf("DownloadUnusedReviewAsync", StringComparison.Ordinal));
        Assert.Contains("portable HTML review set for sharing or offline review", markup);
        Assert.Contains("ViewerSnapshotProjector.Project(inventory)", markup);
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

namespace PbiAssure.Core.Tests;

public sealed class WebReportActionsSurfaceTests
{
    [Fact]
    public void HtmlReportCanBeOpenedOrDownloaded()
    {
        var repositoryRoot = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "Pages", "Home.razor"));
        var script = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "wwwroot", "download.js"));
        var viewer = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "wwwroot", "report-viewer.js"));

        var openButton = markup.IndexOf("<button @onclick=\"OpenHtmlAsync\"", StringComparison.Ordinal);
        var downloadButton = markup.IndexOf("<button @onclick=\"DownloadHtmlAsync\"", StringComparison.Ordinal);

        Assert.True(openButton >= 0);
        Assert.True(downloadButton > openButton);
        Assert.Contains(">Open HTML report</button>", markup, StringComparison.Ordinal);
        Assert.Contains(">Download HTML report</button>", markup, StringComparison.Ordinal);
        Assert.Contains("pbiAssureDownload.open", markup, StringComparison.Ordinal);
        Assert.Contains("report-viewer.html?v=__PBIASSURE_ASSET_VERSION__", script, StringComparison.Ordinal);
        Assert.Contains("window.open(viewerUrl.href, \"_blank\")", script, StringComparison.Ordinal);
        Assert.Contains("event.source !== reportWindow", script, StringComparison.Ordinal);
        Assert.Contains("reportWindow.postMessage", script, StringComparison.Ordinal);
        Assert.Contains("event.origin !== window.location.origin", viewer, StringComparison.Ordinal);
        Assert.Contains("event.source !== sourceWindow", viewer, StringComparison.Ordinal);
        Assert.Contains("window.opener = null", viewer, StringComparison.Ordinal);
        Assert.Contains("document.write(event.data.content)", viewer, StringComparison.Ordinal);
        Assert.Contains("The browser blocked the new tab.", markup, StringComparison.Ordinal);
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

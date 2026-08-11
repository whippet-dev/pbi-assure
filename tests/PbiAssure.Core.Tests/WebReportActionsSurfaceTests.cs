namespace PbiAssure.Core.Tests;

public sealed class WebReportActionsSurfaceTests
{
    [Fact]
    public void HtmlReportCanBeOpenedOrDownloaded()
    {
        var repositoryRoot = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "Pages", "Home.razor"));
        var script = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "wwwroot", "download.js"));

        var openButton = markup.IndexOf("<button @onclick=\"OpenHtmlAsync\"", StringComparison.Ordinal);
        var downloadButton = markup.IndexOf("<button @onclick=\"DownloadHtmlAsync\"", StringComparison.Ordinal);

        Assert.True(openButton >= 0);
        Assert.True(downloadButton > openButton);
        Assert.Contains(">Open HTML report</button>", markup, StringComparison.Ordinal);
        Assert.Contains(">Download HTML report</button>", markup, StringComparison.Ordinal);
        Assert.Contains("pbiAssureDownload.open", markup, StringComparison.Ordinal);
        Assert.Contains("window.open(objectUrl, \"_blank\")", script, StringComparison.Ordinal);
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

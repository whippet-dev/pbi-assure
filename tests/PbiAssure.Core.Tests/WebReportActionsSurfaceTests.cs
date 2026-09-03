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
        Assert.Contains(">Open interactive report</button>", markup, StringComparison.Ordinal);
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

    [Fact]
    public void PostScanOutputsPrioritizeOpeningAndReusableDataWithLegacyExportCollapsed()
    {
        var markup = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "PbiAssure.Web", "Pages", "Home.razor"));
        var actions = markup.IndexOf("<section class=\"output-actions\"", StringComparison.Ordinal);
        Assert.True(actions > markup.IndexOf("@if (inventory is not null)", StringComparison.Ordinal));
        var open = markup.IndexOf(">Open interactive report</button>", actions, StringComparison.Ordinal);
        var export = markup.IndexOf(">Export data</button>", actions, StringComparison.Ordinal);
        var download = markup.IndexOf(">Download HTML report</button>", actions, StringComparison.Ordinal);
        var legacy = markup.IndexOf("<details class=\"summary-help legacy-output\">", actions, StringComparison.Ordinal);
        Assert.True(open > actions && export > open && download > export && legacy > download);
        var legacyEnd = markup.IndexOf("</details>", legacy, StringComparison.Ordinal);
        Assert.Contains(">Download semantic usage CSV</button>", markup[legacy..legacyEnd], StringComparison.Ordinal);
        Assert.Contains("existing fixed semantic-usage CSV", markup[legacy..legacyEnd], StringComparison.Ordinal);
        Assert.Contains("Start here to explore model usage, dependencies, report structure and findings.", markup, StringComparison.Ordinal);
        Assert.Contains("<button @onclick=\"OpenHtmlAsync\" disabled=\"@isBusy\"", markup, StringComparison.Ordinal);
        Assert.Contains("<button class=\"secondary-button\" @onclick=\"() => exportPanelOpen = !exportPanelOpen\"", markup, StringComparison.Ordinal);
        Assert.Contains("<button @onclick=\"DownloadHtmlAsync\" class=\"secondary-button\"", markup, StringComparison.Ordinal);
        Assert.Contains("HTML and CSV files may contain sensitive project metadata, including paths, expressions, source details and model/report metadata.", markup, StringComparison.Ordinal);
        Assert.Contains("outputs are not automatically redacted", markup, StringComparison.Ordinal);
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

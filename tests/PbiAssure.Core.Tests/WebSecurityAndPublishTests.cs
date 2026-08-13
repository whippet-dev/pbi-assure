using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

public sealed class WebSecurityAndPublishTests
{
    [Fact]
    public void CloudflareHeadersRestrictUnusedBrowserCapabilities()
    {
        var repositoryRoot = FindRepositoryRoot();
        var headers = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "wwwroot", "_headers"));
        var viewerRule = headers[headers.IndexOf("/report-viewer.html", StringComparison.Ordinal)..];
        var applicationRule = headers[..headers.IndexOf("/report-viewer.html", StringComparison.Ordinal)];

        Assert.Contains("default-src 'self'", applicationRule, StringComparison.Ordinal);
        Assert.Contains("script-src 'self' 'wasm-unsafe-eval'", applicationRule, StringComparison.Ordinal);
        Assert.Contains("connect-src 'self'", applicationRule, StringComparison.Ordinal);
        Assert.DoesNotContain("connect-src 'none'", applicationRule, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-inline'", applicationRule, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", applicationRule, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", applicationRule, StringComparison.Ordinal);
        Assert.Contains("form-action 'none'", applicationRule, StringComparison.Ordinal);
        Assert.Contains("X-Content-Type-Options: nosniff", headers, StringComparison.Ordinal);
        Assert.Contains("Referrer-Policy: no-referrer", headers, StringComparison.Ordinal);
        Assert.Contains("/download.js", headers, StringComparison.Ordinal);
        Assert.Contains("Cache-Control: no-cache", headers, StringComparison.Ordinal);
        Assert.Contains(
            "Permissions-Policy: camera=(), microphone=(), geolocation=()",
            headers,
            StringComparison.Ordinal);
        Assert.StartsWith("/report-viewer.html", viewerRule, StringComparison.Ordinal);
        Assert.Contains("! Content-Security-Policy", viewerRule, StringComparison.Ordinal);
        Assert.Contains("default-src 'none'", viewerRule, StringComparison.Ordinal);
        Assert.Contains("script-src 'self' 'unsafe-inline'", viewerRule, StringComparison.Ordinal);
        Assert.Contains("style-src 'unsafe-inline'", viewerRule, StringComparison.Ordinal);
        Assert.Contains("connect-src 'none'", viewerRule, StringComparison.Ordinal);
        Assert.DoesNotContain("report-uri", headers, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("report-to", headers, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0.1.0+c6cc6e5c64e8e87ebe299a3783afbc905e5ddc32", "c6cc6e5c64e8")]
    [InlineData("0.1.0+c6cc6e5c64e8e87ebe299a3783afbc905e5ddc32-dirty", "c6cc6e5c64e8-dirty")]
    [InlineData("0.1.0", null)]
    public void DisplaysAnHonestShortBuildRevision(string informationalVersion, string? expected)
    {
        Assert.Equal(expected, BrowserBuildInfo.DisplayRevision(informationalVersion));
    }

    [Fact]
    public void CleanPublishScriptTargetsOnlyTheGeneratedWebDirectory()
    {
        var repositoryRoot = FindRepositoryRoot();
        var script = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "Publish-Web.ps1"));
        var launcher = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "Publish-Web.cmd"));

        Assert.Contains("\"artifacts\\web\"", script, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $outputDirectory", script, StringComparison.Ordinal);
        Assert.Contains("-p:SourceRevisionId=$buildRevision", script, StringComparison.Ordinal);
        Assert.Contains("report-viewer.html", script, StringComparison.Ordinal);
        Assert.Contains("report-viewer.js", script, StringComparison.Ordinal);
        Assert.Contains("__PBIASSURE_ASSET_VERSION__", script, StringComparison.Ordinal);
        Assert.Contains("Browser asset version", script, StringComparison.Ordinal);
        Assert.Contains("Tracked changes are present.", script, StringComparison.Ordinal);
        Assert.Contains("ls-files --others --exclude-standard", script, StringComparison.Ordinal);
        Assert.Contains("-AllowDirty", script, StringComparison.Ordinal);
        Assert.DoesNotContain("samples-local", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes-local", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-ExecutionPolicy Bypass", launcher, StringComparison.Ordinal);
        Assert.Contains("Publish-Web.ps1", launcher, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

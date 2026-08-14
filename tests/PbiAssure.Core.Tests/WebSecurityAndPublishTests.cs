using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

public sealed class WebSecurityAndPublishTests
{
    [Fact]
    public void PublicPrivacyAndSecurityInformationIsLinkedFromReadmeAndBrowser()
    {
        var repositoryRoot = FindRepositoryRoot();
        var privacy = File.ReadAllText(Path.Combine(repositoryRoot, "PRIVACY.md"));
        var security = File.ReadAllText(Path.Combine(repositoryRoot, "SECURITY.md"));
        var readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));
        var browserMarkup = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "Pages", "Home.razor"));

        Assert.Contains("processed locally in your browser", privacy, StringComparison.Ordinal);
        Assert.Contains("does not upload selected project files", privacy, StringComparison.Ordinal);
        Assert.Contains("Private Vulnerability Reporting", security, StringComparison.Ordinal);
        Assert.Contains("[Privacy](PRIVACY.md)", readme, StringComparison.Ordinal);
        Assert.Contains("[Security](SECURITY.md)", readme, StringComparison.Ordinal);
        Assert.Contains(">How privacy works</a>", browserMarkup, StringComparison.Ordinal);
        Assert.Contains("/blob/master/PRIVACY.md", browserMarkup, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\" rel=\"noopener noreferrer\"", browserMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserSurfaceExposesAnExplicitApplicationReadyMarker()
    {
        var repositoryRoot = FindRepositoryRoot();
        var markup = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "Pages", "Home.razor"));

        Assert.Contains("data-pbiassure-app-ready=\"true\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void CloudflareHeadersRestrictUnusedBrowserCapabilities()
    {
        var repositoryRoot = FindRepositoryRoot();
        var headers = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "wwwroot", "_headers"));
        var htmlViewerRuleStart = headers.IndexOf("/report-viewer.html", StringComparison.Ordinal);
        var extensionlessViewerRuleStart = headers.IndexOf("/report-viewer\n", StringComparison.Ordinal);
        var htmlViewerRule = headers[htmlViewerRuleStart..extensionlessViewerRuleStart];
        var extensionlessViewerRule = headers[extensionlessViewerRuleStart..];
        var applicationRule = headers[..htmlViewerRuleStart];

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
        Assert.StartsWith("/report-viewer.html", htmlViewerRule, StringComparison.Ordinal);
        Assert.StartsWith("/report-viewer", extensionlessViewerRule, StringComparison.Ordinal);
        foreach (var viewerRule in new[] { htmlViewerRule, extensionlessViewerRule })
        {
            Assert.Contains("! Content-Security-Policy", viewerRule, StringComparison.Ordinal);
            Assert.Contains("default-src 'none'", viewerRule, StringComparison.Ordinal);
            Assert.Contains("script-src 'self' 'unsafe-inline'", viewerRule, StringComparison.Ordinal);
            Assert.Contains("style-src 'unsafe-inline'", viewerRule, StringComparison.Ordinal);
            Assert.Contains("connect-src 'none'", viewerRule, StringComparison.Ordinal);
            Assert.Contains("frame-ancestors 'none'", viewerRule, StringComparison.Ordinal);
        }
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
            Path.Combine(repositoryRoot, "scripts", "Publish-Web.mjs"));
        var commandLauncher = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "Publish-Web.cmd"));
        var powerShellLauncher = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "Publish-Web.ps1"));
        var cloudflareLauncher = File.ReadAllText(
            Path.Combine(repositoryRoot, "scripts", "Publish-Web-Cloudflare.sh"));

        Assert.Contains("resolve(repositoryRoot, \"artifacts\", \"web\")", script, StringComparison.Ordinal);
        Assert.Contains("rmSync(outputDirectory, { recursive: true, force: true })", script, StringComparison.Ordinal);
        Assert.Contains("`-p:SourceRevisionId=${buildRevision}`", script, StringComparison.Ordinal);
        Assert.Contains("report-viewer.html", script, StringComparison.Ordinal);
        Assert.Contains("report-viewer.js", script, StringComparison.Ordinal);
        Assert.Contains("__PBIASSURE_ASSET_VERSION__", script, StringComparison.Ordinal);
        Assert.Contains("Browser asset version", script, StringComparison.Ordinal);
        Assert.Contains("--source-revision", script, StringComparison.Ordinal);
        Assert.Contains("does not match the checked-out Git revision", script, StringComparison.Ordinal);
        Assert.Contains("Tracked or untracked build-input changes are present.", script, StringComparison.Ordinal);
        Assert.Contains("ls-files", script, StringComparison.Ordinal);
        Assert.Contains("--allow-dirty", script, StringComparison.Ordinal);
        Assert.DoesNotContain("samples-local", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes-local", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Publish-Web.mjs", commandLauncher, StringComparison.Ordinal);
        Assert.Contains("Publish-Web.mjs", powerShellLauncher, StringComparison.Ordinal);
        Assert.Contains("--source-revision", powerShellLauncher, StringComparison.Ordinal);
        Assert.Contains("global.json", cloudflareLauncher, StringComparison.Ordinal);
        Assert.Contains("https://dot.net/v1/dotnet-install.sh", cloudflareLauncher, StringComparison.Ordinal);
        Assert.Contains("--version", cloudflareLauncher, StringComparison.Ordinal);
        Assert.Contains("DOTNET_CLI_TELEMETRY_OPTOUT=1", cloudflareLauncher, StringComparison.Ordinal);
        Assert.Contains("CF_PAGES_COMMIT_SHA", cloudflareLauncher, StringComparison.Ordinal);
        Assert.Contains("Publish-Web.mjs", cloudflareLauncher, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet publish", cloudflareLauncher, StringComparison.Ordinal);
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

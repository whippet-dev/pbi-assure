using System.Diagnostics;
using Microsoft.Playwright;

namespace PbiAssure.Privacy.E2E;

public sealed class PrivacyE2EFixture : IAsyncLifetime
{
    private const string DeployedBaseUrlVariable = "PBIASSURE_PRIVACY_BASE_URL";

    public string RepositoryRoot { get; } = FindRepositoryRoot();
    public string FixtureDirectory => Path.Combine(
        RepositoryRoot,
        "tests",
        "fixtures",
        "privacy-canary",
        PrivacyCanaries.ProjectName);
    public string EvidenceDirectory => Path.Combine(RepositoryRoot, "artifacts", "privacy-e2e", "evidence");
    public string OutputDirectory => Path.Combine(RepositoryRoot, "artifacts", "privacy-e2e", "outputs");
    public string SourceRevision { get; private set; } = string.Empty;
    public string BaseUrl { get; private set; } = string.Empty;
    public bool IsDeployedSmoke { get; private set; }
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    private PrivacyTestHost? host;

    public async Task InitializeAsync()
    {
        Console.WriteLine("Privacy E2E setup: resolving source revision.");
        SourceRevision = (await RunProcessAsync("git", ["rev-parse", "HEAD"])).Trim();
        var deployedBaseUrl = Environment.GetEnvironmentVariable(DeployedBaseUrlVariable);
        if (string.IsNullOrWhiteSpace(deployedBaseUrl))
        {
            Console.WriteLine("Privacy E2E setup: publishing clean local web application.");
            await RunProcessAsync("node", ["./scripts/Publish-Web.mjs", "--allow-dirty"]);
            var publishedRoot = Path.Combine(RepositoryRoot, "artifacts", "web", "wwwroot");
            Console.WriteLine("Privacy E2E setup: starting local static host.");
            host = await PrivacyTestHost.StartAsync(publishedRoot).WaitAsync(TimeSpan.FromSeconds(30));
            BaseUrl = host.BaseUrl;
        }
        else
        {
            BaseUrl = deployedBaseUrl.TrimEnd('/');
            IsDeployedSmoke = true;
        }

        Directory.CreateDirectory(EvidenceDirectory);
        Directory.CreateDirectory(OutputDirectory);
        Console.WriteLine("Privacy E2E setup: starting Playwright Chromium.");
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        })
            .WaitAsync(TimeSpan.FromSeconds(30));
        Console.WriteLine("Privacy E2E setup: ready.");
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        Playwright?.Dispose();
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }

    private async Task<string> RunProcessAsync(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = RepositoryRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
        }
        catch (TimeoutException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException($"{fileName} did not finish within two minutes.");
        }
        var output = await standardOutput;
        var error = await standardError;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{fileName} failed with exit code {process.ExitCode}.{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }

        return output;
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

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PrivacyE2EGroup : ICollectionFixture<PrivacyE2EFixture>
{
    public const string Name = "Privacy E2E";
}

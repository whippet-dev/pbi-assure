namespace PbiAssure.Core.Tests;

public sealed class SupportedProjectInputSurfaceTests
{
    [Fact]
    public void BrowserAndDesktopSurfaceTheSharedTmslInputBoundaryWithoutOfferingOutput()
    {
        var repositoryRoot = FindRepositoryRoot();
        var browser = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Web", "Pages", "Home.razor"));
        var desktop = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Desktop", "MainWindow.xaml.cs"));
        var guidanceDisclosure = browser.IndexOf("<details class=\"guidance-panel\">", StringComparison.Ordinal);
        var alwaysVisibleIntro = browser[..guidanceDisclosure];

        Assert.Contains("Check or prepare your Power BI project", browser, StringComparison.Ordinal);
        Assert.Contains("model.bim</code> (TMSL) is not supported yet", browser, StringComparison.Ordinal);
        Assert.Contains("For full assurance of a local semantic model", alwaysVisibleIntro, StringComparison.Ordinal);
        Assert.Contains("PBIR and TMDL", alwaysVisibleIntro, StringComparison.Ordinal);
        Assert.Contains(".SemanticModel/definition/", alwaysVisibleIntro, StringComparison.Ordinal);
        Assert.Contains("Local <code>model.bim</code> (TMSL) models are not supported", alwaysVisibleIntro, StringComparison.Ordinal);
        Assert.Contains("catch (UnsupportedProjectInputException exception)", browser, StringComparison.Ordinal);
        Assert.Contains("message = exception.Message;", browser, StringComparison.Ordinal);
        Assert.Contains("catch (UnsupportedProjectInputException exception)", desktop, StringComparison.Ordinal);
        Assert.Contains("ClearOutputState();", desktop, StringComparison.Ordinal);
        Assert.Contains("OpenReportButton.IsEnabled = false;", desktop, StringComparison.Ordinal);
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

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

        var guidance = browser[guidanceDisclosure..];

        Assert.Contains("Check or prepare your Power BI project", browser, StringComparison.Ordinal);
        // The boundary is still stated on this page, in the preparation guidance where someone who
        // needs it is already looking, and in full by the exception when a TMSL project is chosen.
        Assert.Contains("model.bim</code> (TMSL) is not supported yet", guidance, StringComparison.Ordinal);
        Assert.Contains("PBIR and TMDL", guidance, StringComparison.Ordinal);
        Assert.Contains(".SemanticModel", guidance, StringComparison.Ordinal);
        // It is deliberately not in the copy a newcomer reads before choosing anything: none of it
        // means anything until you have a project that hits it.
        foreach (var vocabulary in new[] { "PBIR", "TMDL", "TMSL", "model.bim", ".SemanticModel" })
        {
            Assert.DoesNotContain(vocabulary, alwaysVisibleIntro, StringComparison.OrdinalIgnoreCase);
        }

        // What the newcomer copy says instead: which folder to choose.
        Assert.Contains("Power BI Project (<code>.pbip</code>) folder", alwaysVisibleIntro, StringComparison.Ordinal);
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

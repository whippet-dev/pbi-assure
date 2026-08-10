namespace PbiAssure.Core.Tests;

public sealed class DesktopCsvFeatureSurfaceTests
{
    [Fact]
    public void DesktopShellProvidesSemanticCsvAndOutputFolderActions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Desktop", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(repositoryRoot, "src", "PbiAssure.Desktop", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"OpenSemanticCsvButton\"", markup, StringComparison.Ordinal);
        Assert.Contains("Click=\"OpenSemanticCsv_Click\"", markup, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OpenOutputFolderButton\"", markup, StringComparison.Ordinal);
        Assert.Contains("AssuranceOutputWriter.WriteDefaultOutputsAsync", code, StringComparison.Ordinal);
        Assert.Contains("OpenFile(latestSemanticUsageCsvPath, \"semantic CSV\", OpenSemanticCsvButton)", code, StringComparison.Ordinal);
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

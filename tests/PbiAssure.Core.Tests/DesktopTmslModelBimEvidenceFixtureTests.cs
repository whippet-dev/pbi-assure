using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class DesktopTmslModelBimEvidenceFixtureTests
{
    [Fact]
    public void DesktopAuthoredTmslFixtureStopsBeforeIncompleteAssuranceCanRun()
    {
        var exception = Assert.Throws<UnsupportedProjectInputException>(() =>
            ProjectScanner.Scan(FixturePath("desktop-tmsl-model-bim-evidence")));

        Assert.Contains("TMSL format (model.bim)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("No assurance output was generated.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopUpgradedTmdlFixtureScansItsEquivalentModelNormally()
    {
        var inventory = ProjectScanner.Scan(FixturePath("desktop-tmsl-model-bim-evidence-tmdl"));

        Assert.Equal(1, inventory.ReportCount);
        Assert.Equal(1, inventory.SemanticModelCount);
        Assert.Equal(3, inventory.DeveloperSemanticObjectCount);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-MODEL-001");
    }

    private static string FixturePath(string name) => Path.Combine(RepositoryRoot(), "tests", "fixtures", name);

    private static string RepositoryRoot()
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

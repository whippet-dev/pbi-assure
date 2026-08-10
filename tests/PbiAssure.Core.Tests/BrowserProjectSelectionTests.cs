using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

public sealed class BrowserProjectSelectionTests
{
    private static readonly BrowserProjectSelectionLimits Limits = BrowserProjectSelectionLimits.Default;

    [Fact]
    public void UsesReviewedBrowserSafetyLimits()
    {
        Assert.Equal(10_000, Limits.MaxVisitedEntries);
        Assert.Equal(5_000, Limits.MaxAcceptedFiles);
        Assert.Equal(25L * 1024 * 1024, Limits.MaxFileBytes);
        Assert.Equal(100L * 1024 * 1024, Limits.MaxTotalBytes);
        Assert.Equal(64, Limits.MaxDirectoryDepth);
    }

    [Fact]
    public void AcceptsOneRootProjectAndImmediateArtifactTrees()
    {
        var selection = Selection(
            File("Sales.pbip"),
            File("Sales.Report/definition/pages/pages.json"),
            File("Shared.SemanticModel/definition/tables/Sales.tmdl"));

        var validated = BrowserProjectSelectionValidator.Validate(selection);

        Assert.Equal(3, validated.Files.Count);
        Assert.Equal(3, validated.TotalBytes);
    }

    [Fact]
    public void RejectsFolderWithNoRootProject()
    {
        var selection = Selection(File("Sales.Report/definition/pages/pages.json"));

        var exception = Assert.Throws<BrowserProjectSelectionException>(() =>
            BrowserProjectSelectionValidator.Validate(selection));

        Assert.Contains("No Power BI project", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsMultipleRootProjects()
    {
        var selection = Selection(
            File("Sales.pbip"),
            File("Finance.pbip"),
            File("Sales.Report/definition/pages/pages.json"));

        var exception = Assert.Throws<BrowserProjectSelectionException>(() =>
            BrowserProjectSelectionValidator.Validate(selection));

        Assert.Contains("More than one Power BI project", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsCaseInsensitiveDuplicatePaths()
    {
        var selection = Selection(
            File("Sales.pbip"),
            File("Sales.Report/definition/pages/pages.json"),
            File("sales.report/DEFINITION/pages/pages.json"));

        var exception = Assert.Throws<BrowserProjectSelectionException>(() =>
            BrowserProjectSelectionValidator.Validate(selection));

        Assert.Contains("duplicate file paths", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsFilesOutsideImmediateProjectArtifactTrees()
    {
        var selection = Selection(
            File("Sales.pbip"),
            File("Nested/Sales.Report/definition/pages/pages.json"));

        var exception = Assert.Throws<BrowserProjectSelectionException>(() =>
            BrowserProjectSelectionValidator.Validate(selection));

        Assert.Contains("unexpected project metadata paths", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../Sales.pbip")]
    [InlineData("C:\\Sales.pbip")]
    [InlineData("/Sales.pbip")]
    public void RejectsUnsafeBrowserPaths(string path)
    {
        var selection = Selection(
            File(path),
            File("Sales.Report/definition/pages/pages.json"));

        var exception = Assert.Throws<BrowserProjectSelectionException>(() =>
            BrowserProjectSelectionValidator.Validate(selection));

        Assert.Contains("invalid file path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsVisitedEntryLimit()
    {
        var selection = Selection(File("Sales.pbip"), File("Sales.Report/definition/report.json")) with
        {
            VisitedEntries = Limits.MaxVisitedEntries + 1,
        };

        Assert.Throws<BrowserProjectSelectionException>(() => BrowserProjectSelectionValidator.Validate(selection));
    }

    [Fact]
    public void RejectsAcceptedFileLimit()
    {
        var smallLimits = Limits with { MaxAcceptedFiles = 1 };
        var selection = Selection(File("Sales.pbip"), File("Sales.Report/definition/report.json"));

        Assert.Throws<BrowserProjectSelectionException>(() =>
            BrowserProjectSelectionValidator.Validate(selection, smallLimits));
    }

    [Fact]
    public void RejectsIndividualFileSizeLimit()
    {
        var selection = Selection(
            File("Sales.pbip"),
            File("Sales.Report/definition/report.json", Limits.MaxFileBytes + 1));

        Assert.Throws<BrowserProjectSelectionException>(() => BrowserProjectSelectionValidator.Validate(selection));
    }

    [Fact]
    public void RejectsTotalSizeLimit()
    {
        var smallLimits = Limits with { MaxFileBytes = 10, MaxTotalBytes = 5 };
        var selection = Selection(
            File("Sales.pbip", 3),
            File("Sales.Report/definition/report.json", 3));

        Assert.Throws<BrowserProjectSelectionException>(() =>
            BrowserProjectSelectionValidator.Validate(selection, smallLimits));
    }

    [Fact]
    public void RejectsDepthLimit()
    {
        var smallLimits = Limits with { MaxDirectoryDepth = 2 };
        var selection = Selection(
            File("Sales.pbip"),
            File("Sales.Report/one/two/report.json"));

        Assert.Throws<BrowserProjectSelectionException>(() =>
            BrowserProjectSelectionValidator.Validate(selection, smallLimits));
    }

    private static BrowserProjectSelection Selection(params BrowserProjectFileManifest[] files) => new(
        "Sales",
        files.ToList(),
        files.Sum(file => file.Length),
        files.Length,
        files.Max(file => file.RelativePath.Count(character => character == '/')));

    private static BrowserProjectFileManifest File(string path, long length = 1) => new(path, length);
}

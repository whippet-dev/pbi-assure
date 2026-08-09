using PbiAssure.Cli;

namespace PbiAssure.Core.Tests;

public sealed class DefaultScanOutputPathTests
{
    [Fact]
    public void CreateUsesProjectOutputsFolderAndSortableLocalTimestampForHtml()
    {
        var projectPath = Path.Combine("C:", "Projects", "pbi-assure", "samples-local", "Columns Usage");
        var scanTime = new DateTime(2026, 8, 9, 9, 55, 32, DateTimeKind.Unspecified);

        var outputPath = DefaultScanOutputPath.Create(projectPath, scanTime, OutputFormat.Html);

        Assert.Equal(
            Path.Combine(projectPath, "outputs", "2026-08-09_09-55-32-000", "assurance.pbiassure.html"),
            outputPath);
    }

    [Fact]
    public void CreateUsesJsonFilenameWhenJsonIsRequested()
    {
        var outputPath = DefaultScanOutputPath.Create("Test Project", new DateTime(2026, 8, 9, 9, 55, 32), OutputFormat.Json);

        Assert.EndsWith(Path.Combine("outputs", "2026-08-09_09-55-32-000", "inventory.pbiassure.json"), outputPath, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePreservesAnExplicitOutputPath()
    {
        var outputPath = DefaultScanOutputPath.Resolve(
            "review\\custom.html",
            "Test Project",
            new DateTime(2026, 8, 9, 9, 55, 32),
            OutputFormat.Html);

        Assert.Equal("review\\custom.html", outputPath);
    }
}

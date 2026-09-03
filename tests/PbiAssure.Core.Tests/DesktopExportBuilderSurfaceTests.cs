using PbiAssure.Reporting.Exports;

namespace PbiAssure.Core.Tests;

public sealed class DesktopExportBuilderSurfaceTests
{
    [Fact]
    public void DesktopExportBuilderUsesTheSharedContractsAndRetainsOnlyTheSuccessfulScan()
    {
        var root = FindRepositoryRoot();
        var mainMarkup = File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Desktop", "MainWindow.xaml"));
        var mainCode = File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Desktop", "MainWindow.xaml.cs"));
        var dialogMarkup = File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Desktop", "ExportCsvWindow.xaml"));
        var dialogCode = File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Desktop", "ExportCsvWindow.xaml.cs"));

        Assert.Contains("x:Name=\"ExportCsvButton\"", mainMarkup, StringComparison.Ordinal);
        Assert.Contains("Click=\"ExportCsv_Click\"", mainMarkup, StringComparison.Ordinal);
        Assert.Contains("currentInventory = result.inventory", mainCode, StringComparison.Ordinal);
        Assert.Contains("ClearCurrentInventory();", mainCode, StringComparison.Ordinal);
        Assert.Contains("new ExportCsvWindow(currentInventory", mainCode, StringComparison.Ordinal);
        Assert.Contains("Data catalogue", dialogMarkup, StringComparison.Ordinal);
        Assert.Contains("Usage mapping", dialogMarkup, StringComparison.Ordinal);
        Assert.Contains("Select defaults", dialogMarkup, StringComparison.Ordinal);
        Assert.Contains("Save CSV", dialogMarkup, StringComparison.Ordinal);
        Assert.Contains("Cancel", dialogMarkup, StringComparison.Ordinal);
        Assert.Contains("ExportPresetCatalog.GetAllowedColumns", dialogCode, StringComparison.Ordinal);
        Assert.Contains("ExportPresetCatalog.GetDefaultColumnIds", dialogCode, StringComparison.Ordinal);
        Assert.Contains("new(selectedPreset, selectedColumnIds.ToArray())", dialogCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectScanner.Scan", dialogCode, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopExportBuilderProtectsTheExportContractAndCsvEncoding()
    {
        var root = FindRepositoryRoot();
        var dialogCode = File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Desktop", "ExportCsvWindow.xaml.cs"));
        var writerCode = File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Desktop", "DesktopExportCsvWriter.cs"));
        var fileNamesCode = File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Reporting", "Exports", "ExportCsvFileNames.cs"));
        var mainCode = File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Desktop", "MainWindow.xaml.cs"));

        Assert.Contains("selectedColumnIds.Count == 0", dialogCode, StringComparison.Ordinal);
        Assert.Contains("ExportCsvFileNames.Create(projectDisplayName, selectedPreset)", dialogCode, StringComparison.Ordinal);
        Assert.Contains("DesktopExportCsvWriter.Write(inventory, CreateRequest(), dialog.FileName)", dialogCode, StringComparison.Ordinal);
        Assert.Contains("ExportCsvRenderer.Render(inventory, request)", writerCode, StringComparison.Ordinal);
        Assert.Contains("new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)", writerCode, StringComparison.Ordinal);
        Assert.Contains("data-catalogue", fileNamesCode, StringComparison.Ordinal);
        Assert.Contains("usage-mapping", fileNamesCode, StringComparison.Ordinal);
        Assert.Contains("OpenFile(latestSemanticUsageCsvPath, \"semantic CSV\", OpenSemanticCsvButton)", mainCode, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportCsvFileNamesMatchTheBrowserConvention()
    {
        Assert.Equal("Sales Returns.data-catalogue.csv", ExportCsvFileNames.Create("Sales: Returns", ExportPreset.DataCatalogue));
        Assert.Equal("Sales Returns.usage-mapping.csv", ExportCsvFileNames.Create("Sales: Returns", ExportPreset.UsageMapping));
        Assert.Equal("pbi-assure.data-catalogue.csv", ExportCsvFileNames.Create("<>", ExportPreset.DataCatalogue));
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

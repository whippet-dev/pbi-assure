namespace PbiAssure.Core.Tests;

public sealed class WebExportBuilderSurfaceTests
{
    [Fact]
    public void WebExportBuilderUsesSharedContractsAndKeepsLegacyCsvSeparate()
    {
        var root = FindRepositoryRoot();
        var home = File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Web", "Pages", "Home.razor"));
        var panel = File.ReadAllText(Path.Combine(root, "src", "PbiAssure.Web", "Shared", "ExportCsvPanel.razor"));

        Assert.Contains(">Export data</button>", home, StringComparison.Ordinal);
        Assert.Contains(">Download semantic usage CSV</button>", home, StringComparison.Ordinal);
        Assert.Contains("@if (exportPanelOpen && selection is not null)", home, StringComparison.Ordinal);
        Assert.Contains("exportPanelKey++", home, StringComparison.Ordinal);
        Assert.Contains("ExportPreset.DataCatalogue", panel, StringComparison.Ordinal);
        Assert.Contains("ExportPresetCatalog.GetAllowedColumns(selectedPreset)", panel, StringComparison.Ordinal);
        Assert.Contains("ExportPresetCatalog.GetDefaultColumnIds(selectedPreset)", panel, StringComparison.Ordinal);
        Assert.Contains("new ExportRequest(selectedPreset, selectedColumns.ToArray())", panel, StringComparison.Ordinal);
        Assert.Contains("ExportCsvRenderer.Render", panel, StringComparison.Ordinal);
        Assert.Contains("\\uFEFF", panel, StringComparison.Ordinal);
        Assert.Contains("disabled=\"@(selectedColumns.Count == 0)\"", panel, StringComparison.Ordinal);
        Assert.Contains("<fieldset class=\"export-preset-options\">", panel, StringComparison.Ordinal);
        Assert.Contains("<fieldset class=\"export-column-options\">", panel, StringComparison.Ordinal);
        Assert.Contains("User-facing means PBI Assure found direct report evidence", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("SemanticUsageCsvRenderer.Render", panel, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

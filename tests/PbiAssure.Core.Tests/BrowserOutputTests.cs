using System.Text;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;
using PbiAssure.Reporting.Exports;
using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

public sealed class BrowserOutputTests
{
    [Fact]
    public void CreatesBrowserSafeDownloadNames()
    {
        Assert.Equal("Sales Returns.pbiassure.html", BrowserDownloadFileNames.Html("Sales: Returns"));
        Assert.Equal("Sales Returns.semantic-usage.csv", BrowserDownloadFileNames.SemanticUsageCsv("Sales: Returns"));
        Assert.Equal("Sales Returns.data-catalogue.csv", BrowserDownloadFileNames.ExportCsv("Sales: Returns", ExportPreset.DataCatalogue));
        Assert.Equal("Sales Returns.usage-mapping.csv", BrowserDownloadFileNames.ExportCsv("Sales: Returns", ExportPreset.UsageMapping));
        Assert.Equal("pbi-assure.pbiassure.html", BrowserDownloadFileNames.Html("<>"));
    }

    [Fact]
    public void RendersHtmlAndCsvFromBrowserStyleInMemorySource()
    {
        var source = new InMemoryProjectFileSource("Browser Sample", [
            File("Browser Sample.pbip", "{}"),
            File("Browser Sample.Report/definition.pbir", "{ \"datasetReference\": { \"byPath\": { \"path\": \"../Browser Sample.SemanticModel\" } } }"),
            File("Browser Sample.Report/definition/pages/pages.json", "{ \"pageOrder\": [\"page-a\"] }"),
            File("Browser Sample.Report/definition/pages/page-a/page.json", "{ \"name\": \"page-a\" }"),
            File("Browser Sample.SemanticModel/definition/tables/Sales.tmdl", "table Sales\n    column Amount\n        dataType: int64"),
        ]);

        var inventory = ProjectScanner.Scan(source);
        var html = HtmlReportRenderer.Render(inventory);
        var csv = SemanticUsageCsvRenderer.Render(inventory);

        Assert.Equal("Browser Sample", inventory.RootPath);
        Assert.Contains("Source project", html, StringComparison.Ordinal);
        Assert.Contains("Browser Sample", html, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", html, StringComparison.Ordinal);
        Assert.StartsWith("Report,Table,Object", csv, StringComparison.Ordinal);
        Assert.Equal(inventory.DeveloperSemanticObjectCount, csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length - 1);
    }

    private static ProjectFileContent File(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));
}

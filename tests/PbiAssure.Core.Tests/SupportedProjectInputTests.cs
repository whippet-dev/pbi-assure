using System.Text;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class SupportedProjectInputTests
{
    [Fact]
    public void LocalTmslModelStopsBeforeAnyFindingCanBeCreated()
    {
        var exception = Assert.Throws<UnsupportedProjectInputException>(() => ProjectScanner.Scan(Source(
            ("Sales.pbip", "{}"),
            ("Sales.Report/definition.pbir", "{}"),
            ("Sales.SemanticModel/model.bim", "{}"))));

        Assert.Contains("Sales.SemanticModel", exception.Message, StringComparison.Ordinal);
        Assert.Contains("TMSL format (model.bim)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("TMDL format", exception.Message, StringComparison.Ordinal);
        Assert.Contains("No assurance output was generated.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalModelContainingBothTmslAndTmdlStopsAsAmbiguous()
    {
        var exception = Assert.Throws<UnsupportedProjectInputException>(() => ProjectScanner.Scan(Source(
            ("Sales.pbip", "{}"),
            ("Sales.SemanticModel/model.bim", "{}"),
            ("Sales.SemanticModel/definition/model.tmdl", "model Model"))));

        Assert.Contains("both TMSL (model.bim) and TMDL (definition/) files", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalTmdlModelRemainsSupported()
    {
        var inventory = ProjectScanner.Scan(Source(
            ("Sales.pbip", "{}"),
            ("Sales.SemanticModel/definition/tables/Sales.tmdl", "table Sales")));

        Assert.Single(inventory.SemanticModels);
    }

    private static InMemoryProjectFileSource Source(params (string Path, string Contents)[] files) =>
        new("Supported input", files.Select(file => new ProjectFileContent(file.Path, Encoding.UTF8.GetBytes(file.Contents))));
}

using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class DynamicFormatStringDependencyTests
{
    [Fact]
    public void DesktopFixtureRetainsMeasureOwnedMultilineFormatStringDefinition()
    {
        var inventory = ScanFixture();
        var model = Assert.Single(inventory.SemanticModels);
        var fact = Assert.Single(model.Tables, table => table.Name == "Fact");
        var dynamicAmount = Assert.Single(fact.Measures, measure => measure.Name == "Dynamic Amount");
        var baseAmount = Assert.Single(fact.Measures, measure => measure.Name == "Base Amount");

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "VAR SelectedFormat =",
                "    SELECTEDVALUE ( FormatLookup[FormatString], \"#,0\" )",
                "VAR AmountValue = [Base Amount]",
                "RETURN",
                "    IF ( ISBLANK ( AmountValue ), \"#,0\", SelectedFormat )"),
            dynamicAmount.FormatStringExpression);
        Assert.Null(dynamicAmount.FormatString);
        Assert.Equal("#,0", baseAmount.FormatString);
        Assert.Null(baseAmount.FormatStringExpression);
    }

    [Fact]
    public void DesktopFormatStringExpressionProducesColumnAndMeasureReferences()
    {
        var inventory = ScanFixture();
        var model = Assert.Single(inventory.SemanticModels);
        var expression = Assert.Single(model.Tables, table => table.Name == "Fact").Measures
            .Single(measure => measure.Name == "Dynamic Amount").FormatStringExpression!;
        var references = DaxReferenceExtractor.Extract(
            expression,
            model.Tables.Select(table => table.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));

        Assert.Contains(references, reference =>
            reference.Table == "FormatLookup" && reference.ObjectName == "FormatString");
        Assert.Contains(references, reference =>
            reference.Table is null && reference.ObjectName == "Base Amount");
        Assert.Contains(inventory.SemanticDependencies, dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.Dax &&
            dependency.FromTable == "Fact" && dependency.FromObjectName == "Dynamic Amount" &&
            dependency.ToTable == "FormatLookup" && dependency.ToObjectName == "FormatString");
        Assert.Contains(inventory.SemanticDependencies, dependency =>
            dependency.DependencyKind == SemanticDependencyKinds.Dax &&
            dependency.FromTable == "Fact" && dependency.FromObjectName == "Dynamic Amount" &&
            dependency.ToTable == "Fact" && dependency.ToObjectName == "Base Amount");
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "FormatLookup", "FormatString").UsageState);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Fact", "Base Amount").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "FormatLookup", "FormatKey").UsageState);
    }

    [Fact]
    public void DynamicFormatStringExpressionRemainsInProcessOnly()
    {
        var inventory = ScanFixture();

        Assert.Equal("0.26", inventory.SchemaVersion);
        Assert.DoesNotContain("FormatStringExpression", JsonSerializer.Serialize(inventory), StringComparison.Ordinal);
    }

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == objectName);

    private static ProjectInventory ScanFixture() => ProjectScanner.Scan(Path.Combine(
        RepositoryRoot(), "tests", "fixtures", "desktop-dynamic-format-string-evidence"));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

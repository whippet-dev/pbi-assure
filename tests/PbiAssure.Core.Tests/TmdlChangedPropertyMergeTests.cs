using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

// Synthetic valid TMDL collection entries; not a Desktop-authored fixture.
public sealed class TmdlChangedPropertyMergeTests
{
    private const string SalesRepro = """
        table Sales
            changedProperty = Name
            changedProperty = IsHidden
        """;

    [Fact]
    public void SingleTableWithDistinctChangedPropertiesScansSuccessfully()
    {
        var inventory = Scan(SalesRepro);
        var table = Assert.Single(Assert.Single(inventory.SemanticModels).Tables);
        Assert.Equal("Sales", table.Name);
        Assert.Empty(table.Columns);
        Assert.Empty(table.Measures);
        Assert.Equal("0.26", inventory.SchemaVersion);
    }

    [Theory]
    [InlineData("changedProperty=Name", "changedProperty=IsHidden")]
    [InlineData("changedProperty =Name", "changedProperty= IsHidden")]
    [InlineData("changedProperty = Name", "CHANGEDPROPERTY = IsHidden")]
    public void DistinctCollectionEntriesKeepTheirFullDeclarationIdentity(string first, string second)
    {
        var table = Assert.Single(Assert.Single(Scan($"table Sales\n    {first}\n    {second}").SemanticModels).Tables);
        Assert.Equal("Sales", table.Name);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("IsHidden")]
    public void BothOriginalEntriesRemainAccountedForDuringPartialMerge(string property)
    {
        // Repeating either original entry from another partial declaration must fail.
        // This verifies that neither entry was discarded during collection merging.
        var exception = Assert.Throws<InvalidDataException>(() =>
            Scan(SalesRepro, $"table Sales\n    changedProperty = {property}"));
        Assert.Contains($"'changedProperty = {property}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("A.tmdl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("B.tmdl", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("changedProperty = Name", "changedProperty = Name")]
    [InlineData("changedProperty = Name", "CHANGEDPROPERTY = name")]
    public void RepeatedCollectionEntriesAreStillRejectedCaseInsensitively(string first, string second)
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Scan($"table Sales\n    {first}\n    {second}"));
        Assert.Contains("Duplicate TMDL definition", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DisjointCollectionEntriesAndChildrenMergeAcrossFiles()
    {
        var inventory = Scan(
            "table Sales\n    changedProperty = Name\n    column Amount",
            "table Sales\n    changedProperty = IsHidden\n    measure Total = SUM(Sales[Amount])");
        var table = Assert.Single(Assert.Single(inventory.SemanticModels).Tables);
        Assert.Equal("Amount", Assert.Single(table.Columns).Name);
        Assert.Equal("Total", Assert.Single(table.Measures).Name);
        Assert.Contains(inventory.SemanticDependencies, edge => edge.ToTable == "Sales" && edge.ToObjectName == "Amount");
    }

    [Theory]
    [InlineData("column Amount", "column Amount")]
    [InlineData("measure Total = 1", "measure Total = 2")]
    [InlineData("hierarchy Grouping", "hierarchy Grouping")]
    [InlineData("partition Load = m\n        source = 1", "partition Load = m\n        source = 2")]
    [InlineData("calculationGroup\n        calculationItem Current = 1", "calculationGroup\n        calculationItem Current = 2")]
    public void NamedSemanticChildrenStillRejectDuplicateDefinitions(string first, string second)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Scan(
            $"{SalesRepro}\n    {first}",
            $"table Sales\n    {second}"));
        Assert.Contains("Duplicate TMDL definition", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Sales", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("isHidden: true", "isHidden: true", "isHidden")]
    [InlineData("isHidden: true", "isHidden: false", "isHidden")]
    [InlineData("calculationGroup\n        precedence: 1", "calculationGroup\n        precedence: 2", "precedence")]
    [InlineData("calculationGroup\n        noSelectionExpression = 1", "calculationGroup\n        noSelectionExpression = 2", "noSelectionExpression")]
    public void ScalarPropertiesAndExpressionAssignmentsStillRejectDuplicates(string first, string second, string key)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Scan(
            $"{SalesRepro}\n    {first}",
            $"table Sales\n    {second}"));
        Assert.Contains($"'{key}'", exception.Message, StringComparison.Ordinal);
    }

    private static ProjectInventory Scan(params string[] declarations)
    {
        var files = new List<ProjectFileContent>
        {
            new("Repro.SemanticModel/definition.pbism", Encoding.UTF8.GetBytes("{}")),
        };
        files.AddRange(declarations.Select((text, index) => new ProjectFileContent(
            $"Repro.SemanticModel/definition/tables/{(char)('A' + index)}.tmdl", Encoding.UTF8.GetBytes(text))));
        return ProjectScanner.Scan(new InMemoryProjectFileSource("TMDL changedProperty merge", files));
    }
}

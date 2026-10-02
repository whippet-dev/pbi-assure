using System.Text;
using System.Text.Json;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

// Synthetic, specification-based inputs preserving the real user's repro; not Desktop provenance.
public sealed class TmdlTableDeclarationTests
{
    private const string UserRepro = """
        table Data
            column Amount
                dataType: int64
                isHidden: true
                sourceColumn: Amount

            partition Data = m
                mode: import
                source = #table(type table [Amount = Int64.Type], {{1}})

        table Calc
            calculationGroup
                precedence: 1
                noSelectionExpression = SUM(Data[Amount])
                calculationItem Current = SELECTEDMEASURE()

            column Name
                dataType: string
                sourceColumn: Name
        """;

    [Fact]
    public void RealUserShapeKeepsTableOwnershipAndSelectionDependency()
    {
        var inventory = Scan(("Combined.tmdl", UserRepro));
        var model = Assert.Single(inventory.SemanticModels);
        Assert.Equal("Calc,Data", string.Join(',', model.Tables.Select(table => table.Name)));
        var data = model.Tables.Single(table => table.Name == "Data");
        Assert.Equal("Amount", Assert.Single(data.Columns).Name);
        Assert.True(data.Columns[0].IsHidden);
        Assert.Equal("Data", Assert.Single(data.Partitions).Name);
        Assert.Null(data.CalculationGroup);
        var calc = model.Tables.Single(table => table.Name == "Calc");
        Assert.Equal("Name", Assert.Single(calc.Columns).Name);
        Assert.Empty(calc.Partitions);
        Assert.Equal("Current", Assert.Single(calc.CalculationGroup!.Items).Name);
        Assert.Equal("SUM(Data[Amount])", calc.CalculationGroup.NoSelectionExpression);
        Assert.Contains(inventory.SemanticDependencies, edge =>
            edge.FromTable == "Calc" && edge.FromObjectType == SemanticObjectTypes.Table &&
            edge.ToTable == "Data" && edge.ToObjectName == "Amount" &&
            edge.DependencyKind == SemanticDependencyKinds.Dax && edge.EvidenceText == "Data[Amount]");
        Assert.Equal(SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(usage => usage.Table == "Data" && usage.ObjectName == "Amount").UsageState);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Equal("0.26", inventory.SchemaVersion);
        var json = JsonSerializer.Serialize(model);
        Assert.DoesNotContain("NoSelectionExpression", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DefinitionPaths", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("isHidden", true)]
    [InlineData("isHidden: true", true)]
    [InlineData("isHidden: false", false)]
    public void BooleanFormsWorkForTableColumnMeasureAndHierarchy(string property, bool expected)
    {
        var inventory = Scan(("Flags.tmdl", $"""
            table Flags
                {property}
                column Value
                    {property}
                measure Result = 1
                    {property}
                hierarchy Grouping
                    {property}
                    level Value
                        column: Value
            """));
        var table = Assert.Single(Assert.Single(inventory.SemanticModels).Tables);
        Assert.Equal(expected, table.IsHidden);
        Assert.Equal(expected, Assert.Single(table.Columns).IsHidden);
        Assert.Equal(expected, Assert.Single(table.Measures).IsHidden);
        Assert.Equal(expected, Assert.Single(table.Hierarchies).IsHidden);
    }

    [Fact]
    public void PartialTableAddsChildrenWithoutDuplicateDictionaryFailure()
    {
        var inventory = Scan(("A.tmdl", UserRepro), ("B.tmdl", "table Data\n    measure One = 1\n"));
        var model = Assert.Single(inventory.SemanticModels);
        Assert.Equal(2, model.TableCount);
        var table = model.Tables.Single(table => table.Name == "Data");
        Assert.Equal("Amount", Assert.Single(table.Columns).Name);
        Assert.Equal("One", Assert.Single(table.Measures).Name);
        Assert.True(table.Columns[0].IsHidden);
    }

    [Fact]
    public void MultipleFilesAndTablesMergeDeterministicallyAndKeepLateProperties()
    {
        var a = ("A.tmdl", "table Other\n    column First\ntable Data\n    column Amount\n    isHidden: true\n");
        var b = ("B.tmdl", "table Data\n    measure One = 1\n    isPrivate: true\ntable Other\n    column Last\n");
        var first = Assert.Single(Scan(a, b).SemanticModels);
        var second = Assert.Single(Scan(b, a).SemanticModels);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal("Data,Other", string.Join(',', first.Tables.Select(table => table.Name)));
        var data = first.Tables[0];
        Assert.True(data.IsHidden);
        Assert.True(data.IsPrivate);
        Assert.Equal("Repro.SemanticModel/definition/tables/A.tmdl", data.RelativePath);
        Assert.Equal("Repro.SemanticModel/definition/tables/A.tmdl,Repro.SemanticModel/definition/tables/B.tmdl",
            string.Join(',', data.DefinitionPaths));
        Assert.Equal("First,Last", string.Join(',', first.Tables[1].Columns.Select(column => column.Name)));
    }

    [Theory]
    [InlineData("column Value\n        dataType: int64", "column Value\n        dataType: string", "column Value")]
    [InlineData("measure Result = 1", "measure Result = 2", "measure Result")]
    [InlineData("partition Load = m\n        source = 1", "partition Load = m\n        source = 2", "partition Load")]
    [InlineData("hierarchy Grouping", "hierarchy Grouping", "hierarchy Grouping")]
    [InlineData("isHidden: true", "isHidden: false", "isHidden")]
    public void ConflictingDefinitionsFailEarlyWithNamesAndBothPaths(string first, string second, string key)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Scan(
            ("A.tmdl", $"table Data\n    {first}\n"),
            ("B.tmdl", $"table Data\n    {second}\n")));
        Assert.Contains("Data", exception.Message, StringComparison.Ordinal);
        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
        Assert.Contains("A.tmdl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("B.tmdl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SameFilePartialDeclarationsAlsoMerge()
    {
        var inventory = Scan(("Combined.tmdl", "table Data\n    column Amount\ntable Data\n    measure One = 1\n"));
        var table = Assert.Single(Assert.Single(inventory.SemanticModels).Tables);
        Assert.Single(table.Columns);
        Assert.Single(table.Measures);
    }

    [Fact]
    public void SingletonContainersMergeDisjointPropertiesAndCalculationItems()
    {
        var inventory = Scan(
            ("A.tmdl", """
                /// Group description
                table Calc
                    calculationGroup
                        precedence: 1
                        calculationItem Current = SELECTEDMEASURE()
                    refreshPolicy
                        policyType: basic
                    column Name
                    isHidden: false
                table Data
                    column Amount
                """),
            ("B.tmdl", """
                table Calc
                    calculationGroup
                        noSelectionExpression = SUM(Data[Amount])
                        selectionExpression = SELECTEDMEASURE()
                        multipleOrEmptySelectionExpression = SELECTEDMEASURE()
                        calculationItem Previous = SELECTEDMEASURE()
                    refreshPolicy
                        rollingWindowPeriods: 3
                    isPrivate: true
                """));
        var calc = Assert.Single(inventory.SemanticModels).Tables.Single(table => table.Name == "Calc");
        Assert.Equal("Group description", calc.Description);
        Assert.False(calc.IsHidden);
        Assert.True(calc.IsPrivate);
        var group = calc.CalculationGroup!;
        Assert.Equal(1, group.Precedence);
        Assert.Equal("Current,Previous", string.Join(',', group.Items.Select(item => item.Name)));
        Assert.Equal("SELECTEDMEASURE()", group.SelectionExpression);
        Assert.Equal("SELECTEDMEASURE()", group.MultipleOrEmptySelectionExpression);
        Assert.Equal("SUM(Data[Amount])", group.NoSelectionExpression);
        Assert.Equal("basic", calc.RefreshPolicy!.PolicyType);
        Assert.Equal(3, calc.RefreshPolicy.RollingWindowPeriods);
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromTable == "Calc" && edge.ToObjectName == "Amount");
    }

    [Theory]
    [InlineData("noSelectionExpression = SUM(Data[Amount])")]
    [InlineData("noSelectionExpression =\n            SUM(Data[Amount])")]
    [InlineData("noSelectionExpression = ```\n            SUM(Data[Amount])\n            ```")]
    public void NoSelectionExpressionsUseExistingAssignmentAndDaxPaths(string definition)
    {
        var inventory = Scan(("Groups.tmdl", $"""
            table Data
                column Amount
            table Calc
                calculationGroup
                    {definition}
                    selectionExpression = SUM(Data[Amount])
                    multipleOrEmptySelectionExpression = SUM(Data[Amount])
            """));
        var calc = Assert.Single(inventory.SemanticModels).Tables.Single(table => table.Name == "Calc");
        Assert.Equal("SUM(Data[Amount])", calc.CalculationGroup!.NoSelectionExpression);
        Assert.Equal("SUM(Data[Amount])", calc.CalculationGroup.SelectionExpression);
        Assert.Equal("SUM(Data[Amount])", calc.CalculationGroup.MultipleOrEmptySelectionExpression);
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromTable == "Calc" && edge.ToObjectName == "Amount");
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
    }

    [Fact]
    public void UnresolvedNoSelectionReferenceKeepsConservativeEvidence()
    {
        var inventory = Scan(("Groups.tmdl", "table Data\n    column Amount\ntable Calc\n    calculationGroup\n        noSelectionExpression = SUM(Missing[Amount])\n"));
        Assert.Contains(inventory.UnresolvedSemanticDependencies, reference =>
            reference.FromTable == "Calc" && reference.ReferenceText == "Missing[Amount]");
        var amount = inventory.SemanticObjectUsages.Single(usage => usage.Table == "Data" && usage.ObjectName == "Amount");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, amount.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, amount.ClassificationConfidence);
    }

    [Fact]
    public void ATableLikeLineInsideAFencedExpressionIsNotANewDeclarationOrProperty()
    {
        var inventory = Scan(("Fence.tmdl", """
            table Data
                measure Text = ```
            table Phantom
                isHidden
                    ```
            table Calc
                column Name
            """));
        var model = Assert.Single(inventory.SemanticModels);
        Assert.Equal("Calc,Data", string.Join(',', model.Tables.Select(table => table.Name)));
        var data = model.Tables.Single(table => table.Name == "Data");
        Assert.False(data.IsHidden);
        Assert.Contains("table Phantom", Assert.Single(data.Measures).Expression, StringComparison.Ordinal);
        Assert.Empty(data.Columns);
    }

    [Theory]
    [InlineData("precedence: 1", "precedence: 2", "precedence")]
    [InlineData("calculationItem Current = 1", "calculationItem Current = 2", "calculationItem Current")]
    public void CalculationGroupConflictsAreNotSilentlyOverwritten(string first, string second, string key)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Scan(
            ("A.tmdl", $"table Calc\n    calculationGroup\n        {first}\n"),
            ("B.tmdl", $"table Calc\n    calculationGroup\n        {second}\n")));
        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
        Assert.Contains("A.tmdl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("B.tmdl", exception.Message, StringComparison.Ordinal);
    }

    private static ProjectInventory Scan(params (string Path, string Content)[] tables)
    {
        var files = new List<ProjectFileContent> { File("Repro.SemanticModel/definition.pbism", "{}") };
        files.AddRange(tables.Select(table => File($"Repro.SemanticModel/definition/tables/{table.Path}", table.Content)));
        return ProjectScanner.Scan(new InMemoryProjectFileSource("TMDL table declarations", files));
    }

    private static ProjectFileContent File(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));
}

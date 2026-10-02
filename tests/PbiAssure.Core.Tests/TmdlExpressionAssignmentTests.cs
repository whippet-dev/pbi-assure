using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class TmdlExpressionAssignmentTests
{
    [Theory]
    [InlineData(" = ")]
    [InlineData("= ")]
    [InlineData(" =")]
    [InlineData("=")]
    [InlineData("  =  ")]
    [InlineData("\t=\t")]
    [InlineData(" \t = \t ")]
    public void AssignmentWhitespacePreservesCalculationGroupDependenciesAndUsage(string separator)
    {
        var inventory = ScanGroup($"        noSelectionExpression{separator}SUM(Data[Amount])\n");
        var baseline = ScanGroup("        noSelectionExpression = SUM(Data[Amount])\n");

        Assert.Equal("SUM(Data[Amount])", Group(inventory).NoSelectionExpression);
        Assert.Equal(baseline.SemanticDependencies, inventory.SemanticDependencies);
        Assert.Equal(baseline.SemanticObjectUsages, inventory.SemanticObjectUsages);
        AssertAmountDependency(inventory, "Calc", "Calc");
        var usage = Assert.Single(inventory.SemanticObjectUsages,
            item => item.Table == "Data" && item.ObjectName == "Amount");
        Assert.Equal(SemanticUsageStates.UsedOnlyByUnusedBranch, usage.UsageState);
        Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Empty(inventory.AnalysisLimitations);
        Assert.Equal("0.26", inventory.SchemaVersion);
    }

    [Theory]
    [InlineData("        noSelectionExpression =\n            SUM(Data[Amount])\n", "SUM(Data[Amount])")]
    [InlineData("        noSelectionExpression=\n\n            SUM(Data[Amount])\n\n", "\nSUM(Data[Amount])")]
    [InlineData("        noSelectionExpression\t=\t\n            SUM(Data[Amount])\n", "SUM(Data[Amount])")]
    [InlineData("        noSelectionExpression = ```\n            SUM(Data[Amount])\n            ```\n", "SUM(Data[Amount])")]
    [InlineData("        noSelectionExpression=```\n            SUM(Data[Amount])\n            ```\n", "SUM(Data[Amount])")]
    public void MultilineAndFencedAssignmentsStopBeforeFollowingProperties(string definition, string expected)
    {
        var inventory = ScanGroup(definition + "        precedence: 7\n");

        Assert.Equal(expected.Replace("\n", Environment.NewLine), Group(inventory).NoSelectionExpression);
        Assert.Equal(7, Group(inventory).Precedence);
        AssertAmountDependency(inventory, "Calc", "Calc");
    }

    [Fact]
    public void CompactFencesRetainBlankLinesAndRelativeIndentation()
    {
        var inventory = ScanGroup(
            "        noSelectionExpression=```\n" +
            "            VAR Total =\n" +
            "                SUM(Data[Amount])\n" +
            "\n" +
            "            RETURN Total\n" +
            "            ```\n" +
            "        precedence: 7\n");

        Assert.Equal(string.Join(Environment.NewLine,
            "VAR Total =", "    SUM(Data[Amount])", string.Empty, "RETURN Total"),
            Group(inventory).NoSelectionExpression);
        Assert.Equal(7, Group(inventory).Precedence);
        AssertAmountDependency(inventory, "Calc", "Calc");
    }

    [Theory]
    [InlineData("noSelectionExpressionExtra=SUM(Data[Amount])")]
    [InlineData("noSelectionExpressionExtra = SUM(Data[Amount])")]
    [InlineData("noSelectionExpression Extra = SUM(Data[Amount])")]
    public void OnlyTheExactPropertyNameAndEqualsDelimiterMatch(string definition)
    {
        var inventory = ScanGroup($"        {definition}\n        noSelectionExpression = 1\n");

        Assert.Equal("1", Group(inventory).NoSelectionExpression);
        Assert.DoesNotContain(inventory.SemanticDependencies,
            edge => edge.FromTable == "Calc" && edge.ToObjectName == "Amount");
    }

    [Fact]
    public void AColonDoesNotAssignAnExpression()
    {
        var inventory = ScanGroup("        noSelectionExpression: SUM(Data[Amount])\n");

        Assert.Null(Group(inventory).NoSelectionExpression);
        Assert.DoesNotContain(inventory.SemanticDependencies,
            edge => edge.FromTable == "Calc" && edge.ToObjectName == "Amount");
    }

    [Fact]
    public void AChildAssignmentDoesNotBelongToTheCalculationGroup()
    {
        var inventory = ScanGroup(
            "        calculationItem Current = SELECTEDMEASURE()\n" +
            "            noSelectionExpression=SUM(Data[Amount])\n");

        Assert.Null(Group(inventory).NoSelectionExpression);
        Assert.DoesNotContain(inventory.SemanticDependencies,
            edge => edge.FromTable == "Calc" && edge.ToObjectName == "Amount");
    }

    [Theory]
    [InlineData("        noSelectionExpression=\n")]
    [InlineData("        noSelectionExpression = \n\n            \n")]
    [InlineData("        noSelectionExpression=```\n            ```\n")]
    public void EmptyAssignmentsRemainEmptyRatherThanAbsent(string definition)
    {
        var inventory = ScanGroup(definition + "        precedence: 7\n");

        Assert.Equal(string.Empty, Group(inventory).NoSelectionExpression);
        Assert.Equal(7, Group(inventory).Precedence);
    }

    [Theory]
    [InlineData(" = ")]
    [InlineData("=")]
    public void SharedReaderPreservesMeasureFormatAndKpiExpressions(string separator)
    {
        var inventory = Scan("""
            table Data
                column Amount
            table Metrics
                measure Result = 1
            """ + "\n" +
            $"        formatStringDefinition{separator}IF(SUM(Data[Amount]) > 0, \"0\", \"0.00\")\n" +
            "        kpi\n" +
            $"            targetExpression{separator}SUM(Data[Amount])\n" +
            $"            statusExpression{separator}SUM(Data[Amount])\n" +
            $"            trendExpression{separator}SUM(Data[Amount])\n");
        var measure = Assert.Single(Model(inventory).Tables.Single(table => table.Name == "Metrics").Measures);

        Assert.Equal("IF(SUM(Data[Amount]) > 0, \"0\", \"0.00\")", measure.FormatStringExpression);
        var kpi = Assert.IsType<SemanticKpiInventory>(measure.Kpi);
        Assert.Equal("SUM(Data[Amount])", kpi.TargetExpression);
        Assert.Equal("SUM(Data[Amount])", kpi.StatusExpression);
        Assert.Equal("SUM(Data[Amount])", kpi.TrendExpression);
        AssertAmountDependency(inventory, "Metrics", "Result");
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
    }

    [Theory]
    [InlineData(" = ")]
    [InlineData("=")]
    public void SharedReaderPreservesCalculatedAndMPartitionSources(string separator)
    {
        var inventory = Scan("""
            table Data
                column Amount
                partition Data = m
                    mode: import
            """ + "\n" +
            $"        source{separator}#table({{\"Amount\"}}, {{{{1}}}})\n" +
            "table Calculated\n" +
            "    column Amount\n" +
            "    partition Calculated = calculated\n" +
            "        mode: import\n" +
            $"        source{separator}SELECTCOLUMNS(Data, \"Amount\", Data[Amount])\n");

        Assert.Equal("#table({\"Amount\"}, {{1}})",
            Assert.Single(Model(inventory).Tables.Single(table => table.Name == "Data").Partitions).Expression);
        Assert.Equal("SELECTCOLUMNS(Data, \"Amount\", Data[Amount])",
            Assert.Single(Model(inventory).Tables.Single(table => table.Name == "Calculated").Partitions).Expression);
        AssertAmountDependency(inventory, "Calculated", "Calculated");
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
    }

    private static ProjectInventory ScanGroup(string definition) => Scan(
        "table Data\n    column Amount\ntable Calc\n    calculationGroup\n" + definition + "    column Name\n");

    private static ProjectInventory Scan(string tables) => ProjectScanner.Scan(
        new InMemoryProjectFileSource("TMDL expression assignment", [
            new ProjectFileContent("Repro.SemanticModel/definition.pbism", Encoding.UTF8.GetBytes("{}")),
            new ProjectFileContent("Repro.SemanticModel/definition/tables/Repro.tmdl", Encoding.UTF8.GetBytes(tables)),
        ]));

    private static SemanticModelInventory Model(ProjectInventory inventory) => Assert.Single(inventory.SemanticModels);

    private static SemanticCalculationGroupInventory Group(ProjectInventory inventory) =>
        Assert.IsType<SemanticCalculationGroupInventory>(Model(inventory).Tables.Single(table => table.Name == "Calc").CalculationGroup);

    private static void AssertAmountDependency(ProjectInventory inventory, string fromTable, string fromObject) =>
        Assert.Contains(inventory.SemanticDependencies, edge =>
            edge.FromTable == fromTable && edge.FromObjectName == fromObject &&
            edge.ToTable == "Data" && edge.ToObjectName == "Amount" && edge.DependencyKind == SemanticDependencyKinds.Dax);
}

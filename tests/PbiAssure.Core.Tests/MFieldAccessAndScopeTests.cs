using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class MFieldAccessAndScopeTests
{
    [Theory]
    [InlineData("Table.FromRecords({[Alpha = List.Contains({true}, Bar = 1)]})")]
    [InlineData("Table.FromRecords({[Alpha = List.Last({false, Bar = 1})]})")]
    [InlineData("Table.FromRecords({[Alpha = Bar]})")]
    [InlineData("Table.FromRecords({[Alpha = List.Contains({true},\n Bar = 1)]})")]
    [InlineData("let\n Rec = [\n Bar = 1\n ],\n Result = Bar\nin Table.FromValue(Result)")]
    public void RecordValueExpressionsRetainGenuineQueryReferences(string expression)
    {
        var inventory = Scan(expression);
        Assert.Contains(inventory.PowerQueryDependencies,
            edge => edge.FromQueryName == "Probe" && edge.ToQueryName == "Bar");
        Assert.Equal(PowerQueryUsageStates.SupportingQuery, Query(inventory, "Bar").UsageState);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    [Theory]
    [InlineData("[Bar = 1]")]
    [InlineData("[A = 1, Bar = 2]")]
    [InlineData("[A = List.Count({1, 2}), Bar = 2]")]
    [InlineData("[A = [B = 1], Bar = 2]")]
    [InlineData("[#\"A(\" = 1, Bar = 2]")]
    [InlineData("Rec[Bar]")]
    [InlineData("each [Bar]")]
    [InlineData("Table.FromRecords({[Bar = 1, Alpha = Bar]})")]
    [InlineData("Table.FromValue([#\"Bar\" = 1][#\"Bar\"])")]
    [InlineData("Table.FromRecords({[let = 1]})")]
    public void ProvenFieldPositionsDoNotCreateQueryReferences(string expression)
    {
        var inventory = Scan(expression);
        Assert.DoesNotContain(inventory.PowerQueryDependencies, edge => edge.ToQueryName == "Bar");
        Assert.Equal(PowerQueryRoles.ApparentlyOrphaned, Query(inventory, "Bar").QueryRole);
    }

    [Fact]
    public void QuotedLetFieldDoesNotSuppressOrphanFindings()
    {
        const string expression = "Table.FromRecords({[#\"let\" = 1]})";
        Assert.False(MReferenceExtractor.HasIncompleteReferences(expression));
        var inventory = Scan(expression);
        Assert.Equal(PowerQueryRoles.ApparentlyOrphaned, Query(inventory, "Bar").QueryRole);
        Assert.Contains(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    [Theory]
    [InlineData("#\"let\"")]
    [InlineData("#\"a\"\"let\"")]
    [InlineData("\"let let\"")]
    [InlineData("/* let let */ 1")]
    [InlineData("// let let\n1")]
    [InlineData("let\n  Step = [#\"let\" = 1]\nin\n  Step")]
    public void NonKeywordLetTextDoesNotCreateScopeDoubt(string expression)
    {
        Assert.False(MReferenceExtractor.HasIncompleteReferences(expression));
    }

    [Theory]
    [InlineData("let #\"Step\" = 1 in #\"Step\"")]
    [InlineData("let\n  Step = (let\n    Other = 1\n  in Other)\nin Step")]
    [InlineData("{(let\n  A = 1\nin A), (let\n  B = 2\nin B)}")]
    public void RecognizedLetScopesDoNotSuppressOrphanFindings(string expression)
    {
        Assert.False(MReferenceExtractor.HasIncompleteReferences(expression));
        var inventory = Scan(expression);
        Assert.Equal(PowerQueryRoles.ApparentlyOrphaned, Query(inventory, "Bar").QueryRole);
        Assert.Contains(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    [Fact]
    public void RecordKeyIsNotAReferenceToAGlobalQuery()
    {
        var inventory = Scan("let\n  Rec = [Bar = 1, Other = 2]\nin\n  Table.FromValue(Rec[Other])");

        Assert.DoesNotContain(inventory.PowerQueryDependencies, edge => edge.ToQueryName == "Bar");
        Assert.Equal(PowerQueryUsageStates.ApparentlyUnused, Query(inventory, "Bar").UsageState);
    }

    [Fact]
    public void FieldAccessIsNotAReferenceToAGlobalQuery()
    {
        var inventory = Scan("let\n  Rec = [Alpha = 1]\n  ,Picked = Rec[Bar]\nin\n  Table.FromValue(Picked)");

        Assert.DoesNotContain(inventory.PowerQueryDependencies, edge => edge.ToQueryName == "Bar");
    }

    [Fact]
    public void AGenuineReferenceStillCreatesADependency()
    {
        var inventory = Scan("let\n  Result = Bar\nin\n  Table.FromValue(Result)");

        Assert.Contains(
            inventory.PowerQueryDependencies,
            edge => edge.FromQueryName == "Probe" && edge.ToQueryName == "Bar");
        Assert.Equal(PowerQueryUsageStates.SupportingQuery, Query(inventory, "Bar").UsageState);
    }

    [Fact]
    public void ANameUsedAsARecordValueIsStillAReference()
    {
        var inventory = Scan("let\n  Rec = [Alpha = Bar]\nin\n  Table.FromValue(Rec[Alpha])");

        Assert.Contains(inventory.PowerQueryDependencies, edge => edge.ToQueryName == "Bar");
    }

    [Fact]
    public void NestedLetRetainsTheOuterGlobalReference()
    {
        var inventory = Scan(
            "let\n  Outer = Bar,\n  Inner =\n    let\n  Bar = 99\n    in\n      Bar\nin\n  Table.FromValue(Outer)");

        var bar = Query(inventory, "Bar");
        Assert.Equal(PowerQueryUsageStates.SupportingQuery, bar.UsageState);
        Assert.Equal(PowerQueryRoles.HelperOrStaging, bar.QueryRole);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    [Fact]
    public void InlineNestedBindingDoesNotInventAGlobalReference()
    {
        var inventory = Scan("let\n  Inner = (let Bar = 99 in Bar)\nin\n  Table.FromValue(Inner)");

        Assert.Equal(PowerQueryUsageStates.ApparentlyUnused, Query(inventory, "Bar").UsageState);
        Assert.Equal(PowerQueryRoles.ApparentlyOrphaned, Query(inventory, "Bar").QueryRole);
    }

    [Fact]
    public void SingleLineLetRetainsTheConfidentOrphanConclusion()
    {
        var inventory = Scan("let Step = 1 in Table.FromValue(Step)");

        Assert.Equal(PowerQueryRoles.ApparentlyOrphaned, Query(inventory, "Bar").QueryRole);
        Assert.Contains(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    [Fact]
    public void OrdinaryLetStillProducesAConfidentOrphanConclusion()
    {
        var inventory = Scan("let\n  Step = 1\nin\n  Table.FromValue(Step)");

        var bar = Query(inventory, "Bar");
        Assert.Equal(PowerQueryUsageStates.ApparentlyUnused, bar.UsageState);
        Assert.Equal(PowerQueryRoles.ApparentlyOrphaned, bar.QueryRole);
        Assert.Contains(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    [Fact]
    public void ParameterBehaviourIsUnchanged()
    {
        var inventory = Scan(
            "let\n  Result = Bar\nin\n  Table.FromValue(Result)",
            extraExpressions: "expression Threshold = 10 meta [IsParameterQuery=true, Type=\"Number\"]\n");

        var parameter = Query(inventory, "Threshold");
        Assert.True(parameter.IsParameter);
        Assert.Null(parameter.QueryRole);
        Assert.DoesNotContain(
            inventory.Findings,
            finding => finding.RuleId == "PBI-QUERY-002" && finding.Message.Contains("Threshold", StringComparison.Ordinal));
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static PowerQueryUsage Query(ProjectInventory inventory, string queryName) =>
        Assert.Single(inventory.PowerQueryUsages, usage => usage.QueryName == queryName);

    private static ProjectInventory Scan(string probeExpression, string extraExpressions = "")
    {
        var indented = string.Join(
            "\n",
            probeExpression.Split('\n').Select(line => "\t\t\t\t" + line));

        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/expressions.tmdl",
                "expression Bar = \"bar-value\" meta [IsParameterQuery=false]\n\n" + extraExpressions),
            File("Model.SemanticModel/definition/tables/Probe.tmdl",
                "table Probe\n\n" +
                "\tcolumn Value\n\t\tdataType: string\n\t\tsummarizeBy: none\n\t\tsourceColumn: Value\n\n" +
                "\tpartition Probe = m\n\t\tmode: import\n\t\tsource =\n" + indented + "\n"),
        };

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));
}

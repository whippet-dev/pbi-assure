using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class MLexicalReferenceTests
{
    // Full scans check the safety consequence, not only tokenizer output.
    [Theory]
    [InlineData("Bar", true)]
    [InlineData("let Bar = 1 in Bar", false)]
    [InlineData("let First = Bar, Bar = 1 in First", false)]
    [InlineData("let data = 1 in Bar", true)]
    [InlineData("{(let Bar = 1 in Bar), Bar}", true)]
    [InlineData("let Outer = Bar, Inner = let Bar = 99 in Bar in Outer", true)]
    [InlineData("let Inner = (let Bar = 99 in Bar) in Inner", false)]
    [InlineData("[Bar = 1, Alpha = Bar]", false)]
    [InlineData("[Alpha = Bar, Bar = 1]", false)]
    [InlineData("[Outer = Bar, Nested = [Bar = 1, Value = Bar]]", true)]
    [InlineData("let Rec = [\n Bar = 1\n], Result = Bar in Result", true)]
    [InlineData("[Alpha = List.Contains({true}, Bar = 1)]", true)]
    [InlineData("[Alpha = List.Contains({true},\n Bar = 1)]", true)]
    [InlineData("[Alpha = List.Last({false, Bar = 1})]", true)]
    [InlineData("[A = [B = {Bar}], C = 1]", true)]
    [InlineData("Rec[Bar]", false)]
    [InlineData("each [Bar]", false)]
    [InlineData("Rec[[Bar], [Other]]?", false)]
    [InlineData("Rec[Bar]?", false)]
    [InlineData("[let = 1]", false)]
    [InlineData("[#\"let\" = 1]", false)]
    [InlineData("[#\"Bar\" = 1][#\"Bar\"]", false)]
    [InlineData("#\"prefix Bar suffix\"", false)]
    [InlineData("#\"a\"\"Bar\"", false)]
    [InlineData("#\"B#(0061)r\"", true)]
    [InlineData("(Bar) => Bar", false)]
    [InlineData("(Bar as number) as number => Bar", false)]
    [InlineData("(optional Bar as nullable number) => Bar", false)]
    [InlineData("{(Bar) => Bar, Bar}", true)]
    [InlineData("(Bar) => () => Bar", false)]
    [InlineData("(_) => Bar", true)]
    [InlineData("(Bar) => each Bar", false)]
    [InlineData("let Loop = (n) => if n = 0 then Bar else @Loop(n - 1) in Loop(1)", true)]
    [InlineData("let Bar = (n) => if n = 0 then 1 else @Bar(n - 1) in Bar(1)", false)]
    [InlineData("let Step = Bar in Step", true)]
    [InlineData("let\n Step = 1\nin Step", false)]
    [InlineData("\"Bar let\"", false)]
    [InlineData("/* Bar /* nested */ let */ 1 // Bar", false)]
    [InlineData("let Step = #table(type table [Bar = Int64.Type], {{1}}) in Step", false)]
    [InlineData("try Bar otherwise 1", true)]
    [InlineData("try 1 catch (Bar) => Bar", false)]
    [InlineData("each if [X] = 1 then Bar else 0", true)]
    public void KnownReferencesAndOrphanConsequencesFollowLexicalScope(string expression, bool expectedReference)
    {
        var result = MReferenceExtractor.Analyze(expression, ["Bar"]);
        Assert.False(result.Incomplete);
        Assert.Equal(expectedReference, result.References.Contains("Bar", StringComparer.Ordinal));
        var inventory = Scan(expression);
        var query = inventory.PowerQueryUsages.Single(item => item.QueryName == "Bar");
        Assert.Equal(expectedReference, inventory.PowerQueryDependencies.Any(edge => edge.ToQueryName == "Bar"));
        Assert.Equal(expectedReference ? PowerQueryUsageStates.SupportingQuery : PowerQueryUsageStates.ApparentlyUnused, query.UsageState);
        Assert.Equal(expectedReference ? PowerQueryRoles.HelperOrStaging : PowerQueryRoles.ApparentlyOrphaned, query.QueryRole);
        Assert.Equal(!expectedReference, inventory.Findings.Any(finding => finding.RuleId == "PBI-QUERY-002" && finding.ObjectName == "Bar"));
        Assert.Equal("0.26", inventory.SchemaVersion);
    }

    [Theory]
    [InlineData("section S; shared A = Bar;")]
    [InlineData("S!Bar")]
    [InlineData("let A = 1 in (Bar")]
    [InlineData("let A = 1, A = Bar in A")]
    [InlineData("#\"unfinished")]
    [InlineData("/* unfinished Bar")]
    [InlineData("[A = 1, ...]")]
    [InlineData("type function (Bar as number) as number")]
    public void UnsupportedOrMalformedSyntaxCannotProduceConfidentOrphans(string expression)
    {
        Assert.True(MReferenceExtractor.Analyze(expression, ["Bar"]).Incomplete);
        var inventory = Scan(expression);
        Assert.Null(inventory.PowerQueryUsages.Single(query => query.QueryName == "Bar").QueryRole);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    [Theory]
    [InlineData("Expression.Evaluate(\"Bar\", #shared)")]
    [InlineData("Record.Field(#shared, \"Bar\")")]
    public void DynamicDiscoveryProtectsPotentialTargetsWithoutInventingNames(string expression)
    {
        var inventory = Scan(expression);
        Assert.DoesNotContain(inventory.PowerQueryDependencies, edge => edge.ToQueryName == "Bar");
        Assert.Null(inventory.PowerQueryUsages.Single(query => query.QueryName == "Bar").QueryRole);
        Assert.True(inventory.PowerQueryUsages.Single(query => query.QueryName == "Probe").HasDynamicReferences);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    [Theory]
    [InlineData("A\"B", "[Alpha = #\"A\"\"B\"]", true)]
    [InlineData("A\"B", "Rec[#\"A\"\"B\"]", false)]
    [InlineData("Base Line", "[Base Line = 1, Result = #\"Base Line\"]", false)]
    [InlineData("_", "each [X]", false)]
    [InlineData("_", "[X]", true)]
    [InlineData("optional", "(#\"optional\") => #\"optional\"", false)]
    public void QuotedGeneralizedAndImplicitNamesUseTheSameScopedIdentity(string queryName, string expression, bool used)
    {
        var inventory = Scan(expression, queryName);
        var query = inventory.PowerQueryUsages.Single(item => item.QueryName == queryName);
        Assert.Equal(used ? PowerQueryUsageStates.SupportingQuery : PowerQueryUsageStates.ApparentlyUnused, query.UsageState);
        Assert.Equal(used ? PowerQueryRoles.HelperOrStaging : PowerQueryRoles.ApparentlyOrphaned, query.QueryRole);
        Assert.Equal(!used, inventory.Findings.Any(item => item.RuleId == "PBI-QUERY-002" && item.ObjectName == queryName));
    }

    [Fact]
    public void MultilineComparisonRetainsParameterReference()
    {
        var inventory = Scan("Table.FromRecords({[Alpha = List.Contains({true},\n Threshold = 10)]})", "Threshold", parameter: true);
        var parameter = inventory.PowerQueryUsages.Single(query => query.QueryName == "Threshold");
        Assert.True(parameter.IsParameter);
        Assert.Equal(PowerQueryUsageStates.SupportingQuery, parameter.UsageState);
        Assert.Contains(inventory.PowerQueryDependencies, edge => edge.ToQueryName == "Threshold");
        Assert.Null(parameter.QueryRole);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    [Fact]
    public void ExcessiveNestingWithholdsOrphanConfidenceInsteadOfOverflowing()
    {
        var expression = new string('(', 160) + "Bar" + new string(')', 160);
        var inventory = Scan(expression);
        Assert.Null(inventory.PowerQueryUsages.Single(query => query.QueryName == "Bar").QueryRole);
        Assert.DoesNotContain(inventory.Findings, finding => finding.RuleId == "PBI-QUERY-002");
    }

    private static ProjectInventory Scan(string expression, string name = "Bar", bool parameter = false)
    {
        var files = new Dictionary<string, string>
        {
            ["Model.pbip"] = "{}",
            ["Model.SemanticModel/definition/expressions.tmdl"] =
                "expression '" + name.Replace("'", "''", StringComparison.Ordinal) + "' = 10" +
                (parameter ? " meta [IsParameterQuery=true, Type=\"Number\"]" : string.Empty) + "\n",
            ["Model.SemanticModel/definition/tables/Probe.tmdl"] =
                "table Probe\n\tcolumn Value\n\t\tdataType: int64\n\tpartition Probe = m\n\t\tmode: import\n\t\tsource =\n" +
                string.Join("\n", expression.Split('\n').Select(line => "\t\t\t" + line)) + "\n",
        };
        return ProjectScanner.Scan(new InMemoryProjectFileSource("M lexical regression", files.Select(file =>
            new ProjectFileContent(file.Key, Encoding.UTF8.GetBytes(file.Value)))));
    }
}

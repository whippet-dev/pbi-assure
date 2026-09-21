using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// M's not-implemented expression, written <c>...</c>, is a complete primary expression that names
/// nothing. Desktop writes it as a parameter's <c>DefaultValue</c> when no default has been chosen, so
/// an ordinary Power BI parameter used to read as unsupported syntax and raise a limitation that
/// qualified every absence in the model. The three dots are one token; the range operator <c>..</c>
/// is a different one.
/// </summary>
public sealed class MNotImplementedExpressionTests
{
    private const string DesktopParameter =
        "\"Production\" meta [IsParameterQuery=true, List={\"Production\", \"Development\"}, DefaultValue=..., Type=\"Text\", IsParameterQueryRequired=true]";

    [Theory]
    [InlineData("\"Production\" meta [DefaultValue=...]")]
    [InlineData(DesktopParameter)]
    [InlineData("...")]
    [InlineData("let Source = ... in Source")]
    [InlineData("if true then ... else Helper")]
    [InlineData("(x) => ...")]
    [InlineData("[A = ..., B = 2]")]
    public void TheNotImplementedExpressionIsCompleteAndReferencesNothing(string expression)
    {
        var result = MReferenceExtractor.Analyze(expression, ["Helper", "Production", "Development", "Source", "Bar"]);

        Assert.False(result.Incomplete);
        Assert.False(result.Dynamic);
        Assert.Equal(expression.Contains("else Helper", StringComparison.Ordinal) ? ["Helper"] : Array.Empty<string>(), result.References);
    }

    [Fact]
    public void TheEllipsisIsOneTokenDistinctFromTheRangeOperator()
    {
        var (tokens, incomplete) = MReferenceTokenizer.Tokenize("... {1..3} 1.5");

        Assert.False(incomplete);
        Assert.Equal(["...", "{", "1", "..", "3", "}", "1.5", ""], tokens.Select(token => token.Text).ToArray());
        Assert.Equal(MReferenceTokenKind.Symbol, tokens[0].Kind);
        Assert.False(MReferenceExtractor.Analyze("{Bar..3}", ["Bar"]).Incomplete);
        Assert.Equal(["Bar"], MReferenceExtractor.Analyze("{Bar..3}", ["Bar"]).References);
    }

    [Theory]
    [InlineData("\"Daily PT\" meta [IsParameterQuery=true, List={\"Daily PT\"}, DefaultValue=\"Daily PT\", Type=\"Text\", IsParameterQueryRequired=true]")]
    [InlineData("#datetime(2026, 1, 1, 0, 0, 0) meta [IsParameterQuery=true, Type=\"DateTime\", IsParameterQueryRequired=true]")]
    public void ParametersWithLiteralDefaultsAreUnchanged(string expression)
    {
        var result = MReferenceExtractor.Analyze(expression, ["Helper"]);

        Assert.False(result.Incomplete);
        Assert.Empty(result.References);
    }

    [Theory]
    [InlineData("let Coerce = (f as function (x as any) as any) => f, Source = Helper in Source")]
    [InlineData("let Source = Helper in")]
    [InlineData("\"Production\" meta [DefaultValue=.., Type=\"Text\"]")]
    [InlineData("[DefaultValue=....]")]
    public void GenuinelyUnreadableSyntaxIsStillIncomplete(string expression)
    {
        Assert.True(MReferenceExtractor.Analyze(expression, ["Helper"]).Incomplete);
    }

    [Fact]
    public void ADesktopParameterWithoutADefaultNoLongerQualifiesTheModel()
    {
        var inventory = Scan(DesktopParameter);

        var parameter = Assert.Single(inventory.PowerQueryUsages, usage => usage.QueryName == "Version");
        Assert.True(parameter.IsParameter);
        Assert.DoesNotContain(inventory.AnalysisLimitations, limitation => limitation.LimitationId == "PBI-LIMIT-MODEL-QUERY-REFERENCES");
        Assert.All(inventory.SemanticObjectUsages, usage =>
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence));
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == "UnusedMeasure").UsageState);
    }

    [Fact]
    public void AGenuinelyUnreadableExpressionStillQualifiesTheModel()
    {
        var inventory = Scan("let Coerce = (f as function (x as any) as any) => f in Coerce");

        Assert.Single(inventory.AnalysisLimitations, limitation => limitation.LimitationId == "PBI-LIMIT-MODEL-QUERY-REFERENCES");
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation,
            Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == "UnusedMeasure").ClassificationConfidence);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static ProjectInventory Scan(string versionExpression)
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/expressions.tmdl",
                "expression Version = " + versionExpression + "\n\tlineageTag: 092b79cc-9cdc-4630-b00f-d41e7bbb0b32\n\n" +
                "\tannotation PBI_NavigationStepName = Navigation\n\n\tannotation PBI_ResultType = Text\n"),
            File("Model.SemanticModel/definition/tables/Probe.tmdl",
                "table Probe\n\n" +
                "\tcolumn Key\n\t\tdataType: string\n\t\tsummarizeBy: none\n\t\tsourceColumn: Key\n\n" +
                "\tmeasure UnusedMeasure = 1\n\n" +
                "\tpartition Probe = m\n\t\tmode: import\n\t\tsource = #table({\"Key\"}, {{Version}})\n"),
        };

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Not implemented expression", files));
    }

    private static ProjectFileContent File(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));
}

using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class DaxLanguageTokenTests
{
    private static readonly HashSet<string> KnownTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sales", "Sales Data", "RETURN", "NOT", "BY", "IN", "NotSales", "ReturnSales",
    };

    [Theory]
    [InlineData("VAR X = 1 RETURN [Total]", "Total")]
    [InlineData("NOT [Boolean Measure]", "Boolean Measure")]
    [InlineData("var X = 1 return\n\t[Total]", "Total")]
    [InlineData("NOT[Boolean Measure]", "Boolean Measure")]
    [InlineData("VAR X = 1 RETURN/* comment */[Total]", "Total")]
    [InlineData("ORDER BY [Total] DESC", "Total")]
    public void LanguageTokensLeaveBracketReferencesForBareReferenceExtraction(string expression, string name)
    {
        var reference = Assert.Single(DaxReferenceExtractor.Extract(expression, KnownTables));
        Assert.Null(reference.Table);
        Assert.Equal(name, reference.ObjectName);
        Assert.Equal($"[{name}]", reference.Text);
    }

    [Theory]
    [InlineData("VAR X = [Base] RETURN [Total]")]
    [InlineData("VAR X = 1 RETURN [Total]")]
    [InlineData("VAR X = 1 RETURN ([Total])")]
    [InlineData("NOT [Boolean Measure]")]
    public void RealMeasureReferencesRemainDependenciesWithoutFalseConfidenceQualification(string expression)
    {
        var inventory = Scan(expression);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Empty(inventory.AnalysisLimitations);
        if (expression.Contains("[Base]", StringComparison.Ordinal)) AssertEdge(inventory, "Base", SemanticObjectTypes.Measure);
        AssertEdge(inventory, expression.StartsWith("NOT", StringComparison.Ordinal) ? "Boolean Measure" : "Total", SemanticObjectTypes.Measure);
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromObjectName == "Total" && edge.ToObjectName == "Base");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Unused").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Unused").ClassificationConfidence);
        Assert.Equal("0.26", inventory.SchemaVersion);
    }

    [Theory]
    [InlineData("FILTER(INFO.VIEW.TABLES(), NOT [IsHidden])")]
    [InlineData("FILTER(INFO.VIEW.TABLES(), NOT([IsHidden]))")]
    [InlineData("VAR T = INFO.VIEW.TABLES() RETURN FILTER(T, NOT [IsHidden])")]
    [InlineData("FILTER(INFO.VIEW.TABLES(), VAR X = 1 RETURN ([IsHidden]))")]
    public void OperatorsAndReturnKeepTheExistingVirtualRowContext(string expression)
    {
        var inventory = Scan(expression, calculatedTable: true);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Empty(inventory.AnalysisLimitations);
        var reference = Assert.Single(DaxReferenceExtractor.Extract(expression, KnownTables));
        Assert.Null(reference.Table);
        Assert.Equal("IsHidden", reference.ObjectName);
        Assert.True(reference.IsVirtualRowColumn);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Unused").ClassificationConfidence);
    }

    [Theory]
    [InlineData("Sales[Amount]", "Sales")]
    [InlineData("Sales [Amount]", "Sales")]
    [InlineData("Sales\n\t[Amount]", "Sales")]
    [InlineData("'Sales Data'[Amount]", "Sales Data")]
    [InlineData("'RETURN'[Amount]", "RETURN")]
    [InlineData("'NOT' [Amount]", "NOT")]
    [InlineData("ReturnSales[Amount]", "ReturnSales")]
    [InlineData("NotSales [Amount]", "NotSales")]
    public void QualifiedTablesIncludingQuotedReservedNamesRemainIntact(string expression, string table)
    {
        var reference = Assert.Single(DaxReferenceExtractor.Extract(expression, KnownTables));
        Assert.Equal(table, reference.Table);
        Assert.Equal("Amount", reference.ObjectName);
        var inventory = Scan($"SUM({expression})", extraTable: table == "Sales" ? null : table);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromObjectName == "Result" &&
            edge.ToTable == table && edge.ToObjectName == "Amount" && edge.DependencyKind == SemanticDependencyKinds.Dax);
    }

    [Fact]
    public void UnquotedLanguageTokensDoNotBecomeReferencesToSameNamedTables()
    {
        var references = DaxReferenceExtractor.Extract("VAR X = 1 RETURN NOT [Boolean Measure]", KnownTables);
        Assert.Null(Assert.Single(references).Table);
        Assert.Equal("Boolean Measure", references[0].ObjectName);
    }

    [Fact]
    public void ParenthesizedReturnIsAGroupingScopeRatherThanAFunctionCall()
    {
        var reference = Assert.Single(DaxReferenceExtractor.Extract("VAR X = 1 RETURN ([Amount])", KnownTables));
        Assert.Null(reference.Table);
        Assert.True(reference.CanUseOwnerRowContext);
    }

    [Fact]
    public void OrdinaryFunctionsAndBooleanConstantsKeepTheirExistingHandling()
    {
        const string expression = "AND(NOT([Boolean Measure]), OR(TRUE(), FALSE()))";
        var reference = Assert.Single(DaxReferenceExtractor.Extract(expression, KnownTables));
        Assert.Null(reference.Table);
        Assert.Equal("Boolean Measure", reference.ObjectName);
        Assert.Empty(Scan(expression).UnresolvedSemanticDependencies);
    }

    [Fact]
    public void InOperatorAndOrdinaryQueryOrderingRetainExplicitReferences()
    {
        var references = DaxReferenceExtractor.Extract(
            "EVALUATE FILTER(Sales, Sales[Amount] IN VALUES(Sales[Amount])) ORDER BY [Total] DESC", KnownTables);
        Assert.Contains(references, reference => reference.Table == "Sales" && reference.ObjectName == "Amount");
        Assert.Contains(references, reference => reference.Table is null && reference.ObjectName == "Total");
        Assert.DoesNotContain(references, reference => reference.Table is "BY" or "IN" or "DESC");
    }

    [Theory]
    [InlineData("VAR X = 1 RETURN [Missing]", "[Missing]")]
    [InlineData("NOT [Missing]", "[Missing]")]
    [InlineData("MissingTable[Amount]", "MissingTable[Amount]")]
    [InlineData("MYSTERY [Missing]", "MYSTERY [Missing]")]
    public void MissingReferencesAndUnknownIdentifiersRemainConservative(string expression, string evidence)
    {
        var inventory = Scan(expression);
        Assert.Equal(evidence, Assert.Single(inventory.UnresolvedSemanticDependencies).ReferenceText);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Unused").ClassificationConfidence);
    }

    private static ProjectInventory Scan(string expression, bool calculatedTable = false, string? extraTable = null)
    {
        var content = "table Sales\n    column Amount\n    column Unused\n" +
            "    measure Base = 1\n    measure Total = [Base]\n    measure 'Boolean Measure' = TRUE()\n";
        content += calculatedTable
            ? "    partition Sales = calculated\n        source =\n            "
            : "    measure Result =\n        ";
        content += expression.Replace("\n", calculatedTable ? "\n            " : "\n        ", StringComparison.Ordinal);
        var files = new List<ProjectFileContent>
        {
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Sales.tmdl", content),
        };
        if (extraTable is not null)
            files.Add(File("Model.SemanticModel/definition/tables/Other.tmdl", $"table '{extraTable}'\n    column Amount"));
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic DAX language token regressions", files));
    }

    private static ProjectFileContent File(string path, string content) => new(path, Encoding.UTF8.GetBytes(content));

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string name) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == "Sales" && usage.ObjectName == name);

    private static void AssertEdge(ProjectInventory inventory, string name, string type) =>
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromObjectName == "Result" && edge.ToObjectName == name &&
            edge.ToObjectType == type && edge.DependencyKind == SemanticDependencyKinds.Dax);
}

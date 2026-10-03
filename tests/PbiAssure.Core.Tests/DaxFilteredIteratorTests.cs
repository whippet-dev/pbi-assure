using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class DaxFilteredIteratorTests
{
    private static readonly string?[] NestedTables = ["Dim", "Other", "Dim"];
    private static readonly string?[] ShadowedTables = ["Dim", null, "Dim"];

    [Theory]
    [InlineData("SUMX(Dim, [X])")]
    [InlineData("SUMX(FILTER(Dim, Dim[Key] > 0), [X])")]
    [InlineData("VAR t = FILTER(Dim, Dim[Key] > 0) RETURN SUMX(t, [X])")]
    [InlineData("VAR t = FILTER('Dim', Dim[Key] > 0) RETURN SUMX(T, [X])")]
    [InlineData("VAR a = FILTER(Dim, Dim[Key] > 0) VAR b = a RETURN SUMX(b, [X])")]
    [InlineData("VAR t = FILTER(Dim, Dim[Key] > 0) VAR u = FILTER(t, Dim[Key] < 10) RETURN SUMX(u, INT([X]))")]
    [InlineData("SUMX(FILTER(FILTER(Dim, Dim[Key] > 0), Dim[Key] < 10), [X])")]
    [InlineData("SUMX(/* input */ FILTER('Dim', Dim[Key] > 0) /* boundary */, [X])")]
    [InlineData("COUNTROWS(FILTER(FILTER(Dim, Dim[Key] > 0), [X] > 0))")]
    [InlineData("COUNTROWS(SELECTCOLUMNS(FILTER(Dim, Dim[Key] > 0), \"Value\", [X]))")]
    public void ProvenFilteredSourceIndependentlyResolvesItsRowColumn(string expression)
    {
        var inventory = Scan(expression);
        Assert.Contains(inventory.SemanticDependencies, edge => IsColumnEdge(edge, "Dim", "X"));
        if (expression.Contains("Dim[Key]", StringComparison.Ordinal))
            Assert.Contains(inventory.SemanticDependencies, edge => IsColumnEdge(edge, "Dim", "Key"));
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Empty(inventory.AnalysisLimitations);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Dim", "X").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Dim", "Notes").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Dim", "Notes").ClassificationConfidence);
    }

    [Theory]
    [InlineData("VAR t = UnknownTransform(Dim) RETURN SUMX(t, [X])")]
    [InlineData("SUMX(FILTER(UNION(Dim, Other), Dim[Key] > 0), [X])")]
    [InlineData("VAR t = IF(TRUE(), Dim, Other) RETURN SUMX(t, [X])")]
    [InlineData("VAR t = FILTER(SELECTCOLUMNS(Dim, \"X\", Dim[Key]), [X] > 0) RETURN SUMX(t, [X])")]
    [InlineData("VAR t = ADDCOLUMNS(Dim, \"New\", 1) RETURN SUMX(t, [X])")]
    [InlineData("VAR t = Dim RETURN SUMX(t, [X])")]
    [InlineData("VAR a = t VAR t = FILTER(Dim, Dim[Key] > 0) RETURN SUMX(a, [X])")]
    [InlineData("COUNTROWS((VAR t = FILTER(Dim, Dim[Key] > 0) RETURN t)) + SUMX(t, [X])")]
    [InlineData("MAXX(FILTER(Dim, Dim[Key] > 0), [X])")]
    public void UnprovenSourcesRetainMissingEvidenceAndQualifiedAbsence(string expression)
    {
        var inventory = Scan(expression);
        Assert.DoesNotContain(inventory.SemanticDependencies, edge => IsColumnEdge(edge, "Dim", "X") || IsColumnEdge(edge, "Other", "X"));
        Assert.Contains(inventory.UnresolvedSemanticDependencies, reference => reference.ReferenceText == "[X]" &&
            reference.ResolutionOutcome == UnresolvedSemanticDependencyResolutionOutcomes.NotFound);
        Assert.Contains(inventory.AnalysisLimitations, limitation => limitation.LimitationId == "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Dim", "X").UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Dim", "Notes").ClassificationConfidence);
    }

    [Fact]
    public void NestedTableBindingIsLocalAndRestoresTheOuterBinding()
    {
        const string expression = "VAR t = FILTER(Dim, Dim[Key] > 0) RETURN SUMX(t, [X]) + " +
            "(VAR t = FILTER(Other, Other[Key] > 0) RETURN SUMX(t, [X])) + SUMX(t, [x])";
        var references = RowReferences(expression);
        Assert.Equal(NestedTables, references.Select(reference => reference.RowContextTable));
        var inventory = Scan(expression);
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Contains(inventory.SemanticDependencies, edge => IsColumnEdge(edge, "Dim", "X"));
        Assert.Contains(inventory.SemanticDependencies, edge => IsColumnEdge(edge, "Other", "X"));
    }

    [Fact]
    public void ScalarShadowPreventsOuterProvenanceFromLeaking()
    {
        const string expression = "VAR t = FILTER(Dim, Dim[Key] > 0) RETURN SUMX(t, [X]) + " +
            "(VAR t = 1 RETURN SUMX(t, [X])) + SUMX(t, [x])";
        Assert.Equal(ShadowedTables, RowReferences(expression).Select(reference => reference.RowContextTable));
        var inventory = Scan(expression);
        Assert.Contains(inventory.SemanticDependencies, edge => IsColumnEdge(edge, "Dim", "X"));
        Assert.Equal("[X]", Assert.Single(inventory.UnresolvedSemanticDependencies).ReferenceText);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Dim", "Notes").ClassificationConfidence);
    }

    [Fact]
    public void AbortedInnerVariableWalkCannotLeakPersistedRowTableProvenance()
    {
        const string expression = "VAR t = FILTER(Other, TRUE()) RETURN " +
            "(VAR A = 1 + VAR t = FILTER(Dim, TRUE()) RETURN COUNTROWS(t) RETURN SUMX(t, [X]))";
        Assert.Null(Assert.Single(RowReferences(expression)).RowContextTable);
        var inventory = Scan(expression);
        Assert.DoesNotContain(inventory.SemanticDependencies, edge => IsColumnEdge(edge, "Dim", "X"));
        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal("[X]", unresolved.ReferenceText);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.NotFound, unresolved.ResolutionOutcome);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Dim", "Notes").ClassificationConfidence);
    }

    [Theory]
    [InlineData("SUMX(FILTER(Dim, Dim[Key] > 0), [X])")]
    [InlineData("VAR t = FILTER(Dim, Dim[Key] > 0) RETURN SUMX(t, [X])")]
    public void ProvenFilterDoesNotWeakenTheExistingHomeColumnCollisionProtection(string expression)
    {
        var inventory = Scan(expression, homeColumn: true);
        Assert.DoesNotContain(inventory.SemanticDependencies, edge => edge.FromObjectName == "Result" && edge.ToObjectName == "X");
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous,
            Assert.Single(inventory.UnresolvedSemanticDependencies).ResolutionOutcome);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Dim", "X").ClassificationConfidence);
    }

    private static DaxReferenceExtractor.DaxReference[] RowReferences(string expression) =>
        DaxReferenceExtractor.Extract(expression, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Dim", "Other", "Home" })
            .Where(reference => reference.Table is null && reference.ObjectName.Equals("X", StringComparison.OrdinalIgnoreCase)).ToArray();

    private static bool IsColumnEdge(SemanticDependencyEdge edge, string table, string column) =>
        edge.FromObjectName == "Result" && edge.ToTable == table && edge.ToObjectName == column && edge.ToObjectType == SemanticObjectTypes.Column;

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table, string name) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == name);

    private static ProjectInventory Scan(string expression, bool homeColumn = false)
    {
        var files = new Dictionary<string, string>
        {
            ["Model.SemanticModel/definition.pbism"] = "{}",
            ["Model.SemanticModel/definition/tables/Home.tmdl"] = "table Home\n    measure Result = " + expression +
                "\n    column Dummy\n" + (homeColumn ? "    column X\n" : ""),
            ["Model.SemanticModel/definition/tables/Dim.tmdl"] = "table Dim\n    column Key\n    column X\n    column Notes\n",
            ["Model.SemanticModel/definition/tables/Other.tmdl"] = "table Other\n    column Key\n    column X\n",
            ["Model.Report/definition.pbir"] = "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}",
            ["Model.Report/definition/report.json"] = "{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Home\"}},\"Property\":\"Result\"}}",
        };
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic filtered iterator controls",
            files.Select(file => new ProjectFileContent(file.Key, Encoding.UTF8.GetBytes(file.Value)))));
    }
}

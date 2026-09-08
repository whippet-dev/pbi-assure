using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class DaxUnqualifiedCollisionTests
{
    [Theory]
    [InlineData("SUMX(SELECTCOLUMNS(Dim, \"X\", Dim[Value]), [X])")]
    [InlineData("SUMX(\n SELECTCOLUMNS(Dim, \"X\", Dim[Value]),\n INT([X]))")]
    [InlineData("SUMX(FILTER(SELECTCOLUMNS(Dim, \"X\", Dim[Value]), [X] > 0), [X])")]
    public void VirtualIteratorColumnCannotBorrowUniquePersistedHomeColumn(string expression)
    {
        var inventory = Scan(expression, dimColumn: "Value");
        Assert.DoesNotContain(inventory.SemanticDependencies, edge =>
            edge.FromObjectName == "Result" && edge.ToTable == "Fact" && edge.ToObjectName == "X");
        Assert.Contains(inventory.SemanticDependencies, edge =>
            edge.FromObjectName == "Result" && edge.ToTable == "Dim" && edge.ToObjectName == "Value");
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Dim", "Value").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Dim", "Value").ClassificationConfidence);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Fact", "X").UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Fact", "X").ClassificationConfidence);
        Assert.Contains(inventory.UnresolvedSemanticDependencies, item => item.FromObjectName == "Result" &&
            item.ReferenceText == "[X]" && item.ResolutionOutcome == UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous);
        Assert.Contains(inventory.AnalysisLimitations, item =>
            item.LimitationId == "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE" && item.SemanticModel == "Model" &&
            item.DependencyImpact == ConstructDependencyImpacts.MayCreateDependencies);
        Assert.Equal("0.26", inventory.SchemaVersion);
    }

    [Theory]
    [InlineData("SUMX(Fact, [X])")]
    [InlineData("SUMX(/* source */ 'Fact' /* boundary */, [X])")]
    [InlineData("SUMX(SELECTCOLUMNS(Dim, \"Virtual\", Dim[Value]), Fact[X])")]
    public void AccountedPersistedSourceAndExplicitReferencesRemainUnchanged(string expression)
    {
        var inventory = Scan(expression, dimColumn: "Value");
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Fact", "X").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Fact", "X").ClassificationConfidence);
    }

    [Fact]
    public void MeasureInsideUnboundIteratorStillResolves()
    {
        var inventory = Scan("SUMX(SELECTCOLUMNS(Dim, \"Virtual\", Dim[Value]), [SomeMeasure])",
            dimColumn: "Value", extraFact: "\tmeasure SomeMeasure = 1\n");
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Fact", "SomeMeasure").UsageState);
    }

    [Fact]
    public void VirtualIteratorContextDoesNotLeakToLaterOwnerRowReference()
    {
        var inventory = Scan("SUM(Fact[Computed])", dimColumn: "Value",
            extraFact: "\tcolumn Computed = SUMX(SELECTCOLUMNS(Dim, \"Virtual\", Dim[Value]), 1) + YEAR([X])\n\t\tdataType: int64\n");
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromObjectName == "Computed" &&
            edge.ToTable == "Fact" && edge.ToObjectName == "X");
    }

    [Theory]
    [InlineData("Date", "YEAR([Date])")]
    [InlineData("Date", "MONTH([Date])")]
    [InlineData("Date", "DAY([Date])")]
    [InlineData("Date", "FORMAT([Date], \"MMMM\")")]
    [InlineData("MonthNo", "INT(([MonthNo] + 2) / 3)")]
    [InlineData("QuarterNo", "\"Qtr \" & [QuarterNo]")]
    public void CalculatedColumnRetainsItsLocalRowDespiteRemoteNameCollision(string column, string expression)
    {
        var inventory = Scan("SUM(Fact[Computed])", factColumn: column, dimColumn: column,
            extraFact: "\tcolumn Computed = " + expression + "\n\t\tdataType: int64\n");
        Assert.DoesNotContain(inventory.UnresolvedSemanticDependencies, item => item.FromObjectName == "Computed");
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromObjectName == "Computed" &&
            edge.ToTable == "Fact" && edge.ToObjectName == column && edge.ToObjectType == SemanticObjectTypes.Column);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Fact", column).UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Fact", column).ClassificationConfidence);
        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.ConstructType == "semanticReference");
    }

    [Theory]
    [InlineData("SUMX(Dim, [X])")]
    [InlineData("SUMX(Dim, INT(([X] + 1) / 2))")]
    [InlineData("COUNTROWS(FILTER(Dim, [X] > 0))")]
    [InlineData("COUNTROWS(SELECTCOLUMNS(Dim, \"Value\", [X]))")]
    [InlineData("SUMX /* context comment */ (Dim, [X])")]
    [InlineData("MAXX(Dim, [X])")]
    [InlineData("Custom.YEAR([X])")]
    public void CalculatedColumnDoesNotBorrowOwnerRowInsideIteratorOrUnknownCall(string expression)
    {
        var inventory = Scan("SUM(Fact[Computed])",
            extraFact: "\tcolumn Computed = " + expression + "\n\t\tdataType: int64\n");
        Assert.DoesNotContain(inventory.SemanticDependencies, edge =>
            edge.FromObjectName == "Computed" && edge.ToObjectType == SemanticObjectTypes.Column);
        Assert.Contains(inventory.UnresolvedSemanticDependencies, item => item.FromObjectName == "Computed" &&
            item.ResolutionOutcome == UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Dim", "X").ClassificationConfidence);
    }

    [Fact]
    public void IdenticalReferencesInOwnerAndIteratorScopesKeepBothKindsOfEvidence()
    {
        var inventory = Scan("SUM(Fact[Computed])",
            extraFact: "\tcolumn Computed = [X] + SUMX(Dim, [X])\n\t\tdataType: int64\n");
        Assert.Contains(inventory.SemanticDependencies, edge => edge.FromObjectName == "Computed" &&
            edge.ToTable == "Fact" && edge.ToObjectName == "X");
        Assert.Contains(inventory.UnresolvedSemanticDependencies, item => item.FromObjectName == "Computed" &&
            item.ReferenceText == "[X]" && item.ResolutionOutcome == UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Dim", "X").ClassificationConfidence);
    }

    [Theory]
    [InlineData("SUMX({[X], 2}, 1)", true)]
    [InlineData("SUMX({1, 2}, [X])", false)]
    [InlineData("SUMX(Dim, [X]) + [X]", true)]
    [InlineData("YEAR /* ( , */ ([X])", true)]
    [InlineData("FORMAT([X], \"SUMX(Dim,\")", true)]
    public void LexicalContextTracksArgumentsNestingAndScopeRestoration(string expression, bool expectedOwnerContext)
    {
        var reference = DaxReferenceExtractor.Extract(expression,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fact", "Dim" })
            .Last(item => item.Table is null && item.ObjectName == "X");
        Assert.Equal(expectedOwnerContext, reference.CanUseOwnerRowContext);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("x")]
    public void IteratorCollisionRetainsAmbiguityRatherThanChoosingTheHomeColumn(string dimColumn)
    {
        var inventory = Scan("SUMX ( Dim, [X] )", dimColumn: dimColumn);

        Assert.DoesNotContain(inventory.SemanticDependencies, edge =>
            edge.FromObjectName == "Result" && edge.ToObjectType == SemanticObjectTypes.Column);
        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous, unresolved.ResolutionOutcome);
        Assert.Equal("[X]", unresolved.ReferenceText);
        Assert.Equal("Fact", unresolved.FromTable);
        Assert.Equal("Result", unresolved.FromObjectName);
        Assert.Equal("Model.SemanticModel/definition/tables/Fact.tmdl", unresolved.EvidencePath);
        Assert.Contains("ambiguous", unresolved.Reason, StringComparison.OrdinalIgnoreCase);
        var limitation = Assert.Single(inventory.AnalysisLimitations,
            item => item.LimitationId == "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE");
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.Equal("Model", limitation.SemanticModel);
        Assert.Equal(AnalysisLimitationScopes.SemanticModel, limitation.Scope);
        foreach (var table in new[] { "Fact", "Dim" })
        {
            var usage = Usage(inventory, table, table == "Fact" ? "X" : dimColumn);
            Assert.Equal(SemanticUsageStates.ApparentlyUnused, usage.UsageState);
            Assert.Equal(ClassificationConfidences.QualifiedByLimitation, usage.ClassificationConfidence);
        }
        Assert.Equal(SemanticUsageStates.DirectlyUsed, Usage(inventory, "Fact", "Result").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Fact", "Result").ClassificationConfidence);
        Assert.Equal("0.26", inventory.SchemaVersion);
    }

    [Fact]
    public void OrdinaryUnqualifiedMeasureStillResolvesDespiteARemoteSameNamedColumn()
    {
        var inventory = Scan("[SomeMeasure]", dimColumn: "SomeMeasure",
            extraFact: "\tmeasure SomeMeasure = 1\n");
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        var edge = Assert.Single(inventory.SemanticDependencies, edge =>
            edge.FromObjectName == "Result" && edge.DependencyKind == SemanticDependencyKinds.Dax);
        Assert.Equal(SemanticObjectTypes.Measure, edge.ToObjectType);
        Assert.Equal("SomeMeasure", edge.ToObjectName);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Fact", "SomeMeasure").UsageState);
    }

    [Fact]
    public void UnambiguousHomeColumnStillResolves()
    {
        var inventory = Scan("SUM ( [X] )", dimColumn: "Other");
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Fact", "X").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Fact", "X").ClassificationConfidence);
    }

    [Fact]
    public void UnambiguousCalculatedColumnContextStillResolves()
    {
        var inventory = Scan("SUM ( Fact[Computed] )", dimColumn: "Other",
            extraFact: "\tcolumn Computed = [X] + 1\n\t\tdataType: int64\n");
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Contains(inventory.SemanticDependencies, edge =>
            edge.FromObjectName == "Computed" && edge.ToTable == "Fact" && edge.ToObjectName == "X");
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Fact", "X").UsageState);
    }

    [Fact]
    public void ExplicitlyQualifiedColumnStillResolvesWithACollision()
    {
        var inventory = Scan("SUMX ( Dim, Dim[X] )");
        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, Usage(inventory, "Dim", "X").UsageState);
        Assert.Equal(ClassificationConfidences.Established, Usage(inventory, "Dim", "X").ClassificationConfidence);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Fact", "X").UsageState);
    }

    [Fact]
    public void IteratorWithoutHomeColumnStillRetainsNotFoundAndQualifiedAbsence()
    {
        var inventory = Scan("SUMX ( Dim, [X] )", factColumn: "Other");
        Assert.DoesNotContain(inventory.SemanticDependencies, edge => edge.ToObjectType == SemanticObjectTypes.Column);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.NotFound,
            Assert.Single(inventory.UnresolvedSemanticDependencies).ResolutionOutcome);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Dim", "X").UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, Usage(inventory, "Dim", "X").ClassificationConfidence);
    }

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table, string name) =>
        inventory.SemanticObjectUsages.Single(usage => usage.Table == table && usage.ObjectName == name);

    private static ProjectInventory Scan(string expression, string factColumn = "X", string dimColumn = "X", string extraFact = "")
    {
        var files = new Dictionary<string, string>
        {
            ["Model.pbip"] = "{}",
            ["Model.SemanticModel/definition/tables/Fact.tmdl"] =
                "table Fact\n\tmeasure Result =\n\t\t" + expression.Replace("\n", "\n\t\t", StringComparison.Ordinal) +
                "\n" + Column(factColumn) + extraFact,
            ["Model.SemanticModel/definition/tables/Dim.tmdl"] = "table Dim\n" + Column(dimColumn),
            ["Model.Report/definition.pbir"] = "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}",
            ["Model.Report/definition/report.json"] =
                "{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Fact\"}},\"Property\":\"Result\"}}",
        };
        return ProjectScanner.Scan(new InMemoryProjectFileSource("DAX collision", files.Select(file =>
            new ProjectFileContent(file.Key, Encoding.UTF8.GetBytes(file.Value)))));
    }

    private static string Column(string name) => "\tcolumn " + name + "\n\t\tdataType: int64\n";
}

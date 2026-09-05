using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class DaxUnqualifiedCollisionTests
{
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
                "table Fact\n\tmeasure Result = " + expression + "\n" + Column(factColumn) + extraFact,
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

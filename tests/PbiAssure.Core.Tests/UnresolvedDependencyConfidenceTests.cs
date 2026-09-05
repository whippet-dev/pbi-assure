using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// A reference PBI Assure read but could not bind is doubt about the dependency graph. The edge it
/// would have created is missing, so any conclusion that rests on <em>absence</em> of edges in that
/// model rests on an incomplete picture and must not be reported as Established.
///
/// The doubt is routed through <see cref="AnalysisLimitation"/> rather than read directly by the
/// qualifier, so confidence keeps a single input. Scope is the semantic model: an unresolved reference
/// does not say which object it meant, so nothing narrower is safe yet.
/// </summary>
public sealed class UnresolvedDependencyConfidenceTests
{
    [Fact]
    public void NotFoundReferenceQualifiesAbsenceConclusionsInThatModel()
    {
        var inventory = Scan(
            "table Fact\n\n" +
            "\tmeasure Total = SUM ( Fact[Amount] ) + Fact[MissingColumn]\n\n" +
            Column("Amount") + Column("Orphan"));

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.NotFound, unresolved.ResolutionOutcome);

        var limitation = Assert.Single(
            inventory.AnalysisLimitations,
            candidate => candidate.Cause == AnalysisLimitationCauses.ReferenceUnresolved);
        Assert.Equal("PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE", limitation.LimitationId);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.Equal(AnalysisLimitationScopes.SemanticModel, limitation.Scope);

        var orphan = Usage(inventory, "Orphan");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, orphan.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, orphan.ClassificationConfidence);
    }

    [Fact]
    public void AmbiguousReferenceQualifiesAbsenceConclusionsInThatModel()
    {
        // A column and a measure sharing one name on the same table is the documented Ambiguous case.
        var inventory = Scan(
            "table Fact\n\n" +
            "\tmeasure Total = SUM ( Fact[Amount] ) + Fact[Dup]\n\n" +
            "\tmeasure Dup = 1\n\n" +
            Column("Amount") + Column("Dup") + Column("Orphan"));

        var unresolved = Assert.Single(inventory.UnresolvedSemanticDependencies);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.Ambiguous, unresolved.ResolutionOutcome);
        Assert.Contains(
            inventory.AnalysisLimitations,
            candidate => candidate.Cause == AnalysisLimitationCauses.ReferenceUnresolved);

        var orphan = Usage(inventory, "Orphan");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, orphan.UsageState);
        Assert.Equal(ClassificationConfidences.QualifiedByLimitation, orphan.ClassificationConfidence);
    }

    [Fact]
    public void ResolvedDependenciesLeaveAbsenceConclusionsEstablished()
    {
        var inventory = Scan(
            "table Fact\n\n" +
            "\tmeasure Total = SUM ( Fact[Amount] )\n\n" +
            Column("Amount") + Column("Orphan"));

        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.DoesNotContain(
            inventory.AnalysisLimitations,
            candidate => candidate.Cause == AnalysisLimitationCauses.ReferenceUnresolved);

        var orphan = Usage(inventory, "Orphan");
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, orphan.UsageState);
        Assert.Equal(ClassificationConfidences.Established, orphan.ClassificationConfidence);
    }

    /// <summary>
    /// Qualification marks doubt about absence. Evidence already collected cannot be retracted by a
    /// reference that failed to resolve, so every positive state stays Established even in the model
    /// that carries the unresolved reference.
    /// </summary>
    [Fact]
    public void PositiveEvidenceIsNotDowngradedByUnrelatedUnresolvedReferences()
    {
        var inventory = Scan(
            "table Fact\n\n" +
            "\tmeasure Total = SUM ( Fact[Amount] ) + Fact[MissingColumn]\n\n" +
            Column("Amount") + Column("FactKey"),
            projectedMeasure: "Total",
            // A relationship endpoint on a table the report never touches is required by the model's
            // machinery rather than reachable from a report root, which is what StructurallyRequired
            // means. Sort-by would not do: projecting the sorted column makes its target reachable, and
            // reachability wins by precedence.
            dimTable: "table Dim\n\n" + Column("Key"),
            relationships: "relationship r1\n\tfromColumn: Fact.FactKey\n\ttoColumn: Dim.Key\n");

        Assert.Contains(
            inventory.AnalysisLimitations,
            candidate => candidate.Cause == AnalysisLimitationCauses.ReferenceUnresolved);

        foreach (var (objectName, expected) in new[]
                 {
                     ("Total", SemanticUsageStates.DirectlyUsed),
                     ("Amount", SemanticUsageStates.IndirectlyUsed),
                     ("Key", SemanticUsageStates.StructurallyRequired),
                 })
        {
            var usage = Usage(inventory, objectName);
            Assert.Equal(expected, usage.UsageState);
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
        }
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string Column(string name) =>
        $"\tcolumn {name}\n\t\tdataType: int64\n\t\tsummarizeBy: none\n\t\tsourceColumn: {name}\n\n";

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.ObjectName == objectName);

    private static ProjectInventory Scan(
        string factTable,
        string? projectedMeasure = null,
        string? projectedColumn = null,
        string? dimTable = null,
        string? relationships = null)
    {
        var files = new List<ProjectFileContent>
        {
            File("Model.pbip", "{}"),
            File("Model.SemanticModel/definition.pbism", "{}"),
            File("Model.SemanticModel/definition/tables/Fact.tmdl", factTable),
        };

        if (dimTable is not null)
        {
            files.Add(File("Model.SemanticModel/definition/tables/Dim.tmdl", dimTable));
        }

        if (relationships is not null)
        {
            files.Add(File("Model.SemanticModel/definition/relationships.tmdl", relationships));
        }

        if (projectedMeasure is not null)
        {
            files.Add(File("Model.Report/definition.pbir",
                "{\"datasetReference\":{\"byPath\":{\"path\":\"../Model.SemanticModel\"}}}"));
            files.Add(File("Model.Report/definition/pages/pages.json",
                "{\"pageOrder\":[\"p1\"],\"activePageName\":\"p1\"}"));
            files.Add(File("Model.Report/definition/pages/p1/page.json",
                "{\"name\":\"p1\",\"displayName\":\"Page 1\"}"));
            var projections =
                "{\"field\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Fact\"}},\"Property\":\"" +
                projectedMeasure + "\"}},\"queryRef\":\"Fact." + projectedMeasure + "\"}";
            if (projectedColumn is not null)
            {
                projections +=
                    ",{\"field\":{\"Column\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Fact\"}},\"Property\":\"" +
                    projectedColumn + "\"}},\"queryRef\":\"Fact." + projectedColumn + "\"}";
            }

            files.Add(File("Model.Report/definition/pages/p1/visuals/v1/visual.json",
                "{\"name\":\"v1\",\"visual\":{\"visualType\":\"card\",\"query\":{\"queryState\":{\"Values\":{\"projections\":[" +
                projections + "]}}}}}"));
        }

        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, Encoding.UTF8.GetBytes(content));
}

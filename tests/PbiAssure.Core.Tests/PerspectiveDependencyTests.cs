using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

/// <summary>
/// Perspective dependency analysis.
///
/// A perspective is a curated subset of the model that an author deliberately exposed, and which drives
/// the Personalize visuals experience — a report reader can add any of its fields to a visual at run
/// time. Saved report metadata therefore cannot prove which members a reader uses, exactly as it cannot
/// for field-parameter choices, so perspective membership is treated as model-structure evidence rather
/// than left to look unused.
///
/// Membership is narrow: only what a perspective actually lists, plus the documented includeAll case.
///
/// Tests marked Desktop run against genuine Power BI Desktop output. Tests covering includeAll and
/// hierarchies use syntax established by Microsoft documentation, not by any fixture, and are labelled
/// as such.
/// </summary>
public sealed class PerspectiveDependencyTests
{
    // ---- 1 / 2. Real Desktop perspective discovery and resolution ---------------------------

    [Fact]
    public void DesktopPerspectiveIsDiscoveredWithItsTableAndMembers()
    {
        var model = Assert.Single(ScanDesktopFixture().SemanticModels);

        var perspective = Assert.Single(model.Perspectives);
        Assert.Equal("SalesView", perspective.Name);

        var table = Assert.Single(perspective.Tables);
        Assert.Equal("Sales", table.Table);
        Assert.False(table.IncludeAll);
        Assert.Equal(["Region"], table.Columns);
        Assert.Equal(["Total Amount"], table.Measures);
        Assert.Empty(table.Hierarchies);
    }

    [Theory]
    [InlineData("Region", SemanticObjectTypes.Column)]
    [InlineData("Total Amount", SemanticObjectTypes.Measure)]
    public void DesktopPerspectiveMembersResolveToTheirModelObjects(string objectName, string objectType)
    {
        Assert.Contains(
            ScanDesktopFixture().SemanticDependencies,
            edge => edge.DependencyKind == SemanticDependencyKinds.PerspectiveMember &&
                edge.FromObjectName == "SalesView" &&
                edge.FromObjectType == SemanticObjectTypes.Perspective &&
                edge.ToTable == "Sales" &&
                edge.ToObjectName == objectName &&
                edge.ToObjectType == objectType);
    }

    [Fact]
    public void DesktopPerspectiveProducesNoUnresolvedReferences()
    {
        Assert.DoesNotContain(
            ScanDesktopFixture().UnresolvedSemanticDependencies,
            item => item.DependencyKind == SemanticDependencyKinds.PerspectiveMember);
    }

    // ---- 3 / 4 / 5. Usage semantics ---------------------------------------------------------

    /// <summary>
    /// Total Amount is exposed only by the perspective — no report visual references it — so it is the
    /// informative case. Region is deliberately not used to prove this, because row-level security
    /// already roots it.
    /// </summary>
    [Fact]
    public void AMeasureExposedOnlyByAPerspectiveIsStructurallyRequired()
    {
        var usage = Assert.Single(
            ScanDesktopFixture().SemanticObjectUsages,
            u => u.Table == "Sales" && u.ObjectName == "Total Amount");

        Assert.Equal(SemanticUsageStates.StructurallyRequired, usage.UsageState);
    }

    [Fact]
    public void APerspectiveMemberDoesNotBecomeDirectlyUsed()
    {
        var usage = Assert.Single(
            ScanSynthetic(Perspective("View", "Sales", columns: ["Region"])).SemanticObjectUsages,
            u => u.ObjectName == "Region");

        Assert.Equal(SemanticUsageStates.StructurallyRequired, usage.UsageState);
        Assert.Empty(usage.DirectReportReferences);
        Assert.False(usage.IsDirectlyReferencedByReport);
    }

    /// <summary>
    /// The measure's own DAX dependency must come along through ordinary graph traversal, not through
    /// perspective-specific logic.
    /// </summary>
    [Fact]
    public void DependenciesOfAPerspectiveExposedMeasureAreReachedByOrdinaryTraversal()
    {
        var usage = Assert.Single(
            ScanDesktopFixture().SemanticObjectUsages,
            u => u.Table == "Sales" && u.ObjectName == "Amount");

        Assert.Equal(SemanticUsageStates.StructurallyRequired, usage.UsageState);
    }

    // ---- 6. Narrow membership — the critical false-positive guard ---------------------------

    /// <summary>
    /// Microsoft documents that unless includeAll is set, each column, hierarchy and measure must be
    /// added to a perspective individually. Listing one member must therefore not expose the rest of the
    /// table, and naming the table must not expose its fields.
    /// </summary>
    [Fact]
    public void ListingOneMemberDoesNotExposeTheRestOfTheTable()
    {
        var inventory = ScanSynthetic(
            Perspective("View", "Sales", measures: ["Chosen"]),
            table: TableWithTwoMeasures);

        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Chosen").UsageState);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Ignored").UsageState);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Spare").UsageState);
    }

    [Fact]
    public void NamingATableWithoutMembersDoesNotExposeItsFields()
    {
        var inventory = ScanSynthetic(
            "perspective View\n\n\tperspectiveTable Sales\n",
            table: TableWithTwoMeasures);

        Assert.All(
            inventory.SemanticObjectUsages,
            usage => Assert.Equal(SemanticUsageStates.ApparentlyUnused, usage.UsageState));
        // The table itself is exposed, so it is required even though none of its fields are.
        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticTableUsages.Single(u => u.Table == "Sales").UsageState);
    }

    /// <summary>
    /// includeAll is documented TMDL syntax and means every column, hierarchy and measure of the table is
    /// in the perspective. Established by Microsoft documentation only — no fixture emits it.
    /// </summary>
    [Fact]
    public void IncludeAllExposesEveryFieldOfTheTable()
    {
        var inventory = ScanSynthetic(
            "perspective View\n\n\tperspectiveTable Sales\n\t\tincludeAll: True\n",
            table: TableWithTwoMeasures);

        Assert.All(
            inventory.SemanticObjectUsages,
            usage => Assert.Equal(SemanticUsageStates.StructurallyRequired, usage.UsageState));
    }

    // ---- 7 / 8 / 9 / 10. Combination, isolation, unresolved, absence ------------------------

    [Fact]
    public void MembersFromSeveralPerspectivesCombine()
    {
        var inventory = ScanSynthetic(
            [
                Perspective("First", "Sales", measures: ["Chosen"]),
                Perspective("Second", "Sales", measures: ["Ignored"]),
            ],
            table: TableWithTwoMeasures);

        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Chosen").UsageState);
        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Ignored").UsageState);
    }

    [Fact]
    public void APerspectiveInOneModelDoesNotRootAnIdenticallyNamedObjectInAnother()
    {
        var files = new List<ProjectFileContent> { File("Two.pbip", "{}") };
        files.AddRange(ModelFiles("Exposed", TableWithRegion, Perspective("View", "Sales", columns: ["Region"])));
        files.AddRange(ModelFiles("Plain", TableWithRegion));

        var inventory = ProjectScanner.Scan(new InMemoryProjectFileSource("Two models", files));

        Assert.Equal(
            SemanticUsageStates.StructurallyRequired,
            inventory.SemanticObjectUsages
                .Single(u => u.SemanticModel == "Exposed" && u.ObjectName == "Region").UsageState);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            inventory.SemanticObjectUsages
                .Single(u => u.SemanticModel == "Plain" && u.ObjectName == "Region").UsageState);
    }

    [Fact]
    public void APerspectiveMemberNamingAMissingObjectIsRetainedAsUnresolved()
    {
        var inventory = ScanSynthetic(Perspective("View", "Sales", columns: ["Absent"]));

        var unresolved = Assert.Single(
            inventory.UnresolvedSemanticDependencies,
            item => item.DependencyKind == SemanticDependencyKinds.PerspectiveMember);
        Assert.Equal(UnresolvedSemanticDependencyResolutionOutcomes.NotFound, unresolved.ResolutionOutcome);
        Assert.Equal("View", unresolved.FromObjectName);
        Assert.Contains("Absent", unresolved.ReferenceText, StringComparison.Ordinal);
        Assert.DoesNotContain(inventory.SemanticObjectUsages, u => u.ObjectName == "Absent");
    }

    [Fact]
    public void APerspectiveNamingAMissingTableIsRetainedAsUnresolved()
    {
        var inventory = ScanSynthetic(Perspective("View", "Ghost", columns: ["Region"]));

        Assert.Contains(
            inventory.UnresolvedSemanticDependencies,
            item => item.DependencyKind == SemanticDependencyKinds.PerspectiveMember &&
                item.ResolutionOutcome == UnresolvedSemanticDependencyResolutionOutcomes.NotFound);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Region").UsageState);
    }

    [Fact]
    public void AProjectWithoutPerspectivesIsUnchanged()
    {
        var inventory = ScanSynthetic([]);

        Assert.Empty(Assert.Single(inventory.SemanticModels).Perspectives);
        Assert.DoesNotContain(
            inventory.SemanticDependencies,
            edge => edge.DependencyKind == SemanticDependencyKinds.PerspectiveMember);
        Assert.Equal(
            SemanticUsageStates.ApparentlyUnused,
            inventory.SemanticObjectUsages.Single(u => u.ObjectName == "Region").UsageState);
    }

    // ---- 11 / 12 / 14. Limitation semantics --------------------------------------------------

    [Fact]
    public void TheDesktopPerspectiveLimitationIsStillEmittedButCarriesNoDependencyImpact()
    {
        var limitation = Assert.Single(
            ScanDesktopFixture().AnalysisLimitations, item => item.ConstructType == "perspective");

        Assert.Equal(ConstructSupportStates.PartiallyAnalyzed, limitation.SupportState);
        Assert.Contains(AnalysisConcerns.Presentation, limitation.Concerns);
        Assert.Equal(ConstructDependencyImpacts.NoKnownDependencyEffect, limitation.DependencyImpact);
    }

    [Fact]
    public void AnUnrecognisedPerspectiveConstructPreventsNarrowing()
    {
        var inventory = ScanSynthetic(
            "perspective View\n\n\tperspectiveTable Sales\n\t\tperspectiveColumn Region\n\t\tperspectiveSomethingNew Foo\n");

        var limitation = Assert.Single(
            inventory.AnalysisLimitations, item => item.ConstructType == "perspective");
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
        Assert.NotEmpty(
            Assert.Single(Assert.Single(inventory.SemanticModels).Perspectives).UnanalyzedConstructs);
    }

    /// <summary>
    /// The registry keeps its conservative construct-type default. Narrowing is artifact evidence, not a
    /// change of what perspectives can contain.
    /// </summary>
    [Fact]
    public void TheRegistryDefaultForPerspectivesRemainsConservative()
    {
        var rule = SemanticDefinitionFileRegistry.Classify("definition/perspectives/Anything.tmdl");

        Assert.Equal("perspective", rule.ConstructType);
        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, rule.DependencyImpact);
    }

    // ---- 13. Confidence interaction ----------------------------------------------------------

    /// <summary>
    /// Functions remain unparsed, so the fixture still has qualified objects — but the cause is now the
    /// function limitation alone. No perspective-specific rule exists anywhere in the qualifier.
    /// </summary>
    [Fact]
    public void RemainingQualificationComesFromTheFunctionLimitationAlone()
    {
        var inventory = ScanDesktopFixture();

        var qualifying = inventory.AnalysisLimitations
            .Where(item => item.DependencyImpact
                is ConstructDependencyImpacts.MayCreateDependencies
                or ConstructDependencyImpacts.DependencyEffectUnknown)
            .Select(item => item.ConstructType)
            .ToArray();

        Assert.Equal(["function"], qualifying);
        Assert.Contains(
            inventory.SemanticObjectUsages,
            usage => usage.ClassificationConfidence == ClassificationConfidences.QualifiedByLimitation);
    }

    [Fact]
    public void TheFunctionLimitationIsUntouched()
    {
        var limitation = Assert.Single(
            ScanDesktopFixture().AnalysisLimitations, item => item.ConstructType == "function");

        Assert.Equal(ConstructDependencyImpacts.MayCreateDependencies, limitation.DependencyImpact);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private const string TableWithRegion =
        "table Sales\n\n\tcolumn Region\n\t\tdataType: string\n\t\tsourceColumn: Region\n";

    private const string TableWithTwoMeasures =
        "table Sales\n\n\tcolumn Spare\n\t\tdataType: int64\n\t\tsourceColumn: Spare\n" +
        "\n\tmeasure Chosen = 1\n\n\tmeasure Ignored = 2\n";

    private static string Perspective(
        string name,
        string table,
        IReadOnlyList<string>? columns = null,
        IReadOnlyList<string>? measures = null)
    {
        var body = $"perspective {name}\n\n\tperspectiveTable {table}\n";
        foreach (var column in columns ?? [])
        {
            body += $"\t\tperspectiveColumn {Quote(column)}\n";
        }

        foreach (var measure in measures ?? [])
        {
            body += $"\t\tperspectiveMeasure {Quote(measure)}\n";
        }

        return body;
    }

    private static string Quote(string name) => name.Contains(' ') ? $"'{name}'" : name;

    private static ProjectInventory ScanDesktopFixture() =>
        ProjectScanner.Scan(Path.Combine(
            RepositoryRoot(), "tests", "fixtures", "desktop-semantic-constructs"));

    private static ProjectInventory ScanSynthetic(string perspective, string table = TableWithRegion) =>
        ScanSynthetic([perspective], table);

    private static ProjectInventory ScanSynthetic(
        IReadOnlyList<string> perspectives,
        string table = TableWithRegion)
    {
        var files = new List<ProjectFileContent> { File("Sales.pbip", "{}") };
        files.AddRange(ModelFiles("Sales", table, perspectives.ToArray()));
        return ProjectScanner.Scan(new InMemoryProjectFileSource("Synthetic", files));
    }

    private static IEnumerable<ProjectFileContent> ModelFiles(
        string modelName,
        string table,
        params string[] perspectives)
    {
        yield return File($"{modelName}.SemanticModel/definition.pbism", "{}");
        yield return File($"{modelName}.SemanticModel/definition/tables/Sales.tmdl", table);
        foreach (var perspective in perspectives)
        {
            var name = perspective.Split('\n')[0].Replace("perspective ", string.Empty).Trim();
            yield return File(
                $"{modelName}.SemanticModel/definition/perspectives/{name}.tmdl", perspective);
        }
    }

    private static ProjectFileContent File(string relativePath, string content) =>
        new(relativePath, System.Text.Encoding.UTF8.GetBytes(content));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

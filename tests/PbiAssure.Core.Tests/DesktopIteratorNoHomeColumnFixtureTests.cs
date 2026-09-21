using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;
using PbiAssure.Reporting;

namespace PbiAssure.Core.Tests;

/// <summary>
/// A Power BI Desktop-authored PBIP in which a measure on a measures-only table iterates another
/// table and names its column unqualified: <c>SUMX(Dim, [X])</c>, with no column X on the measure's
/// own table. Desktop accepts, saves and reopens that form without rewriting it to <c>Dim[X]</c>. The
/// iterator names its source table as its entire first argument, so the row context of the second
/// argument is proven to be Dim and <c>[X]</c> is Dim[X]. The fixture is byte-for-byte Desktop output;
/// see its README for provenance.
/// </summary>
public sealed class DesktopIteratorNoHomeColumnFixtureTests
{
    private const string MeasuresTable = "Measures (2)";

    [Fact]
    public void DesktopPersistsTheUnqualifiedIteratorReferenceUnchanged()
    {
        var model = Assert.Single(ScanFixture().SemanticModels);

        var measures = Assert.Single(model.Tables, table => table.Name == MeasuresTable);
        Assert.DoesNotContain(measures.Columns, column => column.Name == "X");
        var probe = Assert.Single(measures.Measures, measure => measure.Name == "Iterator Prove");
        Assert.Equal("SUMX(Dim, [X])", probe.Expression);

        // The persisted bytes, not only the parsed expression, hold the unqualified form.
        var tmdl = System.IO.File.ReadAllText(Path.Combine(
            FixturePath(), "desktop-iterator-no-home-column.SemanticModel", "definition", "tables", "Measures (2).tmdl"));
        Assert.Contains("measure 'Iterator Prove' = SUMX(Dim, [X])", tmdl, StringComparison.Ordinal);
        Assert.DoesNotContain("Dim[X]", tmdl, StringComparison.Ordinal);
    }

    [Fact]
    public void TheIteratorRowContextBindsXToDim()
    {
        var inventory = ScanFixture();

        var edge = Assert.Single(inventory.SemanticDependencies, dependency =>
            dependency.FromObjectName == "Iterator Prove" && dependency.ToObjectType == SemanticObjectTypes.Column);
        Assert.Equal("Dim", edge.ToTable);
        Assert.Equal("X", edge.ToObjectName);
        Assert.Equal(SemanticDependencyKinds.Dax, edge.DependencyKind);
        Assert.Equal("[X]", edge.EvidenceText);

        Assert.Empty(inventory.UnresolvedSemanticDependencies);
        Assert.DoesNotContain(inventory.AnalysisLimitations, item => item.LimitationId == "PBI-LIMIT-MODEL-UNRESOLVED-REFERENCE");
        Assert.DoesNotContain(inventory.AnalysisLimitations, item =>
            item.DependencyImpact is ConstructDependencyImpacts.MayCreateDependencies or ConstructDependencyImpacts.DependencyEffectUnknown);
    }

    [Fact]
    public void UsageStatesFollowTheResolvedReferenceAndEveryResultIsEstablished()
    {
        var inventory = ScanFixture();

        var probe = Usage(inventory, MeasuresTable, "Iterator Prove");
        Assert.Equal(SemanticUsageStates.DirectlyUsed, probe.UsageState);

        var x = Usage(inventory, "Dim", "X");
        Assert.Equal(SemanticUsageStates.IndirectlyUsed, x.UsageState);
        Assert.Equal($"Referenced by {MeasuresTable}[Iterator Prove]", SemanticUsagePresentation.DescribeReason(inventory, x));

        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, MeasuresTable, "Unused Control").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, "Dim", "Key").UsageState);
        Assert.Equal(SemanticUsageStates.ApparentlyUnused, Usage(inventory, MeasuresTable, "1").UsageState);

        Assert.Equal(5, inventory.SemanticObjectUsages.Count);
        Assert.All(inventory.SemanticObjectUsages, usage =>
        {
            Assert.Equal(ClassificationConfidences.Established, usage.ClassificationConfidence);
            Assert.Empty(SemanticUsageConfidenceQualifier.Qualifying(usage, inventory.AnalysisLimitations));
        });

        var html = HtmlReportRenderer.Render(inventory);
        Assert.DoesNotContain("class=\"confidence-flag\"", html, StringComparison.Ordinal);
        Assert.Contains("None of them can change a used or unused result.", html, StringComparison.Ordinal);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static SemanticObjectUsage Usage(ProjectInventory inventory, string table, string objectName) =>
        Assert.Single(inventory.SemanticObjectUsages, usage => usage.Table == table && usage.ObjectName == objectName);

    private static ProjectInventory ScanFixture() => ProjectScanner.Scan(FixturePath());

    private static string FixturePath() => Path.Combine(
        RepositoryRoot(), "tests", "fixtures", "desktop-iterator-no-home-column");

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

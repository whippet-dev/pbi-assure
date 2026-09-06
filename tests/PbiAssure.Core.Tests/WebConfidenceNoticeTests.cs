using PbiAssure.Core.Inventory;
using PbiAssure.Web;

namespace PbiAssure.Core.Tests;

/// <summary>
/// The scan itself knows when an absence conclusion rests on incomplete evidence. A reader deciding what
/// to delete should learn that from the summary in front of them, not only by opening the HTML report.
///
/// The notice explains confidence. It does not create a sixth usage state and does not move any object
/// out of the count it already belongs to.
/// </summary>
public sealed class WebConfidenceNoticeTests
{
    [Theory]
    [InlineData(SemanticUsageStates.ApparentlyUnused)]
    [InlineData(SemanticUsageStates.UsedOnlyByUnusedBranch)]
    public void OneQualifiedAbsenceResultProducesTheNotice(string absenceState)
    {
        var summary = Summarize(Usage("Sales", "Amount", absenceState, ClassificationConfidences.QualifiedByLimitation));

        Assert.True(summary.HasQualifiedAbsence);
        Assert.Equal(1, summary.QualifiedAbsenceCount);
        Assert.Equal("1 model object has a classification based on incomplete local evidence.", summary.Detail);
    }

    [Fact]
    public void SeveralQualifiedResultsAreCountedAndReadAsPlural()
    {
        var summary = Summarize(
            Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation),
            Usage("Sales", "Region", SemanticUsageStates.UsedOnlyByUnusedBranch, ClassificationConfidences.QualifiedByLimitation),
            Usage("Sales", "Cost", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation));

        Assert.Equal(3, summary.QualifiedAbsenceCount);
        Assert.Equal("3 model objects have classifications based on incomplete local evidence.", summary.Detail);
    }

    [Fact]
    public void EstablishedAbsenceResultsAloneProduceNoNotice()
    {
        var summary = Summarize(
            Usage("Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.Established),
            Usage("Sales", "Region", SemanticUsageStates.UsedOnlyByUnusedBranch, ClassificationConfidences.Established));

        Assert.False(summary.HasQualifiedAbsence);
        Assert.Equal(0, summary.QualifiedAbsenceCount);
    }

    /// <summary>
    /// Confidence alone is not the trigger. A positive result carrying a qualified marker would still be
    /// evidence PBI Assure actually found, so it must not be counted as a doubtful absence.
    /// </summary>
    [Theory]
    [InlineData(SemanticUsageStates.DirectlyUsed)]
    [InlineData(SemanticUsageStates.IndirectlyUsed)]
    [InlineData(SemanticUsageStates.StructurallyRequired)]
    public void PositiveStatesNeverTriggerTheNotice(string positiveState)
    {
        var summary = Summarize(
            Usage("Sales", "Amount", positiveState, ClassificationConfidences.QualifiedByLimitation),
            Usage("Sales", "Region", positiveState, ClassificationConfidences.Established));

        Assert.False(summary.HasQualifiedAbsence);
        Assert.Equal(0, summary.QualifiedAbsenceCount);
    }

    [Fact]
    public void TheNoticeDoesNotMoveAnObjectOutOfItsExistingCount()
    {
        var qualified = Usage(
            "Sales", "Amount", SemanticUsageStates.ApparentlyUnused, ClassificationConfidences.QualifiedByLimitation);
        var inventory = Inventory(qualified);

        Assert.Equal(1, WebConfidenceSummary.FromInventory(inventory).QualifiedAbsenceCount);
        // The same object is still inside the metric the summary shows.
        Assert.Equal(1, inventory.DeveloperApparentlyUnusedSemanticObjectCount);
        Assert.Equal(1, inventory.DeveloperSemanticObjectCountForState(SemanticUsageStates.ApparentlyUnused));
        Assert.Equal(1, inventory.ApparentlyUnusedSemanticObjectCount);
    }

    [Fact]
    public void TheNoticeRoutesToAnalysisCoverageInTheExistingReport()
    {
        Assert.Contains("Analysis coverage", WebConfidenceSummary.Guidance, StringComparison.Ordinal);
        Assert.Contains("interactive report", WebConfidenceSummary.Guidance, StringComparison.Ordinal);
        Assert.Equal("Some absence-based results are qualified", WebConfidenceSummary.Headline);

        var markup = HomeMarkup();
        // The route reuses the report action already on this page rather than a new app route.
        Assert.Contains("Open interactive report", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"coverage\"", ResultsSection(markup), StringComparison.Ordinal);
    }

    [Fact]
    public void TheNoticeIsRenderedOnlyWhenSomethingIsQualifiedAndIsNotStyledAsASeverity()
    {
        var markup = HomeMarkup();
        var results = ResultsSection(markup);

        Assert.Contains("@if (confidence.HasQualifiedAbsence)", results, StringComparison.Ordinal);
        Assert.Contains("class=\"confidence-notice\"", results, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"alert\"", results, StringComparison.Ordinal);

        var styles = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "PbiAssure.Web", "wwwroot", "css", "app.css"));
        var rule = ExtractBetween(styles, ".confidence-notice {", "}");
        Assert.DoesNotContain("--pa-warning", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("--pa-error", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("--pa-accent", rule, StringComparison.Ordinal);
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static WebConfidenceSummary Summarize(params SemanticObjectUsage[] usages) =>
        WebConfidenceSummary.FromInventory(Inventory(usages));

    private static SemanticObjectUsage Usage(string table, string name, string state, string confidence) =>
        new("Synthetic", table, name, SemanticObjectTypes.Column, null, [], state)
        {
            ClassificationConfidence = confidence,
        };

    private static ProjectInventory Inventory(params SemanticObjectUsage[] usages) =>
        new(
            SchemaVersion: "0.26",
            RootPath: "Synthetic",
            ScannedAtUtc: DateTimeOffset.UnixEpoch,
            Artifacts: [],
            Reports: [],
            SemanticModels: [],
            SemanticObjectUsages: usages,
            SemanticTableUsages: [],
            SemanticDependencies: [],
            PowerQueryUsages: [],
            PowerQueryDependencies: [],
            PowerQueryColumnUsages: [],
            SemanticTablePowerQueryContexts: [],
            DataSources: [],
            UnresolvedSemanticReferences: [],
            UnresolvedSemanticDependencies: [],
            Findings: []);

    private static string HomeMarkup() => File.ReadAllText(Path.Combine(
        FindRepositoryRoot(), "src", "PbiAssure.Web", "Pages", "Home.razor"));

    private static string ResultsSection(string markup) =>
        ExtractBetween(markup, "<section class=\"results\"", "<h3>Project</h3>");

    private static string ExtractBetween(string value, string start, string end)
    {
        var from = value.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"Expected to find '{start}'.");
        var to = value.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to > from, $"Expected to find '{end}' after '{start}'.");
        return value[from..to];
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PbiAssure.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the PBI Assure repository root.");
    }
}

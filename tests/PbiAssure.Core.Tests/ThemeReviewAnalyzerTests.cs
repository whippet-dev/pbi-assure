using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class ThemeReviewAnalyzerTests
{
    [Fact]
    public void ThemeStatusDistinguishesCustomBaseOnlyAndUnresolvedResources()
    {
        var availableBase = Source(ThemeSourceKinds.SharedBase, "Base", ThemeAvailabilityStates.Available);
        var custom = Source(ThemeSourceKinds.RegisteredCustom, "Approved", ThemeAvailabilityStates.Available);

        Assert.Equal(ThemeReviewStatusStates.CustomThemeAppliedOverBase,
            ThemeReviewAnalyzer.Analyze(new ThemeInventory(availableBase, custom, [], []), []).Status.State);
        Assert.Equal(ThemeReviewStatusStates.BaseThemeOnly,
            ThemeReviewAnalyzer.Analyze(new ThemeInventory(availableBase, null, [], []), []).Status.State);
        Assert.Equal(ThemeReviewStatusStates.ThemeResourceUnresolved,
            ThemeReviewAnalyzer.Analyze(new ThemeInventory(
                Source(ThemeSourceKinds.ImplicitBase, null, ThemeAvailabilityStates.MetadataUnavailable), null, [], []), []).Status.State);
    }

    [Fact]
    public void OnlySupportedDifferencesBecomeThemeDeviations()
    {
        var observations = new[]
        {
            Formatting("30", ThemeFormattingComparisonStates.SavedValueDiffersFromTheme, "18"),
            Formatting("18", ThemeFormattingComparisonStates.SavedValueMatchesTheme, "18"),
            Missing(ThemeFormattingComparisonStates.NoSavedLocalValue, "18"),
            Formatting("30", ThemeFormattingComparisonStates.ComparisonAmbiguous, null),
        };

        var review = ThemeReviewAnalyzer.Analyze(AvailableTheme(), [Page(Visual("chart", "clusteredColumnChart", observations))]);

        var deviation = Assert.Single(review.Deviations);
        Assert.Equal("30", deviation.SavedValue);
        Assert.Equal("18", deviation.ThemeValue);
    }

    [Fact]
    public void StrongDominantPatternProducesConsistencyReview()
    {
        var page = Page(
            Visual("one", "clusteredColumnChart", [Formatting("16")]),
            Visual("two", "clusteredColumnChart", [Formatting("16")]),
            Visual("three", "clusteredColumnChart", [Formatting("16")]),
            Visual("outlier", "clusteredColumnChart", [Formatting("11")]));

        var observation = Assert.Single(ThemeReviewAnalyzer.Analyze(AvailableTheme(), [page]).ConsistencyObservations);

        Assert.Equal("11", observation.ObservedValue);
        Assert.Equal("16", observation.DominantValue);
        Assert.Equal(4, observation.PeerCount);
        Assert.Equal(3, observation.DominantCount);
    }

    [Fact]
    public void ConsistencyReviewRequiresEnoughComparablePeersAndClearMajority()
    {
        var tooFew = Page(
            Visual("one", "clusteredColumnChart", [Formatting("16")]),
            Visual("two", "clusteredColumnChart", [Formatting("16")]),
            Visual("three", "clusteredColumnChart", [Formatting("11")]));
        var split = Page(
            Visual("one", "clusteredColumnChart", [Formatting("16")]),
            Visual("two", "clusteredColumnChart", [Formatting("16")]),
            Visual("three", "clusteredColumnChart", [Formatting("11")]),
            Visual("four", "clusteredColumnChart", [Formatting("11")]));

        Assert.Empty(ThemeReviewAnalyzer.Analyze(AvailableTheme(), [tooFew]).ConsistencyObservations);
        Assert.Empty(ThemeReviewAnalyzer.Analyze(AvailableTheme(), [split]).ConsistencyObservations);
    }

    [Fact]
    public void ConsistencyReviewDoesNotCompareDifferentVisualTypesOrUniformPeers()
    {
        var mixed = Page(
            Visual("one", "clusteredColumnChart", [Formatting("16")]),
            Visual("two", "clusteredColumnChart", [Formatting("16")]),
            Visual("three", "clusteredColumnChart", [Formatting("16")]),
            Visual("bar", "clusteredBarChart", [Formatting("11")]));
        var uniform = Page(
            Visual("one", "clusteredColumnChart", [Formatting("16")]),
            Visual("two", "clusteredColumnChart", [Formatting("16")]),
            Visual("three", "clusteredColumnChart", [Formatting("16")]),
            Visual("four", "clusteredColumnChart", [Formatting("16")]));

        Assert.Empty(ThemeReviewAnalyzer.Analyze(AvailableTheme(), [mixed]).ConsistencyObservations);
        Assert.Empty(ThemeReviewAnalyzer.Analyze(AvailableTheme(), [uniform]).ConsistencyObservations);
    }

    [Fact]
    public void AccessibilityReviewStaysEmptyWithoutCompleteEvidence()
    {
        var review = ThemeReviewAnalyzer.Analyze(AvailableTheme(), [Page(Visual("chart", "clusteredColumnChart", [Formatting("16")]))]);

        Assert.Empty(review.AccessibilityObservations);
    }

    private static ThemeInventory AvailableTheme() =>
        new(Source(ThemeSourceKinds.SharedBase, "Base", ThemeAvailabilityStates.Available), null, [], []);

    private static ThemeSourceInventory Source(string kind, string? name, string availability) =>
        new(kind, name, name, null, null, availability, "$.theme", null);

    private static PageInventory Page(params VisualInventory[] visuals) =>
        new("page", "Overview", "report/pages/page", "report/pages/page/page.json", null, null, null,
            0, true, null, null, 1280, 720, [], [], [], visuals);

    private static VisualInventory Visual(string name, string type, IReadOnlyList<PersistedFormattingObservation> formatting) =>
        new(name, type, $"visuals/{name}/visual.json", null, false,
            new VisualPosition(null, null, null, null, null, null),
            new VisualAccessibilityInventory(false, null, false, true, false, null, false),
            null, false, [], [], [])
        {
            PersistedFormatting = formatting,
        };

    private static PersistedFormattingObservation Formatting(
        string value,
        string? comparisonState = null,
        string? themeValue = null) =>
        new("title.fontSize", "Title font size", PersistedFormattingClassifications.PersistedLiteral,
            value, value, "$.title.fontSize", false, null, null, null, "Literal", null, true, false)
        {
            ThemeComparison = comparisonState is null
                ? null
                : new ThemeFormattingComparison(comparisonState, value, themeValue, "$.theme.fontSize", "theme.json"),
        };

    private static PersistedFormattingObservation Missing(string comparisonState, string themeValue) =>
        new("title.fontSize", "Title font size", PersistedFormattingClassifications.NoPersistedValue,
            null, null, "$.title.fontSize", false, null, null, null, null, null, true, false)
        {
            ThemeComparison = new ThemeFormattingComparison(comparisonState, null, themeValue, "$.theme.fontSize", "theme.json"),
        };
}

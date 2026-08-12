using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Core.Tests;

public sealed class ThemeFormattingComparisonTests
{
    [Fact]
    public void DesktopFixtureSequenceConfirmsAbsentDifferentSameAbsentStates()
    {
        Assert.Equal(ThemeFormattingComparisonStates.NoSavedLocalValue, Compare(Missing()).State);
        Assert.Equal(ThemeFormattingComparisonStates.SavedValueDiffersFromTheme, Compare(Literal("30")).State);
        Assert.Equal(ThemeFormattingComparisonStates.SavedValueMatchesTheme, Compare(Literal("18")).State);
        Assert.Equal(ThemeFormattingComparisonStates.NoSavedLocalValue, Compare(Missing()).State);
    }

    [Fact]
    public void MappingIsLimitedToClusteredColumnTitleFontSize()
    {
        Assert.NotNull(ThemeFormattingComparisonAnalyzer.Compare("clusteredColumnChart", Literal("18"), Rules()));
        Assert.Null(ThemeFormattingComparisonAnalyzer.Compare("stackedColumnChart", Literal("18"), Rules()));
        Assert.Null(ThemeFormattingComparisonAnalyzer.Compare("clusteredBarChart", Literal("18"), Rules()));
        Assert.Null(ThemeFormattingComparisonAnalyzer.Compare("clusteredColumnChart", Literal("18", "title.fontFamily"), Rules()));
        Assert.Null(ThemeFormattingComparisonAnalyzer.Compare("clusteredColumnChart", Literal("18", "title.fontColor"), Rules()));
        Assert.Null(ThemeFormattingComparisonAnalyzer.Compare("clusteredColumnChart", Literal("18", "labels.fontSize"), Rules()));
    }

    [Fact]
    public void EqualAndDifferentValuesRetainBothEvidenceValues()
    {
        var equal = Compare(Literal("18"));
        var different = Compare(Literal("30"));

        Assert.Equal(ThemeFormattingComparisonStates.SavedValueMatchesTheme, equal.State);
        Assert.Equal("18", equal.SavedValue);
        Assert.Equal("18", equal.ThemeRuleValue);
        Assert.Equal(ThemeFormattingComparisonStates.SavedValueDiffersFromTheme, different.State);
        Assert.Equal("30", different.SavedValue);
        Assert.Equal("18", different.ThemeRuleValue);
        Assert.Equal("$.visualStyles.columnChart.*.title[0].fontSize", different.ThemeRuleEvidencePath);
        Assert.Equal("Custom.json", different.ThemeSourcePath);
    }

    [Fact]
    public void NoLocalValueIsNotClassifiedAsMatch()
    {
        var result = Compare(Missing());

        Assert.Equal(ThemeFormattingComparisonStates.NoSavedLocalValue, result.State);
        Assert.Null(result.SavedValue);
        Assert.Equal("18", result.ThemeRuleValue);
    }

    [Fact]
    public void MultipleCandidatesRemainAmbiguous()
    {
        var result = ThemeFormattingComparisonAnalyzer.Compare("clusteredColumnChart", Literal("18"),
            new ThemeRuleIndex(Rules().Rules.Concat(Rules(20, ThemeLayers.Base).Rules)));

        Assert.Equal(ThemeFormattingComparisonStates.ComparisonAmbiguous, result!.State);
        Assert.Null(result.ThemeRuleValue);
    }

    [Fact]
    public void UnsupportedAndUnavailableCandidatesStayConservative()
    {
        var unsupportedRule = Rule(ThemeRuleValueKinds.UnsupportedComplex, "[1,2]");
        var unsupported = ThemeFormattingComparisonAnalyzer.Compare("clusteredColumnChart", Literal("18"), new ThemeRuleIndex([unsupportedRule]));
        var unavailable = ThemeFormattingComparisonAnalyzer.Compare("clusteredColumnChart", Literal("18"), ThemeRuleIndex.Empty);

        Assert.Equal(ThemeFormattingComparisonStates.DynamicOrUnsupported, unsupported!.State);
        Assert.Equal(ThemeFormattingComparisonStates.ThemeCandidateUnavailable, unavailable!.State);
    }

    [Fact]
    public void DynamicAndSelectorScopedFormattingAreNotComparedAsStaticValues()
    {
        var dynamic = Missing() with
        {
            Classification = PersistedFormattingClassifications.DynamicExpression,
            NormalizedValue = "Model[Measure]",
        };
        var scoped = Literal("18") with { IsSelectorScoped = true, SelectorKind = VisualSelectorKinds.ScopeId };

        Assert.Equal(ThemeFormattingComparisonStates.DynamicOrUnsupported,
            ThemeFormattingComparisonAnalyzer.Compare("clusteredColumnChart", dynamic, Rules())!.State);
        Assert.Null(ThemeFormattingComparisonAnalyzer.Compare("clusteredColumnChart", scoped, Rules()));
    }

    private static ThemeFormattingComparison Compare(PersistedFormattingObservation observation) =>
        Assert.IsType<ThemeFormattingComparison>(
            ThemeFormattingComparisonAnalyzer.Compare("clusteredColumnChart", observation, Rules()));

    private static ThemeRuleIndex Rules(decimal value = 18, string layer = ThemeLayers.Custom) =>
        new([Rule(ThemeRuleValueKinds.NumericLiteral, value.ToString(System.Globalization.CultureInfo.InvariantCulture), layer)]);

    private static ThemeVisualStyleRule Rule(string kind, string? value, string layer = ThemeLayers.Custom) =>
        new(layer, "Custom.json", "Custom.json", "columnChart", "*", "title", null, "fontSize",
            kind, value, "$.visualStyles.columnChart.*.title[0].fontSize", 0);

    private static PersistedFormattingObservation Missing() =>
        new("title.fontSize", "Title font size", PersistedFormattingClassifications.NoPersistedValue,
            null, null, "$.visual.visualContainerObjects.title[].properties.fontSize", false,
            null, null, null, null, null, true, false);

    private static PersistedFormattingObservation Literal(string value, string property = "title.fontSize") =>
        new(property, property, PersistedFormattingClassifications.PersistedLiteral,
            value, value + "D", "$.visual.visualContainerObjects.title[0].properties.fontSize", false,
            null, null, null, "Literal", null, true, false);
}

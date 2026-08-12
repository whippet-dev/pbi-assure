using System.Globalization;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class ThemeFormattingComparisonAnalyzer
{
    private const string SupportedPbirVisualType = "clusteredColumnChart";
    private const string SupportedProperty = "title.fontSize";
    private const string SupportedThemeVisualType = "columnChart";
    private const string SupportedThemeCard = "title";
    private const string SupportedThemeProperty = "fontSize";

    public static PersistedFormattingObservation[] Apply(
        string? visualType,
        IEnumerable<PersistedFormattingObservation> observations,
        ThemeRuleIndex activeRules)
    {
        return observations.Select(observation => observation with
        {
            ThemeComparison = Compare(visualType, observation, activeRules),
        }).ToArray();
    }

    internal static ThemeFormattingComparison? Compare(
        string? visualType,
        PersistedFormattingObservation observation,
        ThemeRuleIndex activeRules)
    {
        if (!string.Equals(visualType, SupportedPbirVisualType, StringComparison.Ordinal) ||
            !string.Equals(observation.PropertyKey, SupportedProperty, StringComparison.Ordinal) ||
            observation.IsSelectorScoped)
        {
            return null;
        }

        var resolution = activeRules.Resolve(
            SupportedThemeVisualType, "*", SupportedThemeCard, SupportedThemeProperty);
        if (resolution.State == ThemeCandidateResolutionStates.MultipleCandidates)
            return new(ThemeFormattingComparisonStates.ComparisonAmbiguous, observation.NormalizedValue, null, null, null);
        if (resolution.State == ThemeCandidateResolutionStates.NoExplicitRule ||
            resolution.State == ThemeCandidateResolutionStates.MappingUnavailable)
            return new(ThemeFormattingComparisonStates.ThemeCandidateUnavailable, observation.NormalizedValue, null, null, null);
        var firstCandidate = resolution.Candidates.Count == 0 ? null : resolution.Candidates[0];
        if (resolution.State == ThemeCandidateResolutionStates.UnsupportedCandidate ||
            resolution.Candidates.Count != 1 ||
            firstCandidate is not { ValueKind: ThemeRuleValueKinds.NumericLiteral } candidate ||
            !TryNumber(candidate.NormalizedValue, out var themeNumber))
            return new(ThemeFormattingComparisonStates.DynamicOrUnsupported, observation.NormalizedValue, null,
                firstCandidate?.EvidencePath, firstCandidate?.SourcePath);

        var themeValue = themeNumber.ToString("0.################", CultureInfo.InvariantCulture);
        if (observation.Classification == PersistedFormattingClassifications.NoPersistedValue)
            return Result(ThemeFormattingComparisonStates.NoSavedLocalValue, null, themeValue, candidate);
        if (observation.Classification != PersistedFormattingClassifications.PersistedLiteral ||
            !TryNumber(observation.NormalizedValue, out var savedNumber))
            return Result(ThemeFormattingComparisonStates.DynamicOrUnsupported, observation.NormalizedValue, themeValue, candidate);

        var state = savedNumber == themeNumber
            ? ThemeFormattingComparisonStates.SavedValueMatchesTheme
            : ThemeFormattingComparisonStates.SavedValueDiffersFromTheme;
        return Result(state, savedNumber.ToString("0.################", CultureInfo.InvariantCulture), themeValue, candidate);
    }

    private static ThemeFormattingComparison Result(
        string state,
        string? savedValue,
        string? themeValue,
        ThemeVisualStyleRule candidate) =>
        new(state, savedValue, themeValue, candidate.EvidencePath, candidate.SourcePath);

    private static bool TryNumber(string? value, out decimal number) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
}

using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class ThemeReviewAnalyzer
{
    internal const int MinimumConsistencyPeers = 4;
    internal const decimal DominantConsistencyShare = 0.75m;

    private static readonly HashSet<string> ConsistencyProperties = new(StringComparer.Ordinal)
    {
        "title.fontSize",
        "title.fontColor",
        "title.background",
    };

    public static ThemeReviewInventory Analyze(ThemeInventory theme, IReadOnlyList<PageInventory> pages)
    {
        var observations = pages
            .SelectMany(page => page.Visuals.SelectMany(visual => visual.PersistedFormatting.Select(formatting =>
                new ObservationContext(page, visual, formatting))))
            .ToArray();

        return new ThemeReviewInventory(
            Status(theme),
            Deviations(observations),
            Consistency(observations),
            []);
    }

    private static ThemeStatusInventory Status(ThemeInventory theme)
    {
        var unresolved = theme.BaseSource.AvailabilityState != ThemeAvailabilityStates.Available ||
            theme.CustomSource is { AvailabilityState: not ThemeAvailabilityStates.Available };
        var state = unresolved
            ? ThemeReviewStatusStates.ThemeResourceUnresolved
            : theme.CustomSource is null
                ? ThemeReviewStatusStates.BaseThemeOnly
                : ThemeReviewStatusStates.CustomThemeAppliedOverBase;

        var resourceStatus = unresolved
            ? theme.CustomSource?.AvailabilityState ?? theme.BaseSource.AvailabilityState
            : ThemeAvailabilityStates.Available;

        return new ThemeStatusInventory(
            state,
            theme.BaseSource.ThemeName ?? theme.BaseSource.ReferenceName,
            theme.CustomSource?.ThemeName ?? theme.CustomSource?.ReferenceName,
            theme.CustomSource is not null,
            resourceStatus);
    }

    private static ThemeDeviationInventory[] Deviations(IEnumerable<ObservationContext> observations) =>
        observations
            .Where(item => item.Formatting.IncludeInHeadline &&
                item.Formatting.ThemeComparison is { State: ThemeFormattingComparisonStates.SavedValueDiffersFromTheme,
                    SavedValue: not null, ThemeRuleValue: not null })
            .Select(item => new ThemeDeviationInventory(
                item.Page.Name,
                item.Page.DisplayName,
                item.Visual.Name,
                item.Visual.VisualType,
                item.Formatting.PropertyKey,
                item.Formatting.PropertyLabel,
                item.Formatting.ThemeComparison!.SavedValue!,
                item.Formatting.ThemeComparison.ThemeRuleValue!,
                item.Formatting.ThemeComparison.ThemeRuleEvidencePath))
            .ToArray();

    private static ConsistencyObservation[] Consistency(IEnumerable<ObservationContext> observations)
    {
        var eligible = observations.Where(item =>
            item.Formatting.IncludeInHeadline &&
            !item.Formatting.IsSelectorScoped &&
            item.Formatting.Classification == PersistedFormattingClassifications.PersistedLiteral &&
            ConsistencyProperties.Contains(item.Formatting.PropertyKey) &&
            !string.IsNullOrWhiteSpace(item.Formatting.NormalizedValue) &&
            !string.IsNullOrWhiteSpace(item.Visual.VisualType));

        var result = new List<ConsistencyObservation>();
        foreach (var peers in eligible.GroupBy(item => (item.Visual.VisualType!, item.Formatting.PropertyKey)))
        {
            var values = peers.ToArray();
            if (values.Length < MinimumConsistencyPeers) continue;

            var ranked = values
                .GroupBy(item => item.Formatting.NormalizedValue!, StringComparer.OrdinalIgnoreCase)
                .Select(group => new { Value = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.Value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (ranked.Length < 2 || ranked[0].Count == ranked[1].Count) continue;

            var dominant = ranked[0];
            if ((decimal)dominant.Count / values.Length < DominantConsistencyShare) continue;

            result.AddRange(values
                .Where(item => !string.Equals(item.Formatting.NormalizedValue, dominant.Value, StringComparison.OrdinalIgnoreCase))
                .Select(item => new ConsistencyObservation(
                    item.Page.Name,
                    item.Page.DisplayName,
                    item.Visual.Name,
                    item.Visual.VisualType,
                    item.Formatting.PropertyKey,
                    item.Formatting.PropertyLabel,
                    item.Formatting.NormalizedValue!,
                    dominant.Value,
                    values.Length,
                    dominant.Count)));
        }

        return result.ToArray();
    }

    private sealed record ObservationContext(
        PageInventory Page,
        VisualInventory Visual,
        PersistedFormattingObservation Formatting);
}

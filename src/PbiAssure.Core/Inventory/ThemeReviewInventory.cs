namespace PbiAssure.Core.Inventory;

public static class ThemeReviewStatusStates
{
    public const string CustomThemeAppliedOverBase = "CustomThemeAppliedOverBase";
    public const string BaseThemeOnly = "BaseThemeOnly";
    public const string ThemeResourceUnresolved = "ThemeResourceUnresolved";
}

public sealed record ThemeStatusInventory(
    string State,
    string? BaseThemeName,
    string? CustomThemeName,
    bool CustomThemeApplied,
    string ResourceStatus);

public sealed record ThemeDeviationInventory(
    string PageName,
    string PageDisplayName,
    string VisualName,
    string? VisualType,
    string PropertyKey,
    string PropertyLabel,
    string SavedValue,
    string ThemeValue,
    string? ThemeRuleEvidencePath);

public sealed record ConsistencyObservation(
    string PageName,
    string PageDisplayName,
    string VisualName,
    string? VisualType,
    string PropertyKey,
    string PropertyLabel,
    string ObservedValue,
    string DominantValue,
    int PeerCount,
    int DominantCount);

public sealed record ThemeAccessibilityObservation(
    string PageName,
    string VisualName,
    string Property,
    string Observation);

public sealed record ThemeReviewInventory(
    ThemeStatusInventory Status,
    IReadOnlyList<ThemeDeviationInventory> Deviations,
    IReadOnlyList<ConsistencyObservation> ConsistencyObservations,
    IReadOnlyList<ThemeAccessibilityObservation> AccessibilityObservations)
{
    public static ThemeReviewInventory Unavailable { get; } = new(
        new ThemeStatusInventory(
            ThemeReviewStatusStates.ThemeResourceUnresolved,
            null,
            null,
            false,
            ThemeAvailabilityStates.MetadataUnavailable),
        [],
        [],
        []);
}

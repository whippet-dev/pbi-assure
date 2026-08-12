namespace PbiAssure.Core.Inventory;

public static class PersistedFormattingClassifications
{
    public const string NoPersistedValue = "NoPersistedValue";
    public const string PersistedLiteral = "PersistedLiteral";
    public const string ThemeReference = "ThemeReference";
    public const string DynamicExpression = "DynamicExpression";
    public const string Unsupported = "Unsupported";
}

public static class ThemeFormattingComparisonStates
{
    public const string NoSavedLocalValue = "NoSavedLocalValue";
    public const string SavedValueMatchesTheme = "SavedValueMatchesTheme";
    public const string SavedValueDiffersFromTheme = "SavedValueDiffersFromTheme";
    public const string ThemeCandidateUnavailable = "ThemeCandidateUnavailable";
    public const string ComparisonAmbiguous = "ComparisonAmbiguous";
    public const string DynamicOrUnsupported = "DynamicOrUnsupported";
}

public sealed record ThemeFormattingComparison(
    string State,
    string? SavedValue,
    string? ThemeRuleValue,
    string? ThemeRuleEvidencePath,
    string? ThemeSourcePath);

public sealed record PersistedFormattingObservation(
    string PropertyKey,
    string PropertyLabel,
    string Classification,
    string? NormalizedValue,
    string? RawValue,
    string EvidencePath,
    bool IsSelectorScoped,
    string? SelectorKind,
    string? SelectorScope,
    string? SelectorRelevance,
    string? ExpressionKind,
    string? ExpressionSource,
    bool IncludeInHeadline,
    bool IsAmbiguous)
{
    public ThemeFormattingComparison? ThemeComparison { get; init; }
}

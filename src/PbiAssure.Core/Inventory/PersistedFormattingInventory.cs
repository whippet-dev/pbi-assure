namespace PbiAssure.Core.Inventory;

public static class PersistedFormattingClassifications
{
    public const string NoPersistedValue = "NoPersistedValue";
    public const string PersistedLiteral = "PersistedLiteral";
    public const string ThemeReference = "ThemeReference";
    public const string DynamicExpression = "DynamicExpression";
    public const string Unsupported = "Unsupported";
}

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
    bool IsAmbiguous);

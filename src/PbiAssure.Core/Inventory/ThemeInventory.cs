namespace PbiAssure.Core.Inventory;

public static class ThemeSourceKinds
{
    public const string SharedBase = "SharedBase";
    public const string RegisteredCustom = "RegisteredCustom";
    public const string ImplicitBase = "ImplicitBase";
    public const string Unknown = "Unknown";
}

public static class ThemeAvailabilityStates
{
    public const string Available = "Available";
    public const string ReferencedButUnavailable = "ReferencedButUnavailable";
    public const string MetadataUnavailable = "MetadataUnavailable";
    public const string Malformed = "Malformed";
}

public sealed record ThemeImportVersion(
    string? Visual,
    string? Report,
    string? Page);

public sealed record ThemeSourceInventory(
    string Kind,
    string? ReferenceName,
    string? ThemeName,
    string? ResourcePath,
    ThemeImportVersion? ReportVersionAtImport,
    string AvailabilityState,
    string EvidencePath,
    ThemeMetadataInventory? Metadata);

public sealed record ThemeMetadataInventory(
    string? Name,
    IReadOnlyList<string> DataColors,
    IReadOnlyList<ThemeNamedColorInventory> NamedColors,
    IReadOnlyList<ThemeTextClassInventory> TextClasses,
    int VisualStyleRuleCount,
    IReadOnlyList<string> VisualTypes,
    string? SchemaUri)
{
    public int DistinctDataColorCount => DataColors.Distinct(StringComparer.OrdinalIgnoreCase).Count();

    internal ThemeRuleIndex VisualStyleRules { get; init; } = ThemeRuleIndex.Empty;
}

public sealed record ThemeNamedColorInventory(
    string Name,
    string Value,
    string EvidencePath);

public sealed record ThemeTextClassInventory(
    string Name,
    string? FontFamily,
    double? FontSize,
    string? Color,
    string EvidencePath);

public sealed record RegisteredThemeResourceInventory(
    string Name,
    string? ResourcePath,
    bool IsActive,
    string AvailabilityState);

public sealed record ThemeInventory(
    ThemeSourceInventory BaseSource,
    ThemeSourceInventory? CustomSource,
    IReadOnlyList<RegisteredThemeResourceInventory> RegisteredThemeResources,
    IReadOnlyList<string> ResolutionIssues)
{
    internal ThemeRuleIndex ActiveVisualStyleRules { get; init; } = ThemeRuleIndex.Empty;

    public static ThemeInventory Unavailable { get; } = new(
        new ThemeSourceInventory(
            ThemeSourceKinds.ImplicitBase,
            null,
            null,
            null,
            null,
            ThemeAvailabilityStates.MetadataUnavailable,
            "$.themeCollection.baseTheme",
            null),
        null,
        [],
        []);

    public string ActiveState => CustomSource is null
        ? BaseSource.AvailabilityState == ThemeAvailabilityStates.MetadataUnavailable
            ? "Base theme metadata unavailable"
            : "Base theme only"
        : "Custom theme layered over base";
}

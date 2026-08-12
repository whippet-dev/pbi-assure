namespace PbiAssure.Core.Inventory;

internal static class ThemeLayers
{
    public const string Base = "Base";
    public const string Custom = "Custom";
}

internal static class ThemeRuleValueKinds
{
    public const string NumericLiteral = "NumericLiteral";
    public const string BooleanLiteral = "BooleanLiteral";
    public const string TextLiteral = "TextLiteral";
    public const string ColorLiteral = "ColorLiteral";
    public const string ThemeReference = "ThemeReference";
    public const string UnsupportedComplex = "UnsupportedComplex";
}

internal sealed record ThemeVisualStyleRule(
    string Layer,
    string? SourceReference,
    string? SourcePath,
    string VisualType,
    string Preset,
    string Card,
    string? Discriminator,
    string Property,
    string ValueKind,
    string? NormalizedValue,
    string EvidencePath,
    int SourceOrder);

internal static class ThemeCandidateResolutionStates
{
    public const string NoExplicitRule = "NoExplicitRule";
    public const string SingleSupportedCandidate = "SingleSupportedCandidate";
    public const string MultipleCandidates = "MultipleCandidates";
    public const string UnsupportedCandidate = "UnsupportedCandidate";
    public const string MappingUnavailable = "MappingUnavailable";
}

internal sealed record ThemeCandidateResolution(
    string State,
    IReadOnlyList<ThemeVisualStyleRule> Candidates,
    string? Reason = null);

internal static class ThemeVisualTypeMap
{
    // Intentionally empty until an official schema mapping or Desktop-authored fixture proves an alias.
    private static readonly Dictionary<string, string> ProvenAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static string? Resolve(string visualType, IEnumerable<string> availableThemeTypes)
    {
        var exact = availableThemeTypes.FirstOrDefault(candidate =>
            string.Equals(candidate, visualType, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;
        return ProvenAliases.TryGetValue(visualType, out var mapped) ? mapped : null;
    }
}

internal sealed class ThemeRuleIndex
{
    private readonly ThemeVisualStyleRule[] rules;
    private readonly Dictionary<(string Card, string Property), ThemeVisualStyleRule[]> byProperty;

    public static ThemeRuleIndex Empty { get; } = new([]);

    public ThemeRuleIndex(IEnumerable<ThemeVisualStyleRule> rules)
    {
        this.rules = rules.ToArray();
        byProperty = this.rules
            .GroupBy(rule => (rule.Card, rule.Property), ThemeRuleKeyComparer.Instance)
            .ToDictionary(group => group.Key, group => group.ToArray(), ThemeRuleKeyComparer.Instance);
    }

    public int Count => rules.Length;
    public IReadOnlyList<ThemeVisualStyleRule> Rules => rules;

    public ThemeCandidateResolution Resolve(
        string visualType,
        string? preset,
        string card,
        string property,
        string? discriminator = null)
    {
        if (!byProperty.TryGetValue((card, property), out var propertyRules))
        {
            return new(ThemeCandidateResolutionStates.NoExplicitRule, []);
        }

        var mappedVisualType = ThemeVisualTypeMap.Resolve(visualType,
            propertyRules.Where(rule => rule.VisualType != "*").Select(rule => rule.VisualType));
        var visualCandidates = propertyRules.Where(rule =>
            rule.VisualType == "*" || mappedVisualType is not null &&
            string.Equals(rule.VisualType, mappedVisualType, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (visualCandidates.Length == 0)
        {
            return new(ThemeCandidateResolutionStates.MappingUnavailable, [],
                $"No proven visual-type mapping from '{visualType}' to the available theme rule types.");
        }

        var requestedPreset = string.IsNullOrWhiteSpace(preset) ? "*" : preset;
        var candidates = visualCandidates.Where(rule =>
                rule.Preset == "*" || string.Equals(rule.Preset, requestedPreset, StringComparison.OrdinalIgnoreCase))
            .Where(rule => discriminator is null || rule.Discriminator is null ||
                string.Equals(rule.Discriminator, discriminator, StringComparison.OrdinalIgnoreCase))
            .OrderBy(rule => rule.SourceOrder)
            .ToArray();
        if (candidates.Length == 0) return new(ThemeCandidateResolutionStates.NoExplicitRule, []);
        if (candidates.Any(rule => rule.ValueKind == ThemeRuleValueKinds.UnsupportedComplex))
            return new(ThemeCandidateResolutionStates.UnsupportedCandidate, candidates);
        return candidates.Length == 1
            ? new(ThemeCandidateResolutionStates.SingleSupportedCandidate, candidates)
            : new(ThemeCandidateResolutionStates.MultipleCandidates, candidates);
    }

    private sealed class ThemeRuleKeyComparer : IEqualityComparer<(string Card, string Property)>
    {
        public static ThemeRuleKeyComparer Instance { get; } = new();
        public bool Equals((string Card, string Property) x, (string Card, string Property) y) =>
            string.Equals(x.Card, y.Card, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Property, y.Property, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string Card, string Property) obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Card), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Property));
    }
}

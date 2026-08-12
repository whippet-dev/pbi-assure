using System.Globalization;
using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirThemeVisualStyleParser
{
    private static readonly HashSet<string> DiscriminatorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$id", "selector", "discriminator",
    };

    public static ThemeRuleIndex Parse(
        JsonElement themeRoot,
        string layer,
        string? sourceReference,
        string? sourcePath)
    {
        if (!themeRoot.TryGetProperty("visualStyles", out var visualStyles) || visualStyles.ValueKind != JsonValueKind.Object)
            return ThemeRuleIndex.Empty;

        var rules = new List<ThemeVisualStyleRule>();
        var order = 0;
        foreach (var visual in visualStyles.EnumerateObject())
        {
            if (visual.Value.ValueKind != JsonValueKind.Object) continue;
            foreach (var preset in visual.Value.EnumerateObject())
            {
                if (preset.Value.ValueKind != JsonValueKind.Object) continue;
                foreach (var card in preset.Value.EnumerateObject())
                {
                    ParseCardInstances(card.Value, layer, sourceReference, sourcePath, visual.Name, preset.Name,
                        card.Name, $"$.visualStyles.{Escape(visual.Name)}.{Escape(preset.Name)}.{Escape(card.Name)}", rules, ref order);
                }
            }
        }

        return new ThemeRuleIndex(rules);
    }

    private static void ParseCardInstances(
        JsonElement cardValue,
        string layer,
        string? sourceReference,
        string? sourcePath,
        string visualType,
        string preset,
        string card,
        string cardPath,
        List<ThemeVisualStyleRule> rules,
        ref int order)
    {
        if (cardValue.ValueKind == JsonValueKind.Array)
        {
            var instanceIndex = 0;
            foreach (var instance in cardValue.EnumerateArray())
            {
                if (instance.ValueKind == JsonValueKind.Object)
                    ParseInstance(instance, layer, sourceReference, sourcePath, visualType, preset, card,
                        $"{cardPath}[{instanceIndex}]", rules, ref order);
                instanceIndex++;
            }
        }
        else if (cardValue.ValueKind == JsonValueKind.Object)
        {
            ParseInstance(cardValue, layer, sourceReference, sourcePath, visualType, preset, card, cardPath, rules, ref order);
        }
    }

    private static void ParseInstance(
        JsonElement instance,
        string layer,
        string? sourceReference,
        string? sourcePath,
        string visualType,
        string preset,
        string card,
        string instancePath,
        List<ThemeVisualStyleRule> rules,
        ref int order)
    {
        var discriminator = instance.EnumerateObject()
            .FirstOrDefault(property => DiscriminatorNames.Contains(property.Name));
        var discriminatorValue = discriminator.Value.ValueKind == JsonValueKind.Undefined
            ? null
            : NormalizeCompact(discriminator.Value);

        foreach (var property in instance.EnumerateObject())
        {
            if (DiscriminatorNames.Contains(property.Name)) continue;
            var (kind, value) = NormalizeValue(property.Value);
            rules.Add(new ThemeVisualStyleRule(
                layer, sourceReference, sourcePath, visualType, preset, card, discriminatorValue,
                property.Name, kind, value, $"{instancePath}.{Escape(property.Name)}", order++));
        }
    }

    private static (string Kind, string? Value) NormalizeValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) =>
                (ThemeRuleValueKinds.NumericLiteral, number.ToString(CultureInfo.InvariantCulture)),
            JsonValueKind.True => (ThemeRuleValueKinds.BooleanLiteral, "true"),
            JsonValueKind.False => (ThemeRuleValueKinds.BooleanLiteral, "false"),
            JsonValueKind.String when IsHexColor(value.GetString()) => (ThemeRuleValueKinds.ColorLiteral, value.GetString()),
            JsonValueKind.String => (ThemeRuleValueKinds.TextLiteral, value.GetString()),
            JsonValueKind.Object when TryNestedSolidColor(value, out var color) => (ThemeRuleValueKinds.ColorLiteral, color),
            JsonValueKind.Object when ContainsThemeReference(value) => (ThemeRuleValueKinds.ThemeReference, NormalizeCompact(value)),
            _ => (ThemeRuleValueKinds.UnsupportedComplex, NormalizeCompact(value)),
        };
    }

    private static bool TryNestedSolidColor(JsonElement value, out string? color)
    {
        color = null;
        if (!value.TryGetProperty("solid", out var solid) || solid.ValueKind != JsonValueKind.Object ||
            !solid.TryGetProperty("color", out var colorValue)) return false;
        if (colorValue.ValueKind == JsonValueKind.String && IsHexColor(colorValue.GetString()))
        {
            color = colorValue.GetString();
            return true;
        }
        if (colorValue.ValueKind == JsonValueKind.Object && colorValue.TryGetProperty("expr", out var expression) &&
            expression.ValueKind == JsonValueKind.Object && expression.TryGetProperty("Literal", out var literal) &&
            literal.ValueKind == JsonValueKind.Object && literal.TryGetProperty("Value", out var literalValue) &&
            literalValue.ValueKind == JsonValueKind.String)
        {
            var candidate = literalValue.GetString()?.Trim('\'');
            if (IsHexColor(candidate))
            {
                color = candidate;
                return true;
            }
        }
        return false;
    }

    private static bool ContainsThemeReference(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (property.Name.Contains("ThemeDataColor", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("theme", StringComparison.OrdinalIgnoreCase) && property.Name.Contains("color", StringComparison.OrdinalIgnoreCase) ||
                    ContainsThemeReference(property.Value)) return true;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray().Any(ContainsThemeReference);
        }
        return false;
    }

    private static string? NormalizeCompact(JsonElement value)
    {
        var text = value.GetRawText();
        return text.Length <= 256 ? text : text[..256];
    }

    private static bool IsHexColor(string? value) => value is not null && value.Length is 4 or 7 or 9 &&
        value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    private static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace(".", "~1", StringComparison.Ordinal);
}

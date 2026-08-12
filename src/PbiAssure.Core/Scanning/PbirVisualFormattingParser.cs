using System.Globalization;
using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirVisualFormattingParser
{
    private static readonly SupportedProperty[] ContainerProperties =
    [
        new("title.fontSize", "Title font size", "fontSize"),
        new("title.fontColor", "Title text colour", "fontColor"),
        new("title.background", "Title background colour", "background"),
    ];

    public static PersistedFormattingObservation[] Parse(
        JsonElement visualRoot,
        IReadOnlyList<VisualFormattingSelectorContext> selectorContexts)
    {
        if (!TryGetObject(visualRoot, "visual", out var visual))
        {
            return CreateMissingContainerObservations()
                .Append(CreateMissing("dataPoint.fill", "Data colour", "$.visual.objects.dataPoint[].properties.fill"))
                .ToArray();
        }

        var result = new List<PersistedFormattingObservation>();
        ParseContainerTitle(visual, selectorContexts, result);
        ParseDataPoints(visual, selectorContexts, result);
        return result.ToArray();
    }

    private static void ParseContainerTitle(
        JsonElement visual,
        IReadOnlyList<VisualFormattingSelectorContext> selectorContexts,
        List<PersistedFormattingObservation> result)
    {
        var instances = ReadInstances(visual, "visualContainerObjects", "title").ToArray();
        foreach (var supported in ContainerProperties)
        {
            var found = false;
            foreach (var instance in instances)
            {
                if (!TryGetObject(instance.Value, "properties", out var properties) ||
                    !properties.TryGetProperty(supported.PropertyName, out var property))
                {
                    continue;
                }

                found = true;
                result.Add(ParseValue(
                    supported.Key,
                    supported.Label,
                    property,
                    $"$.visual.visualContainerObjects.title[{instance.Index}].properties.{supported.PropertyName}",
                    instance.Value,
                    selectorContexts));
            }

            if (!found)
            {
                result.Add(CreateMissing(supported.Key, supported.Label,
                    $"$.visual.visualContainerObjects.title[].properties.{supported.PropertyName}"));
            }
        }
    }

    private static void ParseDataPoints(
        JsonElement visual,
        IReadOnlyList<VisualFormattingSelectorContext> selectorContexts,
        List<PersistedFormattingObservation> result)
    {
        var found = false;
        foreach (var instance in ReadInstances(visual, "objects", "dataPoint"))
        {
            if (!TryGetObject(instance.Value, "properties", out var properties) ||
                !properties.TryGetProperty("fill", out var fill))
            {
                continue;
            }

            found = true;
            result.Add(ParseValue(
                "dataPoint.fill",
                "Data colour",
                fill,
                $"$.visual.objects.dataPoint[{instance.Index}].properties.fill",
                instance.Value,
                selectorContexts));
        }

        if (!found)
        {
            result.Add(CreateMissing("dataPoint.fill", "Data colour", "$.visual.objects.dataPoint[].properties.fill"));
        }
    }

    private static PersistedFormattingObservation ParseValue(
        string key,
        string label,
        JsonElement property,
        string evidencePath,
        JsonElement instance,
        IReadOnlyList<VisualFormattingSelectorContext> selectorContexts)
    {
        var expression = FindExpression(property);
        var classification = PersistedFormattingClassifications.Unsupported;
        string? normalized = null;
        string? raw = property.GetRawText();
        string? expressionKind = null;
        string? expressionSource = null;

        if (expression is { Kind: "Literal" } literal)
        {
            classification = PersistedFormattingClassifications.PersistedLiteral;
            expressionKind = literal.Kind;
            raw = GetString(literal.Value, "Value") ?? literal.Value.GetRawText();
            normalized = NormalizeLiteral(raw);
        }
        else if (expression is { Kind: "ThemeDataColor" } theme)
        {
            classification = PersistedFormattingClassifications.ThemeReference;
            expressionKind = theme.Kind;
            var colorId = GetInteger(theme.Value, "ColorId");
            var percent = GetDouble(theme.Value, "Percent");
            normalized = $"ColorId {colorId?.ToString(CultureInfo.InvariantCulture) ?? "?"}, Percent {percent?.ToString(CultureInfo.InvariantCulture) ?? "?"}";
        }
        else if (expression is { Kind: "Measure" or "Column" or "Conditional" } dynamic)
        {
            classification = PersistedFormattingClassifications.DynamicExpression;
            expressionKind = dynamic.Kind;
            expressionSource = ReadSemanticSource(dynamic.Value);
            normalized = expressionSource;
        }

        instance.TryGetProperty("selector", out var selector);
        var selectorKind = DetermineSelectorKind(selector);
        var isScoped = selector.ValueKind == JsonValueKind.Object;
        var selectorScope = isScoped ? DescribeSelector(selector, selectorKind) : null;
        var instancePath = evidencePath[..evidencePath.IndexOf(".properties.", StringComparison.Ordinal)];
        var selectorContext = selectorContexts.FirstOrDefault(context =>
            string.Equals(context.EvidencePath, instancePath + ".selector", StringComparison.Ordinal));
        var relevance = selectorContext?.ReferenceRelevance;
        var ambiguous = isScoped && (selectorContext is null || relevance == VisualReferenceRelevance.Ambiguous);
        var include = relevance != VisualReferenceRelevance.HighConfidencePersisted;

        return new PersistedFormattingObservation(
            key, label, classification, normalized, raw, evidencePath,
            isScoped, selectorKind, selectorScope, relevance, expressionKind, expressionSource,
            include, ambiguous);
    }

    private static ExpressionValue? FindExpression(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("expr", out var expr) && expr.ValueKind == JsonValueKind.Object)
            {
                foreach (var kind in new[] { "Literal", "ThemeDataColor", "Measure", "Column", "Conditional" })
                {
                    if (expr.TryGetProperty(kind, out var value))
                    {
                        return new ExpressionValue(kind, value);
                    }
                }

                return new ExpressionValue("Unsupported", expr);
            }

            foreach (var property in element.EnumerateObject())
            {
                if (FindExpression(property.Value) is { } nested)
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (FindExpression(item) is { } nested)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static string? ReadSemanticSource(JsonElement expression)
    {
        var entity = FindStringProperty(expression, "Entity");
        var property = FindStringProperty(expression, "Property");
        return entity is not null && property is not null ? $"{entity}[{property}]" : property ?? entity;
    }

    private static string DescribeSelector(JsonElement selector, string? kind)
    {
        if (kind == VisualSelectorKinds.Wildcard)
        {
            return "Changing data members";
        }

        if (kind == VisualSelectorKinds.ScopeId)
        {
            var entity = FindStringProperty(selector, "Entity");
            var property = FindStringProperty(selector, "Property");
            var value = FindStringProperty(selector, "Value");
            if (property is not null && value is not null)
            {
                return $"{(entity is null ? string.Empty : entity + "[")}{property}{(entity is null ? string.Empty : "]")} = {NormalizeLiteral(value)}";
            }
        }

        return kind ?? VisualSelectorKinds.Unknown;
    }

    private static string? DetermineSelectorKind(JsonElement selector)
    {
        if (selector.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (ContainsProperty(selector, "scopeId")) return VisualSelectorKinds.ScopeId;
        if (FindStringProperty(selector, "metadata") is not null) return VisualSelectorKinds.Metadata;
        if (ContainsProperty(selector, "dataViewWildcard")) return VisualSelectorKinds.Wildcard;
        if (ContainsProperty(selector, "total")) return VisualSelectorKinds.Total;
        if (ContainsProperty(selector, "id")) return VisualSelectorKinds.Id;
        return VisualSelectorKinds.Unknown;
    }

    private static string NormalizeLiteral(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim();
        if (normalized.Length >= 2 && normalized[0] == '\'' && normalized[^1] == '\'')
        {
            normalized = normalized[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        if (normalized.EndsWith('D') &&
            double.TryParse(normalized[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return number.ToString("0.################", CultureInfo.InvariantCulture);
        }

        return normalized;
    }

    private static IEnumerable<(JsonElement Value, int Index)> ReadInstances(JsonElement visual, string collection, string card)
    {
        if (!TryGetObject(visual, collection, out var objects) ||
            !objects.TryGetProperty(card, out var instances) || instances.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var index = 0;
        foreach (var instance in instances.EnumerateArray())
        {
            if (instance.ValueKind == JsonValueKind.Object) yield return (instance, index);
            index++;
        }
    }

    private static IEnumerable<PersistedFormattingObservation> CreateMissingContainerObservations() =>
        ContainerProperties.Select(item => CreateMissing(item.Key, item.Label,
            $"$.visual.visualContainerObjects.title[].properties.{item.PropertyName}"));

    private static PersistedFormattingObservation CreateMissing(string key, string label, string evidencePath) =>
        new(key, label, PersistedFormattingClassifications.NoPersistedValue, null, null, evidencePath,
            false, null, null, null, null, null, true, false);

    private static bool TryGetObject(JsonElement parent, string name, out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object) return true;
        value = default;
        return false;
    }

    private static bool ContainsProperty(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) || ContainsProperty(property.Value, name)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) if (ContainsProperty(item, name)) return true;
        }
        return false;
    }

    private static string? FindStringProperty(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (GetString(element, name) is { } direct) return direct;
            foreach (var property in element.EnumerateObject()) if (FindStringProperty(property.Value, name) is { } nested) return nested;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) if (FindStringProperty(item, name) is { } nested) return nested;
        }
        return null;
    }

    private static string? GetString(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int? GetInteger(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
    private static double? GetDouble(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? number : null;

    private sealed record SupportedProperty(string Key, string Label, string PropertyName);
    private sealed record ExpressionValue(string Kind, JsonElement Value);
}

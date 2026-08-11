using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirVisualReferenceClassifier
{
    public static VisualReferenceClassificationResult Classify(
        JsonElement visualRoot,
        IReadOnlyList<VisualFieldReference> references)
    {
        if (!TryGetObject(visualRoot, "visual", out var visual))
        {
            return new VisualReferenceClassificationResult(references.ToArray(), []);
        }

        var bindingIndex = ReadBindingIndex(visual, references);
        var formattingItems = ReadFormattingItems(visual, references);
        var classifiedReferences = references
            .Select(reference => ClassifyReference(reference, references, bindingIndex, formattingItems))
            .ToArray();
        var selectors = formattingItems
            .Where(item => item.Selector.ValueKind == JsonValueKind.Object)
            .Select(item => ClassifySelector(item, classifiedReferences, bindingIndex))
            .ToArray();

        return new VisualReferenceClassificationResult(classifiedReferences, selectors);
    }

    private static VisualFieldReference ClassifyReference(
        VisualFieldReference reference,
        IReadOnlyList<VisualFieldReference> allReferences,
        VisualBindingIndex bindingIndex,
        IReadOnlyList<FormattingItem> formattingItems)
    {
        if (reference.ReferenceOrigin != VisualReferenceOrigins.FormattingSelectorIdentity)
        {
            return reference;
        }

        var item = formattingItems.FirstOrDefault(candidate =>
            reference.EvidencePath.StartsWith(candidate.EvidencePath + ".selector.", StringComparison.Ordinal));
        if (item is null)
        {
            return reference with { ReferenceRelevance = VisualReferenceRelevance.Ambiguous };
        }

        if (reference.SelectorKind == VisualSelectorKinds.ScopeId)
        {
            var identity = FieldIdentity.Create(reference);
            if (bindingIndex.ActiveBindingIdentities.Contains(identity))
            {
                return reference with
                {
                    ReferenceRelevance = VisualReferenceRelevance.Active,
                    FormattingProperty = item.FormattingProperty,
                    MatchedProjectionQueryRef = bindingIndex.QueryReferenceByIdentity.GetValueOrDefault(identity),
                };
            }

            var selectorHasCurrentBinding = allReferences.Any(candidate =>
                candidate.ReferenceOrigin == VisualReferenceOrigins.FormattingSelectorIdentity &&
                candidate.EvidencePath.StartsWith(item.EvidencePath + ".selector.", StringComparison.Ordinal) &&
                bindingIndex.ActiveBindingIdentities.Contains(FieldIdentity.Create(candidate)));

            return reference with
            {
                ReferenceRelevance = !selectorHasCurrentBinding &&
                                     item.HasPassiveProperties &&
                                     !item.HasPropertySemanticReferences
                    ? VisualReferenceRelevance.HighConfidencePersisted
                    : VisualReferenceRelevance.Ambiguous,
                FormattingProperty = item.FormattingProperty,
            };
        }

        return reference with
        {
            ReferenceRelevance = VisualReferenceRelevance.Ambiguous,
            FormattingProperty = item.FormattingProperty,
        };
    }

    private static VisualFormattingSelectorContext ClassifySelector(
        FormattingItem item,
        IReadOnlyList<VisualFieldReference> references,
        VisualBindingIndex bindingIndex)
    {
        var selectorKind = DetermineSelectorKind(item.Selector);
        var metadata = GetString(item.Selector, "metadata");
        var matchedQueryRef = metadata is not null && bindingIndex.QueryReferences.Contains(metadata)
            ? metadata
            : null;
        var selectorReferences = references.Where(reference =>
            reference.EvidencePath.StartsWith(item.EvidencePath + ".selector.", StringComparison.Ordinal)).ToArray();
        matchedQueryRef ??= selectorReferences
            .Select(reference => reference.MatchedProjectionQueryRef)
            .FirstOrDefault(value => value is not null);

        string relevance;
        if (matchedQueryRef is not null)
        {
            relevance = VisualReferenceRelevance.Active;
        }
        else if (selectorKind == VisualSelectorKinds.ScopeId &&
                 selectorReferences.Any(reference => reference.ReferenceRelevance == VisualReferenceRelevance.Active))
        {
            relevance = VisualReferenceRelevance.Active;
        }
        else if (selectorKind == VisualSelectorKinds.ScopeId &&
                 selectorReferences.Length > 0 &&
                 selectorReferences.All(reference =>
                     reference.ReferenceRelevance == VisualReferenceRelevance.HighConfidencePersisted))
        {
            relevance = VisualReferenceRelevance.HighConfidencePersisted;
        }
        else if (selectorKind == VisualSelectorKinds.Metadata &&
                 metadata is not null &&
                 item.HasPassiveProperties &&
                 !item.HasPropertySemanticReferences)
        {
            relevance = VisualReferenceRelevance.HighConfidencePersisted;
        }
        else if (item.HasPropertySemanticReferences)
        {
            relevance = VisualReferenceRelevance.Active;
        }
        else
        {
            relevance = VisualReferenceRelevance.Ambiguous;
        }

        return new VisualFormattingSelectorContext(
            item.FormattingObject,
            item.FormattingProperty,
            selectorKind,
            relevance,
            metadata,
            matchedQueryRef,
            item.EvidencePath + ".selector");
    }

    private static VisualBindingIndex ReadBindingIndex(
        JsonElement visual,
        IReadOnlyList<VisualFieldReference> references)
    {
        var identities = references
            .Where(reference =>
                reference.ReferenceOrigin == VisualReferenceOrigins.Binding &&
                reference.UsageContext is UsageContexts.Projection or UsageContexts.Sort)
            .Select(FieldIdentity.Create)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var queryReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queryReferenceByIdentity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (TryGetObject(visual, "query", out var query) &&
            TryGetObject(query, "queryState", out var queryState))
        {
            foreach (var role in queryState.EnumerateObject())
            {
                if (!TryGetArray(role.Value, "projections", out var projections))
                {
                    continue;
                }

                var projectionIndex = 0;
                foreach (var projection in projections.EnumerateArray())
                {
                    AddString(projection, "queryRef", queryReferences);
                    AddString(projection, "nativeQueryRef", queryReferences);
                    if (GetString(projection, "queryRef") is { } queryReference)
                    {
                        var evidencePrefix = $"$.visual.query.queryState.{role.Name}.projections[{projectionIndex}].";
                        foreach (var reference in references.Where(reference =>
                                     reference.EvidencePath.StartsWith(evidencePrefix, StringComparison.Ordinal)))
                        {
                            queryReferenceByIdentity.TryAdd(FieldIdentity.Create(reference), queryReference);
                        }
                    }

                    projectionIndex++;
                }
            }
        }

        return new VisualBindingIndex(identities, queryReferences, queryReferenceByIdentity);
    }

    private static FormattingItem[] ReadFormattingItems(
        JsonElement visual,
        IReadOnlyList<VisualFieldReference> references)
    {
        var items = new List<FormattingItem>();
        AddFormattingItems(visual, "objects", "$.visual.objects", references, items);
        AddFormattingItems(visual, "visualContainerObjects", "$.visual.visualContainerObjects", references, items);
        return items.ToArray();
    }

    private static void AddFormattingItems(
        JsonElement visual,
        string collectionName,
        string collectionPath,
        IReadOnlyList<VisualFieldReference> references,
        ICollection<FormattingItem> result)
    {
        if (!TryGetObject(visual, collectionName, out var collection))
        {
            return;
        }

        foreach (var formattingObject in collection.EnumerateObject())
        {
            if (formattingObject.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var index = 0;
            foreach (var item in formattingObject.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    index++;
                    continue;
                }

                var evidencePath = $"{collectionPath}.{formattingObject.Name}[{index}]";
                item.TryGetProperty("selector", out var selector);
                item.TryGetProperty("properties", out var properties);
                var propertyNames = properties.ValueKind == JsonValueKind.Object
                    ? properties.EnumerateObject().Select(property => property.Name).ToArray()
                    : [];
                var hasPropertySemanticReferences = references.Any(reference =>
                    reference.ReferenceOrigin == VisualReferenceOrigins.FormattingPropertyExpression &&
                    reference.EvidencePath.StartsWith(evidencePath + ".properties.", StringComparison.Ordinal));

                result.Add(new FormattingItem(
                    formattingObject.Name,
                    propertyNames.Length == 0 ? null : string.Join(", ", propertyNames),
                    evidencePath,
                    selector,
                    HasPassiveProperties(properties),
                    hasPropertySemanticReferences));
                index++;
            }
        }
    }

    private static bool HasPassiveProperties(JsonElement properties)
    {
        if (properties.ValueKind != JsonValueKind.Object || !properties.EnumerateObject().Any())
        {
            return false;
        }

        return ContainsOnlyPassiveExpressions(properties);
    }

    private static bool ContainsOnlyPassiveExpressions(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, "expr", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.Object &&
                    property.Value.EnumerateObject().Any(expression =>
                        expression.Name is not "Literal" and not "ThemeDataColor"))
                {
                    return false;
                }

                if (!ContainsOnlyPassiveExpressions(property.Value))
                {
                    return false;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (!ContainsOnlyPassiveExpressions(item))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string DetermineSelectorKind(JsonElement selector)
    {
        if (ContainsProperty(selector, "scopeId"))
        {
            return VisualSelectorKinds.ScopeId;
        }

        if (GetString(selector, "metadata") is not null)
        {
            return VisualSelectorKinds.Metadata;
        }

        if (ContainsProperty(selector, "dataViewWildcard"))
        {
            return VisualSelectorKinds.Wildcard;
        }

        if (ContainsProperty(selector, "total"))
        {
            return VisualSelectorKinds.Total;
        }

        if (ContainsProperty(selector, "id"))
        {
            return VisualSelectorKinds.Id;
        }

        return VisualSelectorKinds.Unknown;
    }

    private static bool ContainsProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) ||
                    ContainsProperty(property.Value, propertyName))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsProperty(item, propertyName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AddString(JsonElement parent, string propertyName, HashSet<string> values)
    {
        if (GetString(parent, propertyName) is { } value)
        {
            values.Add(value);
        }
    }

    private static string? GetString(JsonElement parent, string propertyName)
    {
        return parent.ValueKind == JsonValueKind.Object &&
               parent.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool TryGetObject(JsonElement parent, string propertyName, out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetArray(JsonElement parent, string propertyName, out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out value) &&
            value.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        value = default;
        return false;
    }

    private sealed record VisualBindingIndex(
        HashSet<string> ActiveBindingIdentities,
        HashSet<string> QueryReferences,
        Dictionary<string, string> QueryReferenceByIdentity);

    private sealed record FormattingItem(
        string FormattingObject,
        string? FormattingProperty,
        string EvidencePath,
        JsonElement Selector,
        bool HasPassiveProperties,
        bool HasPropertySemanticReferences);
}

internal sealed record VisualReferenceClassificationResult(
    VisualFieldReference[] References,
    VisualFormattingSelectorContext[] Selectors);

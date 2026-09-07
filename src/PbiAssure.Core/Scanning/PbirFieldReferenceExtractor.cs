using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirFieldReferenceExtractor
{
    public static VisualFieldReference[] Extract(JsonElement root, Action<string, string>? onUnresolvedAlias = null)
    {
        var references = new List<VisualFieldReference>();
        var sourceAliases = new Dictionary<string, SourceAlias>(StringComparer.Ordinal);
        Visit(root, "$", [], sourceAliases, references, isHiddenProjection: false, onUnresolvedAlias);

        return references
            .Distinct()
            .OrderBy(reference => reference.Table, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.ObjectType, StringComparer.Ordinal)
            .ThenBy(reference => reference.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.EvidencePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Visit(
        JsonElement element,
        string path,
        IReadOnlyList<string> ancestors,
        IReadOnlyDictionary<string, SourceAlias> sourceAliases,
        ICollection<VisualFieldReference> references,
        bool isHiddenProjection,
        Action<string, string>? onUnresolvedAlias)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            // Each semantic query/filter owns its direct From declarations. Nested queries
            // replace the environment, including when their From is missing or malformed.
            if (OwnsQueryScope(element))
            {
                sourceAliases = ReadSourceAliases(element);
            }
            var descendantIsHiddenProjection = isHiddenProjection || IsHiddenRoleProjection(element, ancestors);
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = $"{path}.{property.Name}";
                var nextAncestors = Append(ancestors, property.Name);
                if (property.Name == "SourceRef" &&
                    GetString(property.Value, "Entity") is null &&
                    GetString(property.Value, "Source") is { } alias &&
                    !IsResolved(sourceAliases, alias))
                {
                    onUnresolvedAlias?.Invoke(propertyPath, alias);
                }

                if (TryReadReference(
                        property.Name,
                        property.Value,
                        propertyPath,
                        nextAncestors,
                        sourceAliases,
                        descendantIsHiddenProjection,
                        out var reference))
                {
                    references.Add(reference);
                }

                Visit(property.Value, propertyPath, nextAncestors, sourceAliases, references,
                    descendantIsHiddenProjection, onUnresolvedAlias);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            Visit(item, $"{path}[{index}]", ancestors, sourceAliases, references, isHiddenProjection, onUnresolvedAlias);
            index++;
        }
    }

    private static bool TryReadReference(
        string expressionKind,
        JsonElement expression,
        string evidencePath,
        IReadOnlyList<string> ancestors,
        IReadOnlyDictionary<string, SourceAlias> sourceAliases,
        bool isHiddenProjection,
        out VisualFieldReference reference)
    {
        reference = null!;
        if (expression.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (expressionKind is SemanticObjectTypes.Column or SemanticObjectTypes.Measure)
        {
            var table = FindTable(expression, sourceAliases);
            var objectName = GetString(expression, "Property");
            if (table is null || objectName is null)
            {
                return false;
            }

            reference = CreateReference(
                table,
                objectName,
                expressionKind,
                hierarchyName: null,
                evidencePath,
                ancestors,
                isHiddenProjection);
            return true;
        }

        if (expressionKind != SemanticObjectTypes.HierarchyLevel)
        {
            return false;
        }

        if (TryReadPropertyVariationSource(expression, sourceAliases, out var variationTable, out var variationColumn))
        {
            reference = CreateReference(
                variationTable,
                variationColumn,
                SemanticObjectTypes.Column,
                hierarchyName: null,
                evidencePath,
                ancestors,
                isHiddenProjection);
            return true;
        }

        var hierarchyTable = FindTable(expression, sourceAliases);
        var levelName = GetString(expression, "Level");
        var hierarchyName = FindStringProperty(expression, "Hierarchy");
        if (hierarchyTable is null || levelName is null)
        {
            return false;
        }

        reference = CreateReference(
            hierarchyTable,
            levelName,
            SemanticObjectTypes.HierarchyLevel,
            hierarchyName,
            evidencePath,
            ancestors,
            isHiddenProjection);
        return true;
    }

    private static VisualFieldReference CreateReference(
        string table,
        string objectName,
        string objectType,
        string? hierarchyName,
        string evidencePath,
        IReadOnlyList<string> ancestors,
        bool isHiddenProjection)
    {
        var origin = DetermineReferenceOrigin(ancestors);
        return new VisualFieldReference(
            Table: table,
            ObjectName: objectName,
            ObjectType: objectType,
            HierarchyName: hierarchyName,
            UsageContext: DetermineUsageContext(ancestors),
            Role: DetermineRole(ancestors),
            EvidencePath: evidencePath)
        {
            ReferenceOrigin = origin,
            ReferenceRelevance = origin is VisualReferenceOrigins.Binding or VisualReferenceOrigins.FormattingPropertyExpression
                ? VisualReferenceRelevance.Active
                : VisualReferenceRelevance.Ambiguous,
            FormattingObject = FindFormattingSegment(ancestors, offset: 1),
            FormattingProperty = FindFormattingProperty(ancestors),
            SelectorKind = origin == VisualReferenceOrigins.FormattingSelectorIdentity
                ? DetermineSelectorKind(ancestors)
                : null,
            IsHiddenProjection = isHiddenProjection,
        };
    }

    private static bool IsHiddenRoleProjection(JsonElement element, IReadOnlyList<string> ancestors) =>
        ancestors.Count > 0 &&
        string.Equals(ancestors[^1], "projections", StringComparison.OrdinalIgnoreCase) &&
        IndexOf(ancestors, "queryState") >= 0 &&
        element.TryGetProperty("hidden", out var hidden) &&
        hidden.ValueKind == JsonValueKind.True;

    private static string DetermineReferenceOrigin(IReadOnlyList<string> ancestors)
    {
        var formattingIndex = FindFormattingIndex(ancestors);
        if (formattingIndex < 0)
        {
            return VisualReferenceOrigins.Binding;
        }

        var selectorIndex = IndexOfAfter(ancestors, "selector", formattingIndex);
        if (selectorIndex >= 0)
        {
            return VisualReferenceOrigins.FormattingSelectorIdentity;
        }

        return IndexOfAfter(ancestors, "properties", formattingIndex) >= 0
            ? VisualReferenceOrigins.FormattingPropertyExpression
            : VisualReferenceOrigins.Unknown;
    }

    private static string? FindFormattingSegment(IReadOnlyList<string> ancestors, int offset)
    {
        var index = FindFormattingIndex(ancestors);
        return index >= 0 && index + offset < ancestors.Count
            ? ancestors[index + offset]
            : null;
    }

    private static string? FindFormattingProperty(IReadOnlyList<string> ancestors)
    {
        var formattingIndex = FindFormattingIndex(ancestors);
        var propertiesIndex = IndexOfAfter(ancestors, "properties", formattingIndex);
        return propertiesIndex >= 0 && propertiesIndex + 1 < ancestors.Count
            ? ancestors[propertiesIndex + 1]
            : null;
    }

    private static string DetermineSelectorKind(IReadOnlyList<string> ancestors)
    {
        var selectorIndex = IndexOf(ancestors, "selector");
        if (IndexOfAfter(ancestors, "scopeId", selectorIndex) >= 0)
        {
            return VisualSelectorKinds.ScopeId;
        }

        if (IndexOfAfter(ancestors, "metadata", selectorIndex) >= 0)
        {
            return VisualSelectorKinds.Metadata;
        }

        if (IndexOfAfter(ancestors, "dataViewWildcard", selectorIndex) >= 0)
        {
            return VisualSelectorKinds.Wildcard;
        }

        if (IndexOfAfter(ancestors, "total", selectorIndex) >= 0)
        {
            return VisualSelectorKinds.Total;
        }

        if (IndexOfAfter(ancestors, "id", selectorIndex) >= 0)
        {
            return VisualSelectorKinds.Id;
        }

        return VisualSelectorKinds.Unknown;
    }

    private static int FindFormattingIndex(IReadOnlyList<string> ancestors)
    {
        var objectsIndex = IndexOf(ancestors, "objects");
        var containerObjectsIndex = IndexOf(ancestors, "visualContainerObjects");
        if (objectsIndex < 0)
        {
            return containerObjectsIndex;
        }

        return containerObjectsIndex < 0
            ? objectsIndex
            : Math.Min(objectsIndex, containerObjectsIndex);
    }

    private static int IndexOfAfter(IReadOnlyList<string> values, string expected, int startIndex)
    {
        for (var index = Math.Max(0, startIndex + 1); index < values.Count; index++)
        {
            if (string.Equals(values[index], expected, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string DetermineUsageContext(IReadOnlyList<string> ancestors)
    {
        if (IndexOf(ancestors, "pageBinding") >= 0)
        {
            return UsageContexts.Drillthrough;
        }

        if (ancestors.Any(segment => segment.Contains("filter", StringComparison.OrdinalIgnoreCase)))
        {
            return UsageContexts.Filter;
        }

        if (ancestors.Any(segment => segment.Contains("sort", StringComparison.OrdinalIgnoreCase)))
        {
            return UsageContexts.Sort;
        }

        if (IndexOf(ancestors, "queryState") >= 0)
        {
            return UsageContexts.Projection;
        }

        if (IndexOf(ancestors, "objects") >= 0 || IndexOf(ancestors, "visualContainerObjects") >= 0)
        {
            return UsageContexts.Formatting;
        }

        return UsageContexts.Other;
    }

    private static string? DetermineRole(IReadOnlyList<string> ancestors)
    {
        if (IndexOf(ancestors, "pageBinding") >= 0)
        {
            return "drillthrough";
        }

        if (ancestors.Any(segment => segment.Contains("conditional", StringComparison.OrdinalIgnoreCase)))
        {
            return "conditionalFormatting";
        }

        if (ancestors.Any(segment => segment.Contains("tooltip", StringComparison.OrdinalIgnoreCase)))
        {
            return "tooltips";
        }

        if (ancestors.Any(segment => segment.Contains("filter", StringComparison.OrdinalIgnoreCase)))
        {
            return "filter";
        }

        var queryStateIndex = IndexOf(ancestors, "queryState");
        return queryStateIndex >= 0 && queryStateIndex + 1 < ancestors.Count
            ? ancestors[queryStateIndex + 1]
            : null;
    }

    private static int IndexOf(IReadOnlyList<string> values, string expected)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], expected, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string? FindTable(
        JsonElement element,
        IReadOnlyDictionary<string, SourceAlias> sourceAliases)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("SourceRef", out var sourceReference) &&
                sourceReference.ValueKind == JsonValueKind.Object)
            {
                if (GetString(sourceReference, "Entity") is { } entity)
                {
                    return entity;
                }

                if (GetString(sourceReference, "Source") is { } alias &&
                    sourceAliases.TryGetValue(alias, out var source) &&
                    !source.IsDerived && source.Entities.Count == 1)
                {
                    return source.Entities.Single();
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                if (FindTable(property.Value, sourceAliases) is { } nestedEntity)
                {
                    return nestedEntity;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (FindTable(item, sourceAliases) is { } nestedEntity)
                {
                    return nestedEntity;
                }
            }
        }

        return null;
    }

    private static bool TryReadPropertyVariationSource(
        JsonElement element,
        IReadOnlyDictionary<string, SourceAlias> sourceAliases,
        out string table,
        out string column)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("PropertyVariationSource", out var variationSource) &&
                variationSource.ValueKind == JsonValueKind.Object &&
                FindTable(variationSource, sourceAliases) is { } variationTable &&
                GetString(variationSource, "Property") is { } variationColumn)
            {
                table = variationTable;
                column = variationColumn;
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (TryReadPropertyVariationSource(property.Value, sourceAliases, out table, out column))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryReadPropertyVariationSource(item, sourceAliases, out table, out column))
                {
                    return true;
                }
            }
        }

        table = string.Empty;
        column = string.Empty;
        return false;
    }

    private static Dictionary<string, SourceAlias> ReadSourceAliases(JsonElement root)
    {
        var aliases = new Dictionary<string, SourceAlias>(StringComparer.Ordinal);
        CollectSourceAliases(root, aliases);
        return aliases;
    }

    /// <summary>
    /// Whether a <c>SourceRef</c> naming this alias points at something this scope declared. A derived
    /// table counts as declared even though it names no model entity: the subquery producing it is
    /// persisted beside it, and its own field references are read from its nested scope.
    ///
    /// An alias declared both ways in one scope resolves neither way. Which meaning applies is not
    /// recoverable from the metadata, so the existing unresolved treatment is kept.
    /// </summary>
    private static bool IsResolved(IReadOnlyDictionary<string, SourceAlias> aliases, string alias) =>
        aliases.TryGetValue(alias, out var source) &&
        (source.IsDerived ? source.Entities.Count == 0 : source.Entities.Count == 1);

    private static bool OwnsQueryScope(JsonElement element) => element.EnumerateObject().Any(property =>
        property.Name.Equals("From", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Equals("Select", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Equals("Where", StringComparison.OrdinalIgnoreCase));

    private static void CollectSourceAliases(
        JsonElement element,
        IDictionary<string, SourceAlias> aliases)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, "From", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var source in property.Value.EnumerateArray())
                    {
                        var alias = GetString(source, "Name") ?? GetString(source, "name");
                        var entity = GetString(source, "Entity") ?? GetString(source, "entity");
                        if (alias is null)
                        {
                            continue;
                        }

                        if (!aliases.TryGetValue(alias, out var declared))
                        {
                            declared = new SourceAlias(new HashSet<string>(StringComparer.Ordinal));
                            aliases.Add(alias, declared);
                        }

                        // A source with no Entity is recognised only when it declares the subquery that
                        // produces it. Anything else with no Entity stays the existing unknown marker.
                        if (entity is null && DeclaresSubquery(source))
                        {
                            declared.IsDerived = true;
                            continue;
                        }

                        declared.Entities.Add(entity ?? string.Empty);
                    }
                }
            }
            // A missing Entity cannot borrow a duplicate declaration's valid target.
            foreach (var declared in aliases.Values.Where(item => item.Entities.Contains(string.Empty)))
            {
                declared.Entities.Clear();
            }
        }
    }

    /// <summary>
    /// A From entry whose source is a derived table persists it as <c>Expression.Subquery.Query</c>
    /// beside the alias. Only that exact shape is recognised. The subquery is not read here: the
    /// ordinary walk already visits it as a scope of its own, which is where its model-field
    /// references come from.
    /// </summary>
    private static bool DeclaresSubquery(JsonElement source) =>
        source.ValueKind == JsonValueKind.Object &&
        source.TryGetProperty("Expression", out var expression) &&
        expression.ValueKind == JsonValueKind.Object &&
        expression.TryGetProperty("Subquery", out var subquery) &&
        subquery.ValueKind == JsonValueKind.Object &&
        subquery.TryGetProperty("Query", out var query) &&
        query.ValueKind == JsonValueKind.Object;

    /// <summary>What one alias was declared as within a single query scope.</summary>
    private sealed record SourceAlias(HashSet<string> Entities)
    {
        /// <summary>Declared as a derived table, and so naming no model entity.</summary>
        public bool IsDerived { get; set; }
    }

    private static string? FindStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (GetString(element, propertyName) is { } value)
            {
                return value;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (FindStringProperty(property.Value, propertyName) is { } nestedValue)
                {
                    return nestedValue;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (FindStringProperty(item, propertyName) is { } nestedValue)
                {
                    return nestedValue;
                }
            }
        }

        return null;
    }

    private static string? GetString(JsonElement parent, string propertyName)
    {
        return parent.ValueKind == JsonValueKind.Object &&
               parent.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string[] Append(IReadOnlyList<string> ancestors, string value)
    {
        var result = new string[ancestors.Count + 1];
        for (var index = 0; index < ancestors.Count; index++)
        {
            result[index] = ancestors[index];
        }

        result[^1] = value;
        return result;
    }
}

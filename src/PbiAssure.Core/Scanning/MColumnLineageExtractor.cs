using System.Text.RegularExpressions;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static partial class MColumnLineageExtractor
{
    public static MColumnReference[] Extract(
        string expression,
        string consumerQuery,
        IReadOnlyCollection<string> knownQueryNames)
    {
        var known = knownQueryNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var states = new Dictionary<string, StepState>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MColumnReference>();

        foreach (var binding in ReadBindings(expression))
        {
            var state = new StepState();
            var directIdentifier = ReadIdentifier(binding.Expression);
            if (directIdentifier is not null && known.Contains(directIdentifier))
            {
                state.DirectQuery = directIdentifier;
                states[binding.Name] = state;
                continue;
            }

            if (!TryReadCall(binding.Expression, out var function, out var arguments) || arguments.Length == 0)
            {
                states[binding.Name] = state;
                continue;
            }

            InheritState(arguments[0], states, state);
            if (function.Equals("Table.NestedJoin", StringComparison.OrdinalIgnoreCase) && arguments.Length >= 5)
            {
                AddResolvedColumns(references, arguments[0], ReadStringList(arguments[1]), states, known,
                    consumerQuery, PowerQueryColumnUsageKinds.MergeKey, function, binding.Name);
                var rightQuery = ResolveQuery(arguments[2], states, known);
                AddResolvedColumns(references, arguments[2], ReadStringList(arguments[3]), states, known,
                    consumerQuery, PowerQueryColumnUsageKinds.MergeKey, function, binding.Name);
                if (rightQuery is not null && ReadString(arguments[4]) is { } nestedColumn)
                {
                    state.NestedQueries[nestedColumn] = rightQuery;
                }
            }
            else if (function.Equals("Table.Join", StringComparison.OrdinalIgnoreCase) && arguments.Length >= 4)
            {
                AddResolvedColumns(references, arguments[0], ReadStringList(arguments[1]), states, known,
                    consumerQuery, PowerQueryColumnUsageKinds.MergeKey, function, binding.Name);
                AddResolvedColumns(references, arguments[2], ReadStringList(arguments[3]), states, known,
                    consumerQuery, PowerQueryColumnUsageKinds.MergeKey, function, binding.Name);
            }
            else if (function.Equals("Table.ExpandTableColumn", StringComparison.OrdinalIgnoreCase) && arguments.Length >= 3)
            {
                var nestedColumn = ReadString(arguments[1]);
                if (nestedColumn is not null && state.NestedQueries.TryGetValue(nestedColumn, out var sourceQuery))
                {
                    AddColumns(references, sourceQuery, ReadStringList(arguments[2]),
                        consumerQuery, PowerQueryColumnUsageKinds.ExpandedColumn, function, binding.Name);
                    state.NestedQueries.Remove(nestedColumn);
                }
            }
            else if (function.Equals("Table.SelectColumns", StringComparison.OrdinalIgnoreCase) && arguments.Length >= 2)
            {
                AddResolvedColumns(references, arguments[0], ReadStringList(arguments[1]), states, known,
                    consumerQuery, PowerQueryColumnUsageKinds.SelectedColumn, function, binding.Name);
            }
            else if (function.Equals("Table.RemoveColumns", StringComparison.OrdinalIgnoreCase) && arguments.Length >= 2)
            {
                AddResolvedColumns(references, arguments[0], ReadStringList(arguments[1]), states, known,
                    consumerQuery, PowerQueryColumnUsageKinds.RemovedColumn, function, binding.Name);
            }
            else if (function.Equals("Table.RenameColumns", StringComparison.OrdinalIgnoreCase) && arguments.Length >= 2)
            {
                var pairs = ReadPairs(arguments[1]);
                AddResolvedColumns(references, arguments[0], pairs.Select(pair => pair.From), states, known,
                    consumerQuery, PowerQueryColumnUsageKinds.RenamedColumn, function, binding.Name);
                foreach (var pair in pairs)
                {
                    state.ColumnOrigins[pair.To] = state.ColumnOrigins.TryGetValue(pair.From, out var origin)
                        ? origin
                        : pair.From;
                    state.ColumnOrigins.Remove(pair.From);
                }
            }
            else if (function.Equals("Table.TransformColumnTypes", StringComparison.OrdinalIgnoreCase) && arguments.Length >= 2)
            {
                AddResolvedColumns(references, arguments[0], ReadTupleFirstStrings(arguments[1]), states, known,
                    consumerQuery, PowerQueryColumnUsageKinds.TransformedColumn, function, binding.Name);
            }

            states[binding.Name] = state;
        }

        return references.Distinct().ToArray();
    }

    public static IReadOnlyDictionary<string, string> ReadOutputRenames(string expression)
    {
        var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in ReadBindings(expression))
        {
            if (!TryReadCall(binding.Expression, out var function, out var arguments) ||
                !function.Equals("Table.RenameColumns", StringComparison.OrdinalIgnoreCase) ||
                arguments.Length < 2)
            {
                continue;
            }

            foreach (var pair in ReadPairs(arguments[1]))
            {
                renames[pair.From] = pair.To;
            }
        }

        return renames;
    }

    private static void AddColumns(
        List<MColumnReference> references,
        string? sourceQuery,
        IEnumerable<string> columns,
        string consumerQuery,
        string usageKind,
        string function,
        string stepName)
    {
        if (sourceQuery is null || sourceQuery.Equals(consumerQuery, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var column in columns.Where(column => !string.IsNullOrWhiteSpace(column)))
        {
            references.Add(new MColumnReference(sourceQuery, column, usageKind, function, stepName));
        }
    }

    private static void AddResolvedColumns(
        List<MColumnReference> references,
        string tableExpression,
        IEnumerable<string> columns,
        IReadOnlyDictionary<string, StepState> states,
        HashSet<string> known,
        string consumerQuery,
        string usageKind,
        string function,
        string stepName)
    {
        var sourceQuery = ResolveQuery(tableExpression, states, known);
        var identifier = ReadIdentifier(tableExpression);
        var state = identifier is not null && states.TryGetValue(identifier, out var resolvedState)
            ? resolvedState
            : null;
        AddColumns(
            references,
            sourceQuery,
            columns.Select(column => state is not null && state.ColumnOrigins.TryGetValue(column, out var origin)
                ? origin
                : column),
            consumerQuery,
            usageKind,
            function,
            stepName);
    }

    private static void InheritState(string expression, IReadOnlyDictionary<string, StepState> states, StepState target)
    {
        var identifier = ReadIdentifier(expression);
        if (identifier is null || !states.TryGetValue(identifier, out var source))
        {
            return;
        }

        target.DirectQuery = source.DirectQuery;
        foreach (var origin in source.ColumnOrigins)
        {
            target.ColumnOrigins[origin.Key] = origin.Value;
        }
        foreach (var nested in source.NestedQueries)
        {
            target.NestedQueries[nested.Key] = nested.Value;
        }
    }

    private static string? ResolveQuery(
        string expression,
        IReadOnlyDictionary<string, StepState> states,
        HashSet<string> known)
    {
        var identifier = ReadIdentifier(expression);
        if (identifier is null)
        {
            return null;
        }

        if (known.Contains(identifier))
        {
            return identifier;
        }

        return states.TryGetValue(identifier, out var state) ? state.DirectQuery : null;
    }

    private static Binding[] ReadBindings(string expression)
    {
        var matches = BindingRegex().Matches(expression).Cast<Match>().ToArray();
        var bindings = new List<Binding>();
        for (var index = 0; index < matches.Length; index++)
        {
            var match = matches[index];
            var end = index + 1 < matches.Length ? matches[index + 1].Index : expression.Length;
            var value = expression[(match.Index + match.Length)..end].Trim();
            value = value.TrimEnd().TrimEnd(',').TrimEnd();
            if (index == matches.Length - 1)
            {
                var inIndex = value.LastIndexOf("\nin", StringComparison.OrdinalIgnoreCase);
                if (inIndex >= 0)
                {
                    value = value[..inIndex].TrimEnd();
                }
            }

            bindings.Add(new Binding(NormalizeIdentifier(match.Groups[1].Value), value));
        }

        return bindings.ToArray();
    }

    private static bool TryReadCall(string expression, out string function, out string[] arguments)
    {
        var match = CallRegex().Match(expression.Trim());
        if (!match.Success)
        {
            function = string.Empty;
            arguments = [];
            return false;
        }

        function = match.Groups[1].Value;
        var openIndex = expression.IndexOf('(', match.Index + match.Length - 1);
        var closeIndex = FindMatching(expression, openIndex, '(', ')');
        if (openIndex < 0 || closeIndex < 0)
        {
            arguments = [];
            return false;
        }

        arguments = SplitTopLevel(expression[(openIndex + 1)..closeIndex]).ToArray();
        return true;
    }

    private static IEnumerable<string> SplitTopLevel(string value)
    {
        var start = 0;
        var round = 0;
        var curly = 0;
        var square = 0;
        var inQuotedText = false;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '"')
            {
                if (inQuotedText && index + 1 < value.Length && value[index + 1] == '"')
                {
                    index++;
                    continue;
                }
                inQuotedText = !inQuotedText;
                continue;
            }
            if (inQuotedText) continue;
            switch (value[index])
            {
                case '(': round++; break;
                case ')': round--; break;
                case '{': curly++; break;
                case '}': curly--; break;
                case '[': square++; break;
                case ']': square--; break;
                case ',' when round == 0 && curly == 0 && square == 0:
                    yield return value[start..index].Trim();
                    start = index + 1;
                    break;
            }
        }
        yield return value[start..].Trim();
    }

    private static int FindMatching(string value, int start, char opening, char closing)
    {
        if (start < 0) return -1;
        var depth = 0;
        var inQuotedText = false;
        for (var index = start; index < value.Length; index++)
        {
            if (value[index] == '"')
            {
                if (inQuotedText && index + 1 < value.Length && value[index + 1] == '"')
                {
                    index++;
                    continue;
                }
                inQuotedText = !inQuotedText;
                continue;
            }
            if (inQuotedText) continue;
            if (value[index] == opening) depth++;
            if (value[index] == closing && --depth == 0) return index;
        }
        return -1;
    }

    private static string[] ReadStringList(string expression)
    {
        var trimmed = expression.Trim();
        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}')) return [];
        return SplitTopLevel(trimmed[1..^1]).Select(ReadString).OfType<string>().ToArray();
    }

    private static ColumnPair[] ReadPairs(string expression)
    {
        var values = StringRegex().Matches(expression).Select(match => match.Groups[1].Value.Replace("\"\"", "\"")).ToArray();
        return values.Chunk(2).Where(pair => pair.Length == 2).Select(pair => new ColumnPair(pair[0], pair[1])).ToArray();
    }

    private static string[] ReadTupleFirstStrings(string expression)
    {
        var trimmed = expression.Trim();
        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}')) return [];
        return SplitTopLevel(trimmed[1..^1])
            .Select(tuple => tuple.Trim())
            .Where(tuple => tuple.StartsWith('{') && tuple.EndsWith('}'))
            .Select(tuple => SplitTopLevel(tuple[1..^1]).FirstOrDefault())
            .Select(value => value is null ? null : ReadString(value))
            .OfType<string>()
            .ToArray();
    }

    private static string? ReadString(string expression)
    {
        var match = ExactStringRegex().Match(expression.Trim());
        return match.Success ? match.Groups[1].Value.Replace("\"\"", "\"") : null;
    }

    private static string? ReadIdentifier(string expression)
    {
        var match = ExactIdentifierRegex().Match(expression.Trim());
        return match.Success ? NormalizeIdentifier(match.Value) : null;
    }

    private static string NormalizeIdentifier(string value) =>
        value.StartsWith("#\"", StringComparison.Ordinal) && value.EndsWith('"')
            ? value[2..^1].Replace("\"\"", "\"")
            : value;

    [GeneratedRegex("(?m)^\\s*(#\"(?:[^\"]|\"\")*\"|[A-Za-z_][A-Za-z0-9_.]*)\\s*=", RegexOptions.CultureInvariant)]
    private static partial Regex BindingRegex();

    [GeneratedRegex("^([A-Za-z_][A-Za-z0-9_.]*)\\s*\\(", RegexOptions.CultureInvariant)]
    private static partial Regex CallRegex();

    [GeneratedRegex("^#\"(?:[^\"]|\"\")*\"$|^[A-Za-z_][A-Za-z0-9_.]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactIdentifierRegex();

    [GeneratedRegex("^\"((?:\"\"|[^\"])*)\"$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactStringRegex();

    [GeneratedRegex("\"((?:\"\"|[^\"])*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex StringRegex();

    private sealed record Binding(string Name, string Expression);
    private sealed record ColumnPair(string From, string To);
    private sealed class StepState
    {
        public string? DirectQuery { get; set; }
        public Dictionary<string, string> ColumnOrigins { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> NestedQueries { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

internal sealed record MColumnReference(
    string SourceQuery,
    string SourceColumn,
    string UsageKind,
    string MFunction,
    string StepName);

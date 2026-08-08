using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class TmdlSemanticModelParser
{
    private static readonly string[] NamedObjectKeywords = ["column", "measure", "hierarchy", "partition"];

    public static SemanticModelInventory Parse(string rootPath, string semanticModelDirectory)
    {
        var directoryName = Path.GetFileName(semanticModelDirectory);
        var name = directoryName.EndsWith(".SemanticModel", StringComparison.OrdinalIgnoreCase)
            ? directoryName[..^".SemanticModel".Length]
            : directoryName;
        var definitionDirectory = Path.Combine(semanticModelDirectory, "definition");
        var tablesDirectory = Path.Combine(definitionDirectory, "tables");

        var tables = Directory.Exists(tablesDirectory)
            ? Directory
                .EnumerateFiles(tablesDirectory, "*.tmdl", SearchOption.TopDirectoryOnly)
                .Select(path => ParseTable(rootPath, path))
                .OrderBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        var relationshipsPath = Path.Combine(definitionDirectory, "relationships.tmdl");
        var relationships = File.Exists(relationshipsPath)
            ? ParseRelationships(relationshipsPath)
            : [];
        var expressionsPath = Path.Combine(definitionDirectory, "expressions.tmdl");
        var namedExpressions = File.Exists(expressionsPath)
            ? ParseNamedExpressions(rootPath, expressionsPath)
            : [];

        return new SemanticModelInventory(
            Name: name,
            RelativePath: Path.GetRelativePath(rootPath, semanticModelDirectory),
            Tables: tables,
            Relationships: relationships,
            NamedExpressions: namedExpressions);
    }

    private static SemanticTableInventory ParseTable(string rootPath, string path)
    {
        var lines = ReadLines(path);
        var tableDeclarationIndex = FindDeclaration(lines, "table", startIndex: 0, requiredIndent: null);
        if (tableDeclarationIndex < 0 ||
            !TryParseDeclaration(lines[tableDeclarationIndex].Trimmed, "table", out var tableName, out _))
        {
            throw new InvalidDataException($"A TMDL table declaration was not found in: {path}");
        }

        var tableIndent = lines[tableDeclarationIndex].Indent;
        var objectIndent = lines
            .Skip(tableDeclarationIndex + 1)
            .Where(line => line.Indent > tableIndent && IsTableObjectDeclaration(line.Trimmed))
            .Select(line => line.Indent)
            .DefaultIfEmpty(tableIndent + 4)
            .Min();
        var firstObjectIndex = FindFirstObjectDeclaration(lines, tableDeclarationIndex + 1, objectIndent);
        var tablePropertyEnd = firstObjectIndex < 0 ? lines.Length : firstObjectIndex;

        var columns = new List<SemanticColumnInventory>();
        var measures = new List<SemanticMeasureInventory>();
        var hierarchies = new List<SemanticHierarchyInventory>();
        var partitions = new List<SemanticPartitionInventory>();
        SemanticCalculationGroupInventory? calculationGroup = null;

        for (var index = tableDeclarationIndex + 1; index < lines.Length; index++)
        {
            if (lines[index].Indent != objectIndent)
            {
                continue;
            }

            var endIndex = FindBlockEnd(lines, index);
            if (TryParseDeclaration(lines[index].Trimmed, "column", out var columnName, out var columnExpression))
            {
                columns.Add(new SemanticColumnInventory(
                    Name: columnName,
                    DataType: FindProperty(lines, index, endIndex, "dataType"),
                    IsHidden: HasFlag(lines, index, endIndex, "isHidden"),
                    SourceColumn: FindProperty(lines, index, endIndex, "sourceColumn"),
                    SortByColumn: NormalizeIdentifierReference(FindProperty(lines, index, endIndex, "sortByColumn")),
                    Expression: ReadExpression(lines, index, endIndex, columnExpression)));
            }
            else if (TryParseDeclaration(lines[index].Trimmed, "measure", out var measureName, out var measureExpression))
            {
                measures.Add(new SemanticMeasureInventory(
                    Name: measureName,
                    Expression: ReadExpression(lines, index, endIndex, measureExpression) ?? string.Empty,
                    FormatString: FindProperty(lines, index, endIndex, "formatString"),
                    IsHidden: HasFlag(lines, index, endIndex, "isHidden")));
            }
            else if (TryParseDeclaration(lines[index].Trimmed, "hierarchy", out var hierarchyName, out _))
            {
                hierarchies.Add(ParseHierarchy(lines, index, endIndex, hierarchyName));
            }
            else if (TryParseDeclaration(lines[index].Trimmed, "partition", out var partitionName, out var sourceType))
            {
                partitions.Add(new SemanticPartitionInventory(
                    Name: partitionName,
                    SourceType: sourceType ?? string.Empty,
                    Mode: FindProperty(lines, index, endIndex, "mode"),
                    Expression: string.Equals(sourceType, "calculated", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(sourceType, "m", StringComparison.OrdinalIgnoreCase)
                        ? ReadAssignmentExpression(lines, index, endIndex, "source")
                        : null));
            }
            else if (string.Equals(lines[index].Trimmed, "calculationGroup", StringComparison.OrdinalIgnoreCase))
            {
                calculationGroup = ParseCalculationGroup(lines, index, endIndex);
            }

            index = endIndex - 1;
        }

        var fieldParameter = partitions
            .Where(partition => partition.Expression is not null)
            .Select(partition => FieldParameterExpressionParser.TryParse(tableName, partition.Expression!))
            .FirstOrDefault(parameter => parameter is not null);

        return new SemanticTableInventory(
            Name: tableName,
            RelativePath: Path.GetRelativePath(rootPath, path),
            IsHidden: HasFlag(lines, tableDeclarationIndex, tablePropertyEnd, "isHidden"),
            IsPrivate: HasFlag(lines, tableDeclarationIndex, tablePropertyEnd, "isPrivate"),
            Columns: columns,
            Measures: measures,
            Hierarchies: hierarchies,
            Partitions: partitions,
            CalculationGroup: calculationGroup,
            FieldParameter: fieldParameter);
    }

    private static SemanticNamedExpressionInventory[] ParseNamedExpressions(string rootPath, string path)
    {
        var lines = ReadLines(path);
        var expressions = new List<SemanticNamedExpressionInventory>();
        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryParseDeclaration(lines[index].Trimmed, "expression", out var name, out var inlineExpression))
            {
                continue;
            }

            var endIndex = FindBlockEnd(lines, index);
            expressions.Add(new SemanticNamedExpressionInventory(
                Name: name,
                Expression: ReadExpression(lines, index, endIndex, inlineExpression) ?? string.Empty,
                Kind: FindProperty(lines, index, endIndex, "kind"),
                RelativePath: Path.GetRelativePath(rootPath, path)));
            index = endIndex - 1;
        }

        return expressions.OrderBy(expression => expression.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static SemanticCalculationGroupInventory ParseCalculationGroup(
        IReadOnlyList<TmdlLine> lines,
        int declarationIndex,
        int endIndex)
    {
        var declarationIndent = lines[declarationIndex].Indent;
        var itemIndent = lines
            .Skip(declarationIndex + 1)
            .Take(endIndex - declarationIndex - 1)
            .Where(line => line.Indent > declarationIndent && IsDeclaration(line.Trimmed, "calculationItem"))
            .Select(line => line.Indent)
            .DefaultIfEmpty(declarationIndent + 4)
            .Min();
        var items = new List<SemanticCalculationItemInventory>();

        for (var index = declarationIndex + 1; index < endIndex; index++)
        {
            if (lines[index].Indent != itemIndent ||
                !TryParseDeclaration(lines[index].Trimmed, "calculationItem", out var itemName, out var itemExpression))
            {
                continue;
            }

            var itemEndIndex = FindBlockEnd(lines, index, endIndex);
            items.Add(new SemanticCalculationItemInventory(
                Name: itemName,
                Expression: ReadExpression(lines, index, itemEndIndex, itemExpression) ?? string.Empty,
                FormatStringExpression: ReadAssignmentExpression(
                    lines,
                    index,
                    itemEndIndex,
                    "formatStringDefinition"),
                Ordinal: FindIntegerProperty(lines, index, itemEndIndex, "ordinal")));
            index = itemEndIndex - 1;
        }

        return new SemanticCalculationGroupInventory(
            Precedence: FindIntegerProperty(lines, declarationIndex, endIndex, "precedence"),
            SelectionExpression: ReadAssignmentExpression(lines, declarationIndex, endIndex, "selectionExpression"),
            MultipleOrEmptySelectionExpression: ReadAssignmentExpression(
                lines,
                declarationIndex,
                endIndex,
                "multipleOrEmptySelectionExpression"),
            Items: items);
    }

    private static SemanticHierarchyInventory ParseHierarchy(
        IReadOnlyList<TmdlLine> lines,
        int declarationIndex,
        int endIndex,
        string name)
    {
        var declarationIndent = lines[declarationIndex].Indent;
        var levelIndent = lines
            .Skip(declarationIndex + 1)
            .Take(endIndex - declarationIndex - 1)
            .Where(line => line.Indent > declarationIndent && IsDeclaration(line.Trimmed, "level"))
            .Select(line => line.Indent)
            .DefaultIfEmpty(declarationIndent + 4)
            .Min();
        var levels = new List<SemanticHierarchyLevelInventory>();

        for (var index = declarationIndex + 1; index < endIndex; index++)
        {
            if (lines[index].Indent != levelIndent ||
                !TryParseDeclaration(lines[index].Trimmed, "level", out var levelName, out _))
            {
                continue;
            }

            var levelEndIndex = FindBlockEnd(lines, index, endIndex);
            levels.Add(new SemanticHierarchyLevelInventory(
                Name: levelName,
                Column: NormalizeIdentifierReference(FindProperty(lines, index, levelEndIndex, "column"))));
            index = levelEndIndex - 1;
        }

        return new SemanticHierarchyInventory(
            Name: name,
            IsHidden: HasFlag(lines, declarationIndex, endIndex, "isHidden"),
            Levels: levels);
    }

    private static SemanticRelationshipInventory[] ParseRelationships(string path)
    {
        var lines = ReadLines(path);
        var relationships = new List<SemanticRelationshipInventory>();

        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryParseDeclaration(lines[index].Trimmed, "relationship", out var name, out _))
            {
                continue;
            }

            var endIndex = FindBlockEnd(lines, index);
            var fromColumn = FindProperty(lines, index, endIndex, "fromColumn");
            var toColumn = FindProperty(lines, index, endIndex, "toColumn");
            if (fromColumn is null || toColumn is null ||
                !TryParseQualifiedName(fromColumn, out var fromTable, out var fromColumnName) ||
                !TryParseQualifiedName(toColumn, out var toTable, out var toColumnName))
            {
                throw new InvalidDataException($"Relationship '{name}' has an invalid endpoint in: {path}");
            }

            relationships.Add(new SemanticRelationshipInventory(
                Name: name,
                IsActive: !string.Equals(FindProperty(lines, index, endIndex, "isActive"), "false", StringComparison.OrdinalIgnoreCase),
                CrossFilteringBehavior: FindProperty(lines, index, endIndex, "crossFilteringBehavior") ?? "oneDirection",
                FromCardinality: FindProperty(lines, index, endIndex, "fromCardinality") ?? "many",
                FromTable: fromTable,
                FromColumn: fromColumnName,
                ToCardinality: FindProperty(lines, index, endIndex, "toCardinality") ?? "one",
                ToTable: toTable,
                ToColumn: toColumnName));
            index = endIndex - 1;
        }

        return relationships.ToArray();
    }

    private static string? ReadExpression(
        IReadOnlyList<TmdlLine> lines,
        int declarationIndex,
        int endIndex,
        string? inlineExpression)
    {
        if (!string.IsNullOrWhiteSpace(inlineExpression))
        {
            return inlineExpression;
        }

        if (!lines[declarationIndex].Trimmed.Contains('='))
        {
            return null;
        }

        var propertyIndent = lines[declarationIndex].Indent + 4;
        var expressionLines = new List<TmdlLine>();
        for (var index = declarationIndex + 1; index < endIndex; index++)
        {
            var line = lines[index];
            if (line.Indent == propertyIndent && IsObjectProperty(line.Trimmed))
            {
                break;
            }

            expressionLines.Add(line);
        }

        while (expressionLines.Count > 0 && string.IsNullOrWhiteSpace(expressionLines[0].Text))
        {
            expressionLines.RemoveAt(0);
        }

        while (expressionLines.Count > 0 && string.IsNullOrWhiteSpace(expressionLines[^1].Text))
        {
            expressionLines.RemoveAt(expressionLines.Count - 1);
        }

        if (expressionLines.Count == 0)
        {
            return string.Empty;
        }

        var commonIndent = expressionLines
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .Min(line => line.Text.Length - line.Text.TrimStart().Length);
        return string.Join(
            Environment.NewLine,
            expressionLines.Select(line => line.Text.Length >= commonIndent ? line.Text[commonIndent..] : string.Empty));
    }

    private static string? FindProperty(
        IReadOnlyList<TmdlLine> lines,
        int declarationIndex,
        int endIndex,
        string propertyName)
    {
        var propertyIndent = lines[declarationIndex].Indent + 4;
        var prefix = propertyName + ":";
        for (var index = declarationIndex + 1; index < endIndex; index++)
        {
            if (lines[index].Indent == propertyIndent &&
                lines[index].Trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return lines[index].Trimmed[prefix.Length..].Trim();
            }
        }

        return null;
    }

    private static string? ReadAssignmentExpression(
        IReadOnlyList<TmdlLine> lines,
        int declarationIndex,
        int endIndex,
        string propertyName)
    {
        var propertyIndent = lines[declarationIndex].Indent + 4;
        var prefix = propertyName + " =";
        for (var index = declarationIndex + 1; index < endIndex; index++)
        {
            if (lines[index].Indent != propertyIndent ||
                !lines[index].Trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var inlineExpression = lines[index].Trimmed[prefix.Length..].Trim();
            if (inlineExpression.Length > 0)
            {
                return inlineExpression;
            }

            var expressionLines = new List<TmdlLine>();
            for (var expressionIndex = index + 1; expressionIndex < endIndex; expressionIndex++)
            {
                if (!string.IsNullOrWhiteSpace(lines[expressionIndex].Text) &&
                    lines[expressionIndex].Indent <= propertyIndent)
                {
                    break;
                }

                expressionLines.Add(lines[expressionIndex]);
            }

            while (expressionLines.Count > 0 && string.IsNullOrWhiteSpace(expressionLines[^1].Text))
            {
                expressionLines.RemoveAt(expressionLines.Count - 1);
            }

            if (expressionLines.Count == 0)
            {
                return string.Empty;
            }

            var commonIndent = expressionLines
                .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                .Min(line => line.Text.Length - line.Text.TrimStart().Length);
            return string.Join(
                Environment.NewLine,
                expressionLines.Select(line => line.Text.Length >= commonIndent
                    ? line.Text[commonIndent..]
                    : string.Empty));
        }

        return null;
    }

    private static bool HasFlag(
        IReadOnlyList<TmdlLine> lines,
        int declarationIndex,
        int endIndex,
        string flagName)
    {
        var propertyIndent = lines[declarationIndex].Indent + 4;
        for (var index = declarationIndex + 1; index < endIndex; index++)
        {
            if (lines[index].Indent == propertyIndent &&
                string.Equals(lines[index].Trimmed, flagName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int? FindIntegerProperty(
        IReadOnlyList<TmdlLine> lines,
        int declarationIndex,
        int endIndex,
        string propertyName)
    {
        var value = FindProperty(lines, declarationIndex, endIndex, propertyName);
        return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static bool IsObjectProperty(string value)
    {
        return value.StartsWith("annotation ", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("formatStringDefinition =", StringComparison.OrdinalIgnoreCase) ||
               value is "isHidden" or "isPrivate" or "isNameInferred" ||
               value.Contains(':');
    }

    private static int FindFirstObjectDeclaration(IReadOnlyList<TmdlLine> lines, int startIndex, int indent)
    {
        for (var index = startIndex; index < lines.Count; index++)
        {
            if (lines[index].Indent == indent && IsTableObjectDeclaration(lines[index].Trimmed))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsTableObjectDeclaration(string value)
    {
        return string.Equals(value, "calculationGroup", StringComparison.OrdinalIgnoreCase) ||
               NamedObjectKeywords.Any(keyword => IsDeclaration(value, keyword));
    }

    private static int FindDeclaration(
        IReadOnlyList<TmdlLine> lines,
        string keyword,
        int startIndex,
        int? requiredIndent)
    {
        for (var index = startIndex; index < lines.Count; index++)
        {
            if ((requiredIndent is null || lines[index].Indent == requiredIndent) &&
                IsDeclaration(lines[index].Trimmed, keyword))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsDeclaration(string value, string keyword)
    {
        return value.StartsWith(keyword + " ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseDeclaration(
        string value,
        string keyword,
        out string name,
        out string? expression)
    {
        name = string.Empty;
        expression = null;
        if (!IsDeclaration(value, keyword))
        {
            return false;
        }

        var remainder = value[(keyword.Length + 1)..].TrimStart();
        if (!TryReadIdentifier(remainder, out name, out var consumed))
        {
            return false;
        }

        remainder = remainder[consumed..].TrimStart();
        if (remainder.StartsWith('='))
        {
            expression = remainder[1..].Trim();
        }

        return true;
    }

    private static bool TryParseQualifiedName(string value, out string table, out string column)
    {
        table = string.Empty;
        column = string.Empty;
        var inQuote = false;
        var separatorIndex = -1;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '\'' && index + 1 < value.Length && value[index + 1] == '\'')
            {
                index++;
                continue;
            }

            if (value[index] == '\'')
            {
                inQuote = !inQuote;
            }
            else if (value[index] == '.' && !inQuote)
            {
                separatorIndex = index;
            }
        }

        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            return false;
        }

        return TryReadWholeIdentifier(value[..separatorIndex].Trim(), out table) &&
               TryReadWholeIdentifier(value[(separatorIndex + 1)..].Trim(), out column);
    }

    private static bool TryReadWholeIdentifier(string value, out string identifier)
    {
        if (!TryReadIdentifier(value, out identifier, out var consumed))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(value[consumed..]);
    }

    private static string? NormalizeIdentifierReference(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return TryReadWholeIdentifier(value, out var identifier) ? identifier : value;
    }

    private static bool TryReadIdentifier(string value, out string identifier, out int consumed)
    {
        identifier = string.Empty;
        consumed = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value[0] != '\'')
        {
            var endIndex = 0;
            while (endIndex < value.Length && !char.IsWhiteSpace(value[endIndex]) && value[endIndex] != '=')
            {
                endIndex++;
            }

            identifier = value[..endIndex];
            consumed = endIndex;
            return identifier.Length > 0;
        }

        var result = new System.Text.StringBuilder();
        for (var index = 1; index < value.Length; index++)
        {
            if (value[index] != '\'')
            {
                result.Append(value[index]);
                continue;
            }

            if (index + 1 < value.Length && value[index + 1] == '\'')
            {
                result.Append('\'');
                index++;
                continue;
            }

            identifier = result.ToString();
            consumed = index + 1;
            return true;
        }

        return false;
    }

    private static int FindBlockEnd(IReadOnlyList<TmdlLine> lines, int declarationIndex, int? limit = null)
    {
        var declarationIndent = lines[declarationIndex].Indent;
        var end = limit ?? lines.Count;
        for (var index = declarationIndex + 1; index < end; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index].Text) && lines[index].Indent <= declarationIndent)
            {
                return index;
            }
        }

        return end;
    }

    private static TmdlLine[] ReadLines(string path)
    {
        return File.ReadAllLines(path)
            .Select(line => new TmdlLine(line, line.Trim(), GetIndent(line)))
            .ToArray();
    }

    private static int GetIndent(string value)
    {
        var indent = 0;
        foreach (var character in value)
        {
            if (character == '\t')
            {
                indent += 4;
            }
            else if (character == ' ')
            {
                indent++;
            }
            else
            {
                break;
            }
        }

        return indent;
    }

    private sealed record TmdlLine(string Text, string Trimmed, int Indent);
}

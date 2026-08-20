using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class TmdlSemanticModelParser
{
    private static readonly string[] NamedObjectKeywords = ["column", "measure", "hierarchy", "partition"];
    private const string ExpressionFence = "```";

    public static SemanticModelInventory Parse(IProjectFileSource source, string semanticModelDirectory)
    {
        var directoryName = ProjectFilePaths.GetFileName(semanticModelDirectory);
        var name = directoryName.EndsWith(".SemanticModel", StringComparison.OrdinalIgnoreCase)
            ? directoryName[..^".SemanticModel".Length]
            : directoryName;
        var definitionDirectory = ProjectFilePaths.Combine(semanticModelDirectory, "definition");
        var tablesDirectory = ProjectFilePaths.Combine(definitionDirectory, "tables");

        var tables = source.EnumerateFiles(tablesDirectory, recursive: false).Any()
            ? source
                .EnumerateFiles(tablesDirectory, recursive: false)
                .Where(file => file.RelativePath.EndsWith(".tmdl", StringComparison.OrdinalIgnoreCase))
                .Select(file => ParseTable(source, file.RelativePath))
                .OrderBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        var relationshipsPath = ProjectFilePaths.Combine(definitionDirectory, "relationships.tmdl");
        var relationships = source.FileExists(relationshipsPath)
            ? ParseRelationships(source, relationshipsPath)
            : [];
        var rolesDirectory = ProjectFilePaths.Combine(definitionDirectory, "roles");
        var roles = source
            .EnumerateFiles(rolesDirectory, recursive: false)
            .Where(file => file.RelativePath.EndsWith(".tmdl", StringComparison.OrdinalIgnoreCase))
            .Select(file => ParseRole(source, file.RelativePath))
            .Where(role => role is not null)
            .Select(role => role!)
            .OrderBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var perspectivesDirectory = ProjectFilePaths.Combine(definitionDirectory, "perspectives");
        var perspectives = source
            .EnumerateFiles(perspectivesDirectory, recursive: false)
            .Where(file => file.RelativePath.EndsWith(".tmdl", StringComparison.OrdinalIgnoreCase))
            .Select(file => ParsePerspective(source, file.RelativePath))
            .Where(perspective => perspective is not null)
            .Select(perspective => perspective!)
            .OrderBy(perspective => perspective.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var functionsPath = ProjectFilePaths.Combine(definitionDirectory, "functions.tmdl");
        var functions = source.FileExists(functionsPath)
            ? ParseFunctions(source, functionsPath)
            : [];
        var expressionsPath = ProjectFilePaths.Combine(definitionDirectory, "expressions.tmdl");
        var namedExpressions = source.FileExists(expressionsPath)
            ? ParseNamedExpressions(source, expressionsPath)
            : [];

        return new SemanticModelInventory(
            Name: name,
            RelativePath: semanticModelDirectory,
            Tables: tables,
            Relationships: relationships,
            NamedExpressions: namedExpressions)
        {
            Roles = roles,
            Perspectives = perspectives,
            Functions = functions,
        };
    }

    private static SemanticTableInventory ParseTable(IProjectFileSource source, string path)
    {
        var lines = ReadLines(source, path);
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
        var systemGeneratedKind = SystemGeneratedKind(lines);

        return new SemanticTableInventory(
            Name: tableName,
            RelativePath: path,
            IsHidden: HasFlag(lines, tableDeclarationIndex, tablePropertyEnd, "isHidden"),
            IsPrivate: HasFlag(lines, tableDeclarationIndex, tablePropertyEnd, "isPrivate"),
            IsSystemGenerated: systemGeneratedKind is not null,
            SystemGeneratedKind: systemGeneratedKind,
            Columns: columns,
            Measures: measures,
            Hierarchies: hierarchies,
            Partitions: partitions,
            CalculationGroup: calculationGroup,
            FieldParameter: fieldParameter);
    }

    private static string? SystemGeneratedKind(IReadOnlyList<TmdlLine> lines)
    {
        if (HasTrueAnnotation(lines, "__PBI_LocalDateTable"))
        {
            return SystemGeneratedSemanticTableKinds.AutoDateTimeLocalTable;
        }

        return HasTrueAnnotation(lines, "__PBI_TemplateDateTable")
            ? SystemGeneratedSemanticTableKinds.AutoDateTimeTemplateTable
            : null;
    }

    private static bool HasTrueAnnotation(IReadOnlyList<TmdlLine> lines, string name)
    {
        var prefix = $"annotation {name}";
        return lines.Any(line =>
            line.Trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            line.Trimmed[prefix.Length..].TrimStart().Equals("= true", StringComparison.OrdinalIgnoreCase));
    }

    private static SemanticNamedExpressionInventory[] ParseNamedExpressions(IProjectFileSource source, string path)
    {
        var lines = ReadLines(source, path);
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
                RelativePath: path));
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

    /// <summary>
    /// Role-level constructs that cannot reference a model object, so leaving them unread cannot hide a
    /// dependency. ModelRole exposes annotations, description, extended properties, members and a model
    /// permission; members name user principals rather than model objects. Anything not listed here is
    /// treated conservatively, including constructs this version has never seen.
    /// </summary>
    private static readonly HashSet<string> RoleConstructsWithoutObjectReferences =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "modelPermission",
            "description",
            "annotation",
            "extendedProperty",
        };

    /// <summary>
    /// Table-permission constructs that cannot reference a model object. Column permissions are parsed
    /// separately because they explicitly name a column for object-level security.
    /// </summary>
    private static readonly HashSet<string> TablePermissionConstructsWithoutObjectReferences =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "metadataPermission",
            "annotation",
            "extendedProperty",
        };

    /// <summary>
    /// Reads DAX user-defined functions. All functions live in one file and are model-scoped; Microsoft
    /// documents that a function name is unique within the model and is never owned by a table.
    /// </summary>
    private static SemanticFunctionInventory[] ParseFunctions(IProjectFileSource source, string path)
    {
        var lines = ReadLines(source, path);
        var functions = new List<SemanticFunctionInventory>();

        for (var index = 0; index < lines.Length; index++)
        {
            if (!TryParseDeclaration(lines[index].Trimmed, "function", out var declaration, out var inlineBody))
            {
                continue;
            }

            var endIndex = FindBlockEnd(lines, index);
            var body = ReadExpression(lines, index, endIndex, inlineBody) ?? string.Empty;
            var (name, parameters) = ReadFunctionSignature(declaration, ref body);
            functions.Add(new SemanticFunctionInventory(name, parameters, body, path));
            index = endIndex - 1;
        }

        return functions.ToArray();
    }

    /// <summary>
    /// Splits a function declaration into its name and parameter list.
    ///
    /// TMDL puts the parameter list on the right of the equals sign, as <c>(p : TYPE) =&gt; body</c>, so
    /// the declared identifier is the name and the parameters are read from the front of the body.
    /// </summary>
    private static (string Name, SemanticFunctionParameterInventory[] Parameters) ReadFunctionSignature(
        string declaration,
        ref string body)
    {
        var parameters = Array.Empty<SemanticFunctionParameterInventory>();
        var trimmed = body.TrimStart();
        if (!trimmed.StartsWith('('))
        {
            return (declaration, parameters);
        }

        var close = trimmed.IndexOf(')');
        var arrow = trimmed.IndexOf("=>", StringComparison.Ordinal);
        if (close < 0 || arrow < close)
        {
            return (declaration, parameters);
        }

        parameters = ReadFunctionParameters(trimmed[1..close]);
        body = trimmed[(arrow + 2)..].Trim();
        return (declaration, parameters);
    }

    private static SemanticFunctionParameterInventory[] ReadFunctionParameters(string parameterList)
    {
        if (string.IsNullOrWhiteSpace(parameterList))
        {
            return [];
        }

        return parameterList
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(parameter =>
            {
                var separator = parameter.IndexOf(':');
                var name = (separator < 0 ? parameter : parameter[..separator]).Trim();
                var hint = separator < 0 ? null : parameter[(separator + 1)..].Trim();
                return new SemanticFunctionParameterInventory(name, string.IsNullOrWhiteSpace(hint) ? null : hint);
            })
            .Where(parameter => parameter.Name.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// Perspective-level constructs that name no model object. A perspective carries annotations and
    /// extended properties; everything that names an object is a perspectiveTable and its members.
    /// </summary>
    private static readonly HashSet<string> PerspectiveConstructsWithoutObjectReferences =
        new(StringComparer.OrdinalIgnoreCase) { "annotation", "extendedProperty", "description" };

    /// <summary>
    /// Members of a perspectiveTable that are analysed, plus its documented reference-free properties.
    /// Anything else — perspectiveSet, or a construct this version has not seen — is left conservative.
    /// </summary>
    private static readonly HashSet<string> PerspectiveTableConstructsAccountedFor =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "perspectiveColumn",
            "perspectiveMeasure",
            "perspectiveHierarchy",
            "includeAll",
            "annotation",
            "extendedProperty",
        };

    /// <summary>
    /// Reads the model objects a perspective exposes. Membership is explicit per object unless
    /// includeAll is set, which Microsoft documents as including every column, hierarchy and measure of
    /// the table. Presentation meaning beyond that is not interpreted.
    /// </summary>
    private static SemanticPerspectiveInventory? ParsePerspective(IProjectFileSource source, string path)
    {
        var lines = ReadLines(source, path);
        var declarationIndex = FindDeclaration(lines, "perspective", startIndex: 0, requiredIndent: null);
        if (declarationIndex < 0 ||
            !TryParseDeclaration(lines[declarationIndex].Trimmed, "perspective", out var name, out _))
        {
            return null;
        }

        var perspectiveEnd = FindBlockEnd(lines, declarationIndex);
        var tables = new List<SemanticPerspectiveTableInventory>();
        var unaccounted = new List<string>();
        var childIndent = ChildIndent(lines, declarationIndex, perspectiveEnd);

        for (var index = declarationIndex + 1; index < perspectiveEnd; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index].Text) || lines[index].Indent != childIndent)
            {
                continue;
            }

            if (TryParseDeclaration(lines[index].Trimmed, "perspectiveTable", out var table, out _))
            {
                var tableEnd = FindBlockEnd(lines, index, perspectiveEnd);
                tables.Add(ReadPerspectiveTable(lines, index, tableEnd, table));
                unaccounted.AddRange(UnaccountedChildren(
                    lines, index, tableEnd, PerspectiveTableConstructsAccountedFor));
                index = tableEnd - 1;
                continue;
            }

            var keyword = LeadingKeyword(lines[index].Trimmed);
            if (!PerspectiveConstructsWithoutObjectReferences.Contains(keyword))
            {
                unaccounted.Add(keyword);
            }
        }

        return new SemanticPerspectiveInventory(name, tables, path)
        {
            UnanalyzedConstructs = unaccounted.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        };
    }

    private static SemanticPerspectiveTableInventory ReadPerspectiveTable(
        IReadOnlyList<TmdlLine> lines,
        int declarationIndex,
        int endIndex,
        string table)
    {
        var columns = new List<string>();
        var measures = new List<string>();
        var hierarchies = new List<string>();
        var includeAll = string.Equals(
            FindProperty(lines, declarationIndex, endIndex, "includeAll"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        for (var index = declarationIndex + 1; index < endIndex; index++)
        {
            var trimmed = lines[index].Trimmed;
            if (TryParseDeclaration(trimmed, "perspectiveColumn", out var column, out _))
            {
                columns.Add(column);
            }
            else if (TryParseDeclaration(trimmed, "perspectiveMeasure", out var measure, out _))
            {
                measures.Add(measure);
            }
            else if (TryParseDeclaration(trimmed, "perspectiveHierarchy", out var hierarchy, out _))
            {
                hierarchies.Add(hierarchy);
            }
        }

        return new SemanticPerspectiveTableInventory(table, includeAll, columns, measures, hierarchies);
    }

    /// <summary>
    /// Reads the supported role permission forms: row-level table filters, table-level metadata access,
    /// and explicitly named column-level object permissions. Other role content remains conservative.
    /// </summary>
    private static SemanticRoleInventory? ParseRole(IProjectFileSource source, string path)
    {
        var lines = ReadLines(source, path);
        var declarationIndex = FindDeclaration(lines, "role", startIndex: 0, requiredIndent: null);
        if (declarationIndex < 0 ||
            !TryParseDeclaration(lines[declarationIndex].Trimmed, "role", out var roleName, out _))
        {
            return null;
        }

        var roleEnd = FindBlockEnd(lines, declarationIndex);
        var permissions = new List<SemanticTablePermissionInventory>();
        var unaccounted = new List<string>();
        var roleChildIndent = ChildIndent(lines, declarationIndex, roleEnd);

        for (var index = declarationIndex + 1; index < roleEnd; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index].Text) || lines[index].Indent != roleChildIndent)
            {
                continue;
            }

            if (TryParseDeclaration(lines[index].Trimmed, "tablePermission", out var table, out var inlineFilter))
            {
                var permissionEnd = FindBlockEnd(lines, index, roleEnd);
                var filter = ReadExpression(lines, index, permissionEnd, inlineFilter) ?? string.Empty;
                var columnPermissions = ReadColumnPermissions(lines, index, permissionEnd);
                permissions.Add(new SemanticTablePermissionInventory(table, filter)
                {
                    MetadataPermission = FindProperty(lines, index, permissionEnd, "metadataPermission"),
                    ColumnPermissions = columnPermissions,
                });

                var unaccountedChildren = UnaccountedChildren(
                    lines, index, permissionEnd, TablePermissionConstructsWithoutObjectReferences).ToList();
                if (HasOnlySupportedColumnPermissions(lines, index, permissionEnd))
                {
                    unaccountedChildren.RemoveAll(keyword =>
                        string.Equals(keyword, "columnPermission", StringComparison.OrdinalIgnoreCase));
                }

                unaccounted.AddRange(unaccountedChildren);
                index = permissionEnd - 1;
                continue;
            }

            var keyword = LeadingKeyword(lines[index].Trimmed);
            if (!RoleConstructsWithoutObjectReferences.Contains(keyword))
            {
                unaccounted.Add(keyword);
            }
        }

        return new SemanticRoleInventory(
            Name: roleName,
            ModelPermission: FindProperty(lines, declarationIndex, roleEnd, "modelPermission"),
            TablePermissions: permissions,
            RelativePath: path)
        {
            UnanalyzedConstructs = unaccounted.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        };
    }

    private static List<SemanticColumnPermissionInventory> ReadColumnPermissions(
        IReadOnlyList<TmdlLine> lines,
        int declarationIndex,
        int endIndex)
    {
        var childIndent = ChildIndent(lines, declarationIndex, endIndex);
        if (childIndent < 0)
        {
            return [];
        }

        var permissions = new List<SemanticColumnPermissionInventory>();
        for (var index = declarationIndex + 1; index < endIndex; index++)
        {
            if (lines[index].Indent != childIndent ||
                !TryParseDeclaration(lines[index].Trimmed, "columnPermission", out var column, out var permission) ||
                string.IsNullOrWhiteSpace(permission))
            {
                continue;
            }

            permissions.Add(new SemanticColumnPermissionInventory(column, permission));
        }

        return permissions;
    }

    private static bool HasOnlySupportedColumnPermissions(
        IReadOnlyList<TmdlLine> lines,
        int declarationIndex,
        int endIndex)
    {
        var childIndent = ChildIndent(lines, declarationIndex, endIndex);
        var foundColumnPermission = false;
        for (var index = declarationIndex + 1; index < endIndex; index++)
        {
            if (lines[index].Indent != childIndent ||
                !string.Equals(LeadingKeyword(lines[index].Trimmed), "columnPermission", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foundColumnPermission = true;
            if (!TryParseDeclaration(lines[index].Trimmed, "columnPermission", out _, out var permission) ||
                string.IsNullOrWhiteSpace(permission))
            {
                return false;
            }
        }

        return foundColumnPermission;
    }

    /// <summary>
    /// The indent of a block's immediate children, or -1 when the block has none. Microsoft documents
    /// that a multi-line expression sits one level deeper than an object's properties, so children found
    /// at this indent are constructs rather than expression continuation.
    /// </summary>
    private static int ChildIndent(IReadOnlyList<TmdlLine> lines, int declarationIndex, int endIndex)
    {
        var indents = new List<int>();
        for (var index = declarationIndex + 1; index < endIndex; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index].Text))
            {
                indents.Add(lines[index].Indent);
            }
        }

        return indents.Count == 0 ? -1 : indents.Min();
    }

    /// <summary>
    /// Immediate children of a block whose leading keyword is not known to be free of model-object
    /// references. Used to decide whether anything dependency-bearing was left unread.
    /// </summary>
    private static IEnumerable<string> UnaccountedChildren(
        IReadOnlyList<TmdlLine> lines,
        int declarationIndex,
        int endIndex,
        HashSet<string> knownWithoutObjectReferences)
    {
        var childIndent = ChildIndent(lines, declarationIndex, endIndex);
        if (childIndent < 0)
        {
            yield break;
        }

        var insideFencedExpression = IsFencedExpressionOpening(lines[declarationIndex].Trimmed);
        for (var index = declarationIndex + 1; index < endIndex; index++)
        {
            if (insideFencedExpression)
            {
                if (IsExpressionFence(lines[index].Trimmed))
                {
                    insideFencedExpression = false;
                }

                continue;
            }

            if (IsFencedExpressionOpening(lines[index].Trimmed))
            {
                insideFencedExpression = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(lines[index].Text) || lines[index].Indent != childIndent)
            {
                continue;
            }

            var keyword = LeadingKeyword(lines[index].Trimmed);
            if (!knownWithoutObjectReferences.Contains(keyword))
            {
                yield return keyword;
            }
        }
    }

    /// <summary>The construct or property name at the start of a TMDL line.</summary>
    private static string LeadingKeyword(string trimmed)
    {
        var end = 0;
        while (end < trimmed.Length && (char.IsLetterOrDigit(trimmed[end]) || trimmed[end] == '_'))
        {
            end++;
        }

        return trimmed[..end];
    }

    private static SemanticRelationshipInventory[] ParseRelationships(IProjectFileSource source, string path)
    {
        var lines = ReadLines(source, path);
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
        if (IsExpressionFence(inlineExpression))
        {
            return ReadFencedExpression(lines, declarationIndex + 1, endIndex);
        }

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
            if (IsExpressionFence(inlineExpression))
            {
                return ReadFencedExpression(lines, index + 1, endIndex);
            }

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

    /// <summary>
    /// Reads a TMDL expression enclosed by triple backticks. TMDL defines the closing delimiter as the
    /// expression's left boundary, so only that structural indentation is removed; relative indentation
    /// and blank lines inside the expression are preserved.
    /// </summary>
    private static string ReadFencedExpression(IReadOnlyList<TmdlLine> lines, int startIndex, int endIndex)
    {
        var closingIndex = -1;
        for (var index = startIndex; index < endIndex; index++)
        {
            if (IsExpressionFence(lines[index].Trimmed))
            {
                closingIndex = index;
                break;
            }
        }

        // Keep malformed or incomplete input from swallowing a following object. The ordinary parser is
        // intentionally tolerant, so retain the available text rather than treating an opening fence as
        // a complete expression.
        var expressionEnd = closingIndex >= 0 ? closingIndex : endIndex;
        if (expressionEnd == startIndex)
        {
            return string.Empty;
        }

        var leftBoundary = closingIndex >= 0 ? LeadingWhitespace(lines[closingIndex].Text) : string.Empty;
        return string.Join(
            Environment.NewLine,
            lines.Skip(startIndex).Take(expressionEnd - startIndex).Select(line =>
                line.Text.StartsWith(leftBoundary, StringComparison.Ordinal)
                    ? line.Text[leftBoundary.Length..]
                    : line.Text));
    }

    private static bool IsExpressionFence(string? value) =>
        string.Equals(value?.Trim(), ExpressionFence, StringComparison.Ordinal);

    private static bool IsFencedExpressionOpening(string value)
    {
        var equalsIndex = value.LastIndexOf('=');
        return equalsIndex >= 0 && IsExpressionFence(value[(equalsIndex + 1)..]);
    }

    private static string LeadingWhitespace(string value)
    {
        var length = 0;
        while (length < value.Length && (value[length] == ' ' || value[length] == '\t'))
        {
            length++;
        }

        return value[..length];
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

    private static TmdlLine[] ReadLines(IProjectFileSource source, string path)
    {
        using var reader = new StreamReader(source.OpenRead(path));
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines
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

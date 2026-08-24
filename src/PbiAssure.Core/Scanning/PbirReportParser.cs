using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirReportParser
{
    public static ReportInventory Parse(IProjectFileSource source, string reportDirectory)
    {
        var reportName = ProjectFilePaths.GetFileName(reportDirectory);
        if (reportName.EndsWith(".Report", StringComparison.OrdinalIgnoreCase))
        {
            reportName = reportName[..^".Report".Length];
        }

        var relativeReportPath = ProjectFilePaths.Normalize(reportDirectory);
        var schemaObservations = new List<ReportSchemaObservation>();
        var modelConnection = ParseModelConnection(source, reportDirectory, schemaObservations);
        var versionMetadata = ParseVersionMetadata(source, reportDirectory, schemaObservations);
        var bookmarkResult = PbirBookmarkParser.Parse(source, reportDirectory, schemaObservations);
        var reportExtensionsPath = ProjectFilePaths.Combine(reportDirectory, "definition", "reportExtensions.json");
        var reportExtensions = ParseReportExtensions(source, reportExtensionsPath, schemaObservations);
        var reportDefinitionPath = ProjectFilePaths.Combine(reportDirectory, "definition", "report.json");
        string? reportSchemaUri = null;
        var theme = ThemeInventory.Unavailable;
        VisualFieldReference[] reportFieldReferences = [];
        ReportFilterInventory[] reportFilters = [];
        if (source.FileExists(reportDefinitionPath))
        {
            using var reportDefinition = OpenJsonDocument(source, reportDefinitionPath);
            reportSchemaUri = GetString(reportDefinition.RootElement, "$schema");
            schemaObservations.Add(PbirSchemaObservationFactory.Create(
                ReportSchemaArtifactKinds.Report, reportDefinitionPath, reportDefinition.RootElement));
            theme = PbirThemeParser.Parse(source, reportDirectory, reportDefinition.RootElement);
            reportFieldReferences = PbirFieldReferenceExtractor.Extract(reportDefinition.RootElement);
            reportFilters = ParseFilters(reportDefinition.RootElement);
        }

        var pagesDirectory = ProjectFilePaths.Combine(reportDirectory, "definition", "pages");
        var pagesMetadataPath = ProjectFilePaths.Combine(pagesDirectory, "pages.json");

        if (!source.FileExists(pagesMetadataPath))
        {
            return new ReportInventory(
                Name: reportName,
                RelativePath: relativeReportPath,
                ModelConnection: modelConnection,
                DefinitionPath: source.FileExists(reportDefinitionPath)
                    ? reportDefinitionPath
                    : null,
                SchemaUri: reportSchemaUri,
                PagesSchemaUri: null,
                ActivePageName: null,
                LandingPageName: null,
                Pages: [],
                Filters: reportFilters,
                FieldReferences: reportFieldReferences,
                ReportExtensionsPath: reportExtensions.Path,
                ReportExtensionsSchemaUri: reportExtensions.SchemaUri,
                ReportMeasures: reportExtensions.Measures,
                BookmarksSchemaUri: bookmarkResult.SchemaUri,
                BookmarkOrder: bookmarkResult.BookmarkOrder,
                Bookmarks: bookmarkResult.Bookmarks)
            {
                SchemaObservations = OrderObservations(schemaObservations),
                VersionMetadataPath = versionMetadata.Path,
                PbirDefinitionVersion = versionMetadata.Version,
                Theme = theme,
                ThemeReview = ThemeReviewAnalyzer.Analyze(theme, []),
            };
        }

        using var pagesMetadata = OpenJsonDocument(source, pagesMetadataPath);
        var metadataRoot = pagesMetadata.RootElement;
        var schemaUri = GetString(metadataRoot, "$schema");
        schemaObservations.Add(PbirSchemaObservationFactory.Create(
            ReportSchemaArtifactKinds.PagesMetadata, pagesMetadataPath, metadataRoot));
        var activePageName = GetString(metadataRoot, "activePageName");
        var landingPageName = GetString(metadataRoot, "landingPageName");
        var pageOrder = ReadPageOrder(metadataRoot);

        var pages = source
            .EnumerateDirectories(pagesDirectory)
            .Select(directory => ParsePage(
                source,
                ProjectFilePaths.Combine(pagesDirectory, directory),
                pageOrder,
                activePageName,
                theme,
                schemaObservations))
            .Where(page => page is not null)
            .Cast<PageInventory>()
            .OrderBy(page => page.Order ?? int.MaxValue)
            .ThenBy(page => page.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ReportInventory(
            Name: reportName,
            RelativePath: relativeReportPath,
            ModelConnection: modelConnection,
            DefinitionPath: source.FileExists(reportDefinitionPath)
                ? reportDefinitionPath
                : null,
            SchemaUri: reportSchemaUri,
            PagesSchemaUri: schemaUri,
            ActivePageName: activePageName,
            LandingPageName: landingPageName,
            Pages: pages,
            Filters: reportFilters,
            FieldReferences: reportFieldReferences,
            ReportExtensionsPath: reportExtensions.Path,
            ReportExtensionsSchemaUri: reportExtensions.SchemaUri,
            ReportMeasures: reportExtensions.Measures,
            BookmarksSchemaUri: bookmarkResult.SchemaUri,
            BookmarkOrder: bookmarkResult.BookmarkOrder,
            Bookmarks: bookmarkResult.Bookmarks)
        {
            SchemaObservations = OrderObservations(schemaObservations),
            VersionMetadataPath = versionMetadata.Path,
            PbirDefinitionVersion = versionMetadata.Version,
            Theme = theme,
            ThemeReview = ThemeReviewAnalyzer.Analyze(theme, pages),
        };
    }

    private static ReportModelConnectionInventory ParseModelConnection(
        IProjectFileSource source,
        string reportDirectory,
        List<ReportSchemaObservation> schemaObservations)
    {
        var definitionPath = ProjectFilePaths.Combine(reportDirectory, "definition.pbir");
        var relativePath = definitionPath;
        if (!source.FileExists(definitionPath))
        {
            return new ReportModelConnectionInventory(
                relativePath, null, null, ReportModelConnectionKinds.Unspecified,
                null, null, null, false);
        }

        using var document = OpenJsonDocument(source, definitionPath);
        var root = document.RootElement;
        var schemaUri = GetString(root, "$schema");
        schemaObservations.Add(PbirSchemaObservationFactory.Create(
            ReportSchemaArtifactKinds.DefinitionProperties, definitionPath, root));
        var version = GetString(root, "version");
        if (!TryGetObject(root, "datasetReference", out var datasetReference))
        {
            return new ReportModelConnectionInventory(
                relativePath, schemaUri, version, ReportModelConnectionKinds.Unspecified,
                null, null, null, false);
        }

        if (TryGetObject(datasetReference, "byPath", out var byPath))
        {
            var configuredPath = GetString(byPath, "path");
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return new ReportModelConnectionInventory(
                    relativePath, schemaUri, version, ReportModelConnectionKinds.ByPath,
                    configuredPath, null, null, false);
            }

            string targetPath;
            try
            {
                targetPath = ProjectFilePaths.ResolveRelative(reportDirectory, configuredPath);
            }
            catch (ArgumentException)
            {
                return new ReportModelConnectionInventory(
                    relativePath, schemaUri, version, ReportModelConnectionKinds.ByPath,
                    configuredPath, null, null, false);
            }

            var targetName = ProjectFilePaths.GetFileName(targetPath);
            if (targetName.EndsWith(".SemanticModel", StringComparison.OrdinalIgnoreCase))
            {
                targetName = targetName[..^".SemanticModel".Length];
            }

            return new ReportModelConnectionInventory(
                relativePath, schemaUri, version, ReportModelConnectionKinds.ByPath,
                configuredPath, targetPath, targetName,
                source.EnumerateDirectories(string.Empty).Any(directory =>
                    string.Equals(directory, targetPath, StringComparison.OrdinalIgnoreCase)));
        }

        if (TryGetObject(datasetReference, "byConnection", out _))
        {
            return new ReportModelConnectionInventory(
                relativePath, schemaUri, version, ReportModelConnectionKinds.ByConnection,
                null, null, null, false);
        }

        return new ReportModelConnectionInventory(
            relativePath, schemaUri, version, ReportModelConnectionKinds.Unspecified,
            null, null, null, false);
    }

    private static ReportExtensionParseResult ParseReportExtensions(
        IProjectFileSource source,
        string path,
        List<ReportSchemaObservation> schemaObservations)
    {
        if (!source.FileExists(path))
        {
            return new ReportExtensionParseResult(null, null, []);
        }

        using var document = OpenJsonDocument(source, path);
        var root = document.RootElement;
        schemaObservations.Add(PbirSchemaObservationFactory.Create(
            ReportSchemaArtifactKinds.ReportExtension, path, root));
        var extensionName = GetString(root, "name") ?? "extension";
        var measures = new List<ReportMeasureInventory>();
        if (root.TryGetProperty("entities", out var entities) && entities.ValueKind == JsonValueKind.Array)
        {
            foreach (var entity in entities.EnumerateArray())
            {
                var entityName = GetString(entity, "name");
                if (entityName is null || !entity.TryGetProperty("measures", out var measureArray) ||
                    measureArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var measure in measureArray.EnumerateArray())
                {
                    var name = GetString(measure, "name");
                    var dataType = GetString(measure, "dataType");
                    var expression = GetString(measure, "expression");
                    if (name is null || dataType is null || expression is null)
                    {
                        continue;
                    }

                    measures.Add(new ReportMeasureInventory(
                        extensionName, entityName, name, dataType, expression,
                        GetString(measure, "formatString"), GetString(measure, "description"),
                        GetString(measure, "displayFolder"), GetBoolean(measure, "hidden") ?? false,
                        ReadUnrecognizedReferences(measure), ReadMeasureReferences(measure),
                        path));
                }
            }
        }

        return new ReportExtensionParseResult(
            path, GetString(root, "$schema"), measures.ToArray());
    }

    private static VersionMetadataParseResult ParseVersionMetadata(
        IProjectFileSource source,
        string reportDirectory,
        List<ReportSchemaObservation> schemaObservations)
    {
        var path = ProjectFilePaths.Combine(reportDirectory, "definition", "version.json");
        if (!source.FileExists(path))
        {
            return new VersionMetadataParseResult(null, null);
        }

        using var document = OpenJsonDocument(source, path);
        var root = document.RootElement;
        schemaObservations.Add(PbirSchemaObservationFactory.Create(
            ReportSchemaArtifactKinds.VersionMetadata, path, root));
        return new VersionMetadataParseResult(path, GetString(root, "version"));
    }

    private static bool ReadUnrecognizedReferences(JsonElement measure) =>
        TryGetObject(measure, "references", out var references) &&
        GetBoolean(references, "unrecognizedReferences") == true;

    private static ReportMeasureReferenceInventory[] ReadMeasureReferences(JsonElement measure)
    {
        if (!TryGetObject(measure, "references", out var references) ||
            !references.TryGetProperty("measures", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray()
            .Select(item => new ReportMeasureReferenceInventory(
                GetString(item, "schema"), GetString(item, "entity") ?? string.Empty,
                GetString(item, "name") ?? string.Empty))
            .Where(item => item.Entity.Length > 0 && item.Name.Length > 0)
            .ToArray();
    }

    private static ReportSchemaObservation[] OrderObservations(
        IEnumerable<ReportSchemaObservation> observations) =>
        observations
            .OrderBy(observation => observation.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(observation => observation.ArtifactKind, StringComparer.Ordinal)
            .ToArray();

    private static PageInventory? ParsePage(
        IProjectFileSource source,
        string pageDirectory,
        Dictionary<string, int> pageOrder,
        string? activePageName,
        ThemeInventory theme,
        List<ReportSchemaObservation> schemaObservations)
    {
        var pagePath = ProjectFilePaths.Combine(pageDirectory, "page.json");
        if (!source.FileExists(pagePath))
        {
            return null;
        }

        using var pageDocument = OpenJsonDocument(source, pagePath);
        var pageRoot = pageDocument.RootElement;
        schemaObservations.Add(PbirSchemaObservationFactory.Create(
            ReportSchemaArtifactKinds.Page, pagePath, pageRoot));
        var name = GetString(pageRoot, "name") ?? ProjectFilePaths.GetFileName(pageDirectory);
        var displayName = GetString(pageRoot, "displayName") ?? name;
        var visualsDirectory = ProjectFilePaths.Combine(pageDirectory, "visuals");
        var containers = ParseContainers(source, visualsDirectory, theme, schemaObservations);

        return new PageInventory(
            Name: name,
            DisplayName: displayName,
            RelativePath: pageDirectory,
            DefinitionPath: pagePath,
            SchemaUri: GetString(pageRoot, "$schema"),
            PageType: GetString(pageRoot, "type"),
            PageBinding: ParsePageBinding(pageRoot),
            Order: pageOrder.TryGetValue(name, out var order) ? order : null,
            IsActive: string.Equals(name, activePageName, StringComparison.Ordinal),
            Visibility: GetString(pageRoot, "visibility"),
            DisplayOption: GetString(pageRoot, "displayOption"),
            Width: GetDouble(pageRoot, "width"),
            Height: GetDouble(pageRoot, "height"),
            Filters: ParseFilters(pageRoot),
            FieldReferences: PbirFieldReferenceExtractor.Extract(pageRoot),
            VisualInteractions: ParseVisualInteractions(pageRoot),
            VisualGroups: containers.Groups,
            Visuals: containers.Visuals);
    }

    private static PageBindingInventory? ParsePageBinding(JsonElement pageRoot)
    {
        if (!TryGetObject(pageRoot, "pageBinding", out var pageBinding))
        {
            return null;
        }

        var parameters = new List<PageBindingParameterInventory>();
        if (pageBinding.TryGetProperty("parameters", out var parameterArray) &&
            parameterArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var parameter in parameterArray.EnumerateArray())
            {
                parameters.Add(new PageBindingParameterInventory(
                    Name: GetString(parameter, "name"),
                    BoundFilter: GetString(parameter, "boundFilter")));
            }
        }

        return new PageBindingInventory(
            Name: GetString(pageBinding, "name"),
            Type: GetString(pageBinding, "type"),
            AcceptsFilterContext: GetString(pageBinding, "acceptsFilterContext"),
            Parameters: parameters.ToArray());
    }

    private static ReportFilterInventory[] ParseFilters(JsonElement root)
    {
        if (!TryGetObject(root, "filterConfig", out var filterConfig) ||
            !filterConfig.TryGetProperty("filters", out var filters) ||
            filters.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return filters.EnumerateArray()
            .Where(filter => filter.ValueKind == JsonValueKind.Object)
            .Select(filter => new ReportFilterInventory(
                Name: GetString(filter, "name"),
                Type: GetString(filter, "type"),
                HowCreated: GetString(filter, "howCreated")))
            .ToArray();
    }

    private static ContainerParseResult ParseContainers(
        IProjectFileSource source,
        string visualsDirectory,
        ThemeInventory theme,
        ICollection<ReportSchemaObservation> schemaObservations)
    {
        var containers = source
            .EnumerateFiles(visualsDirectory)
            .Where(file => string.Equals(ProjectFilePaths.GetFileName(file.RelativePath), "visual.json", StringComparison.OrdinalIgnoreCase))
            .Select(file => ParseContainer(source, file.RelativePath, theme, schemaObservations))
            .ToArray();

        return new ContainerParseResult(
            containers.Where(item => item.Visual is not null)
                .Select(item => item.Visual!)
                .OrderBy(visual => visual.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            containers.Where(item => item.Group is not null)
                .Select(item => item.Group!)
                .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static ParsedContainer ParseContainer(
        IProjectFileSource source,
        string visualPath,
        ThemeInventory theme,
        ICollection<ReportSchemaObservation> schemaObservations)
    {
        using var visualDocument = OpenJsonDocument(source, visualPath);
        var visualRoot = visualDocument.RootElement;
        schemaObservations.Add(PbirSchemaObservationFactory.Create(
            ReportSchemaArtifactKinds.VisualContainer, visualPath, visualRoot));
        var name = GetString(visualRoot, "name") ??
                   ProjectFilePaths.GetFileName(ProjectFilePaths.GetDirectoryName(visualPath)) ??
                   ProjectFilePaths.GetFileNameWithoutExtension(visualPath) ??
                   "unknown";
        var schemaUri = GetString(visualRoot, "$schema");
        var parentGroupName = GetString(visualRoot, "parentGroupName");
        TryGetObject(visualRoot, "position", out var position);
        var parsedPosition = ParsePosition(position);

        if (TryGetObject(visualRoot, "visualGroup", out var groupElement))
        {
            return new ParsedContainer(
                Visual: null,
                Group: new VisualGroupInventory(
                    Name: name,
                    DisplayName: GetString(groupElement, "displayName"),
                    GroupMode: GetString(groupElement, "groupMode"),
                    ParentGroupName: parentGroupName,
                    RelativePath: visualPath,
                    SchemaUri: schemaUri,
                    Position: parsedPosition)
                {
                    IsHidden = GetBoolean(visualRoot, "isHidden") ?? false,
                });
        }

        var mobilePath = ProjectFilePaths.Combine(ProjectFilePaths.GetDirectoryName(visualPath), "mobile.json");
        VisualFieldReference[] mobileReferences = [];
        if (source.FileExists(mobilePath))
        {
            using var mobileDocument = OpenJsonDocument(source, mobilePath);
            schemaObservations.Add(PbirSchemaObservationFactory.Create(
                ReportSchemaArtifactKinds.VisualContainerMobileState, mobilePath, mobileDocument.RootElement));
            mobileReferences = PbirFieldReferenceExtractor.Extract(mobileDocument.RootElement);
        }

        return new ParsedContainer(ParseVisual(
            visualRoot,
            visualPath,
            name,
            schemaUri,
            parentGroupName,
            parsedPosition,
            theme,
            mobileReferences), null);
    }

    private static VisualInventory ParseVisual(
        JsonElement visualRoot,
        string visualPath,
        string name,
        string? schemaUri,
        string? parentGroupName,
        VisualPosition position,
        ThemeInventory theme,
        IReadOnlyList<VisualFieldReference> mobileReferences)
    {
        var visualType = TryGetObject(visualRoot, "visual", out var visualElement)
            ? GetString(visualElement, "visualType")
            : null;
        var onCanvasText = PbirVisualTextParser.Parse(visualElement);
        var references = PbirFieldReferenceExtractor.Extract(visualRoot)
            .Concat(mobileReferences)
            .Distinct()
            .ToArray();
        var referenceClassification = PbirVisualReferenceClassifier.Classify(visualRoot, references);
        var persistedFormatting = ThemeFormattingComparisonAnalyzer.Apply(
            visualType,
            PbirVisualFormattingParser.Parse(visualRoot, referenceClassification.Selectors),
            theme.ActiveVisualStyleRules);

        return new VisualInventory(
            Name: name,
            VisualType: visualType,
            RelativePath: visualPath,
            SchemaUri: schemaUri,
            IsHidden: GetBoolean(visualRoot, "isHidden") ?? false,
            ParentGroupName: parentGroupName,
            Position: position,
            Accessibility: PbirVisualAccessibilityParser.Parse(visualElement),
            OnCanvasText: onCanvasText.Text,
            OnCanvasTextIsDynamic: onCanvasText.IsDynamic,
            FieldReferences: referenceClassification.References,
            Actions: PbirVisualActionParser.Parse(visualElement),
            TooltipBindings: PbirVisualTooltipParser.Parse(visualElement))
        {
            FormattingSelectors = referenceClassification.Selectors,
            PersistedFormatting = persistedFormatting,
        };
    }

    private static VisualPosition ParsePosition(JsonElement position) => new(
        X: GetDouble(position, "x"),
        Y: GetDouble(position, "y"),
        Z: GetDouble(position, "z"),
        Width: GetDouble(position, "width"),
        Height: GetDouble(position, "height"),
        TabOrder: GetInteger(position, "tabOrder"));

    private static VisualInteractionInventory[] ParseVisualInteractions(JsonElement pageRoot)
    {
        if (!pageRoot.TryGetProperty("visualInteractions", out var interactions) ||
            interactions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<VisualInteractionInventory>();
        var index = 0;
        foreach (var interaction in interactions.EnumerateArray())
        {
            if (interaction.ValueKind == JsonValueKind.Object)
            {
                result.Add(new VisualInteractionInventory(
                    SourceVisual: GetString(interaction, "source"),
                    TargetVisual: GetString(interaction, "target"),
                    InteractionType: GetString(interaction, "type"),
                    EvidencePath: $"$.visualInteractions[{index}]"));
            }

            index++;
        }

        return result.ToArray();
    }

    private static Dictionary<string, int> ReadPageOrder(JsonElement metadataRoot)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!metadataRoot.TryGetProperty("pageOrder", out var orderElement) ||
            orderElement.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var index = 0;
        foreach (var item in orderElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } pageName)
            {
                result.TryAdd(pageName, index);
            }

            index++;
        }

        return result;
    }

    private static JsonDocument OpenJsonDocument(IProjectFileSource source, string path)
    {
        try
        {
            using var stream = source.OpenRead(path);
            return JsonDocument.Parse(stream);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The PBIR JSON file could not be parsed: {path}", exception);
        }
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

    private static string? GetString(JsonElement parent, string propertyName)
    {
        return parent.ValueKind == JsonValueKind.Object &&
               parent.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static double? GetDouble(JsonElement parent, string propertyName)
    {
        return parent.ValueKind == JsonValueKind.Object &&
               parent.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetDouble(out var number)
            ? number
            : null;
    }

    private static int? GetInteger(JsonElement parent, string propertyName)
    {
        return parent.ValueKind == JsonValueKind.Object &&
               parent.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out var number)
            ? number
            : null;
    }

    private static bool? GetBoolean(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private sealed record ReportExtensionParseResult(
        string? Path,
        string? SchemaUri,
        ReportMeasureInventory[] Measures);

    private sealed record VersionMetadataParseResult(string? Path, string? Version);

    private sealed record ParsedContainer(VisualInventory? Visual, VisualGroupInventory? Group);

    private sealed record ContainerParseResult(VisualInventory[] Visuals, VisualGroupInventory[] Groups);
}

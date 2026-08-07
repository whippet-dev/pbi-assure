using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirReportParser
{
    public static ReportInventory Parse(string projectRoot, string reportDirectory)
    {
        var reportName = Path.GetFileName(reportDirectory);
        if (reportName.EndsWith(".Report", StringComparison.OrdinalIgnoreCase))
        {
            reportName = reportName[..^".Report".Length];
        }

        var relativeReportPath = Path.GetRelativePath(projectRoot, reportDirectory);
        var bookmarkResult = PbirBookmarkParser.Parse(projectRoot, reportDirectory);
        var pagesDirectory = Path.Combine(reportDirectory, "definition", "pages");
        var pagesMetadataPath = Path.Combine(pagesDirectory, "pages.json");

        if (!File.Exists(pagesMetadataPath))
        {
            return new ReportInventory(
                Name: reportName,
                RelativePath: relativeReportPath,
                PagesSchemaUri: null,
                ActivePageName: null,
                Pages: [],
                BookmarksSchemaUri: bookmarkResult.SchemaUri,
                BookmarkOrder: bookmarkResult.BookmarkOrder,
                Bookmarks: bookmarkResult.Bookmarks);
        }

        using var pagesMetadata = OpenJsonDocument(pagesMetadataPath);
        var metadataRoot = pagesMetadata.RootElement;
        var schemaUri = GetString(metadataRoot, "$schema");
        var activePageName = GetString(metadataRoot, "activePageName");
        var pageOrder = ReadPageOrder(metadataRoot);

        var pages = Directory
            .EnumerateDirectories(pagesDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(directory => ParsePage(projectRoot, directory, pageOrder, activePageName))
            .Where(page => page is not null)
            .Cast<PageInventory>()
            .OrderBy(page => page.Order ?? int.MaxValue)
            .ThenBy(page => page.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ReportInventory(
            Name: reportName,
            RelativePath: relativeReportPath,
            PagesSchemaUri: schemaUri,
            ActivePageName: activePageName,
            Pages: pages,
            BookmarksSchemaUri: bookmarkResult.SchemaUri,
            BookmarkOrder: bookmarkResult.BookmarkOrder,
            Bookmarks: bookmarkResult.Bookmarks);
    }

    private static PageInventory? ParsePage(
        string projectRoot,
        string pageDirectory,
        Dictionary<string, int> pageOrder,
        string? activePageName)
    {
        var pagePath = Path.Combine(pageDirectory, "page.json");
        if (!File.Exists(pagePath))
        {
            return null;
        }

        using var pageDocument = OpenJsonDocument(pagePath);
        var pageRoot = pageDocument.RootElement;
        var name = GetString(pageRoot, "name") ?? Path.GetFileName(pageDirectory);
        var displayName = GetString(pageRoot, "displayName") ?? name;
        var visualsDirectory = Path.Combine(pageDirectory, "visuals");
        var visuals = ParseVisuals(projectRoot, visualsDirectory);

        return new PageInventory(
            Name: name,
            DisplayName: displayName,
            RelativePath: Path.GetRelativePath(projectRoot, pageDirectory),
            SchemaUri: GetString(pageRoot, "$schema"),
            Order: pageOrder.TryGetValue(name, out var order) ? order : null,
            IsActive: string.Equals(name, activePageName, StringComparison.Ordinal),
            Visibility: GetString(pageRoot, "visibility"),
            DisplayOption: GetString(pageRoot, "displayOption"),
            Width: GetDouble(pageRoot, "width"),
            Height: GetDouble(pageRoot, "height"),
            Visuals: visuals);
    }

    private static VisualInventory[] ParseVisuals(string projectRoot, string visualsDirectory)
    {
        if (!Directory.Exists(visualsDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(visualsDirectory, "visual.json", SearchOption.AllDirectories)
            .Select(path => ParseVisual(projectRoot, path))
            .OrderBy(visual => visual.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static VisualInventory ParseVisual(string projectRoot, string visualPath)
    {
        using var visualDocument = OpenJsonDocument(visualPath);
        var visualRoot = visualDocument.RootElement;
        var name = GetString(visualRoot, "name") ??
                   Path.GetFileName(Path.GetDirectoryName(visualPath)) ??
                   Path.GetFileNameWithoutExtension(visualPath) ??
                   "unknown";
        var visualType = TryGetObject(visualRoot, "visual", out var visualElement)
            ? GetString(visualElement, "visualType")
            : null;

        TryGetObject(visualRoot, "position", out var position);

        return new VisualInventory(
            Name: name,
            VisualType: visualType,
            RelativePath: Path.GetRelativePath(projectRoot, visualPath),
            SchemaUri: GetString(visualRoot, "$schema"),
            IsHidden: GetBoolean(visualRoot, "isHidden") ?? false,
            Position: new VisualPosition(
                X: GetDouble(position, "x"),
                Y: GetDouble(position, "y"),
                Z: GetDouble(position, "z"),
                Width: GetDouble(position, "width"),
                Height: GetDouble(position, "height"),
                TabOrder: GetInteger(position, "tabOrder")),
            Accessibility: PbirVisualAccessibilityParser.Parse(visualElement),
            FieldReferences: PbirFieldReferenceExtractor.Extract(visualRoot),
            Actions: PbirVisualActionParser.Parse(visualElement));
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

    private static JsonDocument OpenJsonDocument(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
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
}

using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirBookmarkParser
{
    public static PbirBookmarkParseResult Parse(IProjectFileSource source, string reportDirectory)
    {
        var bookmarksDirectory = ProjectFilePaths.Combine(reportDirectory, "definition", "bookmarks");
        if (!source.EnumerateFiles(bookmarksDirectory).Any())
        {
            return new PbirBookmarkParseResult(null, [], []);
        }

        var metadataPath = ProjectFilePaths.Combine(bookmarksDirectory, "bookmarks.json");
        string? schemaUri = null;
        string[] bookmarkOrder = [];
        if (source.FileExists(metadataPath))
        {
            using var metadata = OpenJsonDocument(source, metadataPath);
            schemaUri = GetString(metadata.RootElement, "$schema");
            bookmarkOrder = ReadBookmarkOrder(metadata.RootElement).ToArray();
        }

        var bookmarks = source
            .EnumerateFiles(bookmarksDirectory, recursive: false)
            .Where(file => file.RelativePath.EndsWith(".bookmark.json", StringComparison.OrdinalIgnoreCase))
            .Select(file => ParseBookmark(source, file.RelativePath))
            .OrderBy(bookmark => BookmarkOrder(bookmark.Name, bookmarkOrder))
            .ThenBy(bookmark => bookmark.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PbirBookmarkParseResult(schemaUri, bookmarkOrder, bookmarks);
    }

    private static BookmarkInventory ParseBookmark(IProjectFileSource source, string path)
    {
        using var document = OpenJsonDocument(source, path);
        var root = document.RootElement;
        TryGetObject(root, "options", out var options);
        TryGetObject(root, "explorationState", out var explorationState);
        var name = GetString(root, "name") ?? Path.GetFileNameWithoutExtension(ProjectFilePaths.GetFileNameWithoutExtension(path));

        return new BookmarkInventory(
            Name: name,
            DisplayName: GetString(root, "displayName") ?? name,
            RelativePath: path,
            SchemaUri: GetString(root, "$schema"),
            ActivePageName: GetString(explorationState, "activeSection"),
            ApplyOnlyToTargetVisuals: GetBoolean(options, "applyOnlyToTargetVisuals"),
            TargetVisualNames: GetStringArray(options, "targetVisualNames"),
            CapturedVisualNames: ReadCapturedVisualNames(explorationState),
            SuppressActivePage: GetBoolean(options, "suppressActiveSection"),
            SuppressData: GetBoolean(options, "suppressData"));
    }

    private static string[] ReadCapturedVisualNames(JsonElement explorationState)
    {
        if (!TryGetObject(explorationState, "sections", out var sections))
        {
            return [];
        }

        return sections.EnumerateObject()
            .SelectMany(section => TryGetObject(section.Value, "visualContainers", out var containers)
                ? containers.EnumerateObject().Select(visual => visual.Name)
                : [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> ReadBookmarkOrder(JsonElement parent)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (item.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                {
                    if (child.ValueKind == JsonValueKind.String && child.GetString() is { } childName)
                    {
                        yield return childName;
                    }
                }

                continue;
            }

            if (GetString(item, "name") is { } name)
            {
                yield return name;
            }
        }
    }

    private static int BookmarkOrder(string name, string[] bookmarkOrder)
    {
        for (var index = 0; index < bookmarkOrder.Length; index++)
        {
            if (string.Equals(name, bookmarkOrder[index], StringComparison.Ordinal))
            {
                return index;
            }
        }

        return int.MaxValue;
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

    private static string[] GetStringArray(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .OfType<string>()
            .ToArray();
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

internal sealed record PbirBookmarkParseResult(
    string? SchemaUri,
    IReadOnlyList<string> BookmarkOrder,
    IReadOnlyList<BookmarkInventory> Bookmarks);

using System.Text.Json;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class PbirThemeParser
{
    private static readonly string[] NamedColorProperties =
    [
        "foreground", "foregroundNeutralSecondary", "foregroundNeutralTertiary",
        "background", "backgroundLight", "backgroundNeutral", "tableAccent",
        "good", "neutral", "bad", "maximum", "center", "minimum", "null",
        "hyperlink", "visitedHyperlink", "firstLevelElements", "secondLevelElements",
        "thirdLevelElements", "fourthLevelElements", "secondaryBackground",
    ];

    public static ThemeInventory Parse(IProjectFileSource source, string reportDirectory, JsonElement reportRoot)
    {
        if (!TryGetObject(reportRoot, "themeCollection", out var collection))
        {
            return ThemeInventory.Unavailable;
        }

        var packages = ReadPackages(reportRoot);
        var issues = new List<string>();
        var baseSource = TryGetObject(collection, "baseTheme", out var baseTheme)
            ? ResolveSource(source, reportDirectory, baseTheme, packages, ThemeSourceKinds.SharedBase,
                "$.themeCollection.baseTheme", issues)
            : ThemeInventory.Unavailable.BaseSource;
        var customSource = TryGetObject(collection, "customTheme", out var customTheme)
            ? ResolveSource(source, reportDirectory, customTheme, packages, ThemeSourceKinds.RegisteredCustom,
                "$.themeCollection.customTheme", issues)
            : null;
        var activeCustomName = customSource?.ReferenceName;
        var registered = packages
            .Where(item => string.Equals(item.ItemType, "CustomTheme", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(item.PackageName, "RegisteredResources", StringComparison.OrdinalIgnoreCase))
            .Select(item =>
            {
                var path = TryResolveResourcePath(reportDirectory, item, out var resolvedPath) ? resolvedPath : null;
                return new RegisteredThemeResourceInventory(
                    item.Name,
                    path,
                    ThemeReferenceMatches(item, activeCustomName),
                    path is not null && source.FileExists(path)
                        ? ThemeAvailabilityStates.Available
                        : ThemeAvailabilityStates.ReferencedButUnavailable);
            })
            .ToArray();

        return new ThemeInventory(baseSource, customSource, registered, issues.ToArray())
        {
            ActiveVisualStyleRules = new ThemeRuleIndex(
                (baseSource.Metadata?.VisualStyleRules.Rules ?? [])
                    .Concat(customSource?.Metadata?.VisualStyleRules.Rules ?? [])),
        };
    }

    private static ThemeSourceInventory ResolveSource(
        IProjectFileSource source,
        string reportDirectory,
        JsonElement reference,
        IReadOnlyList<ResourceItem> packages,
        string kind,
        string evidencePath,
        List<string> issues)
    {
        var referenceName = GetString(reference, "name");
        var referenceType = GetString(reference, "type");
        var version = ReadImportVersion(reference);
        if (string.IsNullOrWhiteSpace(referenceName))
        {
            issues.Add($"Theme reference has no name at {evidencePath}.");
            return new ThemeSourceInventory(kind, null, null, null, version,
                ThemeAvailabilityStates.Malformed, evidencePath, null,
                ThemeResolutionOutcomes.ReferenceNameMissing);
        }

        var matchingItems = packages.Where(candidate =>
            (string.IsNullOrWhiteSpace(referenceType) ||
             string.Equals(candidate.PackageName, referenceType, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(candidate.PackageType, referenceType, StringComparison.OrdinalIgnoreCase)) &&
            (string.Equals(candidate.Name, referenceName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(ProjectFilePaths.GetFileName(candidate.Path), referenceName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(ProjectFilePaths.GetFileNameWithoutExtension(candidate.Path), referenceName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (matchingItems.Length == 0)
        {
            issues.Add($"The referenced theme resource '{referenceName}' has no matching resource package item.");
            return new ThemeSourceInventory(kind, referenceName, null, null, version,
                ThemeAvailabilityStates.ReferencedButUnavailable, evidencePath, null,
                ThemeResolutionOutcomes.PackageItemNotFound);
        }

        if (matchingItems.Length > 1)
        {
            issues.Add($"The referenced theme resource '{referenceName}' matches multiple resource package items.");
            return new ThemeSourceInventory(kind, referenceName, null, null, version,
                ThemeAvailabilityStates.Malformed, evidencePath, null,
                ThemeResolutionOutcomes.AmbiguousPackageItem);
        }

        var item = matchingItems[0];

        if (!TryResolveResourcePath(reportDirectory, item, out var resourcePath))
        {
            issues.Add($"The referenced theme resource '{referenceName}' has an invalid package path.");
            return new ThemeSourceInventory(kind, referenceName, null, null, version,
                ThemeAvailabilityStates.Malformed, evidencePath, null,
                ThemeResolutionOutcomes.InvalidPackagePath);
        }

        if (!source.FileExists(resourcePath))
        {
            issues.Add($"The referenced theme resource '{referenceName}' was not found at '{resourcePath}'.");
            return new ThemeSourceInventory(kind, referenceName, null, resourcePath, version,
                ThemeAvailabilityStates.ReferencedButUnavailable, evidencePath, null,
                ThemeResolutionOutcomes.ResourceFileMissing);
        }

        try
        {
            using var stream = source.OpenRead(resourcePath);
            using var document = JsonDocument.Parse(stream);
            var metadata = ParseMetadata(document.RootElement, kind, referenceName, resourcePath);
            return new ThemeSourceInventory(kind, referenceName, metadata.Name, resourcePath, version,
                ThemeAvailabilityStates.Available, evidencePath, metadata,
                ThemeResolutionOutcomes.Resolved);
        }
        catch (JsonException)
        {
            issues.Add($"The referenced theme resource '{referenceName}' contains malformed JSON.");
            return new ThemeSourceInventory(kind, referenceName, null, resourcePath, version,
                ThemeAvailabilityStates.Malformed, evidencePath, null,
                ThemeResolutionOutcomes.InvalidJson);
        }
        catch (IOException)
        {
            issues.Add($"The referenced theme resource '{referenceName}' could not be read.");
            return new ThemeSourceInventory(kind, referenceName, null, resourcePath, version,
                ThemeAvailabilityStates.Malformed, evidencePath, null,
                ThemeResolutionOutcomes.ResourceUnreadable);
        }
    }

    private static ThemeMetadataInventory ParseMetadata(JsonElement root, string sourceKind, string? sourceReference, string? sourcePath)
    {
        var colors = root.TryGetProperty("dataColors", out var dataColors) && dataColors.ValueKind == JsonValueKind.Array
            ? dataColors.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                .Select(item => item.GetString()!)
                .ToArray()
            : [];
        var namedColors = NamedColorProperties
            .Select(name => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? new ThemeNamedColorInventory(name, value.GetString()!, "$." + name)
                : null)
            .Where(item => item is not null)
            .Cast<ThemeNamedColorInventory>()
            .ToArray();
        var textClasses = new List<ThemeTextClassInventory>();
        if (TryGetObject(root, "textClasses", out var classes))
        {
            foreach (var item in classes.EnumerateObject())
            {
                if (item.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                textClasses.Add(new ThemeTextClassInventory(
                    item.Name,
                    GetString(item.Value, "fontFace") ?? GetString(item.Value, "fontFamily"),
                    GetDouble(item.Value, "fontSize"),
                    GetString(item.Value, "color"),
                    $"$.textClasses.{item.Name}"));
            }
        }

        var layer = sourceKind == ThemeSourceKinds.RegisteredCustom ? ThemeLayers.Custom : ThemeLayers.Base;
        var ruleIndex = PbirThemeVisualStyleParser.Parse(root, layer, sourceReference, sourcePath);
        var visibleRuleCount = TryGetObject(root, "visualStyles", out var visualStyles)
            ? CountLeafProperties(visualStyles)
            : 0;
        var visualTypes = ruleIndex.Rules.Where(rule => rule.VisualType != "*")
            .Select(rule => rule.VisualType).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

        return new ThemeMetadataInventory(
            GetString(root, "name"),
            colors,
            namedColors,
            textClasses.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            visibleRuleCount,
            visualTypes,
            GetString(root, "$schema"))
        {
            VisualStyleRules = ruleIndex,
        };
    }

    private static int CountLeafProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array) return element.EnumerateArray().Sum(CountLeafProperties);
        if (element.ValueKind != JsonValueKind.Object) return 0;
        return element.EnumerateObject().Sum(property => property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? CountLeafProperties(property.Value)
            : 1);
    }

    private static ResourceItem[] ReadPackages(JsonElement reportRoot)
    {
        if (!reportRoot.TryGetProperty("resourcePackages", out var packages) || packages.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<ResourceItem>();
        foreach (var package in packages.EnumerateArray())
        {
            var packageName = GetString(package, "name") ?? string.Empty;
            var packageType = GetString(package, "type") ?? packageName;
            if (!package.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in items.EnumerateArray())
            {
                var name = GetString(item, "name");
                var path = GetString(item, "path");
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path))
                {
                    result.Add(new ResourceItem(packageName, packageType, name, path, GetString(item, "type")));
                }
            }
        }

        return result.ToArray();
    }

    private static bool TryResolveResourcePath(string reportDirectory, ResourceItem item, out string resourcePath)
    {
        resourcePath = string.Empty;
        if (string.IsNullOrWhiteSpace(item.PackageName) || string.IsNullOrWhiteSpace(item.Path) ||
            item.PackageName.Contains('/') || item.PackageName.Contains('\\') || item.PackageName.Contains(':') ||
            item.Path.Replace('\\', '/').Split('/').Any(segment => segment == "..") ||
            item.Path.StartsWith('/') || item.Path.Contains(':'))
        {
            return false;
        }

        resourcePath = ProjectFilePaths.Combine(reportDirectory, "StaticResources", item.PackageName, item.Path);
        var expectedRoot = ProjectFilePaths.Combine(reportDirectory, "StaticResources", item.PackageName) + "/";
        return resourcePath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ThemeReferenceMatches(ResourceItem item, string? referenceName) =>
        referenceName is not null &&
        (string.Equals(item.Name, referenceName, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(ProjectFilePaths.GetFileName(item.Path), referenceName, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(ProjectFilePaths.GetFileNameWithoutExtension(item.Path), referenceName, StringComparison.OrdinalIgnoreCase));

    private static ThemeImportVersion? ReadImportVersion(JsonElement reference)
    {
        return TryGetObject(reference, "reportVersionAtImport", out var version)
            ? new ThemeImportVersion(GetString(version, "visual"), GetString(version, "report"), GetString(version, "page"))
            : null;
    }

    private static bool TryGetObject(JsonElement parent, string name, out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? GetDouble(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : null;

    private sealed record ResourceItem(string PackageName, string PackageType, string Name, string Path, string? ItemType);
}

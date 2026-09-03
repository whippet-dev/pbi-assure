using System.Globalization;
using System.Text;
using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Reporting.Exports;

/// <summary>One row per eligible local semantic column or measure, including zero-direct-use objects.</summary>
public static class DataCatalogueCsvRenderer
{
    public static string Render(ProjectInventory inventory, ExportRequest? request = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        request ??= new ExportRequest(ExportPreset.DataCatalogue);
        if (request.Preset != ExportPreset.DataCatalogue)
        {
            throw new ArgumentException("The Data catalogue renderer requires the DataCatalogue preset.", nameof(request));
        }

        var columns = ExportPresetCatalog.ResolveColumns(request);
        var descriptions = columns.Any(column => column.Id == "Description")
            ? DescriptionLookup(inventory)
            : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var analysis = DirectUsageProvenanceAnalyzer.Analyze(inventory);
        var logicalUsagesByObject = LogicalDirectUsageAnalyzer.Analyze(inventory)
            .GroupBy(usage => Key(usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var sourceUsages = inventory.SemanticObjectUsages
            .ToDictionary(usage => Key(usage.SemanticModel, usage.Table, usage.ObjectName, usage.ObjectType), StringComparer.OrdinalIgnoreCase);

        var csv = new StringBuilder();
        CsvWriter.AppendRow(csv, columns.Select(column => column.Header));
        foreach (var summary in analysis.ObjectSummaries
                     .OrderBy(summary => summary.SemanticModel, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(summary => summary.Table, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(summary => summary.ObjectName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(summary => summary.ObjectType, StringComparer.Ordinal))
        {
            var usages = logicalUsagesByObject.GetValueOrDefault(Key(summary.SemanticModel, summary.Table, summary.ObjectName, summary.ObjectType)) ?? [];
            var source = sourceUsages[Key(summary.SemanticModel, summary.Table, summary.ObjectName, summary.ObjectType)];
            CsvWriter.AppendRow(csv, columns.Select(column => column.Id == "Description"
                ? descriptions.GetValueOrDefault(Key(summary.SemanticModel, summary.Table, summary.ObjectName, summary.ObjectType)) ?? string.Empty
                : Value(column.Id, inventory, summary, usages, source)));
        }

        return csv.ToString();
    }

    private static string Value(
        string column,
        ProjectInventory inventory,
        SemanticObjectDirectUsageSummary summary,
        LogicalDirectUsage[] usages,
        SemanticObjectUsage source) => column switch
    {
        "SemanticModel" => summary.SemanticModel,
        "Table" => summary.Table,
        "Object" => summary.ObjectName,
        "ObjectType" => summary.ObjectType,
        "SemanticUsage" => summary.SemanticUsage,
        "ClassificationConfidence" => summary.ClassificationConfidence,
        "UserFacing" => summary.UserFacing,
        "DirectUsageCount" => usages.Length.ToString(CultureInfo.InvariantCulture),
        "ReportCount" => summary.ReportCount.ToString(CultureInfo.InvariantCulture),
        "PageCount" => summary.PageCount.ToString(CultureInfo.InvariantCulture),
        "VisualCount" => summary.VisualCount.ToString(CultureInfo.InvariantCulture),
        "UsageContexts" => Join(summary.UsageContexts),
        "ReportNames" => Join(usages.Select(usage => usage.Report)),
        "PageNames" => Join(usages.Select(usage => usage.Page)),
        "UsageRoles" => Join(summary.UsageRoles),
        "SemanticReason" => SemanticUsagePresentation.DescribeReason(inventory, source) ?? string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unsupported Data catalogue column."),
    };

    // Descriptive metadata belongs to semantic inventory, not usage/classification records.
    private static Dictionary<string, string?> DescriptionLookup(ProjectInventory inventory)
    {
        var descriptions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in inventory.SemanticModels)
        {
            foreach (var table in model.Tables)
            {
                foreach (var column in table.Columns)
                {
                    descriptions.Add(Key(model.Name, table.Name, column.Name, SemanticObjectTypes.Column), column.Description);
                }

                foreach (var measure in table.Measures)
                {
                    descriptions.Add(Key(model.Name, table.Name, measure.Name, SemanticObjectTypes.Measure), measure.Description);
                }
            }
        }

        return descriptions;
    }

    private static string Join(IEnumerable<string?> values) => string.Join(" | ", values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));

    private static string Key(string model, string table, string objectName, string objectType) =>
        string.Join('\u001f', model, table, objectType, objectName);
}

using System.Text;
using PbiAssure.Core.Inventory;

namespace PbiAssure.Reporting;

public static class SemanticUsageCsvRenderer
{
    private static readonly string[] Header =
    [
        "Report",
        "Table",
        "Object",
        "ObjectType",
        "SemanticUsage",
        "SemanticReason",
        "ReportLocationCount",
        "ReportLocations",
        "PowerQueryUsed",
        "PowerQueryConsumers",
        "PowerQueryRoles",
        "PowerQueryEvidence",
        "ReviewCandidate",
    ];

    public static string Render(ProjectInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var csv = new StringBuilder();
        AppendRow(csv, Header);
        foreach (var usage in inventory.SemanticObjectUsages
                     .Where(usage => !inventory.IsSystemGeneratedSemanticObject(usage))
                     .OrderBy(usage => usage.SemanticModel, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(usage => usage.Table, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(usage => usage.ObjectName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(usage => usage.ObjectType, StringComparer.Ordinal))
        {
            var powerQueryUsages = inventory.PowerQueryColumnUsages
                .Where(powerQueryUsage =>
                    string.Equals(powerQueryUsage.SemanticModel, usage.SemanticModel, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(powerQueryUsage.SourceTable, usage.Table, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(powerQueryUsage.SourceColumn, usage.ObjectName, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(powerQueryUsage => string.Join('\u001f',
                    powerQueryUsage.ConsumerQuery,
                    powerQueryUsage.UsageKind,
                    powerQueryUsage.MFunction), StringComparer.OrdinalIgnoreCase)
                .OrderBy(powerQueryUsage => powerQueryUsage.ConsumerQuery, StringComparer.OrdinalIgnoreCase)
                .ThenBy(powerQueryUsage => powerQueryUsage.UsageKind, StringComparer.Ordinal)
                .ThenBy(powerQueryUsage => powerQueryUsage.MFunction, StringComparer.Ordinal)
                .ToArray();
            var locations = usage.DirectReportLocations
                .Select(location => DescribeLocation(inventory, usage, location))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(location => location, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            AppendRow(csv,
            [
                DescribeReport(inventory, usage.SemanticModel),
                usage.Table,
                usage.ObjectName,
                HumanizeIdentifier(usage.ObjectType),
                UsageLabel(usage.UsageState),
                usage.DirectReportLocationCount == 0 ? SemanticUsagePresentation.DescribeReason(inventory, usage) ?? string.Empty : string.Empty,
                usage.DirectReportLocationCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Join(" | ", locations),
                powerQueryUsages.Length == 0 ? "No" : "Yes",
                JoinDistinct(powerQueryUsages.Select(powerQueryUsage => powerQueryUsage.ConsumerQuery)),
                JoinDistinct(powerQueryUsages.Select(powerQueryUsage => PowerQueryRoleLabel(powerQueryUsage.UsageKind))),
                JoinDistinct(powerQueryUsages.Select(powerQueryUsage => powerQueryUsage.MFunction)),
                IsReviewCandidate(usage.UsageState) ? "Yes" : "No",
            ]);
        }

        return csv.ToString();
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string?> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                csv.Append(',');
            }

            var text = value ?? string.Empty;
            if (text.IndexOfAny([',', '"', '\r', '\n']) >= 0)
            {
                csv.Append('"').Append(text.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
            }
            else
            {
                csv.Append(text);
            }

            first = false;
        }

        csv.Append("\r\n");
    }

    private static string DescribeReport(ProjectInventory inventory, string semanticModel)
    {
        var reports = inventory.Reports
            .Where(report => string.Equals(report.ModelConnection.TargetSemanticModelName, semanticModel, StringComparison.OrdinalIgnoreCase))
            .Select(report => report.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return reports.Length == 0 ? semanticModel : string.Join(" | ", reports);
    }

    private static string DescribeLocation(
        ProjectInventory inventory,
        SemanticObjectUsage usage,
        SemanticUsageLocation location)
    {
        var report = inventory.Reports.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, location.Report, StringComparison.OrdinalIgnoreCase));
        var page = report?.Pages.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, location.Page, StringComparison.OrdinalIgnoreCase));
        var visual = page?.Visuals.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, location.Visual, StringComparison.OrdinalIgnoreCase));
        var parts = new List<string>();
        if (inventory.ReportCount > 1)
        {
            parts.Add(report?.Name ?? location.Report);
        }

        if (!string.IsNullOrWhiteSpace(location.Page))
        {
            parts.Add(page?.DisplayName ?? location.Page);
        }

        if (visual is not null)
        {
            parts.Add(VisualDisplayName(visual));
        }
        else if (location.UsageContext == UsageContexts.Drillthrough)
        {
            parts.Add("Drillthrough field");
        }
        else if (string.IsNullOrWhiteSpace(location.Page))
        {
            parts.Add("Report-level use");
        }
        else
        {
            parts.Add(HumanizeIdentifier(location.UsageContext ?? location.LocationKind));
        }

        var role = UsageRoleLabel(usage, location, visual is not null);
        if (!string.IsNullOrWhiteSpace(role))
        {
            parts.Add(role);
        }

        return string.Join(" > ", parts);
    }

    private static string UsageRoleLabel(SemanticObjectUsage usage, SemanticUsageLocation location, bool hasVisual)
    {
        var roles = usage.DirectReportReferences
            .Where(evidence => string.Equals(evidence.Report, location.Report, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evidence.Page, location.Page, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(evidence.Visual, location.Visual, StringComparison.OrdinalIgnoreCase))
            .Select(evidence => evidence.Role)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (roles.Any(role => !string.Equals(role, "filter", StringComparison.OrdinalIgnoreCase)))
        {
            roles = roles.Where(role => !string.Equals(role, "filter", StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        return string.Join(", ", roles.Select(role =>
            string.Equals(role, "filter", StringComparison.OrdinalIgnoreCase) && hasVisual
                ? "Visual filter"
                : HumanizeIdentifier(role!)));
    }

    private static string VisualDisplayName(VisualInventory visual)
    {
        if (visual.Accessibility.TitleIsVisible != false &&
            !visual.Accessibility.TitleTextIsDynamic &&
            !string.IsNullOrWhiteSpace(visual.Accessibility.TitleText))
        {
            return visual.Accessibility.TitleText;
        }

        if (!visual.OnCanvasTextIsDynamic && IsUsefulVisualText(visual.OnCanvasText))
        {
            return visual.OnCanvasText!;
        }

        return HumanizeVisualType(visual.VisualType);
    }

    private static string PowerQueryRoleLabel(string usageKind) => usageKind switch
    {
        PowerQueryColumnUsageKinds.MergeKey => "Merge key",
        PowerQueryColumnUsageKinds.ExpandedColumn => "Expanded column",
        PowerQueryColumnUsageKinds.SelectedColumn => "Selected column",
        PowerQueryColumnUsageKinds.RenamedColumn => "Renamed column",
        PowerQueryColumnUsageKinds.RemovedColumn => "Removed column",
        PowerQueryColumnUsageKinds.TransformedColumn => "Transformed column",
        _ => HumanizeIdentifier(usageKind),
    };

    private static string UsageLabel(string usageState) => usageState switch
    {
        SemanticUsageStates.DirectlyUsed => "Directly used",
        SemanticUsageStates.IndirectlyUsed => "Indirectly used",
        SemanticUsageStates.StructurallyRequired => "Structurally required",
        SemanticUsageStates.UsedOnlyByUnusedBranch => "Used only by unused branch",
        SemanticUsageStates.ApparentlyUnused => "Apparently unused",
        _ => usageState,
    };

    private static bool IsReviewCandidate(string usageState) => usageState is
        SemanticUsageStates.ApparentlyUnused or SemanticUsageStates.UsedOnlyByUnusedBranch;

    private static string JoinDistinct(IEnumerable<string> values) => string.Join(" | ", values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));

    private static bool IsUsefulVisualText(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Count(char.IsLetterOrDigit) >= 2;

    private static string HumanizeVisualType(string? visualType)
    {
        if (string.IsNullOrWhiteSpace(visualType))
        {
            return "Unknown visual type";
        }

        return visualType switch
        {
            "barChart" => "Bar chart",
            "card" => "Card",
            "columnChart" => "Column chart",
            "pivotTable" => "Matrix",
            "slicer" => "Slicer",
            "tableEx" => "Table",
            _ => HumanizeIdentifier(visualType),
        };
    }

    private static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var words = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is '_' or '-')
            {
                if (words.Length > 0 && words[^1] != ' ')
                {
                    words.Append(' ');
                }

                continue;
            }

            if (index > 0 && char.IsUpper(character) && char.IsLower(value[index - 1]))
            {
                words.Append(' ');
            }

            words.Append(words.Length == 0 ? char.ToUpperInvariant(character) : character);
        }

        return words.ToString();
    }
}

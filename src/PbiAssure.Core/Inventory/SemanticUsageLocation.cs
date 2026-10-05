namespace PbiAssure.Core.Inventory;

public sealed record SemanticUsageLocation(
    string Report,
    string? Page,
    string? Visual,
    string LocationKind,
    string? UsageContext)
{
    public static SemanticUsageLocation FromEvidence(SemanticUsageEvidence evidence)
    {
        if (!string.IsNullOrWhiteSpace(evidence.Visual))
        {
            return new SemanticUsageLocation(evidence.Report, evidence.Page, evidence.Visual, "Visual", null);
        }

        return new SemanticUsageLocation(
            evidence.Report,
            evidence.Page,
            Visual: null,
            string.IsNullOrWhiteSpace(evidence.Page) ? "Report" : "Page",
            evidence.UsageContext);
    }

    /// <summary>
    /// The distinct report locations a set of direct-usage evidence amounts to. A page filter that only
    /// restates a drillthrough field on the same page is not a separate location.
    /// </summary>
    public static IReadOnlyList<SemanticUsageLocation> Distinct(IEnumerable<SemanticUsageEvidence> references)
    {
        var locations = references.Select(FromEvidence).Distinct().ToArray();
        var drillthroughPages = locations
            .Where(location => location.Visual is null && location.UsageContext == UsageContexts.Drillthrough)
            .Select(location => $"{location.Report}\u001f{location.Page}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return locations.Where(location =>
            location.Visual is not null ||
            location.UsageContext != UsageContexts.Filter ||
            !drillthroughPages.Contains($"{location.Report}\u001f{location.Page}"))
            .ToArray();
    }
}

using PbiAssure.Core.Inventory;
using PbiAssure.Core.Scanning;

namespace PbiAssure.Reporting;

/// <summary>
/// Optional viewer presentation alongside the inventory, using the existing export interpretation.
/// This neither changes the public inventory schema nor reclassifies semantic usage.
/// </summary>
public static class InvestigationPresentation
{
    public static IReadOnlyList<InvestigationObjectSummary> ObjectSummaries(ProjectInventory inventory) =>
        DirectUsageProvenanceAnalyzer.Analyze(inventory).ObjectSummaries
            .Select(item => new InvestigationObjectSummary(
                item.SemanticModel, item.Table, item.ObjectName, item.ObjectType, item.UserFacing))
            .ToArray();
}

public sealed record InvestigationObjectSummary(
    string SemanticModel, string Table, string ObjectName, string ObjectType, string UserFacing);

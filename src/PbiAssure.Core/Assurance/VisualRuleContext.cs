using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed record VisualRuleContext(
    ReportInventory Report,
    PageInventory Page,
    VisualInventory Visual);

internal static class VisualRuleContexts
{
    public static IEnumerable<VisualRuleContext> Read(ProjectInventory inventory)
    {
        return inventory.Reports
            .SelectMany(report => report.Pages.Select(page => (report, page)))
            .SelectMany(item => item.page.Visuals.Select(visual => new VisualRuleContext(
                item.report,
                item.page,
                visual)));
    }
}

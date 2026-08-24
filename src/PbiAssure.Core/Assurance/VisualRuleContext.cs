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

    public static IEnumerable<VisualRuleContext> ReadEffectivelyVisible(ProjectInventory inventory)
    {
        foreach (var report in inventory.Reports)
        {
            foreach (var page in report.Pages)
            {
                var effectivelyVisiblePaths = VisualGroupHierarchyResolver.Resolve(page)
                    .Where(container => !container.IsGroup && container.IsEffectivelyVisible)
                    .Select(container => container.RelativePath)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var visual in page.Visuals.Where(visual => effectivelyVisiblePaths.Contains(visual.RelativePath)))
                {
                    yield return new VisualRuleContext(report, page, visual);
                }
            }
        }
    }
}

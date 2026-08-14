using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal enum VisualContainerScopeResolution
{
    Root,
    ResolvedGroup,
    MissingGroup,
    AmbiguousGroup,
    Cycle,
}

internal sealed record VisualContainerScope(
    string Name,
    bool IsGroup,
    string RelativePath,
    VisualPosition Position,
    string? ParentGroupName,
    VisualContainerScopeResolution Resolution,
    VisualGroupInventory? ParentGroup)
{
    public bool IsComparable => Resolution is
        VisualContainerScopeResolution.Root or VisualContainerScopeResolution.ResolvedGroup;
}

internal static class VisualGroupHierarchyResolver
{
    public static IReadOnlyList<VisualContainerScope> Resolve(PageInventory page)
    {
        var groupsByName = page.VisualGroups
            .GroupBy(group => group.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        return page.VisualGroups
            .Select(group => ResolveContainer(
                group.Name, true, group.RelativePath, group.Position, group.ParentGroupName, groupsByName))
            .Concat(page.Visuals.Select(visual => ResolveContainer(
                visual.Name, false, visual.RelativePath, visual.Position, visual.ParentGroupName, groupsByName)))
            .ToArray();
    }

    private static VisualContainerScope ResolveContainer(
        string name,
        bool isGroup,
        string relativePath,
        VisualPosition position,
        string? parentGroupName,
        IReadOnlyDictionary<string, VisualGroupInventory[]> groupsByName)
    {
        if (string.IsNullOrWhiteSpace(parentGroupName))
        {
            return new VisualContainerScope(
                name, isGroup, relativePath, position, parentGroupName,
                VisualContainerScopeResolution.Root, ParentGroup: null);
        }

        var resolution = ResolveParent(parentGroupName, groupsByName, new HashSet<string>(StringComparer.Ordinal));
        return new VisualContainerScope(
            name, isGroup, relativePath, position, parentGroupName,
            resolution.Resolution, resolution.ImmediateParent);
    }

    private static ParentResolution ResolveParent(
        string groupName,
        IReadOnlyDictionary<string, VisualGroupInventory[]> groupsByName,
        HashSet<string> visited)
    {
        if (!groupsByName.TryGetValue(groupName, out var matches))
        {
            return new ParentResolution(VisualContainerScopeResolution.MissingGroup, null);
        }

        if (matches.Length != 1)
        {
            return new ParentResolution(VisualContainerScopeResolution.AmbiguousGroup, null);
        }

        if (!visited.Add(groupName))
        {
            return new ParentResolution(VisualContainerScopeResolution.Cycle, null);
        }

        var immediateParent = matches[0];
        if (string.IsNullOrWhiteSpace(immediateParent.ParentGroupName))
        {
            return new ParentResolution(VisualContainerScopeResolution.ResolvedGroup, immediateParent);
        }

        var ancestor = ResolveParent(immediateParent.ParentGroupName, groupsByName, visited);
        return ancestor.Resolution == VisualContainerScopeResolution.ResolvedGroup
            ? new ParentResolution(VisualContainerScopeResolution.ResolvedGroup, immediateParent)
            : new ParentResolution(ancestor.Resolution, null);
    }

    private sealed record ParentResolution(
        VisualContainerScopeResolution Resolution,
        VisualGroupInventory? ImmediateParent);
}

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
    VisualGroupInventory? ParentGroup,
    bool IsEffectivelyVisible)
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
                group.Name, true, group.RelativePath, group.Position, group.ParentGroupName, group.IsHidden, groupsByName))
            .Concat(page.Visuals.Select(visual => ResolveContainer(
                visual.Name, false, visual.RelativePath, visual.Position, visual.ParentGroupName, visual.IsHidden, groupsByName)))
            .ToArray();
    }

    private static VisualContainerScope ResolveContainer(
        string name,
        bool isGroup,
        string relativePath,
        VisualPosition position,
        string? parentGroupName,
        bool isHidden,
        IReadOnlyDictionary<string, VisualGroupInventory[]> groupsByName)
    {
        if (string.IsNullOrWhiteSpace(parentGroupName))
        {
            return new VisualContainerScope(
                name, isGroup, relativePath, position, parentGroupName,
                VisualContainerScopeResolution.Root, ParentGroup: null,
                IsEffectivelyVisible: !isHidden);
        }

        var resolution = ResolveParent(parentGroupName, groupsByName, new HashSet<string>(StringComparer.Ordinal));
        return new VisualContainerScope(
            name, isGroup, relativePath, position, parentGroupName,
            resolution.Resolution, resolution.ImmediateParent,
            IsEffectivelyVisible: !isHidden && resolution.AncestorsAreVisible);
    }

    private static ParentResolution ResolveParent(
        string groupName,
        IReadOnlyDictionary<string, VisualGroupInventory[]> groupsByName,
        HashSet<string> visited)
    {
        if (!groupsByName.TryGetValue(groupName, out var matches))
        {
            return new ParentResolution(VisualContainerScopeResolution.MissingGroup, null, false);
        }

        if (matches.Length != 1)
        {
            return new ParentResolution(VisualContainerScopeResolution.AmbiguousGroup, null, false);
        }

        if (!visited.Add(groupName))
        {
            return new ParentResolution(VisualContainerScopeResolution.Cycle, null, false);
        }

        var immediateParent = matches[0];
        if (string.IsNullOrWhiteSpace(immediateParent.ParentGroupName))
        {
            return new ParentResolution(
                VisualContainerScopeResolution.ResolvedGroup,
                immediateParent,
                AncestorsAreVisible: !immediateParent.IsHidden);
        }

        var ancestor = ResolveParent(immediateParent.ParentGroupName, groupsByName, visited);
        return ancestor.Resolution == VisualContainerScopeResolution.ResolvedGroup
            ? new ParentResolution(
                VisualContainerScopeResolution.ResolvedGroup,
                immediateParent,
                AncestorsAreVisible: !immediateParent.IsHidden && ancestor.AncestorsAreVisible)
            : new ParentResolution(ancestor.Resolution, null, false);
    }

    private sealed record ParentResolution(
        VisualContainerScopeResolution Resolution,
        VisualGroupInventory? ImmediateParent,
        bool AncestorsAreVisible);
}

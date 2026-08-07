namespace PbiAssure.Core.Inventory;

public sealed record SemanticHierarchyInventory(
    string Name,
    bool IsHidden,
    IReadOnlyList<SemanticHierarchyLevelInventory> Levels);

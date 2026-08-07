namespace PbiAssure.Core.Inventory;

public sealed record SemanticMeasureInventory(
    string Name,
    string Expression,
    string? FormatString,
    bool IsHidden);

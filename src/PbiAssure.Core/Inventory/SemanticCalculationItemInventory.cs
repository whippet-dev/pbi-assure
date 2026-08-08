namespace PbiAssure.Core.Inventory;

public sealed record SemanticCalculationItemInventory(
    string Name,
    string Expression,
    string? FormatStringExpression,
    int? Ordinal);

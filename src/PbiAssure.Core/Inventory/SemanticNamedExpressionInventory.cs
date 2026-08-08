namespace PbiAssure.Core.Inventory;

public sealed record SemanticNamedExpressionInventory(
    string Name,
    string Expression,
    string? Kind,
    string RelativePath);

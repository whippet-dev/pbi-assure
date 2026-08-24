namespace PbiAssure.Core.Inventory;

/// <summary>
/// The three optional DAX expressions persisted by a measure-owned TMDL <c>kpi</c> block.
/// </summary>
public sealed record SemanticKpiInventory(
    string? TargetExpression,
    string? StatusExpression,
    string? TrendExpression);

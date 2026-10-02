using System.Text.Json.Serialization;

namespace PbiAssure.Core.Inventory;

public sealed record SemanticCalculationGroupInventory(
    int? Precedence,
    string? SelectionExpression,
    string? MultipleOrEmptySelectionExpression,
    IReadOnlyList<SemanticCalculationItemInventory> Items)
{
    /// <summary>Retained for dependency analysis without changing JSON schema 0.26.</summary>
    [JsonIgnore]
    public string? NoSelectionExpression { get; init; }

    [JsonIgnore]
    public string? NoSelectionFormatStringExpression { get; init; }

    [JsonIgnore]
    public string? MultipleOrEmptySelectionFormatStringExpression { get; init; }

    public int ItemCount => Items.Count;
}

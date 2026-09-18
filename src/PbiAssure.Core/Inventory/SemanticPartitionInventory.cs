using System.Text.Json.Serialization;

namespace PbiAssure.Core.Inventory;

public sealed record SemanticPartitionInventory(
    string Name,
    string SourceType,
    string? Mode,
    string? Expression)
{
    /// <summary>
    /// For an <c>entity</c> partition, the remote object it is backed by. Desktop persists a composite
    /// model's DirectQuery tables this way: no M of their own, only a name inside the source named by
    /// <see cref="ExpressionSource"/>. Retained in process for Power Query lineage; the lineage edge
    /// and data-source attribution are the public evidence.
    /// </summary>
    [JsonIgnore]
    public string? EntityName { get; init; }

    /// <summary>
    /// For an <c>entity</c> partition, the shared expression that connects to the remote source —
    /// the model's <c>expression 'DirectQuery to AS - …'</c>. Null when the partition names none.
    /// </summary>
    [JsonIgnore]
    public string? ExpressionSource { get; init; }
}

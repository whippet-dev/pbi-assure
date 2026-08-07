namespace PbiAssure.Core.Inventory;

public sealed record SemanticPartitionInventory(
    string Name,
    string SourceType,
    string? Mode);

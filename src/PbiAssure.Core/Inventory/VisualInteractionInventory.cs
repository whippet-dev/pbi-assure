namespace PbiAssure.Core.Inventory;

public sealed record VisualInteractionInventory(
    string? SourceVisual,
    string? TargetVisual,
    string? InteractionType,
    string EvidencePath);

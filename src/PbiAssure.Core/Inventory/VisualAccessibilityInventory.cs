namespace PbiAssure.Core.Inventory;

public sealed record VisualAccessibilityInventory(
    bool HasAltText,
    string? AltText,
    bool AltTextIsDynamic,
    bool? TitleIsVisible,
    bool HasConfiguredTitleText,
    string? TitleText,
    bool TitleTextIsDynamic);

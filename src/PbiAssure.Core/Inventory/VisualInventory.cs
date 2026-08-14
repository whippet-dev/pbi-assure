using System.Text.Json.Serialization;

namespace PbiAssure.Core.Inventory;

public sealed record VisualInventory(
    string Name,
    string? VisualType,
    string RelativePath,
    string? SchemaUri,
    bool IsHidden,
    string? ParentGroupName,
    VisualPosition Position,
    VisualAccessibilityInventory Accessibility,
    string? OnCanvasText,
    bool OnCanvasTextIsDynamic,
    IReadOnlyList<VisualFieldReference> FieldReferences,
    IReadOnlyList<VisualActionInventory> Actions,
    IReadOnlyList<VisualTooltipBindingInventory> TooltipBindings)
{
    [JsonIgnore]
    public IReadOnlyList<VisualFormattingSelectorContext> FormattingSelectors { get; init; } = [];

    public IReadOnlyList<PersistedFormattingObservation> PersistedFormatting { get; init; } = [];

    public bool IsExplicitlyExcludedFromTabOrder => Position.TabOrder is < 0;

    public bool HasExplicitTabOrder => Position.TabOrder is >= 0;

    public bool IsInTabOrder => !IsExplicitlyExcludedFromTabOrder;

    public int FieldReferenceCount => FieldReferences.Count;

    public int ActionCount => Actions.Count;

    public int TooltipBindingCount => TooltipBindings.Count;

    public int DistinctFieldCount => FieldReferences
        .Select(FieldIdentity.Create)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}

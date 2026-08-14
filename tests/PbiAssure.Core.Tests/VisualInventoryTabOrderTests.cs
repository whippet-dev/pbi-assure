using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Tests;

public sealed class VisualInventoryTabOrderTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(-2001)]
    [InlineData(-9999000)]
    public void NegativeTabOrderExplicitlyExcludesVisual(int tabOrder)
    {
        var visual = CreateVisual(tabOrder);

        Assert.True(visual.IsExplicitlyExcludedFromTabOrder);
        Assert.False(visual.HasExplicitTabOrder);
        Assert.False(visual.IsInTabOrder);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3125)]
    public void NonNegativeTabOrderIsAnExplicitKeyboardOrderEntry(int tabOrder)
    {
        var visual = CreateVisual(tabOrder);

        Assert.False(visual.IsExplicitlyExcludedFromTabOrder);
        Assert.True(visual.HasExplicitTabOrder);
        Assert.True(visual.IsInTabOrder);
    }

    [Fact]
    public void MissingTabOrderUsesPowerBiDefaultKeyboardInclusion()
    {
        var visual = CreateVisual(null);

        Assert.False(visual.IsExplicitlyExcludedFromTabOrder);
        Assert.False(visual.HasExplicitTabOrder);
        Assert.True(visual.IsInTabOrder);
    }

    private static VisualInventory CreateVisual(int? tabOrder) => new(
        Name: "visual",
        VisualType: "card",
        RelativePath: "visual.json",
        SchemaUri: null,
        IsHidden: false,
        ParentGroupName: null,
        Position: new VisualPosition(null, null, null, null, null, tabOrder),
        Accessibility: new VisualAccessibilityInventory(false, null, false, null, false, null, false),
        OnCanvasText: null,
        OnCanvasTextIsDynamic: false,
        FieldReferences: [],
        Actions: [],
        TooltipBindings: []);
}

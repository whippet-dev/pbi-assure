using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

/// <summary>
/// Resolves references to a measure's KPI components.
///
/// A TMDL <c>kpi</c> block persists no measure of its own, but the engine exposes each of its
/// expressions as a hidden measure named after the owner: <c>_Sales Goal</c> for the target,
/// <c>_Sales Status</c> and <c>_Sales Trend</c> for the other two. Desktop binds visuals to those
/// names and KPI expressions reference them, which is why the Microsoft IT Spend sample carries
/// <c>Fact[_Actual/Plan Goal]</c> both in a matrix and in a status expression.
///
/// Such a name resolves to the owning measure — the KPI is part of that measure, not a separate
/// object — and only when the owner exists in the named table and its kpi block defines that
/// component. A name that merely looks like one stays an ordinary reference and resolves, or fails
/// to, exactly as before. A measure persisted under the component's own name is that measure.
/// </summary>
internal static class KpiComponentReference
{
    private static readonly (string Suffix, Func<SemanticKpiInventory, string?> Expression)[] Components =
    [
        (" Goal", kpi => kpi.TargetExpression),
        (" Status", kpi => kpi.StatusExpression),
        (" Trend", kpi => kpi.TrendExpression),
    ];

    /// <summary>The component names the engine exposes for this measure's kpi block, if it has one.</summary>
    public static IEnumerable<string> ComponentNames(SemanticMeasureInventory measure)
    {
        if (measure.Kpi is not { } kpi)
        {
            yield break;
        }

        foreach (var (suffix, expression) in Components)
        {
            if (!string.IsNullOrWhiteSpace(expression(kpi)))
            {
                yield return "_" + measure.Name + suffix;
            }
        }
    }

    public static SemanticMeasureInventory? FindOwningMeasure(
        SemanticModelInventory model,
        string table,
        string objectName)
    {
        var owningTable = model.Tables.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, table, StringComparison.OrdinalIgnoreCase));
        return owningTable is null ? null : FindOwningMeasure(owningTable, objectName);
    }

    public static SemanticMeasureInventory? FindOwningMeasure(SemanticTableInventory table, string objectName)
    {
        if (!objectName.StartsWith('_') ||
            table.Measures.Any(measure => string.Equals(measure.Name, objectName, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        foreach (var (suffix, expression) in Components)
        {
            if (objectName.Length <= suffix.Length + 1 ||
                !objectName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var ownerName = objectName[1..^suffix.Length];
            var owner = table.Measures.FirstOrDefault(measure =>
                string.Equals(measure.Name, ownerName, StringComparison.OrdinalIgnoreCase));
            if (owner?.Kpi is { } kpi && !string.IsNullOrWhiteSpace(expression(kpi)))
            {
                return owner;
            }
        }

        return null;
    }
}

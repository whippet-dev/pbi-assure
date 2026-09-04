using System.Text.Json.Serialization;

namespace PbiAssure.Core.Inventory;

public sealed record SemanticMeasureInventory(
    string Name,
    string Expression,
    string? FormatString,
    bool IsHidden)
{
    /// <summary>Desktop-authored description, retained in process only; logical lines use LF.</summary>
    [JsonIgnore]
    public string? Description { get; init; }

    /// <summary>
    /// Measure-owned KPI expressions retained only for dependency analysis. They are deliberately not
    /// a public inventory contract: the existing dependency edges provide the user-facing evidence.
    /// </summary>
    [JsonIgnore]
    public SemanticKpiInventory? Kpi { get; init; }

    /// <summary>
    /// Measure-owned Detail Rows DAX retained only for dependency analysis. Table-owned definitions are
    /// not represented because no Desktop evidence currently establishes that form.
    /// </summary>
    [JsonIgnore]
    public string? DetailRowsDefinitionExpression { get; init; }

    /// <summary>
    /// Measure-owned dynamic format-string DAX retained only for dependency analysis. Literal
    /// <see cref="FormatString"/> metadata remains separate and unchanged.
    /// </summary>
    [JsonIgnore]
    public string? FormatStringExpression { get; init; }
}

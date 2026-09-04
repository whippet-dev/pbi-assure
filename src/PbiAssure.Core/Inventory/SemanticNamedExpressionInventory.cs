using System.Text.Json.Serialization;

namespace PbiAssure.Core.Inventory;

public sealed record SemanticNamedExpressionInventory(
    string Name,
    string Expression,
    string? Kind,
    string RelativePath)
{
    /// <summary>True only when persisted M metadata explicitly declares this named expression as a parameter.</summary>
    [JsonIgnore]
    public bool IsParameter { get; init; }

    /// <summary>The literal parameter type persisted in M metadata, without evaluating the expression.</summary>
    [JsonIgnore]
    public string? ParameterType { get; init; }

    /// <summary>The persisted IsParameterQueryRequired value, when present.</summary>
    [JsonIgnore]
    public bool? IsParameterRequired { get; init; }
}

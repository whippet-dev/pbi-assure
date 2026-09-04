using System.Text.Json.Serialization;

namespace PbiAssure.Core.Inventory;

public sealed record PowerQueryUsage(
    string SemanticModel,
    string QueryName,
    string SourceKind,
    string? Table,
    string? Partition,
    string Expression,
    string ArtifactPath,
    string UsageState,
    string? QueryRole,
    bool HasDynamicReferences,
    IReadOnlyList<PowerQueryReferenceEvidence> ReferencedBy)
{
    /// <summary>Whether this named expression is explicitly marked as an M parameter.</summary>
    [JsonIgnore]
    public bool IsParameter { get; init; }

    /// <summary>The literal parameter type persisted in M metadata, without evaluating its value.</summary>
    [JsonIgnore]
    public string? ParameterType { get; init; }

    /// <summary>The persisted IsParameterQueryRequired value, when present.</summary>
    [JsonIgnore]
    public bool? IsParameterRequired { get; init; }

    /// <summary>Tables whose local refresh-policy source expression statically references this parameter.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> RefreshPolicyTables { get; init; } = [];
}

public sealed record PowerQueryReferenceEvidence(
    string FromQueryName,
    string FromSourceKind,
    string? FromTable,
    string? FromPartition,
    string ArtifactPath);

public sealed record PowerQueryDependencyEdge(
    string SemanticModel,
    string FromQueryName,
    string FromSourceKind,
    string? FromTable,
    string? FromPartition,
    string ToQueryName,
    string ToSourceKind,
    string ArtifactPath);

public static class PowerQuerySourceKinds
{
    public const string TablePartition = "TablePartition";

    public const string NamedExpression = "NamedExpression";
}

public static class PowerQueryUsageStates
{
    public const string LoadedToModel = "LoadedToModel";

    public const string SupportingQuery = "SupportingQuery";

    public const string ApparentlyUnused = "ApparentlyUnused";
}

public static class PowerQueryRoles
{
    public const string LoadedAndSupporting = "LoadedAndSupporting";

    public const string LoadedOnly = "LoadedOnly";

    public const string HelperOrStaging = "HelperOrStaging";

    public const string ApparentlyOrphaned = "ApparentlyOrphaned";
}

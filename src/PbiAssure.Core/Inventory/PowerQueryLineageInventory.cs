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
    bool HasDynamicReferences,
    IReadOnlyList<PowerQueryReferenceEvidence> ReferencedBy);

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

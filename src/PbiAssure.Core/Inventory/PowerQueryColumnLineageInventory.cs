namespace PbiAssure.Core.Inventory;

public sealed record PowerQueryColumnUsage(
    string SemanticModel,
    string SourceQuery,
    string SourceTable,
    string? SourcePartition,
    string SourceColumn,
    string? OriginColumn,
    string ConsumerQuery,
    string? ConsumerTable,
    string? ConsumerPartition,
    string UsageKind,
    string MFunction,
    string? StepName,
    string ArtifactPath);

public static class PowerQueryColumnUsageKinds
{
    public const string MergeKey = "MergeKey";

    public const string ExpandedColumn = "ExpandedColumn";

    public const string SelectedColumn = "SelectedColumn";

    public const string RenamedColumn = "RenamedColumn";

    public const string RemovedColumn = "RemovedColumn";

    public const string TransformedColumn = "TransformedColumn";
}

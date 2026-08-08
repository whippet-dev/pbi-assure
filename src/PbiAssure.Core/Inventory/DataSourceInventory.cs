namespace PbiAssure.Core.Inventory;

public sealed record DataSourceInventory(
    string SemanticModel,
    string QueryName,
    string QuerySourceKind,
    string? Table,
    string? Partition,
    string ConnectorFamily,
    string ConnectorFunction,
    string LocationKind,
    string ArtifactPath);

public static class DataSourceLocationKinds
{
    public const string LocalFile = "LocalFile";

    public const string NetworkFile = "NetworkFile";

    public const string RelativeFile = "RelativeFile";

    public const string WebAddress = "WebAddress";

    public const string NamedServer = "NamedServer";

    public const string DynamicOrUnspecified = "DynamicOrUnspecified";
}

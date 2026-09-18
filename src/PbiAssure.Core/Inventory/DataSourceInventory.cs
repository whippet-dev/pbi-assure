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

    /// <summary>
    /// The data is part of the model definition itself — typed in through Enter data or written as a
    /// table literal — so there is no location outside the project to reach.
    /// </summary>
    public const string EmbeddedInModel = "EmbeddedInModel";

    /// <summary>
    /// The connector reaches a cloud service that is identified by the signed-in account and by
    /// navigation steps, not by an address in the call, so there is no location to inspect or
    /// withhold — only the service itself.
    /// </summary>
    public const string OnlineService = "OnlineService";

    public const string DynamicOrUnspecified = "DynamicOrUnspecified";
}

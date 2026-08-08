namespace PbiAssure.Core.Inventory;

public sealed record ReportModelConnectionInventory(
    string DefinitionPath,
    string? SchemaUri,
    string? Version,
    string ConnectionKind,
    string? ConfiguredPath,
    string? TargetSemanticModelPath,
    string? TargetSemanticModelName,
    bool IsTargetAvailableLocally);

public static class ReportModelConnectionKinds
{
    public const string ByPath = "ByPath";

    public const string ByConnection = "ByConnection";

    public const string Unspecified = "Unspecified";
}

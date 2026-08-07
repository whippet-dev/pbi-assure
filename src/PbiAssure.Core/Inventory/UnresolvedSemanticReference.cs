namespace PbiAssure.Core.Inventory;

public sealed record UnresolvedSemanticReference(
    string Report,
    string Page,
    string Visual,
    string Table,
    string ObjectName,
    string ObjectType,
    string? HierarchyName,
    string UsageContext,
    string? Role,
    string EvidencePath);

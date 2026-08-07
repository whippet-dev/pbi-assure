namespace PbiAssure.Core.Inventory;

public sealed record VisualFieldReference(
    string Table,
    string ObjectName,
    string ObjectType,
    string? HierarchyName,
    string UsageContext,
    string? Role,
    string EvidencePath);

namespace PbiAssure.Core.Inventory;

internal static class FieldIdentity
{
    public static string Create(VisualFieldReference reference)
    {
        return Create(
            reference.Table,
            reference.ObjectName,
            reference.ObjectType,
            reference.HierarchyName);
    }

    public static string Create(
        string table,
        string objectName,
        string objectType,
        string? hierarchyName = null)
    {
        return string.Join('\u001f', objectType, table, hierarchyName ?? string.Empty, objectName);
    }
}

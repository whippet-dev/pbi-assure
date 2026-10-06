namespace PbiAssure.Core.Inventory;

/// <summary>
/// The dependency graph's node identity, shared by the scanner and by presentation so both read one
/// graph.
///
/// An ordinary node is its model and its <see cref="FieldIdentity"/>. A report measure belongs to its
/// report as well: two reports bound to one model may each define a report measure of the same entity
/// and name, and those are different measures with different expressions. Its identity therefore adds
/// the owning report's project-relative path. The path, not the report's name, is used because it is
/// what makes a report unique in a project.
/// </summary>
internal static class SemanticNodeIdentity
{
    public static string Create(
        string semanticModel,
        string table,
        string objectName,
        string objectType,
        string? hierarchyName,
        string? report = null)
    {
        var field = FieldIdentity.Create(table, objectName, objectType, hierarchyName);
        return report is null
            ? string.Join('\u001e', semanticModel, field)
            : string.Join('\u001e', semanticModel, field, report);
    }

    /// <summary>
    /// A report's project-relative path as an owner: forward slashes, no leading or trailing separator.
    /// Owners are compared case-insensitively, as every node identity is.
    /// </summary>
    public static string ReportOwner(string reportRelativePath) =>
        reportRelativePath.Replace('\\', '/').Trim('/');
}

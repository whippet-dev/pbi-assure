namespace PbiAssure.Core.Inventory;

public sealed record ReportMeasureInventory(
    string ExtensionName,
    string Entity,
    string Name,
    string DataType,
    string Expression,
    string? FormatString,
    string? Description,
    string? DisplayFolder,
    bool IsHidden,
    bool HasUnrecognizedReferences,
    IReadOnlyList<ReportMeasureReferenceInventory> References,
    string RelativePath);

public sealed record ReportMeasureReferenceInventory(
    string? Schema,
    string Entity,
    string Name)
{
    public bool IsReportMeasureReference => !string.IsNullOrWhiteSpace(Schema);
}

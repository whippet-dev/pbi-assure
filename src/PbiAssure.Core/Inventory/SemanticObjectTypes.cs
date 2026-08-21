namespace PbiAssure.Core.Inventory;

public static class SemanticObjectTypes
{
    public const string Table = "Table";

    public const string Relationship = "Relationship";

    /// <summary>A security role. Like Relationship, it appears as a dependency source, never as a usage record.</summary>
    public const string Role = "Role";

    /// <summary>A perspective. Like Role, it appears as a dependency source, never as a usage record.</summary>
    public const string Perspective = "Perspective";

    /// <summary>A DAX user-defined function. A graph node, never a usage record and never a root.</summary>
    public const string Function = "Function";

    /// <summary>A table-owned refresh policy. A dependency source, never a usage record.</summary>
    public const string RefreshPolicy = "RefreshPolicy";

    public const string Column = "Column";
    public const string Measure = "Measure";
    public const string ReportMeasure = "ReportMeasure";
    public const string HierarchyLevel = "HierarchyLevel";
    public const string CalculationItem = "CalculationItem";
}

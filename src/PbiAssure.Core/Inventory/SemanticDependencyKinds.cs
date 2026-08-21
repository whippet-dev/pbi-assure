namespace PbiAssure.Core.Inventory;

public static class SemanticDependencyKinds
{
    public const string Dax = "Dax";

    public const string SortBy = "SortBy";

    public const string HierarchyLevel = "HierarchyLevel";

    public const string RelationshipEndpoint = "RelationshipEndpoint";

    public const string ContainingTable = "ContainingTable";

    public const string FieldParameter = "FieldParameter";

    public const string CalculationGroupItem = "CalculationGroupItem";

    public const string ReportMeasure = "ReportMeasure";

    /// <summary>A reference from a role table permission filter expression.</summary>
    public const string TablePermission = "TablePermission";

    /// <summary>An explicitly named column-level object-level security permission.</summary>
    public const string ObjectLevelPermission = "ObjectLevelPermission";

    /// <summary>An explicitly named column in a table's incremental-refresh policy.</summary>
    public const string IncrementalRefreshPolicy = "IncrementalRefreshPolicy";

    /// <summary>A reference from a perspective to a model object it exposes.</summary>
    public const string PerspectiveMember = "PerspectiveMember";

    /// <summary>A call to a DAX user-defined function.</summary>
    public const string FunctionCall = "FunctionCall";
}

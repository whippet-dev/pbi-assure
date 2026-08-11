namespace PbiAssure.Core.Inventory;

public static class VisualReferenceOrigins
{
    public const string Binding = "Binding";
    public const string FormattingPropertyExpression = "Formatting property expression";
    public const string FormattingSelectorIdentity = "Formatting selector identity";
    public const string Unknown = "Unknown";
}

public static class VisualReferenceRelevance
{
    public const string Active = "Active";
    public const string HighConfidencePersisted = "High-confidence persisted";
    public const string Ambiguous = "Ambiguous";
}

public static class VisualSelectorKinds
{
    public const string Metadata = "Metadata";
    public const string ScopeId = "ScopeId";
    public const string Wildcard = "Wildcard";
    public const string Total = "Total";
    public const string Id = "Id";
    public const string Unknown = "Unknown";
}

public sealed record VisualFormattingSelectorContext(
    string FormattingObject,
    string? FormattingProperty,
    string SelectorKind,
    string ReferenceRelevance,
    string? Metadata,
    string? MatchedProjectionQueryRef,
    string EvidencePath);

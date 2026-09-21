using System.Text.Json.Serialization;

namespace PbiAssure.Core.Inventory;

public sealed record UnresolvedSemanticDependency(
    string SemanticModel,
    string FromTable,
    string FromObjectName,
    string FromObjectType,
    string? FromHierarchyName,
    string DependencyKind,
    string ReferenceText,
    string Reason,
    string EvidencePath)
{
    /// <summary>
    /// The structured result of resolving <see cref="ReferenceText"/>. This must be used for machine
    /// decisions; <see cref="Reason"/> is retained only as human-readable diagnostic context.
    /// </summary>
    public string ResolutionOutcome { get; init; } = UnresolvedSemanticDependencyResolutionOutcomes.NotFound;

    /// <summary>
    /// The complete set of model objects <see cref="ReferenceText"/> could have bound to, as
    /// <see cref="FieldIdentity"/> keys, when the outcome is Ambiguous and the resolver proved the set
    /// exhaustive for that reference. Null — the default, and always the case for NotFound — means no
    /// such set is known and the doubt is read as reaching the whole model.
    ///
    /// In process only: the record in JSON is unchanged. It is not part of the record's identity as a
    /// reference; duplicates are merged by their persisted fields with their candidate sets unioned.
    /// </summary>
    [JsonIgnore]
    public IReadOnlySet<string>? CandidateTargets { get; init; }
}

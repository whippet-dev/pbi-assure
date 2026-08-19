namespace PbiAssure.Core.Inventory;

/// <summary>
/// How well established a semantic object's usage state is, given the metadata this scan did not
/// analyse. This is orthogonal to <see cref="SemanticUsageStates"/>: the state says what was found, the
/// confidence says whether something skipped could change that answer.
///
/// It describes this scan of this model. It is not a statement about how well the PBI Assure authors
/// understand Power BI — that distinction stays in engineering documentation and never reaches the
/// product surface, because a reader would take it as uncertainty about their own model.
/// </summary>
public static class ClassificationConfidences
{
    /// <summary>Nothing skipped in this model could change the object's usage state.</summary>
    public const string Established = "Established";

    /// <summary>
    /// Metadata was encountered in this model but not analysed, and it could bear on this object's usage
    /// state. The state itself is unchanged and remains the best available answer.
    /// </summary>
    public const string QualifiedByLimitation = "QualifiedByLimitation";
}

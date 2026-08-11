using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Scanning;

internal static class SemanticReportReferencePolicy
{
    public static bool EstablishesDirectUsage(VisualFieldReference reference) =>
        reference.ReferenceOrigin != VisualReferenceOrigins.FormattingSelectorIdentity ||
        reference.ReferenceRelevance != VisualReferenceRelevance.HighConfidencePersisted;
}

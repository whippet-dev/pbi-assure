namespace PbiAssure.Core.Inventory;

public sealed record SemanticUsageLocation(
    string Report,
    string? Page,
    string? Visual,
    string LocationKind,
    string? UsageContext)
{
    public static SemanticUsageLocation FromEvidence(SemanticUsageEvidence evidence)
    {
        if (!string.IsNullOrWhiteSpace(evidence.Visual))
        {
            return new SemanticUsageLocation(evidence.Report, evidence.Page, evidence.Visual, "Visual", null);
        }

        return new SemanticUsageLocation(
            evidence.Report,
            evidence.Page,
            Visual: null,
            string.IsNullOrWhiteSpace(evidence.Page) ? "Report" : "Page",
            evidence.UsageContext);
    }
}

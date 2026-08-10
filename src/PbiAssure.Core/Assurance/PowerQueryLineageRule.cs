using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class PowerQueryLineageRule : IAssuranceRule
{
    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        foreach (var usage in inventory.PowerQueryUsages)
        {
            if (usage.HasDynamicReferences)
            {
                yield return CreateFinding(
                    usage,
                    "PBI-QUERY-001",
                    "This Power Query expression constructs references dynamically, so its complete query lineage cannot be determined from static metadata.",
                    "Review the M expression and confirm every upstream query before making dependency or deletion decisions.");
            }

            if (usage.SourceKind == PowerQuerySourceKinds.NamedExpression &&
                usage.QueryRole == PowerQueryRoles.ApparentlyOrphaned)
            {
                yield return CreateFinding(
                    usage,
                    "PBI-QUERY-002",
                    "No loaded table or supporting query was found to use this reusable Power Query expression.",
                    "Confirm that the query is not used dynamically or by an external process before considering removal.");
            }
        }
    }

    private static AssuranceFinding CreateFinding(
        PowerQueryUsage usage,
        string ruleId,
        string message,
        string recommendation)
    {
        return new AssuranceFinding(
            ruleId,
            "1.0.0",
            AssuranceCategories.ModelIntegrity,
            FindingSeverities.Information,
            message,
            recommendation,
            Report: null,
            Page: null,
            PageDisplayName: null,
            Visual: null,
            SemanticModel: usage.SemanticModel,
            Table: usage.Table,
            ObjectName: usage.QueryName,
            ArtifactPath: usage.ArtifactPath,
            EvidencePaths: ["M expression"],
            AssessmentType: AssessmentTypes.ReviewRequired,
            ReferenceUrl: null);
    }
}

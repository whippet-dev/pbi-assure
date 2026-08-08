using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class MissingSemanticModelRule : IAssuranceRule
{
    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        return inventory.Reports
            .Where(report => report.ModelConnection.ConnectionKind == ReportModelConnectionKinds.ByPath &&
                             !report.ModelConnection.IsTargetAvailableLocally)
            .Select(report => new AssuranceFinding(
                RuleId: "PBI-MODEL-002",
                RuleVersion: "1.0.0",
                Category: AssuranceCategories.ModelIntegrity,
                Severity: FindingSeverities.Error,
                Message: $"The report is configured to use semantic model '{report.ModelConnection.TargetSemanticModelName ?? "at the configured path"}', but that model definition was not found in the scanned project.",
                Recommendation: "Restore the referenced semantic model folder or correct the report's model path, then scan the complete project again.",
                Report: report.Name,
                Page: null,
                PageDisplayName: null,
                Visual: null,
                SemanticModel: report.ModelConnection.TargetSemanticModelName,
                Table: null,
                ObjectName: null,
                ArtifactPath: report.ModelConnection.DefinitionPath,
                EvidencePaths: ["$.datasetReference.byPath.path"],
                AssessmentType: AssessmentTypes.Finding,
                ReferenceUrl: null));
    }
}

using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal sealed class RelationshipReviewRule : IAssuranceRule
{
    public IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory)
    {
        foreach (var model in inventory.SemanticModels)
        {
            var artifactPath = Path.Combine(model.RelativePath, "definition", "relationships.tmdl");
            foreach (var relationship in model.Relationships)
            {
                var endpoints = $"{relationship.FromTable}[{relationship.FromColumn}] and {relationship.ToTable}[{relationship.ToColumn}]";
                if (string.Equals(relationship.CrossFilteringBehavior, "bothDirections", StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreateFinding(
                        "PBI-MODEL-003",
                        "Bidirectional relationship",
                        $"{relationship.FromTable}[{relationship.FromColumn}] ↔ {relationship.ToTable}[{relationship.ToColumn}] filters in both directions.",
                        "Review whether bidirectional filtering is required for this model. It can be intentional, so confirm the expected filter behaviour rather than changing it automatically.",
                        model.Name,
                        relationship,
                        artifactPath,
                        "crossFilteringBehavior");
                }

                if (string.Equals(relationship.FromCardinality, "many", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(relationship.ToCardinality, "many", StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreateFinding(
                        "PBI-MODEL-004",
                        "Many-to-many relationship",
                        $"{endpoints} form a many-to-many relationship.",
                        "Confirm that the many-to-many design and its filter behaviour are intentional. Many-to-many relationships can be valid but may be less straightforward to reason about.",
                        model.Name,
                        relationship,
                        artifactPath,
                        "fromCardinality, toCardinality");
                }
            }
        }
    }

    private static AssuranceFinding CreateFinding(
        string ruleId,
        string findingName,
        string message,
        string recommendation,
        string modelName,
        SemanticRelationshipInventory relationship,
        string artifactPath,
        string property)
    {
        return new AssuranceFinding(
            RuleId: ruleId,
            RuleVersion: "1.0.0",
            Category: AssuranceCategories.ModelIntegrity,
            Severity: FindingSeverities.Information,
            Message: $"{findingName}: {message}",
            Recommendation: recommendation,
            Report: null,
            Page: null,
            PageDisplayName: null,
            Visual: null,
            SemanticModel: modelName,
            Table: null,
            ObjectName: null,
            ArtifactPath: artifactPath,
            EvidencePaths: [$"relationship '{relationship.Name}'.{property}"],
            AssessmentType: AssessmentTypes.ReviewRequired,
            ReferenceUrl: null);
    }
}

using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

public sealed record AssuranceRuleMetadata(
    string RuleId,
    string FriendlyName,
    string Description,
    string Category);

public static class AssuranceRuleCatalog
{
    public static IReadOnlyList<AssuranceRuleMetadata> ActiveRules { get; } =
    [
        new("PBI-ACCESS-001", "Missing alt text", "Checks visuals included in the tab order for configured alt text.", AssuranceCategories.Accessibility),
        new("PBI-ACCESS-002", "Duplicate tab order", "Checks page items in the same group scope for the same explicit tab order.", AssuranceCategories.Accessibility),
        new("PBI-ACCESS-003", "Visual excluded from tab order", "Checks visible, non-decorative visuals that are explicitly excluded from the tab order.", AssuranceCategories.Accessibility),
        new("PBI-ACCESS-004", "Visual title disabled", "Checks keyboard-reachable visuals whose title is explicitly disabled.", AssuranceCategories.Accessibility),
        new("PBI-ACCESS-005", "Drillthrough page missing Back action", "Checks drillthrough pages for an enabled Back action.", AssuranceCategories.Accessibility),

        new("PBI-COMPAT-001", "Q&A visual retirement", "Checks reports for Power BI Q&A visuals that Microsoft is retiring.", AssuranceCategories.Compatibility),
        new("PBI-SOURCE-001", "Local or network file source", "Checks Power Query for file sources that may not be available to other developers or refresh services.", AssuranceCategories.Compatibility),

        new("PBI-MODEL-001", "Unresolved model object reference", "Checks report bindings for model objects that cannot be found in the matching semantic model.", AssuranceCategories.ModelIntegrity),
        new("PBI-MODEL-002", "Referenced semantic model missing", "Checks that a semantic model referenced by project path is available in the scanned project.", AssuranceCategories.ModelIntegrity),
        new("PBI-MODEL-003", "Bidirectional relationship", "Identifies relationships that filter in both directions for review.", AssuranceCategories.ModelIntegrity),
        new("PBI-MODEL-004", "Many-to-many relationship", "Identifies many-to-many relationships for review.", AssuranceCategories.ModelIntegrity),
        new("PBI-MODEL-005", "Reference not found", "Checks explicit model references whose target cannot be found in the same semantic model.", AssuranceCategories.ModelIntegrity),
        new("PBI-QUERY-001", "Dynamic Power Query references", "Checks for Power Query references built dynamically, where complete query dependencies cannot be determined automatically.", AssuranceCategories.ModelIntegrity),
        new("PBI-QUERY-002", "Power Query with no known use", "Checks reusable Power Query expressions with no detected loaded table or supporting query that uses them.", AssuranceCategories.ModelIntegrity),

        new("PBI-NAV-001", "Bookmark target missing", "Checks saved bookmark-action targets for bookmarks that do not exist.", AssuranceCategories.Navigation),
        new("PBI-NAV-002", "Incomplete visual action", "Checks enabled visual actions for a missing or incomplete action type or target.", AssuranceCategories.Navigation),
        new("PBI-NAV-003", "Bookmark page missing", "Checks bookmarks for target pages that do not exist.", AssuranceCategories.Navigation),
        new("PBI-NAV-004", "Bookmark visual missing", "Checks bookmarks for references to visuals that are no longer on the page.", AssuranceCategories.Navigation),
        new("PBI-NAV-005", "Bookmark definition missing", "Checks the report's bookmark list for bookmarks whose definition file is missing.", AssuranceCategories.Navigation),
        new("PBI-NAV-006", "Bookmark not in report list", "Checks bookmark definition files that are not included in the report's bookmark list.", AssuranceCategories.Navigation),
        new("PBI-NAV-007", "Page navigation target missing", "Checks enabled page-navigation actions for destination pages that do not exist.", AssuranceCategories.Navigation),
        new("PBI-NAV-008", "Dynamic visual action", "Identifies visual actions whose type, enabled state or target changes dynamically and needs testing.", AssuranceCategories.Navigation),
        new("PBI-NAV-009", "Drillthrough fields missing", "Checks drillthrough pages that have no configured drillthrough fields.", AssuranceCategories.Navigation),
        new("PBI-NAV-010", "Drillthrough field not bound", "Checks drillthrough fields that are not linked to a page filter.", AssuranceCategories.Navigation),
        new("PBI-NAV-011", "Drillthrough filter missing", "Checks drillthrough fields linked to a page filter that does not exist.", AssuranceCategories.Navigation),
        new("PBI-NAV-012", "Visual interaction endpoint missing", "Checks configured visual interactions whose source or target visual no longer exists.", AssuranceCategories.Navigation),
        new("PBI-NAV-013", "Tooltip target page missing", "Checks report and visual-header tooltips for destination pages that do not exist.", AssuranceCategories.Navigation),
        new("PBI-NAV-014", "Tooltip target not set", "Checks enabled report and visual-header tooltips that have no destination page.", AssuranceCategories.Navigation),
        new("PBI-NAV-015", "Tooltip target is not a tooltip page", "Checks report and visual-header tooltips whose destination is not configured as a Tooltip page.", AssuranceCategories.Navigation),
        new("PBI-NAV-016", "Dynamic tooltip target", "Identifies report and visual-header tooltip targets that change dynamically and need testing.", AssuranceCategories.Navigation),
        new("PBI-NAV-017", "Configured landing page missing", "Checks an explicitly configured landing page for a page that no longer exists in the report.", AssuranceCategories.Navigation),
    ];

    public static AssuranceRuleMetadata? Find(string ruleId) => ActiveRules.FirstOrDefault(rule =>
        string.Equals(rule.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
}

# Rule catalog

PBI Assure rules turn parsed facts into reviewable results. Rule identifiers and versions are stable output contracts. Severity describes likely impact; assessment type separately distinguishes a detected metadata condition from a risk requiring human judgment.

## Implemented rules

| Rule | Version | Category | Severity | Assessment | Condition |
| --- | --- | --- | --- | --- | --- |
| `PBI-MODEL-001` | 1.0.0 | Model integrity | Error | Finding | A report-, page-, or visual-scoped PBIR field reference does not resolve to an object in the matching semantic model. Repeated evidence for the same container and object is grouped. |
| `PBI-COMPAT-001` | 1.0.0 | Compatibility | Warning | Finding | A visual has type `qnaVisual`. Microsoft has announced full Power BI Q&A retirement in December 2026. |
| `PBI-ACCESS-001` | 1.0.0 | Accessibility | Warning | Finding | An object included in the tab order has no non-empty literal or dynamic alt-text expression. |
| `PBI-ACCESS-002` | 1.0.0 | Accessibility | Warning | Finding | Two or more visuals on one page have the same tab-order position. |
| `PBI-ACCESS-003` | 1.0.0 | Accessibility | Information | Review required | A visible, non-hidden-page visual that is not an image, shape, or text box is excluded from tab order. Human review determines whether it is meaningful or interactive. |
| `PBI-ACCESS-004` | 1.0.0 | Accessibility | Information | Review required | A focusable data visual has its title explicitly disabled. Human review determines whether equivalent context and an accessible name remain available. |
| `PBI-ACCESS-005` | 1.0.0 | Accessibility | Information | Review required | A drillthrough page has no enabled Back action in its static visual metadata. Human review determines whether an equivalent keyboard- and screen-reader-operable return mechanism exists. |
| `PBI-NAV-001` | 1.0.0 | Navigation | Error | Finding | An enabled bookmark action targets a bookmark that has no definition in the report. Disabled actions are retained in inventory but do not trigger this rule. |
| `PBI-NAV-002` | 1.0.0 | Navigation | Warning | Finding | An enabled, statically configured action has no type or is missing the target required by its bookmark, page-navigation, or web-URL type. |
| `PBI-NAV-003` | 1.0.0 | Navigation | Error | Finding | A bookmark's active page does not exist in the report. |
| `PBI-NAV-004` | 1.0.0 | Navigation | Warning | Finding | A bookmark targets a visual that does not exist on the bookmark's active page. |
| `PBI-NAV-005` | 1.0.0 | Navigation | Error | Finding | The bookmark index contains a name for which no bookmark definition file exists. |
| `PBI-NAV-006` | 1.0.0 | Navigation | Information | Review required | A bookmark definition exists but is absent from the report's bookmark index. |
| `PBI-NAV-007` | 1.0.0 | Navigation | Error | Finding | An enabled page-navigation action targets a page that does not exist in the report. |
| `PBI-NAV-008` | 1.0.0 | Navigation | Information | Review required | An action contains a dynamic expression that cannot be fully resolved by static metadata analysis. |
| `PBI-NAV-009` | 1.0.0 | Navigation | Error | Finding | A page is configured as a drillthrough destination but has no drillthrough parameters. |
| `PBI-NAV-010` | 1.0.0 | Navigation | Warning | Finding | A drillthrough parameter has no bound page-filter name. |
| `PBI-NAV-011` | 1.0.0 | Navigation | Error | Finding | A drillthrough parameter names a bound page filter that does not exist on the page. |

## Interpretation

Findings identify metadata conditions; they do not certify legal or WCAG conformance. A missing alt-text expression is detectable, but automation cannot determine whether the object is decorative or whether proposed text communicates the right insight. Likewise, a present alt-text expression must still be reviewed for quality.

The report author should test keyboard operation, screen-reader announcements, bookmark states, action targets, drillthrough, tooltips, focus visibility, high contrast, colour use, and accessible data tables in the actual Power BI consumption environment. Navigation findings verify metadata integrity; they do not prove that an action is understandable, keyboard operable, or appropriate in every bookmark state.

## References

- [Microsoft: Design Power BI reports for accessibility](https://learn.microsoft.com/power-bi/create-reports/desktop-accessibility-creating-reports)
- [Microsoft: Create a Q&A visual in a report](https://learn.microsoft.com/power-bi/visuals/power-bi-visualization-q-and-a)
- [Microsoft: Power BI Desktop project report folder and PBIR schemas](https://learn.microsoft.com/power-bi/developer/projects/projects-report)
- [Microsoft: Use report page drillthrough](https://learn.microsoft.com/power-bi/guidance/report-drillthrough)
- [DWP Accessibility Manual: QA tester](https://accessibility-manual.dwp.gov.uk/guidance-for-your-job-role/qa-tester)
- [GOV.UK accessibility requirements](https://www.gov.uk/guidance/accessibility-requirements-for-public-sector-websites-and-apps)

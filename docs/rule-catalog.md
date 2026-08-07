# Rule catalog

PBI Assure rules turn parsed facts into reviewable results. Rule identifiers and versions are stable output contracts. Severity describes likely impact; assessment type separately distinguishes a detected metadata condition from a risk requiring human judgment.

## Implemented rules

| Rule | Version | Category | Severity | Assessment | Condition |
| --- | --- | --- | --- | --- | --- |
| `PBI-MODEL-001` | 1.0.0 | Model integrity | Error | Finding | A PBIR field reference does not resolve to an object in the matching semantic model. Repeated contexts for the same visual and object are grouped. |
| `PBI-COMPAT-001` | 1.0.0 | Compatibility | Warning | Finding | A visual has type `qnaVisual`. Microsoft has announced full Power BI Q&A retirement in December 2026. |
| `PBI-ACCESS-001` | 1.0.0 | Accessibility | Warning | Finding | An object included in the tab order has no non-empty literal or dynamic alt-text expression. |
| `PBI-ACCESS-002` | 1.0.0 | Accessibility | Warning | Finding | Two or more visuals on one page have the same tab-order position. |
| `PBI-ACCESS-003` | 1.0.0 | Accessibility | Information | Review required | A visible, non-hidden-page visual that is not an image, shape, or text box is excluded from tab order. Human review determines whether it is meaningful or interactive. |
| `PBI-ACCESS-004` | 1.0.0 | Accessibility | Information | Review required | A focusable data visual has its title explicitly disabled. Human review determines whether equivalent context and an accessible name remain available. |

## Interpretation

Findings identify metadata conditions; they do not certify legal or WCAG conformance. A missing alt-text expression is detectable, but automation cannot determine whether the object is decorative or whether proposed text communicates the right insight. Likewise, a present alt-text expression must still be reviewed for quality.

The report author should test keyboard operation, screen-reader announcements, bookmark states, drillthrough, tooltips, focus visibility, high contrast, colour use, and accessible data tables in the actual Power BI consumption environment.

## References

- [Microsoft: Design Power BI reports for accessibility](https://learn.microsoft.com/power-bi/create-reports/desktop-accessibility-creating-reports)
- [Microsoft: Create a Q&A visual in a report](https://learn.microsoft.com/power-bi/visuals/power-bi-visualization-q-and-a)
- [DWP Accessibility Manual: QA tester](https://accessibility-manual.dwp.gov.uk/guidance-for-your-job-role/qa-tester)
- [GOV.UK accessibility requirements](https://www.gov.uk/guidance/accessibility-requirements-for-public-sector-websites-and-apps)

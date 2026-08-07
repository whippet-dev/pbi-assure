# Accessibility assurance approach

PBI Assure supports accessibility review; it does not certify that a report is accessible or legally compliant. Automated analysis must be combined with manual WCAG assessment and testing with assistive technology.

Primary reference material:

- [DWP Accessibility Manual: QA tester](https://accessibility-manual.dwp.gov.uk/guidance-for-your-job-role/qa-tester)
- [GOV.UK accessibility requirements](https://www.gov.uk/guidance/accessibility-requirements-for-public-sector-websites-and-apps)
- [Microsoft: Design Power BI reports for accessibility](https://learn.microsoft.com/power-bi/create-reports/desktop-accessibility-creating-reports)

## Result types

- **Finding**: detectable metadata demonstrates a rule failure.
- **Review required**: automation can identify a risk but cannot determine the user impact.
- **Manual test**: the behaviour must be tested through Power BI, a browser, keyboard navigation, a screen reader, or another assistive technology.
- **Not applicable**: evidence shows that the rule does not apply.

Severity and certainty are separate properties. A potentially severe screen-reader problem may still require manual confirmation.

## Candidate automated rules

The first implemented accessibility rules cover alternative text, duplicate tab positions, potentially meaningful visuals excluded from tab order, explicitly disabled data-visual titles, and detectable broken bookmark or page navigation. Their stable identifiers and applicability boundaries are documented in the [rule catalog](rule-catalog.md).

Further candidate rules include:

- Decorative object is included in tab order.
- Tab order appears inconsistent with visual reading order.
- Visual title is duplicated or uninformative.
- Static foreground and background colours do not meet the configured contrast threshold.
- A chart appears to use colour as its only series differentiator.
- Important content appears to be available only through a tooltip.
- Slicer position or styling is inconsistent across similar pages.
- Page contains an excessive number of focusable objects or visuals.
- Media appears to autoplay or lacks detectable supporting text.
- Custom visual requires explicit accessibility verification.

## Required manual checks

- Keyboard operation and visible focus through every report state.
- Screen-reader announcements, reading order, and clarity of alternative text.
- High-contrast and colour-vision-deficiency behaviour.
- Bookmarks, drillthrough, tooltips, popups, and dynamically shown objects.
- Whether the accessible data table communicates an equivalent result.
- Plain language, cognitive load, and whether instructions are understandable.
- Mobile layouts and exported formats when they are part of the service.

## Semantic-model usability

The model itself is not assigned WCAG conformance. A related usability ruleset can nevertheless flag cryptic display names, unexplained acronyms, missing descriptions, visible technical keys, ambiguous measures, misleading formats, unsuitable default summarisation, and inconsistent terminology.

## Evidence requirements

Every implemented rule declares:

- A stable rule ID.
- The rule-pack version.
- The precise report object and property inspected.
- Observed and expected values where safe to include.
- Why the finding matters.
- Remediation guidance.
- Authoritative reference material.
- Whether the result is a detected finding or requires manual confirmation.

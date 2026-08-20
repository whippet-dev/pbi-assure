# No explicit landing-page fixture

This Power BI Desktop-authored PBIP fixture is the paired control for
[`desktop-landing-page`](../desktop-landing-page/README.md).

It has the same three empty synthetic pages and saved active page (Page 2), but no page was configured
with Power BI Desktop's **Set as landing page** command. Its
`landing-page-a-none.Report/definition/pages/pages.json` contains `activePageName` and no
`landingPageName` property.

This confirms that the absence of `landingPageName` is normal Desktop output, not an empty-string value
or an implicit copy of the active page. It must not produce `PBI-NAV-017`.

The third Desktop experiment, which set then removed the landing page, had the same report-definition
bytes as this control and is intentionally not duplicated here.

Local `.pbi` cache, local settings and editor settings are deliberately excluded; see the paired
fixture README for the full provenance and line-ending note.

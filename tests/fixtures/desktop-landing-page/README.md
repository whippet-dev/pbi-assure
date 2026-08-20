# Explicit landing-page fixture

This is a Power BI Desktop-authored PBIP fixture preserving the PBIR shape created when a report page is
set as its **landing page** in Power BI Desktop.

## Provenance

| | |
|---|---|
| Origin | Created in Power BI Desktop specifically to compare the landing-page setting with no explicit landing page |
| Date authored | 2026-08-20 |
| Pages | Three empty synthetic pages: Page 1, Page 2 and Page 3 |
| Saved active page | Page 2 |
| Explicit landing page | Page 3 |
| Data | None |

Desktop saved the following independent values in
`landing-page-b-page3.Report/definition/pages/pages.json`:

```json
"activePageName": "02765201a957c793a2dd",
"landingPageName": "dc911a3561cdc1a069b2"
```

The referenced page files establish that the first internal name is Page 2 and the second is Page 3.
This proves that `landingPageName` is optional explicit landing-page metadata and is not an alias for
`activePageName`.

The paired [no explicit landing-page fixture](../desktop-landing-page-no-explicit/README.md) has the
same active page and page set but no `landingPageName` property. A third experiment in which the setting
was removed was byte-equivalent to that no-setting report definition, so it is intentionally not kept.

## What it does not prove

- A missing landing-page target persisted by Power BI Desktop. Desktop may repair or remove such stale
  metadata, so `PBI-NAV-017` is tested using clearly labelled synthetic malformed PBIR.
- Any Power BI Service runtime behaviour.
- That a report must configure a landing page. No explicit landing page is a valid state.

## Deliberately not committed

The report and semantic-model `.pbi` cache, local settings and editor settings are excluded. They are
machine-local artifacts and are not needed to preserve the Desktop-authored report definition evidence.

The repository stores text with LF line endings through `.gitattributes`; Desktop wrote these files with
CRLF. Their content is otherwise preserved without reformatting or identifier rewriting.

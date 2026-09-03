# Desktop description persistence evidence

Sanitised model-only derivative of Phil's external `pbi-descriptions` PBIP project, inspected on
2026-09-03. Scope: **Table, Column and Measure descriptions only**. The original is not modified.

## Provenance and limits

**[verified by Power BI Desktop-authored fixture]** Phil authored descriptions through the normal
Desktop UI and confirmed save -> fully close -> reopen successfully -> save -> fully close. The
inspected state therefore survived a Desktop reopen. No first-save byte snapshot exists: this is
not evidence of byte-for-byte save stability or of whitespace normalization between saves.

Descriptions are contiguous `///` lines immediately before the declaration, at the declaration's
indentation (no indentation for the table; one tab for its measures/columns). The observed prefix is
`/// `, with one structural separator space. There is no `description:` property, quoting, backtick
fence or closing delimiter. Apostrophe, colon, hyphen and full stop are literal content.

## Objects and exact values

- `TableA`: `Contains test data for description persistence - Desktop authored.`
- `TableA[ColumnA1]`: `Customer's category: used for grouping.`
- `TableA[MeasureA]`: `Returns total sales - \n\nbefore adjustments.` (escaped LF notation).
  The first line has **one content space after the hyphen**. The blank middle line is persisted as
  a tab followed by `/// `; the final line has no trailing space. Do not trim these fixture lines.
- Undescribed: `TableB`, `TableA[ColumnA2]`, `TableA[MeasureB]`, `TableB[ColumnB1]` and
  `TableB[CollumnB2]` (original spelling). No description block or explicit empty value is present.
  Never-set versus explicitly cleared descriptions cannot be distinguished from this evidence.

## Repository derivative

Retained: semantic model definition, database/model/culture declarations, both tables, ordinary
constant measures and tiny inline Enter Data partitions. Removed: lineage tags, report/empty canvas,
PBIP launcher, platform metadata, themes, local/editor settings and binary cache. This model-only
fixture is valid scanner input; the derivative is not claimed to have been reopened in Desktop.
No business data, credentials or source connections are included.

Source table SHA-256 hashes at inspection:

- TableA: `2A0F7FD0671497F9153DE73C1C0BB1841F1D9C5300F0CEBABF3E847B3EBF8663`
- TableB: `83FC02CC731428541EEDB50E6EE804AD3E3AB1999296675D6A2432328F0FE19E`

Source physical lines were CRLF. The repository uses LF per `.gitattributes`; description content and
indentation are preserved. A file-specific whitespace attribute permits the evidenced trailing spaces.
**[design decision]** Core descriptions use LF internally, preserve content spaces/blank lines, and
remain nullable `[JsonIgnore]` inventory metadata. JSON schema stays `0.26`; exports are unchanged.
Data Catalogue exposure is a separate future slice, not part of this fixture or implementation.

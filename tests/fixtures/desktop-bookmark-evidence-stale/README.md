# Desktop bookmark evidence — stale carriers

This small synthetic PBIP fixture records a Power BI Desktop bookmark state after its live report
carriers were removed. It contains only the `People` Enter Data table (`ID`, `Name`, `Region`,
`SecretCategory`, `ControlUnused`) and no relationships or measures.

## Provenance

- Source path: `C:\Users\morty\Downloads\PBI Assure Testing\desktop-bookmark-evidence`
- Authored, saved, fully closed, reopened and saved again in Power BI Desktop before being supplied.
- The exact Desktop version and a byte-for-byte round-trip comparison were not retained.
- Local `.pbi` cache/settings and the source ZIP are intentionally excluded from this repository copy.

## Experiment

The normal page has a table using `People[Name]`. `B1 - Region North` was captured while
`People[Region]` was temporarily a page filter set to North, then that live filter was removed.
`B2 - SecretCategory Red` was captured while a `People[SecretCategory]` slicer was temporarily set to
Red, then that slicer was deleted. Neither bookmark was updated afterwards.

## Persisted and manual evidence

**[verified from persisted files]** `B1` still contains a `People[Region]` filter snapshot and `B2`
still contains `People[SecretCategory]` slicer/filter/projection state, even though neither carrier is
present in the current page definition.

**[manually verified in Power BI Desktop]** after the save/close/reopen/save round trip, both bookmarks
remained visible and clickable but neither changed the full `Name` table. Codex did not independently
perform those Desktop interactions.

## Product consequence

Persisted bookmark field references are not semantic-usage roots by default: this fixture proves that
Desktop can retain stale, inert bookmark metadata. It is paired with
`../desktop-bookmark-evidence-live-carrier/`, where effective state is carried by fields already visible
in ordinary report metadata. Reopen bookmark graph-edge work only with a Desktop-authored, round-tripped
case that remains effective and uniquely carries an otherwise invisible semantic dependency.

The fixture also supplies exact Desktop-authored schema evidence for `bookmarksMetadata/1.0.0` and
`bookmark/2.1.0`.

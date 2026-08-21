# Desktop bookmark evidence — live carriers

This is the paired synthetic Power BI Desktop PBIP state in which the report retains the live carriers
needed for the tested bookmark state. It contains only the `People` Enter Data table (`ID`, `Name`,
`Region`, `SecretCategory`, `ControlUnused`) and no relationships or measures.

## Provenance

- Source path: `C:\Users\morty\Downloads\PBI Assure Testing\desktop-bookmark-evidence-live-carrier`
- Its internal PBIP/report name remains `desktop-bookmark-evidence`; the containing directory identifies
  this live-carrier state.
- The fixture was saved through Power BI Desktop. Local `.pbi` cache/settings are excluded from the
  repository copy. The Desktop version was not retained.

## Experiment

`People[Region]` was restored as an unfiltered page filter without recreating `B1 - Region North`.
`People[SecretCategory]` was restored as a live slicer; `B3 - Live SecretCategory Red` was created, then
the slicer was cleared while left on the page.

**[verified from persisted files]** the current page contains the Region page filter and the
SecretCategory slicer. The bookmark files retain their matching state snapshots.

**[manually verified in Power BI Desktop]** `B1` filtered the table to Alice and Ben, and `B3` restored
the Red slicer selection. Codex did not independently perform those Desktop interactions.

## Current PBI Assure control result

**[verified in repository]** scanning this fixture classifies `People[Name]`, `People[Region]` and
`People[SecretCategory]` as directly used from current live report metadata. `People[ID]` and
`People[ControlUnused]` remain apparently unused. The bookmark contributes no unique usage evidence.

This fixture is paired with `../desktop-bookmark-evidence-stale/`. Together they demonstrate why PBI
Assure must not infer semantic usage from every persisted bookmark field reference.

It also supplies exact Desktop-authored schema evidence for `bookmarksMetadata/1.0.0` and
`bookmark/2.1.0`.

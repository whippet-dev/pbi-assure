# Bookmark semantic-usage evidence

Date: 2026-08-21
Status: evidence banked; bookmark graph edges parked

Evidence labels: **[verified from persisted files]**, **[manually verified in Power BI Desktop]**,
**[verified in repository]**, **[design decision]**.

## Question

Can a Desktop-authored bookmark remain the unique, durable carrier of a semantic-model dependency that
ordinary live report metadata does not show?

## Controlled fixtures

Both fixtures are synthetic PBIP projects containing only the `People` Enter Data table (`ID`, `Name`,
`Region`, `SecretCategory`, `ControlUnused`), with no relationships or measures.

| State | Local source path | Repository fixture |
|---|---|---|
| Removed carriers | `C:\Users\morty\Downloads\PBI Assure Testing\desktop-bookmark-evidence` | `tests/fixtures/desktop-bookmark-evidence-stale` |
| Live carriers | `C:\Users\morty\Downloads\PBI Assure Testing\desktop-bookmark-evidence-live-carrier` | `tests/fixtures/desktop-bookmark-evidence-live-carrier` |

The reports' internal PBIP/report names remain `desktop-bookmark-evidence`; the containing source path
distinguishes the states. Local cache/settings and the source ZIP are excluded. The source was saved,
closed, reopened and saved again in Desktop; exact Desktop version and byte-level round-trip comparison
were not retained.

## Removed-carrier state

**[verified from persisted files]** `B1 - Region North` still holds a `People[Region]` page-filter
snapshot. `B2 - SecretCategory Red` still holds `People[SecretCategory]` slicer/filter/projection state
and a target visual ID no longer present in the live page. The current page has only the `People[Name]`
table visual: neither Region filter nor SecretCategory slicer remains.

**[manually verified in Power BI Desktop]** after the recorded round trip, B1 and B2 remained visible and
clickable but neither altered the full Name table. Codex did not reproduce these interactions.

## Live-carrier state

**[verified from persisted files]** the current page keeps a Region page filter and a SecretCategory
slicer. B1 retains its Region snapshot; B3 captures the Red selection from the retained slicer.

**[manually verified in Power BI Desktop]** B1 restored Alice and Ben, and B3 restored the Red slicer
selection. Codex did not reproduce these interactions.

**[verified in repository]** the current scanner finds the live carriers without reading bookmark state:

| Object | Current usage result |
|---|---|
| `People[Name]` | Directly used — table visual / Values |
| `People[Region]` | Directly used — page filter / Filter |
| `People[SecretCategory]` | Directly used — slicer / Values |
| `People[ID]` | Apparently unused |
| `People[ControlUnused]` | Apparently unused |

## Decision

**[design decision]** Persisted bookmark field references are not semantic-usage roots by default. The
removed-carrier fixture proves that Desktop can retain stale, inert state. Naively traversing every
bookmark reference would therefore create false-positive usage evidence. The live-carrier fixture found
no effective bookmark dependency that the normal page/visual/filter parser missed.

No bookmark graph edges, usage roots, findings, inventory surface or repair guidance are justified now.
This does not prove a unique effective bookmark dependency can never exist.

Reopen bookmark graph-edge work only with a Desktop-authored, round-tripped case that:

1. remains behaviourally effective;
2. references a semantic object absent from current live report/page/visual/filter metadata; and
3. proves the bookmark is the unique durable carrier of that dependency.

## Schema evidence

Both Desktop fixtures declare `bookmarksMetadata/1.0.0` and `bookmark/2.1.0`. Those exact versions are
now fixture-backed `VerifiedExact` schema baselines. Other versions remain recognised but unverified;
this does not assume semantic-version compatibility. JSON and CSV contracts are unchanged.

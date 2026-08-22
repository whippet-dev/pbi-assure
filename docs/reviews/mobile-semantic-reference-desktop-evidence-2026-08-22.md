# Mobile semantic-reference Desktop evidence

## Evidence

**[verified by Desktop-authored fixture]** `desktop-mobile-layout-evidence` persists mobile layout per
existing visual in sibling `mobile.json`. A position-only state contains presentation coordinates only.
A mobile-only dynamic title persists `visualContainerObjects` with the ordinary `Measure` /
`SourceRef` / `Property` expression; it survived save, close, reopen and save. The corresponding
desktop `visual.json` did not contain that reference.

**[verified in current PBI Assure output before this change]** `Mobile Only Title` was incorrectly
`ApparentlyUnused`; `Unused Measure Control` was correctly `ApparentlyUnused`; the summary was 4 Direct
and 2 ApparentlyUnused.

## Decision

Semantic references extracted from `mobile.json` participate in ordinary direct report usage. PBI Assure
does not expose mobile-specific layout, formatting, accessibility or tab-order inventory. Mobile
presentation metadata without a semantic expression has no usage effect.

The committed `mobile-semantic-reference-sanitized` fixture is minimal and sanitised, derived from the
Desktop evidence rather than represented as untouched Desktop output. It establishes the exact observed
`visualContainerMobileState/2.7.0` schema baseline.

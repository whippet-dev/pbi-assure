# Viewer snapshot contract

`PbiAssure.Reporting.Viewer.ViewerSnapshotProjector.Project(ProjectInventory)` produces one
serializable presentation payload. Contract version **1.0** is independent of the public CLI
inventory schema **0.26**. The production-owned investigation assets consume it; the existing
front door, CLI, Desktop workflow and exports still use their original paths.

Core owns parsing, binding, dependencies, reachability, usage, confidence and provenance. The
projector copies those conclusions, reuses Core/Reporting helpers, and supplies navigation targets.
The viewer must not parse DAX/M, infer ownership, reclassify usage, calculate confidence, or recover
missing analysis by matching display names. Filtering, counting explicitly scoped collections,
and progressive traversal of recorded edges remain presentation responsibilities.

## Contract rules

- IDs are opaque, deterministic and scan-local in scope: they survive rescans/reordering of the same
  project identities, but not a model/object rename. Semantic IDs encode Core's `SemanticNodeIdentity`,
  including hierarchy and report owner. Other entity IDs use typed, encoded coordinate tuples;
  evidence-record IDs hash their exact recorded coordinates/content. Do not parse
  IDs in clients. `Targets` contains navigable entities; dependency IDs address evidence records.
- `ViewerReference.Status` is `Resolved`, `Ambiguous` or `Unavailable`. Resolution means a navigation
  destination is identified, not that semantic usage is established. Candidate lists must never be
  treated as multiple confirmed edges. `RecordedObject` is distinct from `ConfirmedUsageTargetIds`.
- Usage state, UserFacing, confidence and Power Query context are separate. Null UserFacing means the
  existing provenance analysis does not publish that result for this kind of object.
- `InApparentlyUnusedReview` is exactly `ApparentlyUnused && !IsSystemGeneratedSemanticObject`.
  It does not include unused-branch objects or imply permission to delete.
- Report-owned measures retain their owner, Core reachability and direct evidence. They do not acquire
  an invented five-state classification. Missing report-scoped reachability stays null; the merged
  public JSON report-measure row is never used as a substitute.
- `Qualifications` comes from `SemanticUsageConfidenceQualifier.Qualifying`. `BoundedReach` means Core
  bounded the qualification scope; `ConservativeModelScope` means Core qualified the result without
  exact reach. Neither is an exact physical-impact claim. Limitation `ReachKind = Unknown` has null
  reach IDs; bounded empty reach has an empty array. Unmapped known keys are retained explicitly.
  Limitation `Context` identifies a definition context, not an affected-object set. There is no
  exhaustive successful-check register, and no limitations in a filtered view does not prove coverage.
- Power Query edges retain their actual identity precision. Missing target partition identity is not
  filled in. Ambiguous candidates stay ambiguous. Column-operation evidence is already mapped to a
  semantic source column by Core; its navigation link does not change that column's usage state.
- Findings keep their stored reference contexts (usage context/role). Those are not complete typed
  entity coordinates. The projector uses recorded report/page/visual or model coordinates where
  sufficient and exposes ambiguity/unavailability otherwise; it does not derive targets from rule IDs.
  `DefinitionContext` retains a known owner even when the finding references a missing destination.
- Summary names state their scope: artifact counts differ from parsed model/report counts; saved field
  references differ from confirmed direct evidence; qualified objects differ from limitation records.

## Ownership and compatibility

The snapshot deliberately selects definitions and evidence used by the accepted workspaces. It also
exposes selected in-memory facts omitted by JSON 0.26: report ownership, structural provenance,
descriptions, parameter metadata, saved-reference provenance and qualification reach. Small existing
definition/evidence records are reused; review changes to those shared leaf types for viewer contract
compatibility. Removing/changing fields or meanings requires a contract-version change and coordinated
consumer update. Additive fields require explicit producer/consumer review; no public schema bump follows.

Parser internals, executable HTML, renderer anchors/caps, raw KPI parser structures, unsupported target
precision and inferred effective security are intentionally absent. KPI dependency evidence remains in
the graph. Unknowns must stay visible. The snapshot is metadata, not redacted or safe-to-share output.

Public JSON imports need a separate compatibility adapter in a later slice: they cannot recover facts
omitted from 0.26. Existing exports continue to use the original `ProjectInventory`, never this snapshot.

## Hosted viewer

The complete module tree lives in `src/PbiAssure.Web/wwwroot/investigation/`. It has no runtime
dependencies on prototypes, fixtures, external libraries or persistent browser storage. The Web SDK
publishes it; `scripts/Publish-Web.mjs` hashes the entire tree and versions its relative module/asset
references. The current front door and legacy report launches are unchanged.

`app.js` handles startup/errors, `hosted-entry.js` handles delivery, `shell.js` owns the shell markup,
and `controller.js` coordinates the existing workspaces. `state.js` owns independent workspace
buckets (with accessors for retained templates). A flat return trail records the origin's context
and the destination's previous context, never the application or return trail itself. Returning
unwinds temporary visits, including visits back into the same workspace, and restores filters,
selection, inspector tab, expansion, disclosures and scroll. Major navigation preserves each
workspace's state; a new investigation from an unrelated workspace starts its own return trail.
The workspace data modules index and
present `snapshot-data.js` projections; inventory-shaped aliases there are for retained templates,
not an alternative public-JSON interpretation.

`navigation.js` resolves recorded references centrally. Controls carry IDs, not display labels;
report measures retain their report-qualified identities. Unavailable/ambiguous/unsupported
destinations are explicit and are never resolved by matching a name, path or finding message.
Where a finding's affected destination cannot open, its separately recorded definition context
can be offered with an explicit explanation. Routing attributes contain only navigation keys,
not copies of entity records. Lineage's starting object and inspected object remain separate.

Qualifications link objects to limitations only through `Qualifications`; conservative model
associations do not imply bounded reach. Other model/report limitations are labelled contextual.
Apparently unused membership uses exactly `UsageState === "ApparentlyUnused" && !IsSystemGenerated`.
UserFacing, confidence, usage reasons, security definitions and Power Query resolution remain
projected facts. Missing UserFacing is unavailable, never derived from presentation metadata.

Remaining string processing is presentation-only: text search/sort, DAX syntax highlighting,
friendly role/category/rule titles, and limitation-area grouping. None selects an entity,
assigns confidence, establishes usage or maps limitation reach. Visible graph traversal and
source expansion follow recorded resolved edges; ambiguous candidates are evidence, not edges.

Open `/investigation/` from a same-origin opener. The child sends
`{type:"pbi-assure-viewer-snapshot-ready"}`. The opener replies to that child, with an exact target
origin, using `{type:"pbi-assure-viewer-snapshot", deliveryId, snapshot, workspace?}`.
`snapshot` is the single deserialized **1.0** envelope; optional `workspace` is `overview`, `unused`
or `check`. The child accepts one message from that opener/origin, validates the contract and replies
`{type:"pbi-assure-viewer-snapshot-ack", deliveryId, accepted}`. Project data never travels in a URL.
No opener, timed-out delivery, invalid input and unsupported versions show an unavailable/error state.
Refresh cannot restore an accepted snapshot. The opener should release its pending delivery after ack.
Public JSON 0.26 import is intentionally unavailable pending an explicit compatibility adapter.

Development only: from `tests/PbiAssure.Viewer.Tests`, run `npm ci`, `npx playwright install chromium`,
then `npm test`. This freshly scans the coverage and Desktop-authored fixtures plus a small
two-report project with identically named report measures, exercises adapters and nested browser
returns, and captures the two desktop sizes. `npm run snapshots` followed by `npm run host`
opens an isolated host at `http://127.0.0.1:4176/test/host.html`; choose a fixture or a local snapshot.
The host/generator are test tooling and are not published. `VIEWER_BROWSER_CHANNEL=chrome` can use
installed Chrome; `VIEWER_WEB_ROOT` can point to a clean published `wwwroot` for publish smoke tests.

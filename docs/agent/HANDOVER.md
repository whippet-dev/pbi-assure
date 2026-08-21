# Handover

Tactical entry point for an incoming coding agent. Read this first, then
[CURRENT_STATE.md](CURRENT_STATE.md), then [DECISIONS.md](DECISIONS.md).

## What just happened

The Desktop-authored `desktop-userelationship-evidence` fixture now banks one active relationship and
three inactive controls: shipping is activated by a report-used measure, referral is referenced only by
an unused measure, and legacy has no local activating call. No feature was added. The current DAX scanner
flattens references and discards built-in function identity and argument pairing, so activation cannot be
inferred safely from endpoint-column co-occurrence. A future bounded `USERELATIONSHIP` call extractor can
resolve an exact unique endpoint pair and reuse the existing source-node reachability. See
[the evidence review](../reviews/userelationship-inactive-relationship-evidence-2026-08-21.md).

TMDL triple-backtick fenced expressions are now read centrally rather than being retained as the opening
fence. Power BI Desktop's serializer can use this form to preserve expression whitespace, including for
RLS `tablePermission` filters. The shared declaration and assignment readers now consume through the
closing fence, remove only the fence's structural left boundary, preserve relative whitespace and stop
before following properties or objects. The generic unanalysed-child scan also skips fenced bodies, so
DAX such as `VAR` is not misreported as unsupported role metadata. This was verified with synthetic
RLS, measure, UDF, calculation-group and M-partition cases; the real work report that prompted it was
not copied into the repository.

The encountered-PBIR-schema compatibility policy is now implemented for the report artifacts PBI Assure
parses. The parser still reads known properties and silently ignores unknown ones; a structured
`ReportSchemaObservation` records its schema evidence without gating parsing. The committed Desktop
fixtures establish an exact baseline for
`definitionProperties/2.0.0`, `report/3.3.0`, `pagesMetadata/1.1.0`, `page/2.1.0` and
`visualContainer/2.11.0` and `visualContainer/2.12.0`. They also contain `versionMetadata/1.0.0` with PBIR definition version `2.0.0`,
which is now retained separately from the existing `definition.pbir` version. The paired Desktop bookmark
fixtures add exact `bookmarksMetadata/1.0.0` and `bookmark/2.1.0` evidence; `reportExtension/1.0.0`
remains synthetic-only.

The adopted boundary is conservative: exact fixture-backed versions are the verified baseline; another
version in a recognised family is unverified, not automatically unsupported; missing, malformed and
unknown-family schema metadata are separate states; and PBIR-Legacy is a separate format rather than an
old modern-PBIR schema. These states describe PBI Assure's coverage, not defects in the user's project.
Non-exact declarations appear as neutral report-scoped **Analysis coverage** information, never as a
Finding. Exact declarations are silent. Bookmark schemas are exact-verified; report-extension schemas
remain recognised-unverified because there is no committed Desktop baseline. The JSON inventory gained
additive report `SchemaObservations`, `VersionMetadataPath` and `PbirDefinitionVersion` properties; CSV
is unchanged. See
[the compatibility review](../reviews/encountered-pbir-schema-compatibility-policy.md).

Report page cards now always start collapsed, including the page that was active when Desktop last saved
the report. A valid explicit `landingPageName` is instead surfaced quietly as a visible **Landing page**
badge on the matching collapsed page card, and is searchable as landing-page metadata. `activePageName`
remains inventory-only saved authoring state; it has no page-card label or automatic expansion. No page
is labelled when no landing page is configured or when the configured target is missing — that remains
the scoped `PBI-NAV-017` Finding. Validation: Release build clean, **436 core + 2 privacy E2E tests
passed**.

Configured custom-theme resources are now checked narrowly for integrity. `PBI-COMPAT-002` is a
Warning / Finding only where `definition/report.json` explicitly names `themeCollection.customTheme`
and its selected local resource cannot be resolved or read. It does not assess theme quality,
consistency, accessibility or completeness; sparse valid themes, base-only reports and unselected
registered resources remain silent. `ThemeSourceInventory.ResolutionOutcome` is an additive JSON field
that records the machine-readable resolution result, so the rule never relies on `ResolutionIssues`
diagnostic prose. The synthetic tests cover no configured theme, resolved sparse themes, unavailable
package items/files, invalid JSON, ambiguous resources, unselected resources, multi-report scoping and
HTML escaping. Validation: Release build clean, **433 core + 2 privacy E2E tests passed**.

Explicit report landing pages are now parsed and checked. Desktop writes the optional
`landingPageName` property only when a page is set as the landing page; it is separate from
`activePageName`, which remains saved authoring state. The paired Desktop fixtures preserve both valid
states: an explicit Page 3 landing page while Page 2 is active, and no landing-page property at all.

`PBI-NAV-017` is an Error / Finding for an explicit nonblank landing-page target that no longer exists.
It provides the internal target name and `pages.json` evidence, with a Desktop-oriented recommendation
to choose an existing landing page or reset the setting. No landing-page property is valid and silent.
The broken-target case is deliberately synthetic; Desktop persistence of stale landing-page metadata is
not claimed. `LandingPageName` is an additive JSON inventory property; semantic usage and CSV are
unchanged. Validation: Release build clean, **426 core + 2 privacy E2E tests passed**.

A compact **Security roles** review is now part of the generated HTML whenever a semantic model defines
roles. It groups roles by model, shows model permission, retained row-level filter DAX, table-level
metadata permissions and explicitly named column permissions, and keeps technical source paths behind
disclosure. Models, roles and filters are ordered deterministically; long and multiline expressions wrap
safely at desktop and mobile widths.

This is an inventory/review surface, not a security verdict. It adds no findings. Explicit column-level
OLS can legitimately make the named semantic object `StructurallyRequired`; table-level OLS never makes
all child columns used. The role/permission inventory is additive JSON and the semantic-usage CSV schema
is unchanged. The page explicitly says that PBI Assure cannot see Power BI Service role membership,
assess effective runtime identity, confirm the overall security design, or determine access through other
paths.

The first post-confidence feature slice is complete: `PBI-MODEL-005` surfaces **Reference not found**
Warnings for evidence-safe unresolved semantic dependencies. It does not surface every retained
`UnresolvedSemanticDependency`. The producer audit found that provenance quality differs materially:
structured sort-by, hierarchy, relationship, perspective and report-measure metadata can support the
claim that PBI Assure could not find an explicitly named target; DAX and field-parameter text extraction
cannot support the same claim without a caveat; and `TablePermission` currently mixes both forms under
one kind.

The public gate is therefore intentionally narrow: eligible structured kind **and** a structured
`NotFound` resolution outcome. `Reason` is retained as explanatory diagnostic text and must never decide
whether a finding is shown. Ambiguous matches are represented structurally and suppressed. Findings are
scoped and grouped by model and source object, ordered deterministically, encoded by the existing Findings
renderer, and appear through the normal Findings search, rule filter and rule catalogue. Analysis coverage
remains about PBI Assure limitations; this rule remains about the user's artifact.

Validation for this slice: Release build clean, **413 core + 2 privacy E2E tests passed**. Four deliberate
mutations proved prose independence, ambiguity classification, producer-evidence safety and the two-part
gate. Rendered HTML was compared before and after the remedial change. No committed Desktop fixture
contains a broken reference, so persistence of these malformed states through a Desktop save remains
unproven and is not claimed.

The semantic usage/classification workstream described below remains closed and unchanged.

Analysis limitations and classification confidence are now visible to a reader. They were recorded
internally for several slices and surfaced nowhere, so an object could show a flat "Apparently unused"
when the scan knew it was "apparently unused, given metadata nobody read".

The design is split by scope, because the hard constraint is noise rather than discoverability. One
unanalysed construct qualifies 21 of 27 objects in the Desktop fixture, so an **Analysis coverage**
section states the cause once per model, and each affected object carries only a small **Qualified**
link back to it. Limitations are grouped by construct rather than by file, since a model emits one file
per role.

Human review of the rendered output then found the architecture sound but the wording still written in
PBI Assure's own vocabulary, so a follow-up slice translated it. The object marker changed from
**Qualified** — precise, but it made readers ask "qualified how? is my column the problem?" — to
**Usage check incomplete**, which names what is incomplete and attributes it to PBI Assure rather than
to the user's object. One vocabulary now runs through the surface; see [CURRENT_STATE.md](CURRENT_STATE.md)
for the full mapping. Two visual defects were fixed with it: navigation wrapped 8 tiles into an orphaned
row, and the coverage disclosure lacked the report's standard `+`/`−` affordance.

Then the "Why" line was fixed. `Sales[Amount]` read *Indirectly used* with **"Referenced by [TotalOf]"**,
naming an uncalled function. The edge was real; the explanation was not. `DescribeReason` took the first
matching incoming edge, which answers "what references this?" rather than "what supports this state?".

The classifier already computed the answer and threw it away: it builds the sets reachable from report
roots and from model-structure roots, assigns states from them, then discarded both. They are now
published as `SemanticNodeReachability` and the reason is filtered by them. Classifications and the
dependency edge set are byte-identical before and after — only the shown evidence changed.

A closeout pass then audited every reason kind against that invariant. The one remaining case —
a relationship endpoint that a report also reaches — was confirmed by synthetic model and fixed: the
relationship explains `StructurallyRequired` only, and every other kind is gated by the same
reachability check. Wording and precedence order are unchanged.

The confidence and coverage presentation reads `ClassificationConfidence` and never re-derives it; the
reason selection reads published reachability and traverses nothing. Neither reimplements
classification.

## This workstream is complete

**The semantic-usage / analysis-confidence sequence is stable and closed.** Everything below is
implemented, fixture-backed where evidence was needed, and verified:

| | |
|---|---|
| `AnalysisLimitation` detection and the construct registry | done |
| `ClassificationConfidence` propagation | done |
| RLS table-permission dependencies | done |
| Artifact-sensitive role limitation precision | done |
| Perspective member dependencies | done |
| DAX user-defined function dependencies | done |
| UDF model-measure consumer fixture | done |
| User-facing **Analysis coverage** section | done |
| Plain-language confidence terminology | done |
| Classification-compatible usage reasons | done |
| Final reason-precedence consistency | done |

Do not reopen any of it without new evidence. The invariants that hold it together are in
[DECISIONS.md](DECISIONS.md) — read them before changing presentation of usage, confidence or reasons.

A remedial consistency pass audited the finished sequence — CI bookkeeping, domain comments, fixture
provenance, the new JSON field, terminology, reason invariants and fixture hygiene. It found three stale
documentation items and no correctness defect, so **the workstream stays closed**. Details in
[CURRENT_STATE.md](CURRENT_STATE.md); the notable one is that
`tests/fixtures/desktop-udf-references/README.md` had claimed its model was written at compatibility
level 1702 with no upgrade. That was inferred from a snapshot taken *after* the functions were authored
and could not have shown the starting level. Corrected to the 1606 → 1702 upgrade the sibling fixture
records.

## State

- **Last verified product state:** `4e187aa` — bookmark semantic-usage evidence. Desktop-authored paired
  fixtures prove that persisted bookmark references can be stale and inert, so bookmarks are not usage
  roots by default.
- **Working tree:** expected clean apart from untracked local review documents; no tracked modifications
- **Verified locally:** Release build succeeded with 0 warnings; **467 core + 2 privacy tests passed**
- **Known exception:** `dotnet format --verify-no-changes` fails with 24 pre-existing whitespace errors
  in two Theme Review files. Unrelated to current work, deliberately not fixed. See
  [CURRENT_STATE.md](CURRENT_STATE.md).

## Immediate next task

The product backlog was freshly re-ranked on 2026-08-20 after the schema, navigation, RLS, UDF and connector
evidence work. The decision and the scored top five are in
[the product-value re-rank](../reviews/product-value-rerank-2026-08-20.md).

**Next recommended evidence task: create the Desktop incremental-refresh policy fixture.** The
inactive-relationship experiment is complete and banked, but implementation is deferred: the current DAX
scanner flattens references and does not retain the `USERELATIONSHIP` call or its paired arguments. A future
bounded extractor can resolve exact endpoint pairs and reuse existing source-measure reachability. See
[the USERELATIONSHIP evidence review](../reviews/userelationship-inactive-relationship-evidence-2026-08-21.md).

Bookmark-only graph edges also remain parked: paired Desktop fixtures prove that stale/inert bookmark
snapshots can retain field references after the live carrier is removed, and effective carriers were already
found through normal live metadata. See
[the bookmark evidence review](../reviews/bookmark-semantic-usage-evidence-2026-08-21.md).

The existing role/perspective
"Why" presentation gap remains a safe small task between evidence slices, but is not higher product value.

Connector expansion and report-level-measure → UDF traversal remain parked on the evidence already recorded;
`PBI-ACCESS-001` remains unchanged pending independently authored intent evidence. Visual-calculation parsing
is not a current top-five task.

## Do not do yet

- Block-level or property-level limitation detection
- Further registry classification or impact changes without new evidence — the always-present files were
  corrected on the evidence recorded in `SemanticDefinitionFileRegistry`; `dataSources.tmdl` stays
  `DependencyEffectUnknown` until it is actually observed
- Malformed-TMDL recovery
- CSV or browser-app surfaces for limitations and confidence — HTML has one; the CSV header is a
  fixed contract and widening it deserves its own decision
- Report-level measure expression parsing or report-measure → UDF traversal without trustworthy source
  model metadata. A persisted expression in a remote report is not proof of a valid dependency.
- Changes to `PBI-ACCESS-001` without independently authored, author-labelled evidence. The local sample
  measurement found only 13 plausible decorative candidates among 216 representative findings; 22 text
  boxes remain metadata-uncertain. See [the measurement](../reviews/access-001-alt-text-measurement.md).
- The pre-existing `dotnet format` whitespace cleanup

Reasons are recorded in [CURRENT_STATE.md](CURRENT_STATE.md) and [DECISIONS.md](DECISIONS.md). Two are
blocking rather than merely sequenced:

1. **The culture impact value rests on a design decision**, not an observation — translations are treated
   as describing objects rather than consuming them. Everything built on qualification inherits that.
2. **`PBI-ACCESS-001` needs better intent evidence before it changes.** The local sample measurement did
   not support a blanket visual-type exemption. Collect independently authored, author-labelled decorative
   examples before changing the rule.

## Missing evidence

- Role-security forms beyond the committed Desktop fixtures — cross-table filters, other OLS permission shapes
- A Desktop-authored local/bound report-measure shape, or trustworthy source-model metadata for a
  `byConnection` report. The observed live-connect UDF expression was rejected and is not a valid
  dependency fixture. See [the evidence review](../reviews/report-level-measure-udf-fixture-design.md).
- Whether a *translated* culture file names model objects, and whether Q&A synonyms constitute usage
- Whether `dataSources.tmdl` is ever emitted by current Desktop
- Independently authored, author-labelled `PBI-ACCESS-001` examples, particularly decorative shapes,
  images and text boxes

## Reading order

1. This file
2. [CURRENT_STATE.md](CURRENT_STATE.md) — what is true now
3. [DECISIONS.md](DECISIONS.md) — what not to reopen
4. `../../tests/fixtures/desktop-semantic-constructs/README.md` and
   `../../tests/fixtures/desktop-udf-references/README.md` — the Desktop evidence, and its limits
5. [../reviews/unsupported-construct-slice1-registry-correction.md](../reviews/unsupported-construct-slice1-registry-correction.md)
   — what slice 1 does and why the registry looks as it does
6. [../design/unsupported-construct-design.md](../design/unsupported-construct-design.md) — only if
   deeper architectural context is needed

Everything needed is in this repository; no external document is required. Do not read every historical
audit before starting a small task — [../reviews/](../reviews/) is there when you need it, not before.

## Before you finish a task

1. Run the build and the tests appropriate to the change; for anything touching scanning or output, run
   the whole suite including the privacy end-to-end tests.
2. Commit logical changes separately, with a message explaining the reasoning, not just the diff.
3. Update [CURRENT_STATE.md](CURRENT_STATE.md) if the factual state changed — build, tests, CI, what is
   implemented, or an evidence gap. A documentation-only commit does not require an update.
4. Update this file with the next task.
5. Update [DECISIONS.md](DECISIONS.md) **only** when a durable decision or established semantic actually
   changed — not for transient observations.
6. Write a task-specific document only when the task genuinely merits one.
7. Never leave a decision recorded only in chat history. Chat is disposable; this repository is the
   project memory.

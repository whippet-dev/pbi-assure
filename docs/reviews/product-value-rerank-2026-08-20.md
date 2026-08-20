# Product-value re-rank

**Date:** 2026-08-20
**Against commit:** `af48499cf33210443b06cdf222160a8d6f16d108`
**Status:** decision record only; no feature, rule, parser, fixture or report change was made.

Evidence labels: **[verified in repository]** was established from current code, fixtures or project
documentation; **[verified in Microsoft documentation]** comes from a current primary Microsoft source;
**[inferred]** is a cautious product judgement; **[design decision]** is the selected direction.

## Executive decision

The next work should be **Desktop evidence for object-level security (OLS) column and table permissions,
followed by a bounded OLS support slice if the fixture confirms the expected TMDL shape**. [design decision]

PBI Assure already parses row-level-security table filters, but deliberately does not read the
`columnPermission` metadata nested beneath the same role/table structure. A column named only by OLS can
therefore remain `ApparentlyUnused`, qualified by an analysis limitation rather than recognised as required
security configuration. [verified in repository] Microsoft now documents creating table- and column-level
OLS directly in Power BI's TMDL view, so the missing Desktop-authored evidence is practical to obtain rather
than dependent on an external editor. [verified in Microsoft documentation]

This ranks above bookmark-state analysis. Bookmarks are more common and their captured filters can also
hide real semantic usage, but PBIR bookmark payloads contain duplicated saved visual state and can retain
stale snapshots. Treating every captured reference as current usage would exchange a false-unused risk for
systematic over-reporting. OLS object names are explicit configuration and have a substantially cleaner
evidence boundary. [design decision]

The recent report-measure/UDF and connector investigations materially affected this ranking: an attractive
feature is not next merely because code can be imagined for it, and a broad coverage label is not a gap when
measurement finds no missing external connectors. The next slice must begin with evidence that can change
or stop the proposed implementation. [design decision]

## Scoring method

Scores use 1–5, where 5 is better for product value. `Precision` means lower false-positive risk and
`Delivery` means lower implementation cost. Impact and the severity of a missed issue carry more weight than
ease of delivery; the scores are decision aids, not a mathematical product score.

| Rank | Candidate | Impact | Missed severity | Evidence readiness | Precision | Delivery | Distinctiveness | Testability |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | OLS table/column permissions | 5 | 5 | 4 | 5 | 3 | 4 | 5 |
| 2 | Bookmark-captured semantic usage | 5 | 4 | 3 | 2 | 1 | 5 | 3 |
| 3 | Inactive-relationship activation through `USERELATIONSHIP` | 4 | 3 | 4 | 3 | 3 | 5 | 5 |
| 4 | Incremental-refresh policy review | 4 | 4 | 3 | 4 | 3 | 4 | 4 |
| 5 | Explain role/perspective structural usage | 3 | 2 | 5 | 5 | 5 | 3 | 5 |

## Ranked top five

### 1. Object-level security permissions — evidence first, then implementation

**Problem.** OLS restricts tables or columns and their metadata for a role. PBI Assure currently retains
role table-filter DAX but skips `columnPermission`; its RLS surface explicitly says complete OLS and column
permissions are not assessed. [verified in repository] Microsoft describes OLS as protection for sensitive
tables/columns and notes that a visual using an inaccessible object appears broken to the affected viewer.
[verified in Microsoft documentation]

**Treatment.** Inventory and usage analysis, not a security verdict. Retain table/column permissions in the
role inventory, show them in the existing security review, and treat explicitly protected objects as
structural roots. Do not create an OLS finding merely because OLS exists, and do not imply effective access
can be assessed without Power BI Service role membership.

**Evidence.** The parser already encounters and deliberately skips this construct; synthetic tests contain
representative text. What is still required is one minimal Desktop-authored, save/reopen fixture proving
current serialization for both `metadataPermission: none` on a table and on a `columnPermission`.

**Likely size:** medium. It is additive inventory/JSON, TMDL parsing, graph roots, limitation qualification,
HTML security review and focused tests. No new DAX parser is required.

### 2. Bookmark-captured semantic usage — measurement and fixture before design

**Problem.** Report bookmarks can save filters, slicer state, sort/drill state and visual state. [verified in
Microsoft documentation] PBI Assure validates bookmark navigation but deliberately does not use captured
bookmark state as semantic usage. An object used only by a bookmark can therefore look apparently unused.
[verified in repository]

**Treatment.** If the evidence supports it, add carefully reconciled usage evidence—not a bookmark finding
and not a rule that says a bookmark is unused. Prefer only state that the bookmark is configured to apply,
and deduplicate saved copies against current visual state.

**Evidence.** Sales & Returns provides sizeable local bookmark payloads with repeated `activeProjections`,
so it is useful for measuring duplication but not sufficient as a controlled truth set. First create a
Desktop fixture where one field exists only in a bookmark's enabled Data state, plus controls with Data
disabled and deliberately stale state. Measure unique bookmark-only references across existing local
projects before choosing graph semantics.

**Likely size:** large. Bookmark scope, selected/all-visual behavior, stale snapshots and friendly evidence
locations all require reconciliation. This remains second because it affects the core usage promise, not
because it is ready to code.

### 3. Inactive relationship activation — fixture, then review-only support

**Problem.** The relationship review shows active state, but PBI Assure does not identify calls to
`USERELATIONSHIP`. Microsoft documents that the function identifies an existing relationship by its two
endpoint columns and enables it for a calculation. [verified in Microsoft documentation] Developers cannot
currently answer “which calculations activate this inactive relationship?” from the assurance report.

**Treatment.** Add relationship usage evidence and calculation locations. A later observation for an
inactive relationship with no detected activation must be Information/Review required and say “not found in
the analysed project”, never “unused”: calculation groups, external/thin reports and unanalysed consumers
remain real boundaries.

**Evidence.** Existing relationship endpoints, active state and DAX expressions are available. A Desktop
fixture should contain Order Date (active), Ship Date (inactive), one measure using `USERELATIONSHIP`, and a
second inactive relationship with no local activation. It should also cover reversed argument order.

**Likely size:** medium. It needs function-aware DAX extraction and endpoint matching, but not a new graph.

### 4. Incremental-refresh policy review — verify the gap before implementing

**Problem.** Incremental refresh is production-critical configuration, while the current Power Query review
does not identify refresh policies. Microsoft requires case-sensitive `RangeStart` and `RangeEnd` Date/Time
parameters and query filters before a policy is configured. [verified in Microsoft documentation]

**Treatment.** Start with neutral inventory in Power Query: which tables carry a policy and which parameter
queries support it. Only a configuration state that Desktop can actually persist and that the project proves
invalid should become a finding. PBI Assure cannot prove query folding from project metadata, so it must not
claim refresh will succeed.

**Evidence.** Existing M dependency parsing may already make `RangeStart`/`RangeEnd` reachable when they are
correctly used, so the older suspected `PBI-QUERY-002` false positive is not established. A Desktop fixture
and a scan of its pre/post-policy states must answer that before any code is proposed.

**Likely size:** medium after evidence; potentially no implementation if the fixture exposes no useful,
reliably testable distinction beyond inventory.

### 5. Explain role/perspective structural usage — implementable small polish

**Problem.** Role-filter and perspective dependencies already classify objects as Structurally required,
but an object reached only through either source has no user-facing “Why” line. The result is correct but
less explainable than relationship and sort-by cases. [verified in repository]

**Treatment.** Presentation only: reuse retained role/perspective evidence to say, for example, “Needed by
the Regional Manager security filter” or “Included in the Sales View perspective.” Do not change usage
states, graph reachability or confidence.

**Evidence.** Existing Desktop-authored role and perspective fixtures and current dependency kinds are
sufficient. **Likely size:** small, with renderer tests and fixture HTML review.

This is a useful contained task while evidence is being gathered for the higher-ranked work, but it does
not outrank a missing security dependency or a core usage blind spot merely because it is easy.

## Not now

| Candidate | Decision |
|---|---|
| Report-level measure → UDF | Parked. The Desktop experiment produced a rejected expression in a remote `byConnection` report with no source model; synthetic traversal would overclaim. |
| Connector-family expansion | Parked. Measurement across 40 projects found no unrecognised external-source family. Repeat only when a concrete class-A source call appears. |
| `PBI-ACCESS-001` changes | Parked. The sample measurement did not support a blanket decorative-type exception; author-labelled intent evidence is still missing. |
| Visual-calculation parsing | Not top five. Microsoft states that ordinary visual calculations can reference only items already placed on the visual, reducing the normal model-usage blind spot. The remaining unread UDF-consumer path is real but schema-specific, large and currently caveated. |
| Mobile-layout assurance | Not yet. Mobile layouts are important specialist review surfaces, but Microsoft describes them as arrangements of the report page's existing visuals. Establish the PBIR shape and a concrete missed assurance outcome before adding another layout surface. |
| “Unused bookmark”, DAX complexity, visual-count thresholds, missing descriptions/display folders | Do not add generic best-practice findings. Intent and acceptable thresholds are not provable from saved metadata and would dilute high-confidence assurance. |
| Broader Theme Review or rendered contrast | Keep the bounded current specialist review. Conditional formatting, data and final rendering prevent a general static conformance claim. |
| Calculation groups, field parameters, UDF definitions, perspectives | Core dependency support already exists. Improve proven edges or explanations rather than reopening completed architecture. |
| OLS compliance/security verdicts | Out of scope. Project files do not contain Power BI Service role membership or effective runtime identity. |

## Exact immediate next task

Create a redistribution-safe **Desktop-authored OLS evidence fixture**; do not implement OLS parsing in the
same task.

1. In a new PBIP/TMDL project, create small synthetic `Employee` and `Confidential` tables. Keep
   `Employee[Salary]` and the `Confidential` table absent from visuals, measures, relationships and sort-by
   settings so the OLS references are their only intended structural evidence.
2. Add an ordinary visual using a harmless control field such as `Employee[Name]`.
3. In Power BI Desktop TMDL view, apply a `RestrictedViewer` role containing:
   - `columnPermission Salary` with `metadataPermission: none` beneath `tablePermission Employee`; and
   - `metadataPermission: none` beneath `tablePermission Confidential`.
4. Save as PBIP, close, reopen and save a copy. Record the Desktop version and whether the role definition
   is byte-stable across the round trip.
5. Inspect `definition/roles/*.tmdl`, the affected table definitions and `model.tmdl`. Confirm that the
   permission names are explicit and whether Desktop emits any additional role metadata.
6. Scan the unchanged fixture with current PBI Assure and record the JSON usage states, role limitation and
   HTML security review. This pins the pre-implementation behavior without accepting it as a new baseline.
7. Only if the serialization is deterministic and the permission target identity is unambiguous, design the
   medium OLS slice described above.

Microsoft's current OLS instructions and syntax are documented in
[Object-level security (OLS)](https://learn.microsoft.com/en-us/fabric/security/service-admin-object-level-security).

## Next sequence

1. **OLS Desktop fixture and evidence review** — the immediate task above.
2. **Bookmark-only usage experiment and local-corpus measurement** — decide whether any saved state is safe
   enough to become graph evidence.
3. **`USERELATIONSHIP` Desktop fixture and design** — then add relationship activation locations before
   considering a review observation.

The small role/perspective explanation improvement can be taken between evidence tasks if a contained
implementation slice is needed; it must not displace the evidence gathering.

## Primary references checked

- [Object-level security (OLS)](https://learn.microsoft.com/en-us/fabric/security/service-admin-object-level-security)
- [Create report bookmarks in Power BI](https://learn.microsoft.com/en-us/power-bi/create-reports/desktop-bookmarks)
- [`USERELATIONSHIP` function](https://learn.microsoft.com/en-us/dax/userelationship-function-dax)
- [Configure incremental refresh](https://learn.microsoft.com/en-us/power-bi/connect-data/incremental-refresh-overview)
- [Visual calculations overview](https://learn.microsoft.com/en-us/power-bi/transform-model/desktop-visual-calculations-overview)
- [Mobile layout view in Power BI](https://learn.microsoft.com/en-us/power-bi/create-reports/power-bi-create-mobile-optimized-report-mobile-layout-view)

## Scope

Investigation and product triage only. No scanner, rule, renderer, schema, fixture or test behavior changed.

# Product-value re-rank

**Date:** 2026-08-21

**Against commit:** `b47881424e4b320cefa45161f3a3c72ab39b2fe8`

**Status:** updated after the bounded aggregation-mapping implementation. The ranking remains a decision
record; the delivered aggregation item is recorded below.

Evidence labels: **[verified in repository]** means established from the current code, committed
fixtures or retained review evidence. **[inferred]** is a cautious product judgement. **[design
decision]** is the selected direction.

## Executive decision

The TMSL (`model.bim`) input-format investigation is complete. Current Power BI Desktop can retain a
PBIP whose local semantic model is stored as `model.bim`; PBI Assure now rejects that unsupported local
format before normal analysis/output rather than presenting incomplete results or a false model-reference
Finding. It does not implement a TMSL parser. See
[the evidence review](tmsl-model-bim-desktop-evidence-2026-08-21.md).

This had ranked first because a supported-looking PBIP with a whole semantic model in an unread format
could otherwise receive incomplete semantic inventory and unused-object conclusions. The evidence
confirmed the path and justified an early supported-input boundary rather than a speculative parser.
[verified in repository]

Aggregation mapping support is now complete. Desktop's Manage aggregations UI persists explicit
column-owned `alternateOf` metadata, which PBI Assure now treats as a narrow structural dependency between
the configured aggregation and detail columns. This protects exact resolved endpoints from
`ApparentlyUnused` without claiming a runtime aggregation hit, improved performance or Power BI Service
behaviour. See [the evidence review](aggregation-alternateof-desktop-evidence-2026-08-21.md).

The mobile semantic-reference slice is now complete: mobile-only formatting expressions participate in
ordinary direct semantic usage, while layout-only state remains outside the product surface. See
[the evidence review](mobile-semantic-reference-desktop-evidence-2026-08-22.md).

The desktop formatting semantic-reference audit is evidence-banked with no implementation required:
the existing generic PBIR extractor already covers the tested title, subtitle, conditional-colour,
background, analytics and conditional-icon shapes. See
[the evidence review](desktop-formatting-semantic-reference-evidence-2026-08-22.md).

KPI and measure Detail Rows dependency support is now complete. A round-tripped Desktop fixture
establishes the three measure-KPI expressions and a measure-owned `detailRowsDefinition`; their DAX
references now use the ordinary graph path, with no new usage state or output contract. See
[the evidence review](kpi-detail-rows-desktop-evidence-2026-08-24.md).

The previous ranking is substantially complete: OLS, incremental refresh, inactive-relationship
activation, the role/perspective explanation and the relevant schema work are now delivered. Bookmark
semantic usage and report-level-measure expression parsing were investigated and deliberately parked.

## Scoring model

Scores are 1–5. Higher is better for **impact**, **missed-result severity**, **evidence readiness**,
**precision**, **delivery** (smaller/safer delivery), **distinctiveness** and **testability**. The score
orders discovery work; it is not a promise that every candidate should be implemented.

| Rank | Candidate | Impact | Severity | Evidence | Precision | Delivery | Distinctive | Testable | Next action |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | TMSL `model.bim` input coverage | 5 | 5 | 5 | 5 | 5 | 5 | 5 | Complete: safe local input gate |
| 2 | Aggregation mappings (`alternateOf`) | 4 | 4 | 5 | 5 | 5 | 4 | 5 | Complete: bounded structural mapping support |
| 3 | Mobile semantic references | 4 | 4 | 5 | 5 | 5 | 5 | 5 | Complete: direct usage only |
| 4 | KPI and measure Detail Rows DAX dependencies | 3 | 3 | 5 | 5 | 4 | 3 | 5 | Complete: bounded measure metadata support |
| 5 | Culture/Q&A linguistic metadata boundary | 3 | 2 | 2 | 3 | 3 | 3 | 4 | Desktop fixture |
| 6 | Visual-calculation dependency evidence | 3 | 3 | 1 | 2 | 1 | 3 | 2 | Keep evidence-bounded |
| 7 | `EXTERNALMEASURE` consequence check | 2 | 3 | 2 | 3 | 2 | 3 | 3 | Focused investigation |
| 8 | `dataSources.tmdl` / connector follow-up | 2 | 2 | 1 | 2 | 2 | 2 | 3 | Keep parked |

## Ranked candidates

### 1. TMSL `model.bim` input coverage — complete

**Problem and user outcome.** The semantic-definition registry recognises `model.bim` as the TMSL
alternative to the TMDL `definition/` folder, but does not parse it. It correctly records a
whole-model limitation with a potentially dependency-bearing impact. A project in that format therefore
cannot receive PBI Assure's normal semantic inventory, dependency graph or unused-object analysis.
[verified in repository]

**Why it ranks first.** If current Desktop still emits this layout through an ordinary project workflow,
this is an input-coverage boundary affecting the core promise of the product, not a marginal new rule.
The report can already explain the limitation honestly, but that is weaker than supporting a common
format. The cost is intentionally not underestimated: a TMSL parser would be a separate, potentially
large product decision.

**Evidence and treatment.** Paired Desktop fixtures establish TMSL save/reopen retention and explicit
TMDL upgrade. The current release stops before normal analysis if it encounters a local `model.bim`; a
future TMSL parser remains a separate, evidence-led product decision.

**Risk.** Do not treat a hand-authored `.bim` file, an old downloaded artifact or a format name in
documentation as proof that current Power BI Desktop users need this support. Do not claim TMDL and TMSL
are semantically interchangeable without fixture evidence.

**Approximate size:** investigation small; any implementation large. **Action:** investigate.

### 2. Aggregation mappings (`alternateOf`) — complete

**Problem and user outcome.** `alternateOf` is an explicit aggregation mapping between an aggregation
table column and its detail-table counterpart. The older coverage review identifies it as a genuine
structural dependency: either endpoint can otherwise look unused, and the mapping is invisible in the
current report. [verified by the retained Desktop evidence and committed regression fixture]

**Evidence and treatment.** A current Desktop fixture now establishes a clear, column-owned mapping.
The existing structural graph retains a narrow edge and concise reason without a new usage state. The
committed regression fixture is sanitised because the Desktop project contains environment-specific
connection metadata.

**Risk.** Aggregations are specialised. Do not manufacture a structural root from undocumented TMDL,
or imply query acceleration, refresh performance or Service behaviour from metadata alone.

**Approximate size:** delivered. **Action:** complete; do not broaden into aggregation performance analysis.

### 3. Mobile semantic references — complete

**Problem and user outcome.** PBI Assure's accessibility and layout-facing evidence is based on ordinary
report pages. A separately persisted mobile layout could mean that the mobile experience has different
placement, tab order or visual participation that is not represented by the current review. The older
coverage review has no committed fixture proving the PBIR shape or whether mobile layouts can introduce
a unique semantic/accessibility outcome. [verified in repository]

**Evidence and treatment.** A Desktop save/close/reopen/save fixture establishes sibling `mobile.json`
and a mobile-only dynamic title expression. The existing field-reference extractor now reads that
expression through the normal visual usage path; position-only state is ignored. No mobile-specific
inventory or assurance surface was introduced.

**Risk.** Do not assume responsive placement itself is an assurance defect, or create a mobile rule
without a user-impactful persisted condition.

**Approximate size:** delivered. **Action:** complete; do not broaden into mobile-layout assurance.

### 4. KPI and measure Detail Rows DAX dependencies — complete

**Problem and user outcome.** A KPI can name target, status and trend expressions; measure Detail Rows
can name DAX used for drill-to-detail. If a referenced object appears nowhere else, it can be classified
as apparently unused despite explicit model metadata referring to it.

**Evidence and treatment.** The retained Desktop fixture establishes all three measure-KPI expression
properties and a multiline measure-owned `detailRowsDefinition`, including a save/close/reopen cycle and
successful `DETAILROWS` invocation. The existing DAX extraction and graph paths now retain the exact
references, without a new usage state. The committed fixture is sanitised and pins the metadata-only
targets and controls.

**Risk.** Do not combine this with broad model-property parsing or infer table-owned Detail Rows. Only
the observed measure-owned expressions become edges.

**Approximate size:** delivered. **Action:** complete; do not broaden without new Desktop evidence.

### 5. Culture/Q&A linguistic metadata boundary

**Problem and user outcome.** Default culture files are empty and are correctly treated as presentation
metadata. A culture containing ordinary translations still describes objects rather than consuming them.
The open sub-case is Q&A linguistic metadata/synonyms, which may be closer to a consumer and could make
an otherwise unused object discoverable at run time. [verified in repository]

**Evidence and treatment.** Create a small Desktop-authored culture fixture with both a translated
caption and Q&A synonym metadata, then inspect persistence and determine whether Q&A metadata is still
meaningful while the Q&A feature is being retired. This may produce a clarified coverage boundary rather
than graph support.

**Risk.** Do not treat a caption as usage, and do not invest in broad Q&A analysis if current Desktop no
longer produces useful metadata or the retirement leaves little practical value.

**Approximate size:** fixture/investigation small. **Action:** create Desktop fixture.

### 6. Visual-calculation dependency evidence

**Problem and user outcome.** A visual calculation can be a consumer of a UDF or model object not
captured by model-DAX parsing. It is the remaining reason `functions.tmdl` is still partially analysed
with a dependency-bearing impact. [verified in repository]

**Disposition.** Keep it evidence-bounded. Microsoft documentation and current repository reasoning say
ordinary visual calculations are constrained by items already placed on the visual, reducing the usual
semantic-usage gap. There is no committed Desktop example showing a unique missed object dependency, and
the expected PBIR shape is not established.

**Approximate size:** likely large. **Action:** do not implement; obtain a fixture only if a real project
shows a unique outcome.

### 7. `EXTERNALMEASURE` consequence check

**Problem and user outcome.** The report-measure experiment observed a local composite model measure
using `EXTERNALMEASURE` as a proxy for a remote measure. Existing evidence does not show a false usage
classification or a missing user-facing explanation from this representation. [verified in retained
experiment evidence]

**Disposition.** Do not turn it into parser work. A small investigation may scan a safe composite-model
fixture to establish whether PBI Assure already reports the local proxy truthfully and whether a real
gap exists. No remote source model may be invented or joined by name.

**Approximate size:** investigation small; implementation unknown. **Action:** investigate only after a
concrete user-facing gap is demonstrated.

### 8. `dataSources.tmdl` and connector expansion

**Problem and user outcome.** `dataSources.tmdl` has not been observed in any committed Desktop fixture.
The aggregate connector measurement across 40 projects found no unrecognised class-A external connector
function. [verified in repository]

**Disposition.** Keep parked. There is no evidence that a missing connector family or unparsed
data-source definition currently changes a meaningful result. Revisit only with a redistribution-safe
class-A source example or an observed `dataSources.tmdl` file.

## Explicitly parked

- **Bookmark-only graph edges:** stale/inert saved bookmark state is proven; no unique effective carrier
  is proven.
- **Report-level measure expression parsing / report-measure → UDF:** persisted expression text in an
  unbound live-connected report is not trustworthy dependency evidence; valid converted measures became
  ordinary local TMDL measures.
- **`PBI-ACCESS-001` changes:** 216 representative findings were measured, but decorative intent was
  not author-labelled. Do not add visual-type exemptions.
- **Visual calculations:** retain the current coverage limitation; do not implement on speculation.
- **Connector-family expansion:** no measured class-A gap.

## Recommendation and sequence

1. **Create the aggregation-mapping fixture** next.
2. Keep the local TMSL input gate until a separately evidenced parser/inventory decision exists.
3. Keep the remaining candidates evidence-led; do not start visual calculations, EXTERNALMEASURE,
   bookmark usage or accessibility-rule changes without their stated evidence.

## Scope

This review is product triage only. It does not approve a parser, finding, classification change or new
product claim.

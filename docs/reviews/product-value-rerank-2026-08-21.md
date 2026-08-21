# Product-value re-rank

**Date:** 2026-08-21

**Against commit:** `a0fbf554d5ce907425b52ded95e9e96a93293627`

**Status:** decision record only. No scanner, fixture, rule, renderer, JSON or CSV behaviour changed.

Evidence labels: **[verified in repository]** means established from the current code, committed
fixtures or retained review evidence. **[inferred]** is a cautious product judgement. **[design
decision]** is the selected direction.

## Executive decision

The next task should be a **small Desktop-authored TMSL (`model.bim`) input-format investigation**.
Its purpose is to establish whether a current, ordinary Power BI Desktop workflow still produces a PBIP
whose semantic model is stored as `model.bim`, and, if so, to define a safe fixture and the smallest
truthful product treatment. **Do not implement a TMSL parser from this decision alone.**

This ranks first because a supported-looking PBIP with a whole semantic model in an unread format is a
larger potential user outcome than another narrow dependency edge: the current registry records
`model.bim` as an unanalysed whole-model limitation, so it cannot provide the normal semantic inventory
or unused-object conclusions for that model. The format is deliberately not claimed to be a current
Desktop path yet; that is the evidence question. [verified in repository]

The previous ranking is substantially complete: OLS, incremental refresh, inactive-relationship
activation, the role/perspective explanation and the relevant schema work are now delivered. Bookmark
semantic usage and report-level-measure expression parsing were investigated and deliberately parked.

## Scoring model

Scores are 1–5. Higher is better for **impact**, **missed-result severity**, **evidence readiness**,
**precision**, **delivery** (smaller/safer delivery), **distinctiveness** and **testability**. The score
orders discovery work; it is not a promise that every candidate should be implemented.

| Rank | Candidate | Impact | Severity | Evidence | Precision | Delivery | Distinctive | Testable | Next action |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | TMSL `model.bim` input coverage | 5 | 5 | 1 | 5 | 1 | 5 | 4 | Desktop investigation |
| 2 | Aggregation mappings (`alternateOf`) | 4 | 4 | 2 | 5 | 3 | 4 | 5 | Desktop fixture |
| 3 | Mobile-layout evidence and assurance scope | 4 | 4 | 1 | 3 | 2 | 5 | 3 | Desktop fixture/investigation |
| 4 | KPI and detail-rows DAX dependencies | 3 | 3 | 2 | 5 | 4 | 3 | 5 | Combined Desktop fixture |
| 5 | Culture/Q&A linguistic metadata boundary | 3 | 2 | 2 | 3 | 3 | 3 | 4 | Desktop fixture |
| 6 | Visual-calculation dependency evidence | 3 | 3 | 1 | 2 | 1 | 3 | 2 | Keep evidence-bounded |
| 7 | `EXTERNALMEASURE` consequence check | 2 | 3 | 2 | 3 | 2 | 3 | 3 | Focused investigation |
| 8 | `dataSources.tmdl` / connector follow-up | 2 | 2 | 1 | 2 | 2 | 2 | 3 | Keep parked |

## Ranked candidates

### 1. TMSL `model.bim` input coverage

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

**Evidence and treatment.** Current Desktop emission is unknown. First create a minimal Desktop-authored
project or conversion experiment that proves whether `model.bim` is still produced, records save/reopen
stability, and establishes the precise PBIP/PBIR binding and schema shape. If it is no longer a practical
Desktop path, retain the current limitation and document the evidence. If it is current, design a
bounded parser/inventory decision before implementation.

**Risk.** Do not treat a hand-authored `.bim` file, an old downloaded artifact or a format name in
documentation as proof that current Power BI Desktop users need this support. Do not claim TMDL and TMSL
are semantically interchangeable without fixture evidence.

**Approximate size:** investigation small; any implementation large. **Action:** investigate.

### 2. Aggregation mappings (`alternateOf`)

**Problem and user outcome.** `alternateOf` is an explicit aggregation mapping between an aggregation
table column and its detail-table counterpart. The older coverage review identifies it as a genuine
structural dependency: either endpoint can otherwise look unused, and the mapping is invisible in the
current report. [inferred from the retained review; not yet fixture-proven]

**Evidence and treatment.** This is a strong candidate for a compact Desktop fixture authored through
Manage aggregations. If Desktop persists a clear, table/column-owned mapping, the existing structural
dependency graph should be able to retain a narrow edge and a concise reason without a new usage state.

**Risk.** Aggregations are specialised. Do not manufacture a structural root from undocumented TMDL,
or imply query acceleration, refresh performance or Service behaviour from metadata alone.

**Approximate size:** medium after evidence. **Action:** create Desktop fixture.

### 3. Mobile-layout evidence and assurance scope

**Problem and user outcome.** PBI Assure's accessibility and layout-facing evidence is based on ordinary
report pages. A separately persisted mobile layout could mean that the mobile experience has different
placement, tab order or visual participation that is not represented by the current review. The older
coverage review has no committed fixture proving the PBIR shape or whether mobile layouts can introduce
a unique semantic/accessibility outcome. [verified in repository]

**Evidence and treatment.** Use a minimal Desktop-authored page with a deliberately different mobile
layout, a mobile-only visibility/placement control if Desktop permits one, and an accessibility-relevant
tab-order control. First determine whether it represents the same visual inventory or independent
metadata, then decide whether PBI Assure should inventory it, add neutral coverage or make a bounded
accessibility check.

**Risk.** Do not assume responsive placement itself is an assurance defect, or create a mobile rule
without a user-impactful persisted condition.

**Approximate size:** investigation small; implementation medium/large. **Action:** create Desktop fixture/investigate.

### 4. KPI and detail-rows DAX dependencies

**Problem and user outcome.** A KPI can name target, status and trend expressions; detail rows can name
DAX used for drill-to-detail. If a referenced object appears nowhere else, it can be classified as
apparently unused despite explicit model metadata referring to it. The older coverage review identifies
both as contained DAX-shaped dependency sources. [inferred; not yet Desktop-fixture-backed]

**Evidence and treatment.** One deliberately small Desktop model can test both constructs and preserve
the exact TMDL locations, save/reopen form and a control object used only through each construct. If the
shape is explicit, reuse the existing DAX extraction and graph paths rather than creating new usage
semantics.

**Risk.** Do not combine this with broad model-property parsing. Only persisted expressions that
unambiguously name model objects should become edges.

**Approximate size:** small/medium after evidence. **Action:** create combined Desktop fixture.

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

1. **Start the TMSL `model.bim` Desktop investigation.** This is the selected next task. It is an
   evidence/fixture task, not parser implementation.
2. If current Desktop no longer produces TMSL PBIP projects, retain the existing boundary and take the
   **aggregation-mapping fixture** next.
3. If TMSL is current and materially used, write a separate design decision for bounded input support
   before implementation.
4. Keep the remaining candidates evidence-led; do not start visual calculations, EXTERNALMEASURE,
   bookmark usage or accessibility-rule changes without their stated evidence.

## Scope

This review is product triage only. It does not approve a parser, finding, classification change or new
product claim.

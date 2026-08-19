# Proposed rules — candidate analysis

**Date:** 2026-08-19 · **Against commit:** `42f2c42` · **Status: proposal only. No code, tests or fixtures were written.**

Companion to `../reviews/architecture-review.md`. Every candidate below was found by looking for **analysis the codebase already performs that no rule consumes**, rather than by listing Power BI best practices. Each entry states what evidence already exists, what would need building, and — most importantly — how it could produce a false positive.

---

## Method

`grep` of each `*Inventory` type against `src/PbiAssure.Core/Assurance/*.cs` shows **26 of ~40 inventory types are referenced by no rule**. Some of those legitimately feed the dependency graph rather than rules (`SemanticColumnInventory`, `SemanticMeasureInventory`). But several represent completed analysis with no output path at all.

The two strongest candidates are cases where the *hard* work — parsing, resolution, comparison — is already done and correct.

---

## Design principles these proposals respect

Carried from the existing product, not invented here:

1. **Never assert wrongness the metadata cannot prove.** Prefer `ReviewRequired` over `Finding` wherever human judgement decides.
2. **Severity describes impact; assessment type describes confidence.** They are independent.
3. **Do not promote inventory to findings without cause.** `ApparentlyUnused` is deliberately not a rule. Nothing here changes that.
4. **A rule that fires constantly is worse than no rule**, because it teaches users to skim past the ones that matter.

---

## Tier A — the analysis exists; only a rule is missing

### A1. `PBI-MODEL-005` — model-internal reference does not resolve

| | |
|---|---|
| Category | Model integrity |
| Severity | Error |
| Assessment | Finding |
| New parsing | **None** |

**Condition.** A semantic dependency edge could not be resolved to an object in the same model — a sort-by column, hierarchy level, relationship endpoint, calculation-group item, field-parameter choice or DAX reference whose target does not exist.

**Evidence already available.** `UnresolvedSemanticDependency` carries `SemanticModel`, `FromTable`, `FromObjectName`, `FromObjectType`, `FromHierarchyName`, `DependencyKind`, `ReferenceText`, `Reason` and `EvidencePath`. That maps almost one-to-one onto `AssuranceFinding`, including a human-readable `Reason` the rule can surface directly.

**Why it matters.** This is the exact mirror of `PBI-MODEL-001`, which reports unresolved *report* references. Model-internal breakage is currently computed, recorded with full evidence, and then reported to nobody. A sort-by column pointing at a deleted column, or a hierarchy level over a renamed one, is a real defect that a user would want to know about and cannot currently see.

**False-positive analysis.** Low in principle: `SemanticUsageReconciler` and the dependency analyzer already refuse to guess at targets, so an unresolved edge is genuinely unresolved. **But precision is inherited from `DaxReferenceExtractor`.** If the extractor ever over-extracts — treating a function name, variable, or string literal as an object reference — those artefacts would surface as Error-severity findings. `SemanticDependencyKinds.Dax` is therefore the risky kind; `SortBy`, `HierarchyLevel` and `RelationshipEndpoint` are structural and far safer.

**Recommended sequencing.** Before enabling at Error severity, count current unresolved dependencies by `DependencyKind` across several real projects. If DAX-kind entries are noisy, ship the structural kinds first and treat DAX as `ReviewRequired` until the extractor's precision is measured. **This measurement should happen before any implementation.**

### A2. `PBI-THEME-001` — visual formatting duplicates the theme value

| | |
|---|---|
| Category | **New: Maintainability** (requires a new `AssuranceCategories` constant) |
| Severity | Information |
| Assessment | Review required |
| New parsing | **None** |

**Condition.** A visual persists a formatting value identical to the value its theme already supplies, so the local override has no effect and could be removed.

**Evidence already available.** `ThemeFormattingComparisonAnalyzer` and `ThemeReviewAnalyzer` already perform this comparison and feed the HTML theme-review view. No rule consumes either. *(The exact carrier type should be confirmed before implementation — I verified that no rule references these analyzers, not the shape of their output.)*

**Why it matters.** Redundant local formatting is the main reason themes appear "not to work" — the theme changes and the visual doesn't, because a stale local override wins. Surfacing it turns an existing analysis into actionable advice.

**False-positive analysis.** Moderate, which is why it is `ReviewRequired`. An author may set a value deliberately to pin it against future theme changes. The finding should say *"this override currently has no visual effect"*, never *"remove this"*.

**Note.** This needs a fifth category constant. Adding one is a small change with an output-contract consequence, so it is worth deciding deliberately rather than reusing `ModelIntegrity`.

---

## Tier B — small new logic over inventory already parsed

### B1. `PBI-ACCESS-006` — partial explicit tab order

**Warning · Review required · no new parsing**

Some items on a page carry an explicit `tabOrder` while others rely on Power BI's default. Given the confirmed semantics (`null` = included by default ordering), the author has ordered part of the page and left the rest to Desktop — usually without realising.

*Evidence:* `VisualInventory.HasExplicitTabOrder` already exists; `VisualGroupHierarchyResolver.Resolve` already yields the full comparable item set per scope.

*False positives:* possible on pages where the default order is genuinely acceptable for the remainder. `ReviewRequired`, and it should only fire when **both** populations are non-empty within one scope.

*Open question:* whether the check is per page or per group scope. Group scope is more consistent with `PBI-ACCESS-002` but depends on the group-semantics question below.

### B2. `PBI-ACCESS-007` — decorative object explicitly included in tab order

**Warning · Review required · no new parsing**

A `basicShape`, `image` or `textbox` carries an explicit non-negative `tabOrder`, placing it in keyboard navigation.

*Why this matters beyond its own merit:* it is the principled way to resolve the `PBI-ACCESS-001` asymmetry identified in the architecture review. Rather than silencing alt-text warnings for decorative types, this states the actual condition — a decorative object in the tab order needs either alt text or exclusion. It converts a high-volume, low-precision Warning into a low-volume, high-precision one.

*False positives:* an author may intend a text box to be reachable. `ReviewRequired` is correct. Note this fires only on **explicit** inclusion, not on the default-ordering case, which keeps volume low.

### B3. `PBI-MODEL-006` — calculation group with no calculation items

**Error · Finding · no new parsing**

*Evidence:* `SemanticCalculationGroupInventory.ItemCount` already exists.

*False positives:* essentially none — a calculation group with no items is non-functional. One of the cleanest candidates on this list.

### B4. `PBI-MODEL-007` — duplicate measure name across tables

**Warning · Review required · no new parsing**

Two measures with the same name in different tables create genuine ambiguity in DAX and in the field list.

*False positives:* legitimate in some modelling styles. `ReviewRequired`.

### B5. `PBI-MODEL-008` — hierarchy with a single level

**Information · Review required · no new parsing**

*Evidence:* `SemanticHierarchyInventory` + `SemanticHierarchyLevelInventory`.

*False positives:* a single-level hierarchy can be a deliberate placeholder or a drill target. Low severity, review-only.

### B6. `PBI-NAV-017` — page contains no visuals

**Information · Review required · no new parsing**

*Evidence:* `PageInventory.Visuals` and `PageInventory.PageType`.

*False positives:* **high unless filtered.** Tooltip pages, drillthrough shells and deliberately blank navigation pages are all legitimate. The rule must exclude pages already typed as tooltip or drillthrough via `PageType`, and remain `ReviewRequired` even then. Weakest candidate here; included for completeness rather than recommendation.

---

## Tier C — requires new extraction

### C1. Inactive relationship never activated by `USERELATIONSHIP`

**Information · Review required · needs new DAX extraction**

An inactive relationship exists but no measure anywhere references it through `USERELATIONSHIP`, so it is unreachable metadata.

*Current state:* `SemanticRelationshipInventory.IsActive` is parsed. **`USERELATIONSHIP` is not extracted anywhere in the codebase** — verified by search. This is the only proposal requiring real new work in `DaxReferenceExtractor`.

*Why it is worth it:* this is the highest-value *model* rule I can identify. Inactive relationships are a common source of confusion, and "is this one actually used?" is a question no static tool currently answers for the user.

*False-positive analysis:* genuine. `USERELATIONSHIP` can be constructed dynamically, invoked from calculation groups, or used by external tools and thin reports outside the scanned project — all of which `usage-classification.md` already lists as out of scope. `ReviewRequired` is mandatory, and the message should name the limitation explicitly.

### C2. Relationship across mismatched column data types

**Warning · Review required · needs verification, not much new parsing**

*Current state:* `SemanticColumnInventory.DataType` and both relationship endpoints are already available, so the comparison is cheap.

*Blocker:* it is not established which type combinations Power BI genuinely rejects versus silently coerces. **This needs Desktop verification before implementation**, or the rule will confidently report valid models as broken — precisely the failure mode the product exists to avoid.

---

## Deliberately not proposed

| Candidate | Why not |
|---|---|
| "Too many visuals per page" / "measure too complex" | Threshold rules are not provable from metadata as *wrong*. This is how assurance tools become noise. |
| Colour-contrast checks from theme colours | Rendered contrast depends on conditional formatting, overlays and data. Would be confidently incorrect. |
| Anything requiring data | Cardinality claims, uniqueness of the "one" side, refresh cost. Not in scope of static metadata. |
| Promoting `ApparentlyUnused` to a finding | Deliberate existing decision. Nothing here changes it, and it should stay inventory. |
| "Unused column should be deleted" | The documentation is explicit that `ApparentlyUnused` is never permission to delete. A rule would contradict it. |

---

## Recommended first

**A1 (`PBI-MODEL-005`), preceded by measurement.**

It closes a real correctness gap where the evidence is already computed and already trustworthy; it mirrors an existing rule so the shape, severity and evidence format are settled; and unlike most new rules it cannot invent false positives, because the analyzer already declines to guess at targets.

The one precondition is counting current unresolved dependencies by `DependencyKind` across real projects, to decide whether DAX-kind entries ship at Error or `ReviewRequired`. That measurement is itself a useful diagnostic regardless of whether the rule proceeds.

**Second: B3** (`PBI-MODEL-006`, empty calculation group) — trivial, unambiguous, no false-positive surface.

**Third: B2** (`PBI-ACCESS-007`) — but only after the group-semantics question is settled, since it is entangled with the `PBI-ACCESS-001` decision.

---

## Open questions blocking specific proposals

1. **Are groups focusable page items?** Blocks B1 and B2, and remains the top item in the architecture review's Desktop-verification list.
2. **What is `DaxReferenceExtractor`'s precision?** Determines whether A1 ships at Error severity for DAX-kind edges.
3. **Which relationship data-type combinations does Power BI reject?** Blocks C2 entirely.
4. **Does a fifth `AssuranceCategories` value belong in the output contract?** Blocks A2's categorisation, though not its logic.

---

## Scope statement

Proposal only. No rules were implemented, no code, tests or fixtures were created or modified, and no existing documentation was changed. Rule identifiers are suggestions that follow the existing convention and do not collide with the 29 currently in `docs/rule-catalog.md`; they are not reserved until implemented.

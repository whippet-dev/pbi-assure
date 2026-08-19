# PBI Assure — Architecture, Correctness and Product Review

**Date:** 2026-08-18 · **Reviewed at commit:** `42f2c42` · **Baseline:** `dotnet build` clean (0 warnings), `dotnet test PbiAssure.slnx` → **187 passed, 0 failed**
**Scope:** independent read-only audit. **No application code, tests, fixtures or existing documentation were modified.**

Findings are marked **[verified]** where I executed or mechanically checked something, and **[inferred]** where the conclusion comes from reading code without running it. Where Power BI's real behaviour is the deciding factor and the repository cannot prove it, the finding says so explicitly rather than guessing.

---

## 1. Executive assessment

**This is a well-built product with an unusually mature attitude to epistemic honesty, and its main risks are not where you might expect.**

The things that normally rot in a rules engine have not rotted here. The rule catalogue is **exactly** in sync with the implementation — 29 rule identifiers documented, 29 in code, zero drift in either direction [verified]. The usage classification model distinguishes five states rather than a used/unused binary, and `docs/usage-classification.md` states plainly that `ApparentlyUnused` means *"no usage found within the analysed scope… a review candidate, never automatic permission to delete an object."* The code implements that model faithfully. Auto date tables are identified by explicit TMDL annotation rather than by name heuristics. Unresolved references are retained as evidence instead of being silently corrected. The separation of **severity** from **assessment type** (`Finding` vs `ReviewRequired`) is the right primitive for a tool whose credibility depends on not overclaiming.

The tab-order semantics you verified experimentally are implemented correctly and exactly: `TabOrder is < 0` → excluded, `TabOrder is >= 0` → explicit rank, `null` → included [verified]. All negative values are treated identically, so Desktop's `-9999000` and its normalised `-1` behave the same. The `tests/fixtures/tab-order-states/README.md` is the best piece of documentation in the repository — it records the experiment, the observed Desktop behaviour, and a warning not to re-save the fixture because saving would destroy the state it exists to preserve.

**The risks are concentrated in three places.**

First, a **false-positive volume problem in `PBI-ACCESS-001`** that follows directly from the (correct) "missing tabOrder means included" rule, combined with a decorative-type exclusion that exists in `PBI-ACCESS-003` but not in `PBI-ACCESS-001`. On a real report authored by someone who never opened the Tab order pane, every decorative shape, image and text box without alt text produces a **Warning**. This is the single most likely way the tool loses a user's trust.

Second, **the privacy test project is not in the solution** and therefore never runs — including under CI [verified]. The product's headline claim is that report content never leaves the machine, and the tests written to guard that claim are silently inert.

Third, **the dependency analyzer has no dedicated tests**. It is the largest and most consequential file in the codebase (834 lines) and drives every usage state the product reports, yet it is exercised only incidentally through scanner and renderer tests [verified].

None of these are architectural failures. The architecture is sound and, in the areas that matter most, better than it needs to be. They are gaps in a product that has grown faster than its safety net.

---

## 2. What the product currently does

```
PBIP project folder
  → ProjectFileSource (bounded, canonical file access; also browser directory API)
  → discovery of *.Report / *.SemanticModel artifacts
  → PBIR parsers (report, visuals, bookmarks, actions, tooltips, accessibility,
                  formatting, theme) + TMDL semantic-model parser
  → normalised ArtifactInventory / ProjectInventory
  → ReportModelBinder (report → local model, by explicit byPath reference)
  → SemanticUsageReconciler (direct report evidence → DirectlyUsed / ApparentlyUnused)
  → SemanticDependencyAnalyzer (graph traversal → 5-state classification)
  → PowerQueryLineageAnalyzer + column-level lineage (separate graph)
  → AssuranceRuleEngine (29 rule identifiers across 15 rule classes)
  → HtmlReportRenderer / SemanticUsageCsvRenderer
  → CLI, Desktop (WinForms-style shell), Web (Blazor WASM)
```

**Supported input:** PBIP projects containing PBIR report folders and TMDL semantic models. PBIX is explicitly out of scope (`docs/pbix-ingestion-feasibility.md`).

**Capabilities found:** report/page/visual inventory; visual groups as first-class structural containers; tab-order analysis with group scoping; accessibility checks (alt text, tab order, disabled titles, drillthrough back actions); bookmark and action reconciliation; drillthrough parameter validation; visual interaction endpoints; report-tooltip page bindings; semantic model parsing including measures, columns, hierarchies, relationships, calculation groups, field parameters and partitions; DAX reference extraction; Power Query M lineage at query and column level; connector inventory with deliberate location minimisation; theme review and formatting comparison; HTML and CSV output.

### 2.1 Where the internal model does not map cleanly onto Power BI

**Groups are correctly modelled as structural containers, but rules disagree about whether they are page items.** `VisualRuleContexts.Read` enumerates `page.Visuals` only, so groups never reach `PBI-ACCESS-001` or `PBI-ACCESS-003` [verified]. But `DuplicateTabOrderRule` resolves `VisualGroupHierarchyResolver.Resolve(page)`, which concatenates groups *and* visuals, so groups **do** participate in `PBI-ACCESS-002` [verified]. Both behaviours are defensible; having both simultaneously is not obviously intended. In Power BI Desktop, groups appear in the Selection and Tab order panes as focusable items — see §15.

**`ProjectInventory.SchemaVersion` is a hardcoded literal `"0.21"`** (`ProjectScanner.cs:43`) [verified]. Individual parsers do read `$schema` and `version` from PBIR files, so the encountered schema is captured per artifact — but this top-level field is a constant that will not change when Microsoft ships a new format. The name invites the reader to believe it reflects the input.

---

## 3. Architecture map

| Project | Responsibility | Assessment |
|---|---|---|
| `PbiAssure.Core` (112 files) | Domain model, parsers, dependency engine, rules | Sound; internally well-partitioned into `Scanning` / `Inventory` / `Assurance` |
| `PbiAssure.Reporting` (10) | HTML + CSV rendering | Correct boundary, but `HtmlReportRenderer.cs` is 3,301 lines |
| `PbiAssure.Cli` (10) | Automation, output naming | Thin, appropriate |
| `PbiAssure.Desktop` (12) | Local Windows workflow | Thin, appropriate |
| `PbiAssure.Web` (14) | Blazor WASM front end | Thin; no backend, no upload path |

**The layering claimed in `docs/architecture.md` holds.** Core has no UI or console dependency; Reporting contains no parsing. The `Scanning → Inventory → Assurance` split inside Core is real, not nominal: rules consume `ProjectInventory` and never touch files.

**Extensibility is good for rules, weaker for output.** Adding a rule means implementing `IAssuranceRule` and registering it in `AssuranceRuleCatalog` — genuinely cheap. Adding a *presentation* concern means editing a 3,301-line renderer.

---

## 4. What is strong and should be preserved

1. **Rule catalogue ↔ code synchronisation.** 29 documented, 29 implemented, no drift either way [verified]. This is rare and worth protecting with a test that fails when they diverge.
2. **The five-state usage model and its stated limits.** `DirectlyUsed → IndirectlyUsed → StructurallyRequired → UsedOnlyByUnusedBranch → ApparentlyUnused`, implemented as a clean precedence chain in `SemanticDependencyAnalyzer.ClassifyObjects` (lines 516–531) exactly matching `docs/usage-classification.md` §"State precedence" [verified].
3. **Annotation-based auto date-table detection.** `TmdlSemanticModelParser.SystemGeneratedKind` requires literal `__PBI_LocalDateTable = true` or `__PBI_TemplateDateTable = true` [verified]. The documentation explicitly rejects name matching and hiddenness as evidence. This is precisely right.
4. **`VisualGroupHierarchyResolver`'s failure modes.** It models `MissingGroup`, `AmbiguousGroup` and `Cycle` as first-class outcomes, and `IsComparable` gates rules to only `Root` and `ResolvedGroup` [verified]. Unresolvable hierarchy produces silence rather than a wrong answer. This is the single best-designed component in the repository.
5. **Tab-order semantics.** `IsExplicitlyExcludedFromTabOrder => Position.TabOrder is < 0`; `IsInTabOrder => !IsExplicitlyExcludedFromTabOrder`; `HasExplicitTabOrder => Position.TabOrder is >= 0` [verified]. Three lines that encode a non-obvious, experimentally-established truth.
6. **The `tab-order-states` fixture and its README.** Records the Desktop experiment, the `-9999000` observation, the post-save normalisation to `-1`, and the instruction not to re-save.
7. **Assessment type separated from severity.** `Finding` vs `ReviewRequired` lets the tool say "this needs a human" without pretending to be certain.
8. **Unresolved references retained, never corrected.** `SemanticUsageReconciler` emits `UnresolvedSemanticReference` records with full evidence rather than fuzzy-matching `Sales[Dates]` onto `Sales[Date]`.
9. **Narrow exception handling.** Only four `catch` blocks in all of Core, each catching a specific exception type [verified]. Very low risk of an unknown being silently converted into a confident answer.
10. **Privacy engineering intent.** A dedicated canary fixture with unique sentinel strings, a network monitor, and a documented threat model.

---

## 5. Correctness risks

### 5.1 `PBI-ACCESS-001` has no decorative-type exclusion — **P0**

`VisualExcludedFromTabOrderRule` maintains `DecorativeCandidateTypes = { basicShape, image, textbox }` and skips them. `MissingAltTextRule` has **no such set** — `grep -c DecorativeCandidateTypes MissingAltTextRule.cs` → `0` [verified]. Its only filter is `IsInTabOrder && !HasAltText`.

Because a missing `tabOrder` correctly means *included*, and most report authors never open the Tab order pane, the practical effect is:

> **Every decorative shape, image and text box in a typical report produces a `PBI-ACCESS-001` Warning.**

Severity `Warning`, assessment `Finding` — the tool's most confident classification, applied to its least reliable inference. The two rules encode opposite beliefs about whether a shape can be decorative.

*Why it matters:* this is the highest-volume finding class on real reports, and volume plus low precision is how an assurance tool trains users to ignore it. **Inferred** as to real-world volume — I have not run the tool against a large real report. That measurement is the first thing I would do.

### 5.2 Schema version is never validated — **P1**

`docs/architecture.md` states: *"Parsers must record the encountered schema and fail with a useful unsupported-version finding rather than silently misinterpreting a newer format."*

Parsers do record `$schema` per artifact. **No rule consumes it.** There is no unsupported-version rule identifier in the catalogue, and no rule class references version comparison [verified]. The documented invariant is aspirational.

*Why it matters:* this is the exact failure the doc anticipated. When Microsoft ships a PBIR revision that moves or renames a property, the parsers will read what they recognise and report confidently on a partial understanding.

### 5.3 Group participation in rules is inconsistent — **P1**

As described in §2.1: groups participate in `PBI-ACCESS-002` but are invisible to `PBI-ACCESS-001` and `PBI-ACCESS-003` [verified]. If Desktop treats a group as a focusable page item, a group excluded from tab order or lacking alt text is currently undetectable — a false *negative*. If it does not, `PBI-ACCESS-002` may be comparing items that never compete for focus. Needs Desktop verification (§15).

### 5.4 Evidence strings can misdescribe what was observed — **P2**

`MissingAltTextRule` emits evidence `["$.position.tabOrder", "$.visual..altText (not found)"]` unconditionally. When the visual is included *because `tabOrder` is absent*, the first path points at a property that does not exist [verified]. `VisualExcludedFromTabOrderRule` has the same shape with `"$.position.tabOrder (negative value)"`, which is accurate for that rule.

*Why it matters:* evidence paths are the product's explainability contract. A user who opens the JSON and finds nothing at the cited path learns not to trust the evidence.

---

## 6. False-positive / false-negative risk, ranked

| Rank | Rule / area | Risk | Direction | Basis |
|---|---|---|---|---|
| 1 | `PBI-ACCESS-001` alt text | Decorative objects flagged at Warning severity, at volume | **False positive** | verified asymmetry, inferred volume |
| 2 | Group-scoped accessibility | Groups invisible to ACCESS-001/003 | **False negative** | verified |
| 3 | `ApparentlyUnused` presentation | Correct model, but depends entirely on the HTML conveying "review candidate" | **False positive if mis-presented** | see §11 |
| 4 | `PBI-ACCESS-003` decorative list | `basicShape`/`image`/`textbox` may not be the complete decorative set; custom visuals never match | **False positive** | inferred |
| 5 | `PBI-QUERY-002` unreachable named expression | Dynamic M consumers cannot be proven statically | **False positive** | acknowledged in docs; correctly `ReviewRequired` |
| 6 | Unknown/custom visual types | `VisualType` is compared as a string; custom visuals fall through every type-based exclusion | **False positive** | verified |
| 7 | `PBI-MODEL-001` unresolved references | Well-guarded — suppressed when the model is unavailable, and persisted selectors excluded | Low | verified |

**On "unused" specifically:** the architecture deliberately does **not** emit an "unused object" *finding*. Usage state is inventory data surfaced in HTML and CSV, not a rule with a severity. That is the correct decision and materially reduces the risk you were most worried about. The residual risk is entirely in presentation (§11).

**Structurally required is robust for the case you named.** Relationship endpoints seed `structuralRoots`, and traversal proceeds from there, so a Date column joined by an auto date-table relationship is reachable and classified `StructurallyRequired` rather than unused [verified, by code trace]. The auto date table itself remains a full participant in the graph per `usage-classification.md` §"Power BI-generated objects".

---

## 7. Rule engine and explainability

`AssuranceFinding` carries rule id, version, category, severity, message, remediation, report/page/visual/model/table/object, artifact path, **evidence paths**, assessment type and an authoritative reference URL. That answers most of the explainability questions well:

| Question | Answered? |
|---|---|
| What did PBI Assure observe? | Yes — message + evidence paths |
| Why is it an issue? | Yes — message + reference URL |
| What evidence caused it? | Yes, but see §5.4 |
| How confident is the tool? | **Partly** — via `AssessmentType`, but there are only two values |
| What should the user do? | Yes — remediation text |
| Is "review" available instead of pass/fail? | Yes — `ReviewRequired` |

**The gap is confidence granularity.** `Finding` vs `ReviewRequired` is binary, but the underlying analysis already distinguishes richer states — resolved vs ambiguous group, static vs dynamic expression, persisted-selector vs active reference. Those distinctions are computed and then flattened at the finding boundary. The information exists; the output contract cannot carry it.

The five usage states are the model to copy: they were the right answer for semantic objects and the same reasoning applies to findings.

---

## 8. Model dependency findings

`SemanticDependencyAnalyzer` (834 lines) builds a directed graph and traverses from two root sets — direct report roots and structural roots — then classifies by first-match precedence. Edge kinds observed: DAX references, sort-by column, hierarchy levels, relationship endpoints, containing table, field-parameter choices, calculation items, report measures.

**Strengths:** report measures are kept distinct from model measures and followed transitively; field-parameter `NAMEOF` choices are all treated as reachable because saved metadata cannot prove which a reader selects; `SELECTEDMEASURE()` correctly does *not* invent dependencies on every measure.

**Risks:**

- **No dedicated test file** — see §9. This is the highest-consequence untested component.
- **`ClassifyTables` re-scans all usages per table** (`SemanticDependencyAnalyzer.cs:570`, `usages.Where(...)` inside a per-table `Select`), giving O(tables × objects) [verified]. See §10.
- **Cross-model identity.** `NodeKey(model.Name, …)` scopes nodes by model name. Two models with the same name in one project would collide. Unlikely, unverified.
- **Documented limits are real and correctly stated:** bookmark-captured state, external consumers, XMLA, Analyze in Excel and thin reports are all out of scope, and the docs say so.

---

## 9. Test and fixture quality

**Baseline: 187 tests, all passing, ~4s** [verified].

### 9.1 `PbiAssure.Privacy.E2E` is not in the solution — **P0**

`grep -c "Privacy.E2E" PbiAssure.slnx` → `0` [verified]. The project exists with five source files including `PrivacyNetworkMonitor.cs` and `PrivacyWorkflowTests.cs`, and a committed canary fixture. `dotnet test PbiAssure.slnx` reports *"A total of 1 test files matched"* — only `Core.Tests` ran.

The product's central privacy claim is guarded by tests that do not execute.

### 9.2 Coverage distribution

| Area | Tests | Assessment |
|---|---|---|
| Visual groups | 27 (`VisualGroupSupportTests`) | **Strong** — the recent group work is genuinely protected |
| Tab-order states | `TabOrderStatesFixtureTests` + `VisualInventoryTabOrderTests` | **Strong**, against a real Desktop fixture |
| Project scanning | 24 | Good |
| Visual reference classification | 16 | Good |
| HTML rendering | 15 | Reasonable |
| **Dependency analyzer** | **0 dedicated** | **Gap** — only incidental coverage |
| `PBI-NAV-003 / 005 / 006` | **0** | **Gap** — no test references these ids at all [verified] |

### 9.3 Fixture strategy

Six fixtures. `tab-order-states` and `grouped-tab-order` are Desktop-authored with `.pbip`/`.pbir`/`.pbism`/`.platform`/`.abf` present. `privacy-canary` is explicitly synthetic and says so. Expected-output snapshots (18 HTML, 18 CSV) are committed.

**Recommended separation** (direction only):

1. **Synthetic unit fixtures** — hand-written JSON for parser edge cases. Cheap, unlimited.
2. **Minimal hand-crafted edge cases** — e.g. group cycles, ambiguous group names, missing `tabOrder`. Some exist; the `VisualContainerScopeResolution` failure modes deserve explicit ones.
3. **Real Desktop-authored fixtures** — the current gold standard. Each should carry a README in the style of `tab-order-states`.
4. **Save/reopen normalisation fixtures** — currently represented only by prose in one README. The `-9999000` → `-1` transition is exactly the kind of behaviour that deserves a *pair* of fixtures (pre-save and post-save) so a regression in either state is caught.

---

## 10. Performance and scalability

No measurements taken — the following are **inferred** from code shape.

- **`ClassifyTables` is O(tables × objects)** (`SemanticDependencyAnalyzer.cs:570`). On a 200-table, 20,000-object model that is 4M comparisons per scan. A dictionary keyed by table name would remove it.
- **`ClassifyObjects` is sound** — it builds hash sets and an adjacency map once, then traverses.
- **`HtmlReportRenderer` at 3,301 lines** builds the entire report as a string. Memory scales with finding count; a report with tens of thousands of usage rows will produce a very large single HTML document with no pagination.
- **Repeated `EnumerateReferences(report)`** is called in both `ReadEvidence` passes and again in the unresolved loop — three traversals of the same report tree per model.

**How to measure:** generate a synthetic model with parameterised table/column/measure counts and time `ProjectScanner.Scan`. No such harness exists today.

---

## 11. Output and product UX

**Assessed structurally rather than visually — I did not render a report against a real project.**

- **Escaping is disciplined:** a single `Encode` helper wrapping `HtmlEncoder.Default`, used 153 times [verified]. No `MarkupString` or raw-HTML injection points found in Reporting or Web. Report names, field names and descriptions flow through it.
- **Counts:** `ProjectInventory` exposes `SystemGeneratedSemanticObjectCount` and `DeveloperSemanticObjectCount` separately, and the HTML defaults to developer-authored objects with a filter [verified via code + `usage-classification.md`]. This directly implements "groups and generated artefacts must not inflate counts".
- **The critical UX question is whether `ApparentlyUnused` reads as "delete this".** The documentation is emphatic that it must not. Whether the HTML conveys that with equal force is the highest-value UX check remaining, and it is the point where an otherwise careful analysis could still mislead.
- **Large result sets:** no pagination or virtualisation observed in the renderer.

---

## 12. Robustness and security

- **Malformed input:** four narrow `catch` blocks in Core [verified] — `JsonException` in bookmark, report and theme parsers, `ArgumentException` in one path operation. Failures are localised rather than swallowed globally.
- **Unknown constructs are named, not guessed:** `UnsupportedComplex`, `DynamicOrUnsupported`, `UnsupportedCandidate` are explicit classifications. This is the correct instinct and is applied consistently.
- **Unknown visual types:** `VisualType` is a nullable string compared case-insensitively; unknown types simply fail every membership test. Safe by default, but see §6 rank 6.
- **Privacy:** no upload, backend or telemetry path found in Web; `docs/browser-privacy.md` documents the model. The guarding tests do not run (§9.1).
- **Supply chain:** minimal dependency surface; no runtime package references of concern observed at project level.

---

## 13. Documentation gaps

`docs/` is a genuine strength — `architecture.md`, `usage-classification.md`, `rule-catalog.md`, plus feasibility spikes and a decision record. Accuracy is high. Specific gaps:

1. **`architecture.md` claims unsupported-version findings exist. They do not** (§5.2).
2. **`ProjectInventory.SchemaVersion = "0.21"`** is undocumented and its meaning is ambiguous (§2.1).
3. **Only one decision record** (`0001-use-dotnet-and-separate-core.md`), despite several decisions that clearly warrant one — the five-state usage model, annotation-only auto-date detection, groups-as-containers, and the tab-order semantics.
4. **The tab-order knowledge lives in a fixture README**, not in `docs/`. It is the most valuable Power BI knowledge in the repository and is discoverable only by someone browsing test fixtures.
5. **No documented invariant list.** Several load-bearing invariants exist only as code (`IsComparable` gating, negative-tabOrder equivalence, annotation-only generated-table detection).

---

## 14. Findings ranked

### P0
1. **`PbiAssure.Privacy.E2E` absent from the solution** — privacy guarantees untested in CI (§9.1) [verified]
2. **`PBI-ACCESS-001` lacks decorative-type exclusion** — likely dominant false-positive source (§5.1) [verified asymmetry / inferred volume]
3. **No dedicated tests for `SemanticDependencyAnalyzer`** — 834 lines driving every usage state (§9.2) [verified]

### P1
4. Schema version captured but never validated; documented invariant unmet (§5.2)
5. Group participation inconsistent across accessibility rules (§5.3)
6. `PBI-NAV-003 / 005 / 006` have zero test coverage (§9.2)
7. No save/reopen normalisation fixture pair (§9.3)

### P2
8. Evidence paths can cite absent properties (§5.4)
9. `ClassifyTables` O(tables × objects) (§10)
10. `HtmlReportRenderer` at 3,301 lines; no pagination for large result sets (§10, §11)
11. Confidence is binary at the finding boundary though richer states are computed (§7)

### P3
12. Cross-model name collision in `NodeKey` (§8)
13. Only one decision record (§13)
14. Triple traversal of report references per model (§10)

---

## 15. Assumptions requiring real Power BI Desktop verification

| # | Assumption | Where | Current status |
|---|---|---|---|
| 1 | **Groups are / are not focusable page items** for tab order and alt text | `VisualRuleContexts` vs `DuplicateTabOrderRule` | **Contradictory in code** — highest priority |
| 2 | `basicShape`, `image`, `textbox` is the complete set of decorative-capable types | `VisualExcludedFromTabOrderRule` | Plausible but unverified |
| 3 | Whether Desktop assigns focus order to a **group's children** relative to root items, and whether the friendly `1.3.1` model matches its actual traversal | `VisualGroupHierarchyResolver` | Inferred from structure |
| 4 | Whether a **group itself** can carry a negative `tabOrder`, and what that does to its descendants | not modelled | Unknown |
| 5 | Custom visuals' tab-order and alt-text metadata shape | type-string comparisons | Unverified |
| 6 | Whether nested groups can exceed one level and how Desktop normalises deep nesting on save | resolver handles N levels | Untested against Desktop |
| 7 | Post-save normalisation of **explicit rank** values (the `3000/2000/1000/0` → `1000/…` shift observed in the fixture README) | fixture prose only | Observed once, not pinned by a fixture pair |

---

## 16. Recommended next sequence

1. **Add `PbiAssure.Privacy.E2E` to the solution and CI.** Smallest change with the largest credibility impact. Expect it to fail or need repair first — that is the point.
2. **Measure `PBI-ACCESS-001` volume against a real report** before changing it. Decide deliberately whether decorative types should be excluded, downgraded to `ReviewRequired`, or left as-is with better wording.
3. **Write `SemanticDependencyAnalyzerTests`** covering the five-state precedence, auto date-table structural requirement, field-parameter reachability, and calculation-group traversal.
4. **Settle assumption 1** (groups as page items) with a Desktop fixture, then make the three accessibility rules consistent.
5. **Add the save/reopen fixture pair** for tab-order normalisation.
6. **Implement the schema-version finding** that `architecture.md` already promises.
7. Only then: performance work and renderer decomposition.

---

## 17. Things NOT to rewrite

| Keep | Why |
|---|---|
| **`VisualGroupHierarchyResolver`** | The `MissingGroup`/`AmbiguousGroup`/`Cycle` + `IsComparable` design is exactly right. Copy this pattern elsewhere. |
| **Five-state usage model** | Correctly implemented, correctly documented, correctly caveated. |
| **Annotation-only generated-table detection** | Resist any suggestion to fall back to name matching. |
| **Tab-order semantics** (`< 0`, `>= 0`, `null`) | Experimentally established. Do not "simplify". |
| **`tab-order-states` fixture and its README** | Do not open and re-save it. The README says so; heed it. |
| **Severity / assessment-type separation** | The foundation of the product's honesty. |
| **Unresolved-reference retention** | Never make the tool guess a target. |
| **Core / Reporting boundary** | Clean and worth defending. |
| **`docs/usage-classification.md`** | Among the best-judged documents I have read in a codebase of this size. |

---

## Scope statement

Read-only. `git status` in this repository shows only this new file. No source, test, fixture or existing documentation was modified. The build and test suite were executed to establish a baseline and were not altered.

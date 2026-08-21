# Durable decisions and established semantics

Decisions and Power BI behaviours that a coding agent should **not** casually reopen. Curated, not a
backlog and not a changelog. Add an entry only when something durable actually changes.

Architectural decision records live in [`docs/decisions/`](../decisions/) and remain the home for full
ADRs with context and consequences. This file is the short index of standing invariants, including the
Power BI semantics that are easy to get wrong and expensive to rediscover.

## Evidence discipline

Statements about Power BI behaviour carry their evidence level, and the levels are not
interchangeable:

- **[verified]** — checked by running or reading something specific, in this repository
- **[verified by Microsoft primary documentation]** — stated in current Microsoft Learn documentation
- **[verified by Power BI Desktop-authored fixture]** — observed in files a real Desktop emitted
- **[inferred]** — reasoned, not evidenced
- **[design decision]** — a choice, not a fact

Documentation is never silently promoted to observed behaviour. **A hand-written PBIP or TMDL file is
never described as Desktop-authored**, in code comments, documentation or commit messages.

Where file serialization or save/reopen normalisation is what matters, prove it with a Desktop-authored
fixture. Changes applied through Power BI Desktop's TMDL view count as Desktop serialization, because
Desktop writes the files — but record them as **TMDL-view-authored** rather than GUI-authored, since the
distinction matters if Desktop later gains a GUI for the same object.

## The repository is the project memory

Chat history is disposable. Any decision that outlives a single task belongs in the repository — here, in
an ADR, in a fixture README, in [`docs/design/`](../design/), in [`docs/reviews/`](../reviews/), or in a
commit message. An incoming agent must be able to work from a clone alone.

**The repository copy is authoritative.** Documents are often drafted or shared through an external
review folder first. That copy is for exchange and review only. Once a document is accepted it is
committed here, and from then on **the repository version wins if the two ever differ**. Do not maintain
two equal sources of truth, and do not treat an external copy as current state.

The normal flow for a task report is: draft it externally for review → once accepted, commit the
authoritative copy under `docs/design/` (forward-looking) or `docs/reviews/` (point-in-time record).
Not every report merits committing; only those carrying durable project state.

## Analysis honesty

- **Never present a confident conclusion drawn from metadata PBI Assure knows it skipped.** This is the
  reason the unsupported-construct work exists.
- **"Apparently unused" means "no usage found within the analysed scope"** — a review candidate, never
  automatic permission to delete. Reflected in `docs/usage-classification.md` and in the product's
  own wording.
- **Persisted bookmark references are not semantic-usage roots by default.** Desktop can retain stale,
  inert bookmark snapshots after their live carrier is removed. Bookmark-derived usage requires evidence
  that the state remains behaviourally effective and uniquely carries a dependency not represented by
  current live report metadata.
- **Relationship activation is not inferred from endpoint-column co-occurrence.** It requires a retained
  structured `USERELATIONSHIP` call whose paired endpoints resolve exactly and uniquely. Whether the source
  calculation is report-reachable is separate evidence. An inactive relationship with no detected call is
  never described as unused or safe to remove.
- **RangeStart/RangeEnd usage is not incremental-refresh policy evidence.** Power BI Desktop can retain the
  reserved parameters and parameter-filtered M without a configured policy. Only an explicit table-owned
  `refreshPolicy` object establishes authored policy settings. Those settings do not prove query folding,
  service refresh success or generated partitions.
- **An explicit refresh-policy polling column is a structural dependency.** A single qualified reference
  to the owning table's column can make that column structurally required. Custom or ambiguous polling M
  remains retained evidence and must not be converted into a guessed model-object dependency.
- Prefer `Unknown` or `ReviewRequired` over a confident claim when metadata cannot support certainty.
- Unresolved references are retained as evidence and never silently corrected or fuzzy-matched onto a
  similar name.

## Unsupported-construct model

- **An unresolved dependency and an unanalysed construct are different concepts.** An unresolved
  dependency is bounded — source, kind and reference text are known, and exactly one edge is missing. An
  unanalysed construct is unbounded — it is not known whether it creates dependencies at all. They must
  not share a type.
- **`AnalysisLimitation` is per-scan; `AnalysisScopeBoundary` is a separate, permanent concept.** A
  limitation describes metadata encountered in *this* project but not analysed, and disappears when
  support is added. A boundary describes information that can never appear in the input format at all —
  Power BI Service role membership, workspace permissions, app audiences, sharing. Boundaries must never
  propagate uncertainty, because `ApparentlyUnused` is already defined in terms of the analysed scope.
- **Definition artifacts are registry-classified.** Every semantic-model definition artifact receives
  exactly one classification, including packaging files that are correctly not parsed. A known
  unsupported construct must never silently disappear, and a packaging file must never be reported as an
  unanalysed semantic construct.
- **An unrecognised construct is assumed capable of creating dependencies.** An unnecessary caveat is
  recoverable; a confident deletion recommendation for an object something uses is not.
- **The registry states the construct-type default; a limitation describes encountered metadata.** The
  registry must stay conservative about what a construct *can* contain. Where the scanner has affirmative
  evidence that a particular role file holds no unanalysed child content, it produces no role limitation:
  a hypothetical unsupported role form is not a limitation in this project. A role file with any
  unrecognised child remains conservatively limited. Other artifact-level refinements may narrow an emitted
  limitation to `NoKnownDependencyEffect`. `AnalysisLimitation` describes metadata actually encountered,
  not a catalogue of what the construct type could theoretically hold.
- **Narrowing requires affirmative coverage, never silence.** A construct is treated as harmless only
  when it is known to carry no model-object reference. Anything unrecognised keeps the conservative
  impact, so absence of evidence is never read as evidence of absence.

## Usage classification

- **Keep the five semantic usage states.** `DirectlyUsed` → `IndirectlyUsed` → `StructurallyRequired` →
  `UsedOnlyByUnusedBranch` → `ApparentlyUnused`, applied as first-match precedence. Uncertainty is
  expressed on an **orthogonal** axis, never as a sixth state, so existing consumers keep working.
- **Positive classifications are preserved under known referential limitations as a deliberate
  conservative product rule** — not as an eternal proof. Every construct known today only *adds*
  references, so skipped metadata cannot retract evidence already collected. That reasoning does **not**
  extend to an unrecognised future construct that might change how existing evidence is *interpreted*
  (root eligibility, object identity, relationship activity). If such a construct is ever identified, it
  is the case where positive states would need qualifying.
- **A mechanism that caveats essentially every model is worse than silence.** Property-level detection in
  particular must not ship if it would qualify every model for harmless descriptive metadata. The same
  test applies to any future limitation source.
- **Naming an object and consuming it are different propositions.** A construct that mentions a model
  object does not necessarily create usage. Collection-ordering declarations list every member
  regardless of use; a translation supplies a caption and is deleted along with the object it describes.
  Neither can keep an otherwise-unused object alive, so neither may caveat a usage conclusion. Ask what
  would break if the object were removed, not whether its name appears somewhere.

## Presenting uncertainty

- **Usage state and classification confidence stay separate on screen, not only in the domain.** The
  state says what was found; the confidence says how complete the evidence behind it is. Qualification
  must never be expressed as a different usage label, a modified badge or an extra status — that would
  reintroduce the sixth state the domain deliberately avoids.
- **Qualification is context about the analysis, not a defect in the object.** It must not borrow the
  report's error or warning treatment. A qualified classification is also **not** a low-confidence one:
  PBI Assure may hold strong positive evidence and simply not have read one more possible source.
- **An analysis limitation is explained once at model scope, never repeated per affected object.** One
  unanalysed construct can qualify most of a model — 21 of 27 objects in the Desktop fixture — so
  per-object prose would be 21 copies of one sentence, and per-object warnings would teach readers to
  ignore warnings. Limitations are grouped by construct rather than by file for the same reason, since a
  model emits one file per role.
- **The renderer consumes `ClassificationConfidence`; it never infers qualification** from a usage state,
  a construct name or an artifact path. The rule lives in `SemanticUsageConfidenceQualifier` so the
  registry stays the single place a construct's effect is declared, and so the reserved
  `MayInvalidateExistingEvidence` impact would qualify a positive state with no renderer change.
- **Counts, never scores.** No percentage, no red/amber/green, no accuracy rating, no invented
  limitation severity. There is no evidence basis for any of them, and prioritisation must be derived
  from the existing `SupportState`, `DependencyImpact` and `Concerns` semantics.
- **Nothing is shown when nothing was left unanalysed.** A panel announcing that there is nothing to
  report is reassurance nobody asked for; the standing caveats already have a home in the scope section.
  A limitation that cannot affect a conclusion is still disclosed, but must not imply qualification.
- **Domain enum names are not automatically user-facing vocabulary.** The report explains consequences,
  not the taxonomy that produced them: what an unread construct does to someone's used/unused answers,
  rather than what the construct is called internally. `ClassificationConfidence`, `DependencyImpact`,
  `MayCreateDependencies` and the rest belong in the domain, the JSON and engineering documents. A term
  is only good product copy if a competent Power BI developer understands it without reading this
  repository — precision that has to be looked up is not clarity.
- **Translation must not blur a distinction the domain draws.** `NoKnownDependencyEffect` is rendered as
  "does not change any used or unused result", never as "fully checked": the construct is still only
  partly read, and only its effect on usage is established. Where plain language would collapse two
  domain concepts into one, keep two phrases.
- **One vocabulary, single-sourced.** The word on an object marker must be the same word its explanation
  uses, or the marker leads nowhere. Render such a phrase from one constant rather than repeating the
  literal at each site.
- **A usage reason must explain evidence compatible with the object's current usage state.** An arbitrary
  incoming dependency is not sufficient. An uncalled function genuinely references a column, but it is
  not why that column is `IndirectlyUsed`, and a reason naming a dead branch undermines a correct answer
  standing beside it. Both facts are preserved: the edge stays in the graph, and the explanation comes
  from a predecessor whose own reachability matches the state. Never repair a mismatch by changing the
  classification to fit the reason.
- **Other true facts do not take presentation precedence when they explain a state the object did not
  get.** A relationship endpoint that a report also reaches displays `IndirectlyUsed`; the relationship
  is real, and it explains `StructurallyRequired`, so the live dependency is shown instead. The test is
  whether the cited evidence supports the *displayed* state — not whether the reason's wording sounds
  structural. "Available through field parameter X" is a live explanation whenever the report uses that
  field parameter.
- **Reachability is published by the classifier, not re-derived downstream.** The scanner already
  computes which nodes are reachable from report roots and from model-structure roots on its way to
  assigning a state; that is what presentation consumes. It covers nodes with no usage row — report
  measures and DAX user-defined functions — because a live path can run through one, so a rule based on
  the usage states of public objects cannot follow it. Reporting must not traverse the dependency graph.
- **When several reasons are equally valid, one is chosen deterministically.** Ordering of parsed
  dependency edges must never change an explanation. The original defect was exactly that: "first
  incoming edge" meant "first in sort order".

## Unresolved references require evidence about the reference itself

- **An unresolved dependency and an analysis limitation are different categories.** A limitation says
  PBI Assure could not fully check a construct. An evidence-safe unresolved reference says PBI Assure
  understood an explicit reference but could not find its target. Do not move either into the other's
  presentation merely because both involve uncertainty.
- **Retained evidence is not automatically a user defect.** `UnresolvedSemanticDependency` also records
  best-effort text extraction and ambiguous resolution. A public finding requires evidence that the
  reference shape and identity were understood, plus a resolution outcome establishing absence. "Could
  not resolve" alone is insufficient.
- **Machine decisions must not depend on diagnostic prose.** `ResolutionOutcome` is structured evidence
  (`NotFound` or `Ambiguous` for the current producers); `Reason` is explanatory text only. A public
  unresolved-reference finding requires both an evidence-safe producer kind and `NotFound`, so a
  parser-derived `NotFound` remains suppressed and an ambiguous record cannot become a finding because
  its wording happens to say "not found".
- **Parser uncertainty stays internal until separately justified.** DAX token extraction, specialised
  expression parsing, or a dependency kind that mixes structured and inferred producers must not be
  presented as a missing project object wholesale. Add narrow provenance only if existing retained
  evidence cannot support a safe gate; do not create a broad confidence subsystem for presentation.
- **Wording follows the proven claim.** Use **Reference not found** / "PBI Assure could not find" for the
  current evidence. Do not say broken or invalid, and do not use Error severity, without Desktop or
  equivalent evidence proving that stronger interpretation.

## Established Power BI semantics — verified, do not re-derive

Confirmed experimentally in Power BI Desktop and pinned by tests and fixtures.

### Visual groups

- Groups are **structural metadata, not visuals**.
- Groups must **not** contribute to visual counts or cards.
- Group-aware tab ordering is required.
- Friendly positions such as `1.3.1` represent nested, grouped tab order.

### Tab order

- `position.tabOrder >= 0` → **included**, with that explicit rank.
- `position.tabOrder < 0` → **excluded**.
- **All negative values are equivalent** for exclusion. Desktop has been observed writing `-9999000` and
  normalising it to `-1` on save; magnitude carries no meaning.
- **Missing `position.tabOrder` → included**, using Power BI's default ordering. Verified experimentally:
  the project opened normally, keyboard navigation reached the visual, and saving later normalised the
  ranks.

Do not "simplify" these three rules. See `tests/fixtures/tab-order-states/README.md`, which also warns
against re-saving that fixture, since saving destroys the pre-save state it exists to preserve.

### Explicit report landing page

- `landingPageName` in `definition/pages/pages.json` is the optional explicit **Set as landing page**
  setting. `activePageName` is separate saved authoring state; the values can legitimately name different
  pages. The absence of `landingPageName` is a valid state.
- `PBI-NAV-017` applies only to a nonblank `landingPageName` whose internal page name is absent. It must
  not infer a landing page from `activePageName`, and no finding is raised when no landing page is set.
- A stale `activePageName` is not currently a user-facing integrity finding. Reconsider only with
  evidence of a meaningful consequence, not merely because the property names a missing page.

### Configured custom-theme resolution

- `ThemeSourceInventory.ResolutionOutcome` is structured evidence about the PBIR resource-resolution
  attempt. It is distinct from the user-facing availability state and from explanatory resolution text.
  Machine decisions must use the structured outcome, never wording in `ResolutionIssues`.
- `PBI-COMPAT-002` applies only when `themeCollection.customTheme` is explicitly present and the
  configured resource has an evidence-safe unavailable outcome. A sparse but valid theme, base-only
  report and unselected registered resource are all valid and silent. The rule is not a Theme Review
  quality or completeness assessment.

### Row-level security serialization and classification

- **Triple-backtick fences are TMDL expression syntax, never expression content or child metadata.** A
  shared reader must consume from an opening fence immediately following `=` through the closing fence,
  remove only the closing fence's structural left boundary and retain relative expression whitespace.
  This applies equally to declaration and assignment expressions; do not add RLS-specific fence parsing.
  Any generic scan of child constructs must skip the fenced body so DAX/M tokens are not reported as
  unsupported model metadata.

- Desktop emits **one file per role** under `definition/roles/`.
- Inside a `tablePermission`, the owning table is named once on the `tablePermission` line and the
  filter expression uses **unqualified** column references — `[Region]`, not `Sales[Region]`. References
  resolve against the table named by the `tablePermission`; never assume a qualified `Table[Column]` form.

Established by `tests/fixtures/desktop-semantic-constructs`.

- **An object referenced by a role's table permission filter is `StructurallyRequired`.** The model
  requires it regardless of any report, which is precisely what that state means. It is deliberately not
  `DirectlyUsed`, because that state means *report* metadata references the object, and importance is not
  the same as report usage.
- Role filters are implemented as **model-structure roots**, the mechanism relationship endpoints and
  field-parameter metadata already use, so ordinary graph traversal produces the classification and the
  filter's transitive dependencies come along. There is no RLS-specific classification rule, and there
  must not be one.
- **Stored role security has bounded, explicit dependency semantics.** Table-permission filters and
  Desktop's inline `columnPermission <column> = <permission>` form are analysed; an explicitly named
  column becomes a model-structure root. A table-level `metadataPermission` is inventory/security-review
  evidence only: it protects the table object and does not imply semantic usage of every child column.
  Roles remain `PartiallyAnalyzed`; genuinely unrecognised role content keeps a qualifying dependency
  impact rather than being hidden to reduce caveat counts.
- Role membership lives in the Power BI service and never appears in a project. It is outside the
  analysed scope, not an unanalysed construct, and PBI Assure must never imply it can assess deployed
  security.

### Perspectives

- **Perspective membership is affirmative model-usage evidence.** A perspective is a curated subset an
  author deliberately exposed, and it drives Personalize visuals, so a report reader may add any member
  to a visual at run time. Saved report metadata cannot prove which members they pick — the same
  reasoning already applied to field-parameter choices.
- **An object a perspective exposes is `StructurallyRequired`**, via the shared model-structure root
  mechanism. Not `DirectlyUsed`: that means *report* metadata references the object.
- **Membership is exactly what is listed.** Naming a table does not expose its fields. Microsoft
  documents that each column, hierarchy and measure must be added individually unless `includeAll` is
  set. Widening a listed table to all its fields would be a large source of false "used" conclusions.
- This records **intent present in the model**, not evidence that any consumer used the perspective.
  PBI Assure cannot observe that, and must not imply otherwise.

### DAX user-defined functions

- **A function is a dependency node, not a model-structure root.** A function is a *definition*, and
  nothing in the model requires a definition to exist. What it references becomes reachable only when
  something reachable calls it, so an uncalled function's references correctly land on
  `UsedOnlyByUnusedBranch`. This is the line between a function and a role filter or perspective member:
  those are roots because the model, or a report reader, genuinely requires them.
- **A function has no owning table.** Microsoft documents that an unqualified name inside a function
  body is interpreted as a measure reference. Do not invent a table context for it the way a measure's
  own table supplies one.
- **Parameters are local symbols and shadow model objects.** A parameter named the same as a table or
  column must not resolve to it. `desktop-udf-references` contains exactly this case.
- **A UDF name cannot conflict with a built-in DAX function name** [verified by Microsoft primary
  documentation]. That is what makes it safe to identify a call by matching the callee against declared
  function names: the match cannot capture `SUM` or `COUNTROWS`.
- **Reading function definitions does not retire the function limitation.** The unread part is where a
  function is *called from* — visual calculations and report-level measures can both call one and
  neither is parsed. `functions.tmdl` therefore stays `PartiallyAnalyzed` with `MayCreateDependencies`.
  Missing a consumer under-reports usage, which is the dangerous direction. Do not lower this impact to
  reduce caveat volume.
- **A `byConnection` report-measure expression is authored text, not resolvable model evidence.** The
  offline report carries no source-model definitions, and Desktop has been observed preserving a rejected
  `Doubled()` expression with no structured reference or unrecognised marker. Inventory the text, but do
  not turn it into a semantic dependency without trustworthy source-model metadata. Do not bind a remote
  report to a convenient local model or use synthetic traversal to claim support for a state Desktop did
  not produce validly.
- **A valid report-level measure is not expression-resolvable against a local model merely because both
  concepts can exist in Power BI.** In the tested Desktop transition from a live connection to **Add a
  local model**, Desktop migrated valid report measures into local TMDL measures rather than preserving
  the mixed state. Treat those local measures through the ordinary model-DAX path; do not manufacture a
  synthetic report-measure/local-model join.

Established by `tests/fixtures/desktop-udf-references`, which also records what it does not prove.

### PBIR schema compatibility evidence

- **A different PBIR schema version is not automatically unsupported.** The exact schema family/version
  combinations in committed Desktop fixtures form PBI Assure's verified baseline. Other versions in the
  expected family are recognised but unverified until fixture evidence exists; semantic-version numbers
  alone are not a compatibility promise.
- **Schema compatibility is an analysis-coverage concern before it is a Finding.** Unknown, newer,
  missing or malformed schema metadata limits what PBI Assure can claim. It does not by itself prove a
  defect in the user's report. The first implementation must surface these states in scan metadata or
  Analysis coverage and must not add `PBI-COMPAT-003`.
- **Schema evidence must be structured and raw evidence preserved.** Compare parsed artifact family and
  version against a central baseline; do not drive behaviour from URI substrings or diagnostic prose.
  Continue property-wise parsing unless the artifact itself is unreadable.
- **PBIR-Legacy is a separate format boundary, not an old modern-PBIR schema version.** TMDL
  compatibility is likewise outside the report-side PBIR JSON policy.

Established by
[the encountered PBIR schema compatibility review](../reviews/encountered-pbir-schema-compatibility-policy.md).

### Local semantic-model input formats

- **A recognised local model that PBI Assure cannot read must stop before normal assurance output.** A
  current Desktop-authored PBIP can retain its semantic model as TMSL `model.bim`; this release supports
  local TMDL `definition/` models only. Do not emit incomplete inventory, an Analysis coverage substitute
  or a normal Finding from the unread model. Reject before parsing/rules/output, and reject a local model
  containing both `model.bim` and `definition/` as ambiguous. Remote `byConnection` is a distinct existing
  boundary and must not be treated as a local TMSL model.

Established by [the TMSL Desktop evidence review](../reviews/tmsl-model-bim-desktop-evidence-2026-08-21.md).

### Generated model objects

- Power BI-generated Auto Date/Time tables are identified **only** by the explicit TMDL annotations
  `__PBI_LocalDateTable` or `__PBI_TemplateDateTable`. A hidden table, a matching-looking name or an
  unused object is not evidence. Do not add name-matching fallbacks.
- Generated artefacts can make an otherwise unused column structurally required — for example a Date
  column reached through an auto date-table relationship.

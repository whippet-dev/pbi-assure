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

### Row-level security serialization

- Desktop emits **one file per role** under `definition/roles/`.
- Inside a `tablePermission`, the owning table is named once on the `tablePermission` line and the
  filter expression uses **unqualified** column references — `[Region]`, not `Sales[Region]`. Future RLS
  dependency parsing must resolve unqualified references against the table named by the
  `tablePermission` rather than assuming a qualified `Table[Column]` form.

Established by `tests/fixtures/desktop-semantic-constructs`. RLS is **not** parsed yet; a column
referenced only by a security filter still classifies as `ApparentlyUnused`. That is a known deficiency
and must never be encoded in a test as desired behaviour.

### Generated model objects

- Power BI-generated Auto Date/Time tables are identified **only** by the explicit TMDL annotations
  `__PBI_LocalDateTable` or `__PBI_TemplateDateTable`. A hidden table, a matching-looking name or an
  unused object is not evidence. Do not add name-matching fallbacks.
- Generated artefacts can make an otherwise unused column structurally required — for example a Date
  column reached through an auto date-table relationship.

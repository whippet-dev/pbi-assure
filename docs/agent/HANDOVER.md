# Handover

Tactical entry point for an incoming coding agent. Read this first, then
[CURRENT_STATE.md](CURRENT_STATE.md), then [DECISIONS.md](DECISIONS.md).

## What just happened

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

## State

- **Last verified product state:** `ae6be56` on `master`. Later commits may be documentation-only; run
  `git log --oneline` to see whether anything after it touched behaviour.
- **Working tree:** expected clean apart from untracked local review documents; no tracked modifications
- **Verified at that commit:** build succeeded with 0 warnings; **400 core + 2 privacy tests passed**; CI green
- **Known exception:** `dotnet format --verify-no-changes` fails with 24 pre-existing whitespace errors
  in two Theme Review files. Unrelated to current work, deliberately not fixed. See
  [CURRENT_STATE.md](CURRENT_STATE.md).

## Immediate next task

**Start a new workstream — the previous one is closed (see above).** Choose on user impact, not on what
was touched last. Ranked:

1. **Surface unresolved semantic dependencies.** `UnresolvedSemanticDependency` is retained as evidence
   and reaches the JSON inventory only. It is a *bounded* uncertainty — source, kind and reference text
   are all known — which makes it more actionable than a limitation, and a broken reference in a report
   is a real defect rather than a coverage gap. The architecture is ready and no new evidence is needed.
2. **Measure `PBI-ACCESS-001` against real reports.** Its false-positive concern is [inferred] and has
   never been measured. That inference currently blocks changing an accessibility rule that fires on
   every report, so the measurement unblocks a decision rather than adding a feature.
3. **Read report-level measure expressions as DAX.** A report measure's dependencies come from the
   structured `references.measures` list Power BI writes beside it; its `Expression` is never parsed, so
   a UDF call or a column reference in one is invisible. This narrows but does not retire the function
   limitation, and needs a Desktop fixture with a report measure calling a UDF.

The remaining UDF-consumer gap is **narrower than it looks**: an ordinary semantic-model measure calling
a UDF is already followed correctly, now proven by `tests/fixtures/desktop-udf-measure-consumer`. Only
report-level measures and visual calculations remain unread.

**Known presentation gap, deliberately not filled:** an object that is structurally required only by a
perspective or a role filter gets no "Why" line, because no reason wording exists for those kinds. That
is silence rather than a wrong statement, and adding copy was out of scope. Worth a small slice if a
user asks why those objects are unexplained.

Deliberately *not* ranked first: visual-calculation parsing. It is the other unread UDF consumer, but
it is the largest of the three and only moves a caveat that the report now explains honestly.

## Do not do yet

- Block-level or property-level limitation detection
- Further registry classification or impact changes without new evidence — the always-present files were
  corrected on the evidence recorded in `SemanticDefinitionFileRegistry`; `dataSources.tmdl` stays
  `DependencyEffectUnknown` until it is actually observed
- Malformed-TMDL recovery
- CSV or browser-app surfaces for limitations and confidence — HTML has one; the CSV header is a
  fixed contract and widening it deserves its own decision
- Changes to `PBI-ACCESS-001`
- The pre-existing `dotnet format` whitespace cleanup

Reasons are recorded in [CURRENT_STATE.md](CURRENT_STATE.md) and [DECISIONS.md](DECISIONS.md). Two are
blocking rather than merely sequenced:

1. **The culture impact value rests on a design decision**, not an observation — translations are treated
   as describing objects rather than consuming them. Everything built on qualification inherits that.
2. **`PBI-ACCESS-001` volume is unmeasured.** The false-positive concern is inferred, never measured
   against a real report. Do not change the rule on that inference alone.

## Missing evidence

- RLS forms beyond the two the Desktop fixture proves — cross-table filters, column permissions (OLS)
- **Where a UDF is called from outside the model definition** — visual calculations and report-level
  measures, neither parsed. This is now the reason functions still qualify
- Whether a *translated* culture file names model objects, and whether Q&A synonyms constitute usage
- Whether `dataSources.tmdl` is ever emitted by current Desktop
- Real-report measurement of `PBI-ACCESS-001` finding volume

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

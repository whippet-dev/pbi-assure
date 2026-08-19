# Unsupported and unknown construct handling — design proposal, revision 2

**Date:** 2026-08-19 · **Against commit:** `b84d487` (`master`) · **Supersedes:** an earlier revision not retained in this repository (see `../reviews/README.md`)
**Status when written: design only. No production code, tests, rules, HTML or RLS implementation.**

> **Implementation status, added later.** Slice 1 (file-level detection, §10.4) shipped, and the
> uncertainty-propagation rule (§4.4) shipped as `ClassificationConfidence` at `bd19501`. The user-facing
> behaviour in §5 has **not** been implemented and predates the working code, so review it against actual
> behaviour rather than following it directly. Everything below is the original reasoning, unedited.

Fifth document in the series, after `../reviews/architecture-review.md`, `../reviews/object-coverage-review.md`,
`proposed-rules.md` and `../reviews/audit-verification.md`. It answers one question:

> **How should PBI Assure behave when it encounters a Power BI construct that it recognises exists but
> does not yet understand well enough to analyse safely?**

This revision responds to a design review of V1. It is standalone — no prior document is needed to
review it. §12 lists exactly what changed and why.

Claims are marked **[verified]** (executed or mechanically checked against the repository at `b84d487`),
**[inferred]** (reasoned from code or Power BI semantics without direct evidence here), or
**[design decision]** (a proposal).

---

## 0. The motivating evidence

A synthetic PBIP project was scanned with the real CLI. The model held three columns, one measure, and a
`definition/roles.tmdl` containing:

```
role RegionalManager
	modelPermission: read

	tablePermission Sales = Sales[Region] = USERNAME()
```

Observed [verified]:

```
Sales[Amount]        Column  -> UsedOnlyByUnusedBranch
Sales[Region]        Column  -> ApparentlyUnused
Sales[Total Amount]  Measure -> ApparentlyUnused

UnresolvedSemanticDependencies: []
AssuranceFindings:              (none)
Artifacts: SemanticModel Probe DefinitionFileCount=3
```

Three facts define the problem:

1. `Sales[Region]` — a column whose only purpose is security filtering — is presented as a review
   candidate for removal [verified].
2. Nothing in the output records that `roles.tmdl` existed [verified].
3. **The scanner knew it was there.** `DefinitionFileCount=3` counts `definition.pbism`,
   `definition/tables/Sales.tmdl` and `definition/roles.tmdl`; only two are ever opened [verified].

Point 3 is the crux. The information is computed and discarded, not missing.

---

## PART 1 — How the current architecture handles uncertainty

| Situation | Current handling | Retained? |
|---|---|---|
| Recognised, parsed construct | `SemanticDependencyEdge` with source, target, kind, evidence | **Yes** |
| Report reference resolving to nothing | `UnresolvedSemanticReference` | **Yes** |
| Model dependency resolving to nothing | `UnresolvedSemanticDependency` | **Yes** |
| Ambiguous reference | `ModelLookup.TryResolveQualified` refuses via `hasColumn ^ hasMeasure` | **Yes** |
| Dynamic Power Query | `HasDynamicReferences` → `PBI-QUERY-001`, `Information` + `ReviewRequired` | **Yes** — closest behavioural precedent |
| Missing/malformed theme resource | `ThemeAvailabilityStates.{ReferencedButUnavailable, MetadataUnavailable, Malformed}` + `ThemeInventory.ResolutionIssues` | **Yes** — closest structural precedent |
| **Unknown TMDL block in a parsed table file** | Silently skipped | **No** |
| **Unparsed file in `definition/`** | Never opened | **No** |
| **Unread property in a parsed block** | Never requested | **No** |
| Malformed TMDL | `InvalidDataException` — **aborts the whole scan** | **No** |
| Future schema version | `$schema` recorded per artifact; `ProjectInventory.SchemaVersion` is the literal `"0.21"` (`ProjectScanner.cs:43`); no rule consumes either | **No** |

### 1.1 The three loss points

**A — file level.** `TmdlSemanticModelParser.Parse` opens exactly three things [verified]:
`definition/tables/*.tmdl` (non-recursive), `definition/relationships.tmdl`,
`definition/expressions.tmdl`.

Never opened, confirmed by zero references anywhere in `src/PbiAssure.Core` [verified]:
`roles.tmdl`, `model.tmdl`, `database.tmdl`, `cultures/`, `perspectives.tmdl`.

**RLS is lost here** — not at block level, but because the file is never opened.

Meanwhile `ProjectScanner.CountDefinitionFiles` enumerates every `.tmdl`, `.bim` and `.pbism`
recursively and counts them [verified]. Two enumerations disagree; nothing compares them.

**B — block level.** In `TmdlSemanticModelParser.ParseTable`, the loop handles `column`, `measure`,
`hierarchy`, `partition`, `calculationGroup`. **There is no `else` branch** [verified]. Anything else
advances `index = endIndex - 1` and vanishes.

**C — property level.** `FindProperty` / `HasFlag` are pull-based. A property nobody asks for is never
observed.

### 1.2 The parser degrades safely but silently

A probe with `kpi`, `detailRowsDefinition`, `alternateOf` and `variation` blocks interleaved with
ordinary columns [verified]:

```
columns:  Amount, Target, Region, Aggregated, Date      <- all five parsed correctly
unresolvedDeps: []
Sales[Target] -> ApparentlyUnused   (referenced only by the skipped kpi targetExpression)
Sales[Region] -> ApparentlyUnused   (referenced only by the skipped detailRowsDefinition)
```

**Good news:** the parser is not fragile. Unknown blocks are skipped without corrupting siblings
[verified for these shapes]. The problem is purely information loss, so recording can be added without
touching parse correctness.

Caveat [inferred]: the probe was hand-written. Whether Desktop serialises these constructs identically is
**not** established here.

### 1.3 Malformed input is the worst-handled case

One unparseable TMDL table file crashes the entire scan with an unhandled exception and a raw stack
trace [verified]. `Program.cs:72` catches `ArgumentException`, `DirectoryNotFoundException`,
`IOException`, `UnauthorizedAccessException` — but not `InvalidDataException`.

Contrast `PbirThemeParser.cs:114`: catches `JsonException`, records `Malformed`, appends to
`ResolutionIssues`, continues [verified]. **The right pattern already exists, applied to themes only.**

### 1.4 Where limits are stated today

`docs/usage-classification.md` §"Current limits" documents boundaries in prose, and the HTML repeats a
generic caveat [verified]:

> "PBI Assure could not find anything in this project that uses it. Check before removing it because
> external reports and dynamic behaviour may not be visible here."

Honest but **static** — identical whether or not anything was actually skipped in *this* scan. A search
for *limitation*, *not analysed* or *does not yet analyse* across `src/` returns nothing [verified].

**The design's purpose: convert a static documented limitation into an instance-specific,
evidence-backed record.**

---

## PART 2 — Two different concepts, not one

V1 folded permanent input-format limits into the same record as per-scan gaps. **The review is right that
this was wrong.** [design decision — revised]

### 2.1 The distinction

| | `AnalysisLimitation` | `AnalysisScopeBoundary` |
|---|---|---|
| What it is | Metadata **encountered in this project** but not fully analysed | Information that **cannot exist in the input format**, ever |
| Example | `roles.tmdl` was present and unparsed | Service role membership; workspace permissions; app audiences; sharing |
| Has an artifact path? | **Yes** — a real file, a real location | **No** — there is nothing to point at |
| Varies per scan? | **Yes** | **No** — identical for every scan at a given product version |
| Changes when support is added? | **Yes** — disappears | **No** — permanent until the input format itself changes |
| Propagates uncertainty? | **Yes** (§4) | **No** (§2.3) |

### 2.2 Why merging them is harmful

Three concrete harms, not stylistic objections:

1. **It destroys the signal.** If every scan reports the same five permanent boundaries, a count of
   "Analysis limitations: 6" is 83% noise, and the one entry that varies — the one that matters — is
   buried. This is the same alarmism failure as §6.8.
2. **The evidence fields would be meaningless.** `ArtifactPath` and `EvidencePath` are load-bearing on
   every existing record type [verified on `AssuranceFinding` and `UnresolvedSemanticDependency`]. A
   permanent boundary has neither, so they would be null on a record type whose whole convention is that
   they are populated.
3. **They answer different user questions.** "We have not analysed this yet" invites the user to wait for
   a future version. "This is not in the file" tells them to look somewhere else entirely, and always
   will.

### 2.3 Boundaries must not propagate uncertainty — and the repository already says so

**[verified]** `CONTRIBUTING.md:28` defines *analysed scope* as "the exact local project or set of Fabric
items included in a scan", and `CONTRIBUTING.md:29` defines *apparently unused* as "no inbound usage was
found **inside the analysed scope**." `docs/usage-classification.md` repeats this.

So the scope boundary is **already encoded in the meaning of the state**. `ApparentlyUnused` does not
claim "nothing uses this"; it claims "nothing in scope uses this." Propagating boundary-driven
uncertainty onto it would double-count a caveat the state's own definition already carries — and would
qualify every object in every model forever.

This is the decisive argument, and it comes from the product's existing vocabulary rather than from
preference.

### 2.4 Recommended shape for boundaries

**[design decision]** A **static catalog**, not per-scan data — the same shape as
`AssuranceRuleCatalog.ActiveRules`, which is a static `IReadOnlyList` of metadata records [verified that
this pattern exists].

```csharp
public sealed record AnalysisScopeBoundary(
    string BoundaryId,          // "PBI-BOUNDARY-RLS-MEMBERSHIP"
    string Concern,             // Security | Refresh | Distribution | Usage
    string Summary,             // "Role membership is not stored in a PBIP project."
    string WhereToLook,         // "Power BI Service workspace security settings"
    string RelevanceCondition); // when to surface it
```

**Relevance is contextual even though the catalog is static.** The role-membership boundary should
surface when the model actually defines roles, and stay hidden otherwise. A boundary shown
unconditionally is documentation; a boundary shown when it bears on what the user is looking at is
guidance.

This matters most for the future Security surface (§8) and costs nothing now: the catalog can exist with
its relevance conditions unevaluated until there is a surface to show them on.

---

## PART 3 — The six per-scan cases

| # | Case | Example | Today |
|---|---|---|---|
| 1 | Known, fully supported | relationship endpoint, sort-by | `SemanticDependencyEdge` |
| 2 | Known, intentionally unsupported | `tablePermission` | **No record** |
| 3 | Known, partially supported | `variation` — report side works, model side does not | **No record** |
| 4 | Unknown / future | any unrecognised TMDL block | **No record** |
| 5 | Recognised, unresolved reference | `sortByColumn: MonthNumber` with no such column | `UnresolvedSemanticDependency` |
| 6 | Malformed | TMDL that does not parse | **No record** — scan aborts |

### 3.1 Case 5 must stay separate from 2/3/4

**[design decision]** This is the distinction the whole design exists to preserve.

- **Unresolved (5)** — we know the *shape*: source object, kind, reference text, and that exactly one
  edge is missing. Bounded, named uncertainty.
- **Unanalysed (2/3/4)** — we do not know whether there are dependencies at all, how many, or where they
  point. Unbounded uncertainty.

`UnresolvedSemanticDependency` says "this arrow has a broken tip." An unanalysed construct says "there
may be arrows we never drew." Conflating them would let a reader believe the second was as
well-understood as the first.

### 3.2 Cases 2, 3, 4 share one type with a discriminator

**[design decision]** Not three types. The fields are identical, the propagation is identical, and the
boundary between them moves every release — today's unknown is tomorrow's intentionally-unsupported.
Three types would force a schema migration each time support advances; one discriminator makes it a value
change.

### 3.3 Case 6 is a different axis — corrected from V1

**[design decision — revised]** V1 placed `Malformed` alongside `NotYetAnalysed` and `Unrecognised` in a
single `SupportState`. **That was a modelling error**, and the review's question exposed it.

`SupportState` describes **our support for a construct type** — a property of PBI Assure, read from a
registry, identical across every project.

Malformedness describes **this instance of this file** — a property of the user's project. A fully
supported construct type can have a malformed instance.

These are orthogonal. Putting them on one axis makes "supported but broken here" inexpressible. §7 gives
the corrected model.

---

## PART 4 — Impact on usage classification

### 4.1 Containment scope is not impact scope

A `kpi` inside measure `[Total Amount]` has a `targetExpression` referencing `Sales[Target]`. The object
at risk is **`Sales[Target]`**, not its owner. Knowing *where a construct lives* says nothing about *what
it references*.

**[inferred]** Determining true impact requires reading the expression — which is what "unsupported"
means we have not done. So without parsing, the honest impact scope is *any object in the same semantic
model*.

That appears to force qualifying everything. It does not, because of §4.2.

### 4.2 The asymmetry — restated as a conservative rule, not a proof

**V1 asserted this was deductive. The review challenged that, and the challenge is correct.**
[design decision — revised]

The V1 argument was: skipped metadata can only *add* dependency edges, and graph reachability is
monotonic in edges, so no added edge can invalidate a positive state.

**That argument is sound only if the unknown construct's sole effect is to add references.** For the
known constructs it holds — `kpi`, `detailRows`, `tablePermission`, `alternateOf` and model-side
`variation` are all *referential*: they name model objects [verified for `kpi` and `detailRows` by probe;
[inferred] for the rest].

For a genuinely unrecognised future construct it is **not provable**, because "unrecognised" means its
effect is unknown by definition. A construct could invalidate a positive state without removing any edge:

- by changing **root eligibility** — something that marks a report binding inactive would make
  `DirectlyUsed` evidence wrong;
- by changing **object identity** — an alias or rename mechanism would make an existing edge point at the
  wrong object;
- by making a **relationship conditional** — `StructurallyRequired` is seeded from relationship
  endpoints, so a conditionally inactive relationship would overstate it.

Call these **interpretive** constructs — they change how existing evidence should be read — as opposed to
**referential** constructs, which only add references.

**The recommended behaviour is unchanged: positive states are not qualified.** What changes is its
status. It is a **conservative product rule grounded in the fact that every construct known today is
referential** [verified for the six catalogued in §6], **not a logical certainty.**

Why keep the behaviour despite the gap:

- No interpretive construct is known to exist in TMDL today [verified for the constructs examined].
- The cost of the alternative is total: qualifying positive states on the theoretical possibility that
  some future construct might be interpretive would qualify every object in every model, permanently,
  destroying the signal the design exists to create.
- The registry (§7.3) gives the escape hatch: if an interpretive construct is ever identified, it gets
  `DependencyImpact = MayInvalidateExistingEvidence`, and *that* value — and only that value — qualifies
  positive states. The design is extensible rather than merely hedged.

**[design decision]** Add that third impact value now, unused, so the model can express the case the day
it appears rather than needing a schema change under pressure.

### 4.3 No sixth usage state

**[design decision]** Adding `Unknown` to `SemanticUsageStates` would break the documented five-state
precedence chain in `docs/usage-classification.md`, change the CSV contract, force every consumer (HTML,
CSV, Web) to handle a new value, and destroy a property the architecture review lists as worth
preserving.

Instead, an **orthogonal** field on `SemanticObjectUsage`:

```csharp
public string ClassificationConfidence { get; init; } = ClassificationConfidences.Established;
// Established | QualifiedByLimitation
```

The state keeps its computed value. Consumers ignoring the new field behave exactly as today — which
matters, because `SemanticObjectUsage` is a `public record` consumed by Reporting, CLI and Web
[verified].

Note this field describes **a fact about this scan of this model** (something here was not analysed), not
a fact about our development knowledge. §5 explains why that distinction decides what else survives.

### 4.4 The recommended rule

**[design decision]**

> For each semantic model, if that model has one or more `AnalysisLimitation` records whose
> `DependencyImpact` is `MayCreateDependencies` or `DependencyEffectUnknown`, then every object in that
> model whose usage state is `ApparentlyUnused` or `UsedOnlyByUnusedBranch` is marked
> `ClassificationConfidence = QualifiedByLimitation`.
>
> No usage state changes. Positive states are not qualified (§4.2). Models without such limitations are
> untouched. `AnalysisScopeBoundary` never qualifies anything (§2.3).

Two narrowings, both taken:

1. **Per model, not per project** [verified safe]. `SemanticDependencyAnalyzer.NodeKey` joins the model
   name into every key, and a committed test pins it
   (`UseInOneModelDoesNotClassifyTheSameNamesInAnotherModel`).
2. **By `DependencyImpact`** — `NoKnownDependencyEffect` qualifies nothing.

### 4.5 Options considered

| Option | Verdict |
|---|---|
| Keep state, add a model-level warning only | **Necessary but insufficient** — a summary warning does not travel with the CSV row a user sorts and acts on |
| Downgrade only objects the construct is known to reference | **Deferred** — requires the parsing we lack. Viable later via §4.6 |
| Downgrade every object in the affected table | **Rejected** — assumes table-scoped impact, which §4.1 disproves. *Narrower than the truth* |
| Downgrade everything in the model | **Rejected** — qualifying `DirectlyUsed` is incoherent; the alarmist failure mode |
| Record the limitation, change no classification | **Rejected as destination, accepted as slice 1** — fixes the silence but leaves the CSV unmarked |
| **Keep state + orthogonal qualifier on absence claims only** | **Recommended** (§4.4) |

### 4.6 Deferred refinement

**[design decision, deferred]** Impact could later be narrowed without full construct support by
lexically scanning skipped text for `Table[Column]` patterns — the technique `DaxReferenceExtractor`
already implements [verified it exists]. A `roles.tmdl` limitation could then say "may affect `Sales`"
rather than "may affect this model."

Not proposed for implementation: it produces "may reference" evidence that reads as "does reference", and
that boundary needs its own design pass.

---

## PART 5 — `Confidence` is removed from the runtime model

**The review challenged `Confidence = Verified | Inferred` on `AnalysisLimitation`. Removing it is
correct.** [design decision — revised]

V1 used it for two things. Both collapse:

1. **"We verified this construct is unsupported."** Tautological at runtime — if a record was emitted, the
   construct was observed. The record's existence *is* the verification.
2. **"We infer this construct type can create dependencies."** Already carried by `DependencyImpact`:
   `DependencyEffectUnknown` means exactly "we do not know", `MayCreateDependencies` means "we believe it
   can."

Is anything lost? Only the distinction between "`MayCreateDependencies`, confirmed by a Desktop fixture"
and "`MayCreateDependencies`, inferred from Power BI semantics." **The user-facing guidance is identical
in both cases** — check before deleting. It is a fact about the maturity of our own knowledge, not about
their model.

**The general principle** [design decision]: *the runtime model carries facts about the user's artifact;
it does not carry facts about our development knowledge.* `[verified]` and `[inferred]` remain
load-bearing in these documents and belong as annotations on **registry entries in source**, where a
developer reading `DependencyImpact = DependencyEffectUnknown // [inferred] needs Desktop fixture` gets
the provenance without it reaching the product surface.

This also removes a real hazard: a user seeing `Confidence: Inferred` would reasonably read it as "PBI
Assure is unsure about *my model*", when it actually meant "the PBI Assure authors have not yet
confirmed this against Desktop." That is a misleading label, not merely a redundant one.

---

## PART 6 — Applying the design to known gaps

Absence of support is [verified] by reference count across `src/PbiAssure.Core` and, where noted, by
executing a probe. Claims about what a construct references in real Desktop TMDL are [inferred] unless
stated.

### 6.1 RLS — `role` / `tablePermission`
- **Support:** [verified] unsupported. Zero references. `roles.tmdl` never opened. Probe confirms
  `Sales[Region]` → `ApparentlyUnused`, no diagnostic.
- **Impact:** `MayCreateDependencies`. [verified] the file is skipped; [inferred, high confidence] that
  RLS filters reference columns — the defining purpose of a table permission. Exact Desktop
  serialisation not established here.
- **Record:** `SupportState = NotYetAnalysed`, `ConstructType = "role"`, `Scope = SemanticModel`,
  `Concerns = [Dependency, Security]`.
- **Qualified:** absence-state objects in that model. **Unaffected:** all positive states, the whole
  report side, other models, all accessibility and navigation findings.

### 6.2 `refreshPolicy` / incremental refresh
- **Support:** [verified] unsupported — zero references to `refreshPolicy`, `rangeStart`, `RangeStart`.
- **Impact:** `DependencyEffectUnknown`. [inferred] probably relevant via a policy date column and
  `RangeStart`/`RangeEnd` parameters, but **it is not established whether the effect lands on the
  semantic graph, the Power Query graph, or both.** Desktop fixture required.
- **Reason text must differ from RLS** — for RLS we can say security filters reference columns; here the
  honest statement is that the effect is not yet determined.

### 6.3 KPI
- **Support:** [verified] unsupported. Probe [verified]: a `kpi` whose `targetExpression` referenced
  `Sales[Target]` left that column `ApparentlyUnused` with no diagnostic.
- **Impact:** `MayCreateDependencies` [verified for the probe shape; Desktop shape [inferred]].
- **Scope:** `Object`, with the owning measure recorded. **Containment only — the owner is not qualified
  for owning it** (§4.1).

### 6.4 `detailRows`
- **Support:** [verified] unsupported. Probe [verified]: `detailRowsDefinition` referencing
  `Sales[Region]` skipped; column left `ApparentlyUnused`.
- **Impact:** `MayCreateDependencies` [verified for probe shape].

### 6.5 `alternateOf` / aggregation mappings
- **Support:** [verified] unsupported — zero references.
- **Impact:** `DependencyEffectUnknown` until a fixture exists. [inferred] an aggregation mapping names a
  base column and table. Written by Desktop's Manage aggregations UI — serialisation must not be guessed.

### 6.6 Model-side `variation`
- **Support:** **Partial** [verified]. Report-side variation *is* handled —
  `PbirFieldReferenceExtractor.cs:105` reads `PropertyVariationSource` and resolves it to the underlying
  table and column. Model-side TMDL `variation` is unparsed [verified].
- **Impact:** `DependencyEffectUnknown`, and [inferred] **possibly lower than the others** — a variation
  names a relationship and a default hierarchy, and relationships are already parsed and already seed
  structural roots [verified]. Its targets may already be reachable by another path. **Inference, not
  evidence.**
- **This case is why `PartiallyAnalysed` exists.** Reporting variation as flatly unsupported would
  understate what the product does.

### 6.7 An unknown future construct
- **Impact:** `MayCreateDependencies` — the conservative default. Being wrong here produces an
  unnecessary caveat; being wrong the other way produces a confident deletion recommendation for a column
  something uses. The asymmetry of harm sets the default.
- **Interpretive constructs (§4.2) are the exception this cannot detect** — an unrecognised construct
  cannot be known to be interpretive. Accepted residual risk, recorded rather than hidden.
- **Why this case matters most:** it is the only one that keeps working without a code change when
  Microsoft ships something new.

### 6.8 Constructs that must qualify nothing — and the hard constraint

**[inferred — not yet verified]** `lineageTag`, `summarizeBy`, `dataCategory` and `isKey` appear to be
descriptive properties carrying no object references.

**[design decision — hard constraint, strengthened per review]**

> A limitation mechanism that fires on essentially every model is worse than the current silence.

`lineageTag` appears 33 times and `summarizeBy` 28 times in the small committed fixtures alone. If
property-level detection shipped before their dependency effect were verified, every model ever scanned
would carry limitations and every absence state would be qualified. The output would be uniformly
caveated, users would learn to ignore it, and the product would be worse off than saying nothing.

Therefore: **verification of §6.8 is a precondition for property-level detection, not a nice-to-have.**
Slice 1 excludes property-level detection entirely (§10).

---

## PART 7 — Revised internal model

### 7.1 Naming

**[design decision]** `AnalysisLimitation`. Rejected: `UnsupportedConstruct` / `UnknownConstruct` (encode
the discriminator in the type name — the migration problem of §3.2); `PartialSupportRecord` (same, and
describes one state); `ReviewRequired` (already taken — `AssessmentTypes.ReviewRequired` is an existing
finding-level concept [verified]).

### 7.2 The record

**[design decision]** All names are proposals.

```csharp
public sealed record AnalysisLimitation(
    string LimitationId,        // stable, e.g. "PBI-LIMIT-MODEL-ROLE"
    string Cause,               // ConstructNotSupported | ParseFailed        <- new in V2
    string SupportState,        // NotYetAnalysed | PartiallyAnalysed | Unrecognised | Analysed
    string ConstructType,       // from the registry, never inferred inline
    string Scope,               // SemanticModel | Table | Object
    string? SemanticModel,
    string? Table,
    string? ObjectName,         // containment only, where lexically known
    string ArtifactPath,
    string EvidencePath,
    string DependencyImpact,    // see 7.4
    IReadOnlyList<string> Concerns,  // Dependency | Security | Refresh | Presentation
    string Reason);
```

Changes from V1: **`Confidence` removed** (§5); **`Cause` added** to separate malformedness from support
state (§3.3); `Malformed` removed from `SupportState`.

`ArtifactPath` and `EvidencePath` deliberately mirror `AssuranceFinding` and
`UnresolvedSemanticDependency` [verified those fields exist on both].

### 7.3 The construct registry — now central, not an afterthought

**[design decision — promoted per review]** V1 mentioned a registry late and then proposed deriving
`ConstructType` from filenames in the first slice. The review is right that this scatters logic the
registry should own from day one.

**One registry is the single source of truth**, consulted by both detection and tests:

```csharp
internal sealed record ConstructRegistryEntry(
    string ConstructType,
    string MatchKind,        // DefinitionFile | TableBlock | Property
    string Pattern,          // "definition/roles.tmdl", "kpi", ...
    string Classification,   // see 7.5
    string SupportState,
    string DependencyImpact,
    IReadOnlyList<string> Concerns,
    string Reason);
```

Adding support later means **changing one registry entry**. Without this, every support addition needs a
matching deletion elsewhere, and eventually one is forgotten.

### 7.4 `DependencyImpact`

| Value | Meaning | Qualifies |
|---|---|---|
| `MayCreateDependencies` | Can contain object references. Default for `Unrecognised`. | absence states |
| `DependencyEffectUnknown` | Not yet determined — typically awaiting a Desktop fixture | absence states |
| `NoKnownDependencyEffect` | Established to carry no object references | **nothing** |
| `MayInvalidateExistingEvidence` | *Interpretive* — could change how existing evidence reads (§4.2) | absence **and positive** states |

The fourth value is **defined but unused today** — no known TMDL construct is interpretive [verified for
those examined]. It exists so the model can express the case without a schema change.

### 7.5 File classification — closing the packaging false positive

**[design decision — new in V2]** The review correctly warns against reporting packaging files as
unsupported semantic constructs.

This is a live risk: `CountDefinitionFiles` counts `.tmdl`, `.bim` **and `.pbism`** for semantic models
[verified]. A naive diff of "counted" against "parsed" would report `definition.pbism` — the artifact
manifest — as an unanalysed semantic construct on day one, in every project ever scanned.

Every definition file is classified by the registry into exactly one of:

| Classification | Meaning | Emits a limitation? |
|---|---|---|
| `Analysed` | Parsed into the inventory | No |
| `SemanticNotYetAnalysed` | Semantic content, not parsed | **Yes** |
| `Packaging` | Manifest / control file, correctly not parsed | No |
| `Unrecognised` | Unknown to this version | **Yes** |

Proposed initial classification [verified that none of the "not parsed" entries are read anywhere in
Core]:

| Path | Classification | Note |
|---|---|---|
| `definition/tables/*.tmdl` | `Analysed` | |
| `definition/relationships.tmdl` | `Analysed` | |
| `definition/expressions.tmdl` | `Analysed` | |
| `definition/roles.tmdl` | `SemanticNotYetAnalysed` | **the motivating case** |
| `definition/perspectives.tmdl` | `SemanticNotYetAnalysed` | [inferred] presentation-scoped; `Concerns = [Presentation]`, impact `DependencyEffectUnknown` |
| `definition/cultures/*.tmdl` | `SemanticNotYetAnalysed` | [inferred] translations; likely no usage effect, unverified |
| `definition/model.tmdl` | `SemanticNotYetAnalysed` | [inferred] carries model-level settings and table refs |
| `definition/database.tmdl` | `Packaging` | [inferred] compatibility level only |
| `definition.pbism` | `Packaging` | manifest |
| anything else | `Unrecognised` | the future-proofing default |

Two consequences:

- **Packaging files are explicitly classified, not silently ignored.** The invariant (§9) is "every file
  is classified", so re-introducing silence is a test failure. Only some classifications surface.
- **`Packaging` entries are [inferred] and reversible.** If `model.tmdl` or `database.tmdl` turn out to
  carry dependency-bearing content, they move classification — a one-line registry change.

**Report-side PBIR files are out of scope for slice 1** [design decision] — the motivating case is
model-side, and the PBIR file set is larger and less regular. Deferred, not forgotten.

### 7.6 Malformed files — the corrected treatment

**[design decision — revised]** `Cause = ParseFailed`, independent of `SupportState` (§3.3).

The review asks what scope should be qualified when an entire table file cannot be parsed. This case is
**more severe than a skipped construct**, and the difference is worth naming:

- A skipped construct leaves the **object inventory complete** and only edges missing.
- A malformed table file leaves the **object inventory itself incomplete** — the objects in that file do
  not appear at all.

So:

- **Objects in the malformed file:** nothing to qualify — they are absent from the inventory. This is a
  gap a per-object qualifier structurally cannot express.
- **Objects in other tables of the same model:** qualified under the normal §4.4 rule, since the
  unreadable file could have contained measures referencing them.
- **Counts are understated.** `SemanticTableCount`, `SemanticColumnCount` and the developer-object counts
  all silently under-report [verified these are computed from parsed models]. The design should mark
  model-level counts as incomplete when a `ParseFailed` limitation exists in that model.

Once parsing can recover, `Malformed` does **not** return to `SupportState` — it stays on `Cause`, which
is where instance-level failure belongs.

**The crash itself (§1.3) is a separate verified defect** with its own fix, tracked independently of this
design. Not implemented here.

---

## PART 8 — Fit with future RLS and security work

### 8.1 The design helps

1. **RLS support will never be complete.** Dynamic RLS using `USERNAME()`, `USERPRINCIPALNAME()`,
   `PATH()` or lookup tables cannot be fully resolved statically — the same class of limit
   `PBI-QUERY-001` already acknowledges for dynamic M [verified this precedent exists]. Shipping RLS
   without somewhere to record partial understanding would either overclaim or force this framework to be
   retrofitted under pressure.
2. **`PartiallyAnalysed` is where RLS will live for a long time** — roles detected but DAX unparsed; then
   DAX parsed but dynamic patterns not; then relationship propagation modelled. Each step is a registry
   value change.
3. **`AnalysisScopeBoundary` is what lets a Security surface be honest.** PBIP contains role *definitions*
   but never role *membership*, workspace permissions, app audiences or sharing. A Security tab unable to
   distinguish "not analysed yet" from "not in the file you gave us" would mislead on the highest-stakes
   subject the product touches — and §2 keeps that distinction structural rather than a matter of wording.

### 8.2 Preserved for later, not designed now

Model roles; table permissions; RLS DAX expressions; dynamic RLS patterns; dependencies used by security
filters; security propagation through relationships; implementation-quality findings. This design
constrains none of them.

---

## PART 9 — Test strategy

No tests written here.

### 9.1 The V1 contradiction, corrected

**The review caught a real inconsistency.** V1 stated the principle "assert what remains true after the
fix" and then proposed `RoleMetadataDoesNotContributeDependencyEdges` — which becomes **false** the day
RLS is correctly implemented. That is the same trap as naming a test after the wrong outcome, one level
removed. Withdrawn.

**Also withdrawn:** any test of the form
`RlsOnlyColumnIsNotYetRecognisedAndClassifiesApparentlyUnused`. A test named after a known-wrong outcome
asserts the bug; when support lands it fails, and whoever fixes it must decide from the name alone
whether the failure is progress or regression.

### 9.2 Durable invariants

**[design decision]** Tests assert either a **registry-to-behaviour consistency** or a **structural
invariant**. Both survive support changes because the registry is the single source of truth.

```
EveryDefinitionArtifactIsClassifiedByTheConstructRegistry
EveryRegistryEntryMarkedSemanticNotYetAnalysedProducesALimitation
EveryRegistryEntryMarkedAnalysedProducesNoLimitation
EveryRegistryEntryMarkedPackagingProducesNoLimitation
UnrecognisedDefinitionFilesProduceALimitation
LimitationsInOneModelDoNotQualifyAnotherModel
PositiveUsageStatesAreNeverQualifiedByCurrentImpactValues
NoKnownDependencyEffectQualifiesNothing
```

When RLS support lands, the `roles.tmdl` entry moves from `SemanticNotYetAnalysed` to `Analysed`, and the
same two tests then assert the opposite behaviour — **correctly, with no rename and no judgement call**.
That is the property V1's naming lacked.

### 9.3 Refinement to the invariant the review proposed

The review proposed `EveryDefinitionArtifactIsEitherAnalysedOrRecordedAsALimitation`. **That exact
invariant would fail on `definition.pbism`**, which is correctly neither analysed nor limited (§7.5).

The durable form is `EveryDefinitionArtifactIsClassifiedByTheConstructRegistry`, with analysed /
limited / packaging as outcomes. Same intent — silent disappearance becomes a test failure — without
encoding the packaging false positive into the invariant itself.

This is the backbone test. It is **impossible to satisfy today** and fails on exactly one file in the RLS
probe, making it a precise driver for slice 1.

### 9.4 Fixtures

**Synthetic, writable by hand with confidence** [verified — probes of this kind executed successfully
during this analysis]: `roles.tmdl` with a `tablePermission` referencing an otherwise-unused column; an
unrecognised definition file; a malformed TMDL file; a two-model project where only one model has
limitations; a project with only packaging files unparsed (the false-positive guard).

**Require Power BI Desktop** — hand-writing these would pin an assumption about the format rather than
Power BI's behaviour, the exact failure the `tab-order-states` fixture README exists to prevent:
`refreshPolicy`; `alternateOf`; model-side `variation`; and confirmation that real `kpi` / `detailRows`
serialisation matches the synthetic shapes in §1.2.

### 9.5 Layers

| Layer | Target |
|---|---|
| Unit — registry | classification is total and unambiguous; no path matches two entries |
| Unit — propagation | positive states never qualified; per-model isolation; `NoKnownDependencyEffect` qualifies nothing |
| Integration — `ProjectScanner` | synthetic on-disk PBIP, matching the existing `ProjectScannerTests` idiom [verified as the repo convention] |
| Integration — CLI / output | limitation survives to JSON; `ClassificationConfidence` reaches CSV |
| Invariant | §9.3 |

---

## PART 10 — Conclusions

### 1. Revised `AnalysisLimitation` model

Single record (§7.2) with `Cause` (`ConstructNotSupported` | `ParseFailed`) **separate from**
`SupportState` (`NotYetAnalysed` | `PartiallyAnalysed` | `Unrecognised` | `Analysed`), a
`DependencyImpact` of four values including the unused-but-defined `MayInvalidateExistingEvidence`, and
**no `Confidence` field**. Driven entirely by one construct registry (§7.3). Kept strictly separate from
`UnresolvedSemanticDependency`, which describes bounded uncertainty (§3.1).

### 2. Is `AnalysisScopeBoundary` separate? — **Yes**

A separate static catalog, shaped like `AssuranceRuleCatalog`, with contextual relevance conditions
(§2.4). It has no artifact path, does not vary per scan, does not disappear when support is added, and
**never propagates uncertainty** — because `ApparentlyUnused` is already defined in terms of the analysed
scope in `CONTRIBUTING.md:29` [verified], so propagating it would double-count a caveat the state's own
definition carries.

### 3. Revised uncertainty-propagation rule

Per semantic model: if any `AnalysisLimitation` has `DependencyImpact` of `MayCreateDependencies` or
`DependencyEffectUnknown`, mark every `ApparentlyUnused` and `UsedOnlyByUnusedBranch` object in *that
model* as `ClassificationConfidence = QualifiedByLimitation`. No state changes; no sixth state; other
models untouched; boundaries never propagate.

Positive states are preserved as a **conservative product rule grounded in every known construct being
referential** — not as a proof (§4.2). Should an interpretive construct ever be identified, it takes
`MayInvalidateExistingEvidence`, and only then are positive states qualified.

### 4. Exact first implementation slice

**Registry-driven definition-file classification for semantic models. Detection only.**

1. Add the construct registry with `MatchKind = DefinitionFile` entries exactly as tabulated in §7.5.
2. Classify every file `CountDefinitionFiles` enumerates for each `.SemanticModel` artifact against it.
3. Emit one `AnalysisLimitation` per file classified `SemanticNotYetAnalysed` or `Unrecognised`.
4. Expose `ProjectInventory.AnalysisLimitations`.

Why this slice:

- **It fixes the motivating case.** RLS is lost at file level (§1.1), so this alone closes the silence.
- **It touches no parser logic** — a comparison of two enumerations that already exist.
- **It needs no new Power BI semantics**, so none of the §11 open questions block it.
- **It is provable immediately** by §9.3, which fails today and passes after.
- **It cannot produce the packaging false positive**, because classification is explicit from the start
  rather than inferred from filenames.

**Explicitly excluded from slice 1:** block-level detection; **all property-level detection** (§6.8 is a
precondition); the `ClassificationConfidence` qualifier and its propagation; malformed-TMDL recovery; the
`AnalysisScopeBoundary` catalog; every user-facing surface; report-side PBIR files; lexical impact
narrowing.

Slice 1 deliberately changes **no** classification and **no** output surface. It creates the record and
proves nothing disappears.

### 5. Durable test invariants

`EveryDefinitionArtifactIsClassifiedByTheConstructRegistry` (backbone, fails today), plus the
registry-to-behaviour consistency tests in §9.2. No test encodes a known-wrong usage outcome as desired
behaviour. Behaviour changes naturally when a registry entry moves.

### 6. Rejected or refined from this review

| Review point | Response |
|---|---|
| 1 — separate `NotInArtifact` | **Accepted in full.** Strengthened with the `CONTRIBUTING.md:29` argument, which makes it structural rather than stylistic |
| 2 — drop runtime `Confidence` | **Accepted in full.** Also identified an active hazard: users would read it as uncertainty about *their model* |
| 3 — positive-state reasoning is not a proof | **Accepted.** Behaviour deliberately **unchanged**; only its epistemic status revised. Added the referential/interpretive distinction and `MayInvalidateExistingEvidence` so the gap is expressible rather than merely acknowledged |
| 4 — test contradiction | **Accepted in full.** `RoleMetadataDoesNotContributeDependencyEdges` withdrawn |
| 4 — proposed invariant wording | **Refined, not adopted verbatim.** `...EitherAnalysedOrRecordedAsALimitation` would fail on `definition.pbism`; corrected to `...ClassifiedByTheConstructRegistry` (§9.3) |
| 5 — registry from the start | **Accepted in full**, and promoted to the centre of the design |
| 6 — no property-level detection in slice 1 | **Accepted**, and hardened into a stated precondition |
| 7 — malformed treatment | **Accepted**, and it exposed a V1 modelling error: `Malformed` was on the wrong axis. Added `Cause`, plus the incomplete-object-set vs incomplete-edges distinction |

**Nothing in the review was rejected.** The only deviation is the §9.3 invariant wording, refined for a
reason the review's own point 5 supplies.

### 7. Open questions requiring Power BI Desktop

1. **Whether `lineageTag`, `summarizeBy`, `dataCategory`, `isKey` are genuinely reference-free.**
   **Highest priority** — getting this wrong makes every model qualified and destroys the signal (§6.8).
   Blocks property-level detection entirely.
2. Real Desktop serialisation of `kpi`, `detailRows`, `alternateOf`, model-side `variation` — the probes
   here were hand-written.
3. Whether `refreshPolicy` affects the semantic graph, the Power Query graph, or both.
4. Whether model-side `variation` has any impact beyond what relationship parsing already covers — if
   not, it becomes `NoKnownDependencyEffect`.
5. Whether `roles.tmdl` is the only location for role definitions, or whether they can appear in
   `model.tmdl`.
6. Whether `model.tmdl` and `database.tmdl` carry dependency-bearing content (affects §7.5 classification).

**None of these block slice 1**, which is why slice 1 is scoped as it is.

### 8. Is the design ready for implementation?

**Slice 1: yes.** It is registry-driven, additive, changes no classification and no output, is provable by
a test that fails today, and depends on none of the open questions.

**Slices 2+: not yet.** The propagation rule is sound but should not ship before at least open question 1
is answered, because that answer determines whether the mechanism is precise or universally noisy — the
difference between the design working and actively harming the product.

**Recommended sequencing:** implement slice 1 → answer open question 1 with a Desktop fixture → implement
propagation and the user-facing surface → then RLS.

**Should this precede RLS support?** Yes. The motivating defect is not the missing RLS feature; it is the
*silence*, which affects five other constructs today and every future one, and is far cheaper to fix.
Building RLS first would ship a version confidently wrong about `kpi`, `detailRows`, `alternateOf` and
`variation` while being right about roles — the same defect, quietly relocated.

---

## Scope statement

Read-only with respect to the repository. No production code, tests, fixtures or existing documentation
modified. No rules added. HTML output and accessibility logic untouched; `PBI-ACCESS-001` not worked on;
RLS not implemented.

Verification: reading `src/PbiAssure.Core` at `b84d487`, and executing the CLI against three synthetic
PBIP projects in a temporary scratch directory outside the repository — an RLS probe, an unknown-block
probe and a malformed-TMDL probe. Their output is quoted in §0 and §1. All names for proposed types,
fields and values are proposals for review, not decisions.

# Encountered PBIR schema compatibility policy

Date: 2026-08-20

Scope: report-side PBIR JSON only

Status: investigation and adopted policy; no production gate or finding implemented

Evidence labels used below:

- **[verified by Power BI Desktop-authored fixture]** — observed in a committed fixture created and
  saved by Power BI Desktop.
- **[verified in repository]** — established by current source, tests or a controlled repository
  measurement.
- **[Microsoft-documented]** — stated in Microsoft's PBIR documentation or published schema catalogue.
- **[inferred]** — a conservative consequence of the evidence, not a Microsoft compatibility promise.
- **[design decision]** — the policy PBI Assure will follow.

## Executive decision

PBI Assure will use **verified schema baseline** rather than a binary claim that a whole PBIR version is
supported or unsupported.

An artifact is within the verified schema baseline when:

1. its schema URI belongs to the expected Microsoft PBIR artifact family;
2. its exact schema version has Power BI Desktop-authored fixture evidence; and
3. the PBI Assure capability using that artifact has tests against the relevant shape.

This is deliberately capability-specific. An exact schema match does not prove that PBI Assure analyses
every optional construct allowed by that schema. Conversely, a different version is **unverified**, not
automatically unsupported. A parser completing without error is not sufficient evidence of full support,
because the current property-wise parsers ignore fields they do not recognise.

Schema compatibility is primarily **analysis coverage / scan metadata**, not a defect in the user's
Power BI project. No compatibility finding is justified yet.

## Microsoft format boundary

Microsoft documents modern PBIR as a `definition/` folder whose report, pages, visuals, bookmarks and
extensions are separate JSON files with public, versioned schema declarations. Microsoft also documents
`definition/version.json` as the PBIR file version that helps determine which files are loaded, and
distinguishes modern PBIR from PBIR-Legacy's single `report.json` representation.

Sources:

- [Power BI Desktop project report folder](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-report)
- [Microsoft PBIR JSON schema catalogue](https://github.com/microsoft/json-schemas/tree/main/fabric/item/report)

Microsoft says schema URLs define document versions that will change as the report definition evolves.
The reviewed documentation does **not** promise semantic-version compatibility between schema releases.
PBI Assure therefore must not infer compatibility from major/minor/patch numbers alone.

PBIR-Legacy is a separate representation, not an old version of a modern PBIR schema family. Semantic
model TMDL is also outside this policy: its `compatibilityLevel` and TMDL grammar require a separate
support policy rather than being blended into report JSON schema handling.

## Current schema surface

The committed Desktop fixtures share the following modern PBIR schema baseline. Controlled derivative
fixtures retain the same versions, but are not counted as independent Desktop evidence.

| Artifact / retained property | File | Microsoft family and exact Desktop-fixture version | Other repository evidence | Current behaviour and risk |
|---|---|---|---|---|
| `ReportModelConnectionInventory.SchemaUri`; `Version` | `<report>.Report/definition.pbir` | `definitionProperties/2.0.0`; separate value `4.0` **[verified by Power BI Desktop-authored fixture]** | Microsoft documentation shows the same shape and also documents a `definitionProperties/1.0.0` example **[Microsoft-documented]** | Parsed property-wise; no version branch. New dataset-reference shapes could be missed. |
| `ReportInventory.SchemaUri` | `definition/report.json` | `report/3.3.0` **[verified by Power BI Desktop-authored fixture]** | Synthetic tests retain arbitrary `example.test` URIs **[verified in repository]** | Parsed property-wise; no version branch. New report filters, resources or settings could be missed. |
| `ReportInventory.PagesSchemaUri` | `definition/pages/pages.json` | `pagesMetadata/1.1.0` **[verified by Power BI Desktop-authored fixture]** | Synthetic URI-retention tests only **[verified in repository]** | Parsed property-wise; no version branch. New navigation/page metadata could be missed. |
| `PageInventory.SchemaUri` | `definition/pages/<page>/page.json` | `page/2.1.0` **[verified by Power BI Desktop-authored fixture]** | Synthetic URI-retention tests only **[verified in repository]** | Parsed property-wise; no version branch. New page filters, settings or formatting could be missed. |
| `VisualInventory.SchemaUri`; `VisualGroupInventory.SchemaUri` | `definition/pages/<page>/visuals/<visual>/visual.json` | `visualContainer/2.11.0` **[verified by Power BI Desktop-authored fixture]** | Synthetic URI-retention tests only **[verified in repository]** | Parsed property-wise; no version branch. New visual/query/filter/action structures could be silently ignored. |
| `ReportInventory.BookmarksSchemaUri` | `definition/bookmarks/bookmarks.json` | No committed Desktop-fixture evidence | `bookmarksMetadata/1.0.0` in local sample projects; arbitrary synthetic URI retained **[verified in repository]** | Parsed property-wise; no version branch. New ordering/group metadata could be missed. Local samples are not a public verified baseline. |
| `BookmarkInventory.SchemaUri` | `definition/bookmarks/*.bookmark.json` | No committed Desktop-fixture evidence | `bookmark/2.1.0` in local sample projects; arbitrary synthetic URI retained **[verified in repository]** | Parsed property-wise; no version branch. New bookmark state, targets or actions could be missed. |
| `ReportInventory.ReportExtensionsSchemaUri` | `definition/reportExtensions.json` | No committed Desktop-fixture evidence | `reportExtension/1.0.0` appears only in synthetic tests **[verified in repository]** | Parsed property-wise; no version branch. New extension item types or reference forms could be missed. |
| `ThemeMetadataInventory.SchemaUri` | selected theme resource JSON | No report-schema baseline established | The renderer retains a theme resource's own `$schema` when present **[verified in repository]** | This is theme-resource metadata, not a core PBIR artifact family. It belongs to a separate theme-schema policy. |

### Encountered but not retained as schema inventory

| Artifact | Evidence | Consequence |
|---|---|---|
| `definition/version.json` | `versionMetadata/1.0.0` and PBIR definition version `2.0.0` across the committed modern Desktop fixtures **[verified by Power BI Desktop-authored fixture]** | Required by Microsoft and materially relevant, but currently neither parsed nor retained. This is the most important gap before implementing compatibility presentation. |
| visual `mobile.json` | `visualContainerMobileState/2.6.0` in local sample projects **[verified in repository]** | Not parsed or retained; it must not be implied to be covered by the current visual schema URI. |
| report `.pbi/localSettings.json` | `report/localSettings/1.0.0` in local projects **[verified in repository]** | Machine/user-local metadata, already outside normal report analysis and source control. Exclude from the first compatibility slice. |

The committed Desktop baseline covers multiple independent fixtures at the versions shown above. The
broken model-reference fixtures and the removed-tab-order state are controlled derivatives and should
not be used to inflate the independent evidence count.

## What the parsers do today

- No report parser branches on a schema URI, schema family or schema version **[verified in repository]**.
- Known properties are read with JSON property/kind checks; unknown properties are ignored
  **[verified in repository]**.
- A missing `$schema`, or one with the wrong JSON type, becomes `null` and parsing continues. Those two
  cases are not currently distinguishable **[verified in repository]**.
- A malformed URI stored as a JSON string is retained verbatim and parsing continues
  **[verified in repository]**.
- Invalid JSON fails the scan as invalid data. That is a syntax/read failure, not a schema-version
  classification **[verified in repository]**.
- A newer same-family artifact will probably retain all shapes PBI Assure already recognises, but any new,
  moved or retyped construct can be silently missed **[inferred]**.

## Proposed support-state model

The first implementation should parse schema metadata structurally and record raw evidence. Suggested
states (names are illustrative until a domain type is designed):

| State | Meaning |
|---|---|
| `VerifiedExact` | Expected family and exact version in PBI Assure's fixture-backed baseline. |
| `RecognisedUnverifiedVersion` | Expected Microsoft family, but the exact version is not fixture-backed (older or newer). |
| `UnknownFamily` | A schema URI was supplied but its origin/family does not match the expected artifact family. |
| `MetadataMissing` | No usable `$schema` value was supplied. |
| `MetadataMalformed` | A nonblank schema value exists but cannot be parsed into the expected URI/family/version shape. |
| `OutsideSupportedFormat` | The report uses a genuinely separate representation such as PBIR-Legacy. This is a discovery/format state, not a schema-version comparison. |

Each observation should preserve artifact kind, source path, raw schema text, parsed family, parsed semantic
version and state. The URI form observed in Microsoft schemas is stable enough for structured parsing,
but raw text must remain available. Do not compare versions or classify families with ad-hoc substring or
diagnostic-prose checks.

## User-visible policy

| Encountered state | Policy |
|---|---|
| Exact verified version | Silent. The schema baseline may be available in technical inventory. |
| Older version in the expected family | Analysis coverage / scan metadata only: recognised but not fixture-verified. Continue property-wise parsing. |
| Newer patch version in the expected family | Analysis coverage / scan metadata only: newer than the verified baseline. Continue parsing; do not assume patch compatibility. |
| Newer minor version in the expected family | Same as newer patch: a restrained review note, not a project warning. |
| Newer major version in the expected family | Stronger Analysis coverage review wording because structural change is more plausible, but still not a finding or automatic hard failure while known properties can be read. |
| Completely unknown family/URI | Analysis coverage / scan metadata Review required. Explain that coverage could not be verified. Do not say the Power BI report is invalid. |
| Missing `$schema` | Inventory/coverage note only, normally silent in Findings. Continue parsing known properties. Absence alone is not proof of a defect. |
| Malformed/unparseable schema URI | Analysis coverage Review required. Preserve the raw value in technical details; continue parsing if the JSON itself is usable. |
| PBIR-Legacy or another separate representation | Handle explicitly at format discovery. If that representation is not supported, report an unsupported scan state—not a modern PBIR version finding. |
| Malformed JSON / unreadable required file | Existing scan failure behaviour remains appropriate; this is not a compatibility-policy outcome. |

The presentation should distinguish **PBI Assure has not verified this schema version** from **PBI Assure
could not read this report**. Only the second supports a hard failure.

## Is `PBI-COMPAT-003` justified?

Not yet **[design decision]**.

The honest first surface is Analysis coverage or scan metadata. A different schema version describes the
limits of PBI Assure's evidence; it is not itself a defect in the Power BI project. Adding a Warning /
Finding now would create false urgency and imply more certainty than the parsers possess.

If later user testing shows that this information is missed unless it appears in Findings, a narrowly
scoped rule could be reconsidered:

- **Name:** PBIR schema newer than verified support
- **Trigger:** an artifact consumed by PBI Assure has the expected Microsoft family and a parsed version
  newer than the fixture-backed baseline.
- **Non-triggers:** exact baseline, older version, missing metadata, malformed metadata, unknown family,
  PBIR-Legacy and ordinary parser limitations. Those are distinct coverage/format states.
- **Category:** Compatibility
- **Severity / assessment:** Information / Review required
- **Boundary:** “PBI Assure has not verified this newer schema version; review analysis coverage.” Never
  “unsupported report”, “invalid report” or a claim that Microsoft introduced a breaking change.

That future rule should group by report, artifact family and encountered version. It should not be
implemented until the structured observations and coverage UI exist.

## Minimum implementation and fixture plan

Recommended next slice:

1. Add a small report-side schema observation model and robust parser for the canonical Microsoft URI
   shape, preserving raw text.
2. Retain `definition/version.json` schema URI and PBIR definition version alongside the currently retained
   artifact schemas.
3. Classify each artifact independently against a central verified-baseline registry.
4. Surface non-exact states in Analysis coverage / scan metadata only. Do not add a finding.
5. Keep parsing behaviour unchanged; classification observes, it does not gate.

Focused tests should use:

- current Desktop fixtures to pin the exact verified baseline;
- small synthetic URI mutations for older, newer patch/minor/major, unknown family, missing and malformed
  cases;
- a test proving known properties still parse for `RecognisedUnverifiedVersion`;
- tests proving malformed metadata is distinct from malformed JSON and PBIR-Legacy;
- deterministic grouping and HTML escaping tests for any eventual presentation.

Do not copy dozens of fixtures. Add a new Desktop-authored fixture only when Desktop actually emits a new
schema version or when a currently sample/synthetic-only family needs verified support. The smallest
future evidence fixture would contain one report page, one ordinary visual, one bookmark and—if Desktop
can author it validly—one report extension. Its README must record Desktop version, exact creation steps,
save/reopen behaviour and every emitted schema URI.

## Remaining evidence gaps

- No committed Desktop-authored bookmark or bookmarks-metadata schema example.
- No committed Desktop-authored `reportExtension` schema example; current evidence is synthetic only.
- No fixture-backed evidence for a newer or older version of the same artifact family, so graceful parsing
  is an implementation inference rather than a compatibility guarantee.
- `mobile.json` is encountered locally but not retained or analysed.
- The current parser cannot distinguish a missing `$schema` from a non-string `$schema` without retaining
  more source-shape evidence.
- Microsoft documents versioned schemas but the reviewed primary documentation contains no promise that
  semantic-version increments are backward compatible.

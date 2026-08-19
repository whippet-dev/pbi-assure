# Desktop semantic constructs fixture

A Power BI Desktop-authored PBIP fixture preserving how Desktop actually serialises row-level security
roles, a perspective, a DAX user-defined function, and the model-level files emitted for every semantic
model.

It exists because the semantic-model definition-file registry in `SemanticDefinitionFileRegistry` was
built from Microsoft documentation. This fixture is the evidence that the documented paths match what
Desktop emits.

## Provenance

| | |
|---|---|
| Power BI Desktop release | July 2026 |
| Product version | `2.156.951.0 (26.07)+c9381f8e5efc99c8de04425f1572e841914690d8 (x64)` |
| Date authored | 2026-08-19 |
| Model mode | Import |
| Compatibility level | 1702 (upgraded from 1606 — see below) |
| Data | **Synthetic.** Created with **Home → Enter data**; no external source, no credentials, no gateway |

The three data rows use `example.test` addresses, a reserved TLD for testing. Nothing here is real
organisation data.

## Model

Table `Sales`, created with Enter data, with columns `Region`, `UserEmail`, `Amount`, `Date`, and one
measure:

```dax
Total Amount = SUM(Sales[Amount])
```

The `Date` column caused Power BI's normal Auto Date/Time artefacts to appear:
`DateTableTemplate_c184ab56-…` and `LocalDateTable_98928d87-…`, plus the relationship joining
`Sales.Date` to the local date table. **These are genuine Desktop output and must not be removed or
tidied** — they are also why this fixture exercises the `StructurallyRequired` classification.

## How each construct was authored

Three different authoring routes are represented, and the distinction matters when reasoning about what
this fixture proves.

| Construct | Route | Notes |
|---|---|---|
| Table, measure, date artefacts | **GUI-authored** | Home → Enter data |
| RLS roles | **GUI-authored** | Modeling → Manage roles, DAX editor |
| Perspective | **TMDL-View-authored** | Desktop has no GUI for perspectives |
| DAX user-defined function | **TMDL-View-authored** | Applied through TMDL view |

All four are **Desktop-serialized**: whatever the authoring route, Power BI Desktop wrote the files on
disk. TMDL view applies a script to the model inside Desktop, so its output is Desktop serialization —
not a hand-written file.

### Row-level security — GUI-authored

Two roles were created through **Modeling → Manage roles**, using the DAX editor:

- `RegionalManager` → `[Region] = "West"`
- `DynamicUser` → `[UserEmail] = USERPRINCIPALNAME()`

*Incidental authoring observation, not a PBI Assure finding:* the first attempt to create both roles in
one pass failed with `RoleDoesntExistInModel` — "The model doesn't have a role named Untitled." This
appears to come from the new-role placeholder/rename state in the Manage roles dialog. Creating the
roles one at a time and committing each name explicitly resolved it. Recorded only so a future reader
reproducing the fixture is not surprised; it says nothing about PBI Assure.

### Perspective and function — TMDL-View-authored

The perspective was applied from TMDL view as:

```tmdl
createOrReplace
    perspective SalesView
        perspectiveTable Sales
            perspectiveMeasure 'Total Amount'
            perspectiveColumn Region
```

The function was applied as:

```tmdl
createOrReplace
    /// AddTax takes in amount and returns amount including tax
    function AddTax = (amount : NUMERIC) => amount * 1.1
```

Applying the function required a **semantic-model compatibility-level upgrade from 1606 to 1702**.
Desktop prompted; the upgrade was accepted and the script re-applied successfully. `database.tmdl` now
records `compatibilityLevel: 1702`.

## Emitted paths

Every path below is what Desktop produced, relative to `desktop-semantic-constructs.SemanticModel/`:

```
definition.pbism
.platform
.pbi/editorSettings.json
definition/database.tmdl
definition/model.tmdl
definition/relationships.tmdl
definition/functions.tmdl
definition/cultures/en-US.tmdl
definition/perspectives/SalesView.tmdl
definition/roles/DynamicUser.tmdl
definition/roles/RegionalManager.tmdl
definition/tables/Sales.tmdl
definition/tables/DateTableTemplate_c184ab56-f593-459e-b52f-b71fbc0c8705.tmdl
definition/tables/LocalDateTable_98928d87-792b-4767-bd98-019e93b0a083.tmdl
TMDLScripts/Script 1.tmdl
TMDLScripts/Script 2.tmdl
TMDLScripts/.pbi/tmdlScripts.json
```

`dataSources.tmdl` was **not** emitted. Neither was `model.bim`; this project uses the TMDL format
(`definition.pbism` reports `"version": "4.2"`).

## Representative emitted content

### Roles — note the unqualified column references

`definition/roles/RegionalManager.tmdl`:

```tmdl
role RegionalManager
	modelPermission: read

	tablePermission Sales = [Region] = "West"

	annotation PBI_Id = 9949dfdbc56843c186a081639d68d821
```

`definition/roles/DynamicUser.tmdl`:

```tmdl
role DynamicUser
	modelPermission: read

	tablePermission Sales = [UserEmail] = USERPRINCIPALNAME()

	annotation PBI_Id = 174dc7c840d94300ac49ccfd98b41bd1
```

**Desktop serialises the column reference as `[Region]`, not `Sales[Region]`.** The owning table appears
once, on the `tablePermission` line, and the filter expression itself uses an unqualified reference.
RLS dependency parsing resolves unqualified references against the table named by the `tablePermission`
rather than assuming a qualified `Table[Column]` form — this fixture is what established that.

### Perspective — names model objects

`definition/perspectives/SalesView.tmdl`:

```tmdl
perspective SalesView

	perspectiveTable Sales

		perspectiveMeasure 'Total Amount'

		perspectiveColumn Region
```

### Function

`definition/functions.tmdl`:

```tmdl
/// AddTax takes in amount and returns amount including tax
function AddTax = (amount : NUMERIC) => amount * 1.1
	lineageTag: f18f288f-7652-462a-8ee5-2023b5592778
```

### Model-level files

`definition/database.tmdl` in full:

```tmdl
database
	compatibilityLevel: 1702
```

`definition/model.tmdl` ends with `ref` declarations covering **every** object in each collection:

```tmdl
ref table Sales
ref table DateTableTemplate_c184ab56-f593-459e-b52f-b71fbc0c8705
ref table LocalDateTable_98928d87-792b-4767-bd98-019e93b0a083

ref role RegionalManager
ref role DynamicUser

ref perspective SalesView

ref cultureInfo en-US
```

`definition/cultures/en-US.tmdl` in full — an empty culture, no translations:

```tmdl
cultureInfo en-US
```

### Model-side variation

`definition/tables/Sales.tmdl` contains a Desktop-emitted model-side `variation` block on the `Date`
column, referencing the auto date-table relationship and hierarchy:

```tmdl
		variation Variation
			isDefault
			relationship: e3dd0bea-f87d-4bdf-9e0d-b178ef404114
			defaultHierarchy: LocalDateTable_98928d87-792b-4767-bd98-019e93b0a083.'Date Hierarchy'
```

### TMDL view editor artefacts

Because the perspective and function were authored in TMDL view, Desktop also emitted `TMDLScripts/`
containing the two script tabs and `TMDLScripts/.pbi/tmdlScripts.json` recording tab order and the
default tab. These are **editor artefacts, not semantic-model definition**, even though they carry the
`.tmdl` extension and sit inside the semantic-model folder.

## Reopen and save stability

The project was closed completely, reopened successfully with no warning or error, saved without any
intentional change, and closed again. A snapshot taken before reopening was compared by SHA-256.

**Every semantic-model definition file, every TMDL view script, `.platform` and `definition.pbism` was
byte-for-byte unchanged.** No normalisation of any kind occurred.

Two files differed, neither committed here:

- `.pbi/editorSettings.json` — the line `"runBackgroundAnalysis": true` was removed
- `.pbi/localSettings.json` — its encrypted `securityBindingsSignature` changed

Unlike `tab-order-states`, this fixture therefore has **no pre-save state to protect**, and reopening it
in Desktop is not known to be destructive. Even so, prefer working on a copy.

## What this fixture proves

- Roles serialise as **one file per role** under `definition/roles/`
- Perspectives serialise as **one file per perspective** under `definition/perspectives/`
- Cultures serialise as **one file per culture** under `definition/cultures/`
- DAX user-defined functions serialise into a single **`definition/functions.tmdl`**
- `model.tmdl` and `database.tmdl` are emitted for every model, and `model.tmdl`'s `ref` declarations
  list every object in a collection regardless of whether anything uses it
- A default culture file contains **no translations and no object references**
- TMDL view authoring produces `TMDLScripts/` editor artefacts alongside the model definition
- RLS filter expressions reference columns **unqualified**
- Perspectives **do** name tables, measures and columns
- Reopening and saving does not normalise any semantic definition file, at this Desktop version

## What this fixture does NOT prove

- **How a UDF that references a model object is serialised.** `AddTax` deliberately uses only its
  parameter. A function referencing `Sales[Amount]` may serialise differently
- **What a culture containing actual translations looks like.** This one is empty
- **Whether `dataSources.tmdl` is ever emitted**, or by what kind of source. It is absent here
- **Whether `model.bim` (TMSL) can still be produced** by this Desktop version
- **Anything about other Desktop versions, locales, or DirectQuery/Direct Lake models.** Import only,
  one version, one machine
- **That role support is complete.** Table permission *filters* are now parsed, so `Sales[Region]` and
  `Sales[UserEmail]` are correctly `StructurallyRequired` rather than deletion candidates. Column
  permissions — object-level security, which `TablePermission` also carries — are still not read, so
  roles remain a partially analysed construct and still record a limitation

## Do not regenerate this fixture from hand-written TMDL

If these files are lost, they must be **re-authored in Power BI Desktop**. A hand-written PBIP proves
nothing about what Desktop emits, which is this fixture's entire purpose, and describing hand-written
files as Desktop-authored would corrupt the evidence base other decisions rest on.

Deliberately not committed: `.pbi/cache.abf` (an Analysis Services backup that can contain data),
`.pbi/localSettings.json` (machine-specific, contains an encrypted signature), and the
`before-reopen.SemanticModel` comparison snapshot.

## One difference from Desktop's bytes: line endings

Power BI Desktop wrote these files with CRLF line endings. The repository's `.gitattributes` sets
`* text=auto eol=lf`, so they are stored and checked out with LF. **Content is otherwise preserved
exactly** — no reformatting, no GUID rewriting, no whitespace tidying, no removal of generated date
tables.

This matches how the existing Desktop-authored fixtures are already stored, so the fixture is consistent
with them. If a future question depends on Desktop's exact line endings, this fixture cannot answer it;
compare against a freshly authored project instead.

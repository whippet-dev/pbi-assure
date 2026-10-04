# Desktop dynamic-text persistence evidence

This controlled project was created in current Power BI Desktop using the Desktop UI for both a text-box dynamic value bound to `_Measures[Headline]` and a custom Narrative/dynamic-text value bound to `_Measures[Narrative]`. It uses synthetic Enter Data content, with no private or production data and no manual PBIR or TMDL edits.

## Provenance

The user saved the project as PBIP, closed and reopened it, verified the bindings, and saved again. The user performed and reported this Desktop round trip; Codex inspected the supplied persisted files rather than independently performing those UI interactions. The exact Desktop build was not recorded.

The supplied fixture files are preserved unchanged. Desktop cache and local settings files remain excluded from Git by the repository's existing ignore rules.

## Persisted bindings

Both visual definitions use `visualType: "textbox"`, including the custom Narrative value. Neither has an ordinary visual query/projection block; the saved Narrative is text-box-style metadata rather than a separate custom-visual payload.

In each visual:

- `visual.objects.general[0].properties.paragraphs[0].textRuns[0].value` contains `propertyIdentifier: { objectName: "values", propertyName: "expr" }` and `selector: { id: "Value" }`.
- The matching `visual.objects.values[0]` entry has the same selector ID and stores the binding under `properties.expr.expr.Measure`.
- That measure expression contains `Expression.SourceRef.Entity: "_Measures"` and `Property: "Headline"` or `"Narrative"` respectively.
- The expression also retains `Annotations.NaturalLanguage`, with `version: 1`, `kind: "NaturalLanguage"` and `annotation.name: "Value"`.

PBI Assure's existing reference extractor recognises the explicit measure expression at `$.visual.objects.values[0].properties.expr.expr.Measure` as an active formatting-property reference. The paragraph's property identifier connects the text run to the saved value; it is not itself another semantic reference.

## Regression boundary

[The fixture tests](../../PbiAssure.Core.Tests/DesktopDynamicTextEvidenceFixtureTests.cs) pin:

| Objects | Usage state |
| --- | --- |
| Headline and Narrative | DirectlyUsed, each at one report location with formatting usage evidence |
| Total Sales and Sales Amount | IndirectlyUsed |
| Unused Measure and dummy Column1 | ApparentlyUnused |
| Sales UnusedValue | UsedOnlyByUnusedBranch |

All tested classifications have `Established` confidence, with no unresolved semantic references or qualifying semantic limitations. Informational coverage items may remain.

The page contains only these two text-box-style bindings. No ordinary chart, card or table directly uses Total Sales, Amount or the unused branch. This fixture establishes the saved shapes and resulting analysis for this Desktop workflow; it does not prove every dynamic-text/Narrative format or independently validate rendered text.

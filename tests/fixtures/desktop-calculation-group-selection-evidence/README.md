# Desktop calculation-group selection evidence

This small Power BI Desktop PBIP project uses synthetic data to isolate calculation-group default selection behaviour.

## Provenance

The fixture was created in current Power BI Desktop, saved as PBIP, closed, reopened and saved again. The user performed and reported this round trip; Codex did not independently perform the Desktop interactions. The exact Desktop build was not recorded.

On that round trip, Desktop:

- retained `noSelectionExpression` and its nested `formatStringDefinition`;
- persisted the `Conversion` calculation-group table without a partition block;
- reordered `noSelectionExpression` ahead of the calculation items.

The retained `Conversion.tmdl` confirms the expressions, their nesting, the final declaration order and the absence of a partition block. The change in ordering is the user's reported observation.

## Controlled model

The report contains a single card using `Sales[Total]`. It does not reference the `Conversion` calculation group through a visual, slicer or filter.

- The default `noSelectionExpression` references `Rates[DefaultRate]`; its nested format expression references `Rates[DefaultFormat]`.
- The `Converted` calculation item references `Rates[Rate]` and its format expression references `Rates[FormatString]`.
- `Rates[Notes]` is an unused control.

## Evidence boundary

[The fixture regression tests](../../PbiAssure.Core.Tests/DesktopCalculationGroupSelectionEvidenceFixtureTests.cs) verify the persisted shape and analysis: default selection dependencies are `StructurallyRequired`, ordinary calculation-item dependencies remain `UsedOnlyByUnusedBranch`, and the unused control remains `ApparentlyUnused`, all with `Established` confidence.

This fixture establishes the retained Desktop shape for `noSelectionExpression` and its owned format string. It does not establish every calculation-group authoring workflow or Desktop serialization of multiple/empty-selection expressions; those additional parser controls use synthetic tests.

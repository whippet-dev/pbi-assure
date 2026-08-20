# Desktop object-level security evidence

This is a small, synthetic Power BI Desktop-authored PBIP fixture for bounded object-level security (OLS) support. It contains no work, personal or connection data.

## What it proves

The role definition in `desktop-ols-evidence.SemanticModel/definition/roles/RestrictedViewer.tmdl` retains these Power BI Desktop TMDL forms:

```tmdl
role RestrictedViewer
    modelPermission: read

    tablePermission Employee
        columnPermission Salary = none

    tablePermission Confidential
        metadataPermission: none
```

- `columnPermission Salary = none` is an explicitly named column-level OLS permission.
- `metadataPermission: none` is a table-level OLS permission.
- The report visual uses only `Employee[Name]`; it is a precision control for the scanner tests.

The fixture was manually saved, closed, reopened and saved again in Power BI Desktop before it was supplied. That process retained the role shape above. The Desktop version and a byte-for-byte before/after comparison were not retained, so this fixture does not claim either.

## Repository form

The committed fixture retains report and semantic-model definition files required for scanning. Local `.pbi` cache/settings, DAX query history and TMDL editor-script artefacts are intentionally excluded. The role definition file is the authoritative evidence for this fixture.

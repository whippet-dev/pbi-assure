# Desktop formatting semantic-reference evidence

## Evidence

**[verified by Desktop-authored fixture]** The `desktop-formatting-semantic-evidence` project survived
save, close, reopen and save. Dynamic title/subtitle, conditional data colour, dynamic background,
Y-axis reference line, error-bar lower/upper bounds and rule-based conditional icon formatting each
persist an explicit semantic Measure / SourceRef / Property reference. `Unused Measure Control` does
not appear in report PBIR.

**[verified in current PBI Assure output]** Every persisted reference is already `DirectlyUsed`.
Conditional Icon Only is presented as Conditional Formatting; the other tested cases are Formatting.
Unused Measure Control remains `ApparentlyUnused`.

## Conclusion

No implementation gap was found. The generic PBIR field-reference extractor already covers these tested
Desktop formatting/analytics families. The committed
`desktop-formatting-semantic-reference-sanitized` fixture is minimal and sanitised, not untouched
Desktop output. Do not create a backlog item for these cases; resume investigation only for a new
persisted semantic-reference shape.

# Test fixtures

These projects use synthetic data or sanitised metadata. Their READMEs distinguish Desktop-authored bytes, constructed projects accepted on a Desktop round trip, and sanitised derivatives. A round trip proves only the documented shape and workflow; it does not prove every UI authoring path.

| Group | Regression purpose |
| --- | --- |
| `aggregation-alternateof-sanitized` | Explicit aggregation mappings and structural dependencies. |
| `desktop-bookmark-evidence-live-carrier`, `desktop-bookmark-evidence-stale` | Live references versus inert saved bookmark state; keep both controls. |
| `desktop-descriptions-sanitized` | Description text and significant whitespace. |
| `desktop-dynamic-format-string-evidence` | Measure format strings as dependency-bearing expressions. |
| `desktop-formatting-semantic-reference-sanitized`, `mobile-semantic-reference-sanitized` | Formatting and mobile references outside ordinary visual projections. |
| `desktop-hidden-visual-calculation-evidence` | Hidden supporting projections remain used without becoming user-facing evidence. |
| `desktop-incremental-refresh-evidence`, `desktop-incremental-refresh-evidence-baseline` | Persisted policy versus similar M filters without policy metadata. |
| `desktop-landing-page`, `desktop-landing-page-no-explicit` | Explicit landing page versus absence of that setting. |
| `desktop-ols-evidence` | Table and column object-level security forms. |
| `desktop-semantic-constructs` | Desktop semantic baseline: roles, perspectives, functions, generated dates and packaging. |
| `desktop-tmsl-model-bim-evidence`, `desktop-tmsl-model-bim-evidence-tmdl` | Unsupported local format and converted supported control. |
| `desktop-udf-references`, `desktop-udf-measure-consumer` | Function references and different report reachability paths. |
| `desktop-userelationship-evidence` | Inactive relationship activation evidence. |
| `grouped-tab-order`, `tab-order-states` | Group scope and controlled tab-order persistence states. |
| `kpi-detailrows-sanitized` | KPI and measure-owned Detail Rows dependencies. |
| `model-reference-context`, `model-reference-context-broken`, `model-reference-context-broken-2visualfilter` | Clean, missing-target and multiple-context reference controls. |
| `pbi-assure-coverage` | Canonical synthetic feature matrix, indexed by `coverage-manifest.json`. |
| `privacy-canary` | Synthetic markers used by browser network/privacy tests. |

Do not resave `tab-order-states` merely to tidy files: Desktop can normalise the values under test. The description fixture deliberately preserves trailing whitespace through `.gitattributes`. Shared base-theme files keep the projects self-contained; identical Git blobs already share storage.

`grouped-tab-order` and the `model-reference-context` family are exercised as Desktop baselines/controlled variants by their fixture tests, but lack a separate recorded authoring procedure. Do not infer a more precise origin from their filenames. Preserve these bytes until any replacement has equivalent assertions.

## Interpretation boundaries

The bookmark pair proves that saved state can outlive live usage, not that every bookmark reference is inert. Theme observations compare saved values; equal or differing values cannot establish editing history. The accessibility measurement that informed the rules did not label decorative intent, so it does not justify blanket visual-type exemptions beyond the implemented rules.

For current test commands and privacy verification, see [testing](../../docs/development/testing.md).

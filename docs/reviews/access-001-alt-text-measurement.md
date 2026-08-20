# PBI-ACCESS-001 alt-text measurement

**Date:** 2026-08-20

**Starting commit:** `fce5d86591774d44717b2e869ca92e3cbe646fd2`
**Scope:** read-only measurement of existing local PBIP samples; no scanner, rule, renderer or fixture changes.

Evidence labels: **[verified]** is mechanically measured from the repository or a scan output;
**[inferred]** is a cautious interpretation of the available metadata; **[design decision]** is a
recommended product choice.

---

## Question

`PBI-ACCESS-001` currently raises a Warning for every non-group visual that is included in tab order and
has neither a configured literal nor a dynamic alt-text value. The concern was that shapes, images and
text boxes might make this rule materially noisy. This review measures the available sample projects
before considering any rule change.

## What was measured

The scanner and rule were read before the scans. The rule is exactly:

```csharp
visual.IsInTabOrder && !visual.Accessibility.HasAltText
```

Visual groups are not in `page.Visuals`, so they are not candidates. A missing `position.tabOrder` is
included by Power BI's default order; a negative value is excluded. The `basicShape`, `image` and
`textbox` list belongs to the separate `PBI-ACCESS-003` excluded-from-tab-order review rule. It is not an
existing `PBI-ACCESS-001` exception. [verified]

Twelve locally available PBIP projects were scanned with the normal CLI into ignored local artifacts.
They include Columns Usage, IT Spend Analysis Sample, Sales & Returns, its PBIR/TMDL counterpart,
SamplePBIPLegacy, six StaleMetadata variants and Tab Order Test Sample. Theme fixtures were deliberately
excluded: they are controlled variations of the same theme test set, not independent report evidence.

The raw scan produced **387 `PBI-ACCESS-001` findings**. Two sources intentionally duplicate report
content for format/regression testing:

- Sales & Returns and its PBIR/TMDL counterpart each produce 166 identical findings.
- The six StaleMetadata variants each produce one controlled finding.

For an evidence-oriented comparison, the duplicated counterpart and five repeated StaleMetadata variants
are not counted as independent report designs. This leaves six representative project variants and
**216 findings** (five projects with findings; SamplePBIPLegacy has none).

| Project variant | Findings | Content-bearing or interactive | Plausibly decorative | Uncertain text boxes |
|---|---:|---:|---:|---:|
| Columns Usage | 10 | 10 | 0 | 0 |
| IT Spend Analysis Sample | 34 | 19 | 1 | 14 |
| Sales & Returns | 166 | 146 | 12 | 8 |
| SamplePBIPLegacy | 0 | 0 | 0 | 0 |
| StaleMetadata_00 | 1 | 1 | 0 | 0 |
| Tab Order Test Sample | 5 | 5 | 0 | 0 |
| **Representative total** | **216** | **181** | **13** | **22** |

The local measurement artifacts are intentionally ignored, including the per-project JSON inventories and
the finding extract. They are reproducible from the listed projects and are not part of the repository
contract.

## Measured visual types

The raw 387 findings cover 23 visual types. The largest categories are action buttons (135), cards (46),
text boxes (30), images (21), basic shapes (14), matrixes (16), multi-row cards (16), slicers (8), Q&A
visuals (8), and standard charts. The remaining entries are custom or specialist data visuals. [verified]

In the representative 216 findings:

- **181 (83.8%)** are data, custom or interactive visuals, including 68 action buttons and five images
  with configured actions. These are not evidence for a blanket decorative exclusion. [verified]
- **13 (6.0%)** are plausible decorative candidates: seven basic shapes and six images without an action,
  field reference, title or other content-bearing inventory metadata. Examples include small separator or
  pop-out shapes on Sales & Returns and a non-action image on IT Spend. This is a cautious classification,
  not proof that every one is decorative. [inferred from verified metadata]
- **22 (10.2%)** are text boxes. The current inventory exposes no usable on-canvas text for these samples,
  so it cannot show whether they communicate content, duplicate adjacent text or are decorative. They are
  uncertain, not safely removable from the rule. [verified]

None of the 387 measured visuals is explicitly excluded from tab order, and none has configured alt text.
That is a property of this sample corpus, not a claim about typical Power BI reports. [verified]

## What this supports

The data supports keeping the current rule unchanged. The plausible decorative subset is real but small;
excluding all shapes, images and text boxes would suppress 65 raw findings (or 40 representative findings)
without evidence that they are decorative. In particular, text boxes cannot be safely exempted from the
available metadata, and an image with an action is interactive.

The measured result does **not** establish a real-world false-positive rate. The corpus is small, partly
sample/control material and unusually has no configured alt text or explicit exclusion from tab order.
It is sufficient to retire the prior claim that decorative types are *probably* the dominant source of
noise, but not to prove that the rule is optimally precise. [design decision]

## Recommended next step — not started

Collect author-labelled examples from several independently authored reports, especially:

1. decorative shapes/images deliberately excluded from tab order;
2. text boxes that duplicate adjacent visual labels versus text boxes that carry essential content;
3. image and shape buttons with actions;
4. reports using configured literal and dynamic alt text.

Only then consider a narrowly evidenced exception or a separate review finding. Any future change should
remain based on actual Power BI metadata and user intent, not visual type alone.

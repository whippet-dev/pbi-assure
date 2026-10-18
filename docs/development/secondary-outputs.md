# Secondary outputs on the modern viewer prototype

This decision applies to `modern-viewer-prototype`. No production integration or desktop-host migration is included.

| Existing output / entry | Decision | Purpose and implementation |
| --- | --- | --- |
| Browser Apparently unused investigation | **Primary viewer workflow** | Opens Model Objects with the Apparently unused filter across all models. Search, type, table, confidence and object inspection use the existing workspace. |
| Browser standalone Apparently unused **open** action | **Retire** | Removed. There is no second interactive investigation route from analysis completion. |
| Apparently unused HTML download | **Export** | Retained as a self-contained, portable review set for sharing and offline review. Renderer, selection rule and download contents are unchanged. |
| Data Catalogue CSV | **Export** | Existing eligible-column/measure catalogue, including zero-usage objects; useful for documentation and governance. Configuration and CSV semantics unchanged. No separate catalogue HTML application is exposed by the browser. |
| Usage Mapping CSV | **Export** | Existing direct-report-usage rows and provenance for further analysis. Configuration and CSV semantics unchanged. Its interactive investigation already belongs to Model Objects and Report pages. |
| Full generated HTML report | **Export** | Retained for sharing and offline use, explicitly described as the existing report layout. Demoted into Downloads & exports. No cosmetic redesign. |
| Fixed semantic-usage CSV | **Export** | Retained under Advanced: legacy export for compatibility and technical consumers. CLI and desktop also generate this format. |
| CLI JSON inventory | **Export** | Machine-readable analysis and standalone viewer input. Existing public schema and CLI behaviour unchanged. |
| CLI default HTML/CSV bundle | **Export** | Existing saved-file automation contract remains unchanged. |
| Desktop launcher / Open latest report | **Retain separately, provisionally** | Currently opens saved HTML, alongside CSV and output-folder actions. Moving this host to the browser's in-memory handoff requires a separate hosting/lifecycle decision. Left unchanged in this browser prototype pass. |
| Theme/accessibility and other findings | **Primary viewer workflow** | Already present in Reviews & findings. They do not have separate browser result/download cards to retire. |

## Review contract

Membership remains exactly `SemanticObjectUsages` with `UsageState == ApparentlyUnused` and `!inventory.IsSystemGeneratedSemanticObject(usage)`. The viewer's system-table lookup now follows Core's case-insensitive model/table matching. The Include system-generated control is unavailable in this focused set. **Used only by unused branch is a separate set**, linked explicitly without adding its objects to Apparently unused.

Model Objects retains the existing reason, confidence, dependency, report-reference and model-limitation inspectors. The shared Summary adds a Lineage destination and expandable Power Query context. Matching recorded column operations and table/query links are contextual only; they neither establish report use nor alter classification. Missing evidence is stated as missing, not complete checking. No deletion recommendation or score is added.

A temporary review checkpoint captures filters, selection, history and scroll before an excursion. **Back to apparently unused** restores it after related-object inspection, Show in collection, Lineage, Power Query, scope or another workspace. It survives nested workspace return contexts. Reset clears refinements while retaining the Apparently unused state; the usage selector exits that state explicitly. Nothing is persisted to browser storage or encoded in URLs.

`UserFacing` is not in the public JSON inventory contract. Live analysis supplies optional presentation summaries from the same Core provenance analyzer used by the CSV exports. The viewer does not derive it from usage. Unsupported object types and older/imported snapshots without a summary show that the result is unavailable.

Completion leads with **Open investigation**, with a subordinate **Review apparently unused in Model Objects** shortcut. All portable outputs are grouped under the initially closed **Downloads & exports** disclosure. About explains the same distinction.

## Targeted validation

Focused tests cover exact membership (including mixed-case system identifiers), independent confidence/UserFacing, Power Query context, desktop review returns at 1366×768 and 1440×900, related unused branches, preserved filters/selection/scroll, and existing object inspection. Browser application checks cover real in-memory handoff, offline downloads, CSV exports, privacy/network boundaries, retained export configuration and analysis completion. No repository-wide test programme or production deployment is part of this change.

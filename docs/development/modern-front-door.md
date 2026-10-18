# Modern front door — prototype branch

The browser front door now opens the signed-off modern investigation (visual baseline
`4841473`). This work is confined to `modern-viewer-prototype`; there is no production
deployment or merge.

## Preview

Run `dotnet run --project src/PbiAssure.Web --urls http://127.0.0.1:4174` and open
that address. Choose a local PBIP folder, analyse it, then open the investigation.
`tests/fixtures/pbi-assure-coverage` exercises the limitations cue and scope route.
The standalone fixture viewer remains available through its existing npm dev command.

## Boundaries

- `wwwroot/css/site.css` aligns the outer application with the viewer: petrol chrome,
  cool canvas, blue actions, white working surfaces and restrained plum qualifications.
  The existing appearance preference remains available for the outer pages and legacy
  reports. The signed-off viewer styles and workspace architecture are unchanged.
- The Web project links the viewer assets from `prototypes/modern-viewer`, without a
  second maintained copy or fixture data. A separate hosted entry document waits for
  the current analysis instead of loading a demonstration project.
- The handoff uses same-origin, source-window-checked messages and acknowledges only
  after the inventory has been adapted and rendered. Project data stays in memory;
  it is not placed in URLs, browser storage or network requests. The original tab keeps
  its selection and folder capability. Return to project focuses that tab when available.
- A direct/reloaded viewer URL cannot recover an analysis. It explains how to reopen
  from the project. Popup failures leave the completed analysis available.
- The completion cue appears for recorded limitations or qualified absence results.
  It does not infer affected-object counts or complete checking from missing records.
- Apparently unused, CSV exports and standalone HTML downloads keep their existing
  behaviour. The downloadable report still uses its existing layout, stated beside
  its action; output rationalisation is deferred.

## Targeted validation

`FrontDoorJourneyTests` covers selection, running/focus, completion, scope entry,
retained access, snapshot reselection, cancellation, invalid input, popup failure and
desktop/reflow checks. `PrivacyWorkflowTests` checks the same-origin static viewer
requests, absence of project-data leakage, retained About navigation and existing
downloads. Relevant source-contract tests and the three modern viewer investigation
browser tests cover the integration boundary without an exhaustive repository run.

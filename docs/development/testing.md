# Testing and evidence

For restore/build and the solution test run, see [Contributing](../../CONTRIBUTING.md#build-and-test). Core tests also exercise Reporting and Web source contracts; Privacy E2E runs real browser workflows. Use focused affected tests during development rather than regenerating fixtures unnecessarily.

## Fixture preservation

See the [fixture index](../../tests/fixtures/README.md). Keep provenance beside the bytes: synthetic, sanitised Desktop-derived, UI-authored and constructed-then-round-tripped are different evidence claims. Keep positive and negative controls when they prove different persistence behaviours. Do not normalise significant description whitespace or resave controlled tab-order states merely for tidiness.

Desktop fixture observations are bounded to their documented version and workflow. One historical manual experiment found Desktop rejected sibling named expressions `Data` and `data`; the experiment was not retained as a reproducible fixture. This does not relax case-sensitive lexical handling of a local `data` binding versus a global `Data` reference.

For unsupported forms, retain the smallest realistic counterexample and test user-visible consequences, including confidence or orphan findings. Do not promote rejected expressions, stale bookmark state or connector-like library functions into dependencies merely because their text persists.

The [privacy statement](../../PRIVACY.md) is the user-facing contract. The procedures below verify observable behaviour, not every possible side channel or hosting-account setting.

## Reproducible privacy verification

The purpose-built browser tests use Microsoft Playwright for .NET and the redistribution-safe project in
`tests/fixtures/privacy-canary`. The fixture contains distinctive synthetic canary text in its project,
model, Power Query and visual metadata. It contains no real organisation data, credentials or private
URLs.

These tests are part of the normal solution test run, so `dotnet test PbiAssure.slnx` executes them
alongside the core tests, and continuous integration runs them on every push and pull request. They
require Node.js and a matching Playwright Chromium build on the machine; install the browser once per
checkout with:

```powershell
.\tests\PbiAssure.Privacy.E2E\bin\Debug\net10.0\playwright.ps1 install chromium
```

Run the deterministic local verification on its own from the repository root:

```powershell
.\scripts\Test-Privacy-E2E.ps1
```

The runner builds the separate browser-test project, installs the matching Chromium build if required,
uses the canonical web publisher with review builds allowed, starts a temporary local static host that applies the checked-in
`_headers` policy, then runs online and offline workflows.

The application-ready boundary is the visible
`data-pbiassure-app-ready="true"` marker followed by Playwright's network-idle state. Monitoring starts
only after both conditions are met, so initial HTML, CSS, JavaScript, .NET assemblies, WebAssembly and
runtime-data downloads form the startup baseline rather than scan-time traffic.

The test fails if it observes:

- any HTTP(S), WebSocket or other Playwright-observable request during project selection, scanning or
  HTML/CSV generation and download;
- any cross-origin request after the startup baseline;
- any report-viewer request other than the expected same-origin viewer document/script;
- any fixture canary in an observable outbound URL, request headers or request body;
- a missing or weakened report-viewer CSP;
- a scan, report interaction, HTML download or CSV download failure.

The current local workflow records three expected same-origin viewer requests: the `.html` route that
redirects, the final extensionless viewer document, and the viewer script. These requests load only the
static viewer shell. The generated report content is transferred locally with `postMessage` and is not
included in those requests.

Compact JSON evidence and the generated synthetic HTML/CSV outputs are written under
`artifacts/privacy-e2e/`. The directory is ignored by Git. Large traffic logs and HAR files are not retained
by default.

An optional read-only smoke run can apply the same workflow to a deployed site without making production
availability part of the normal test suite:

```powershell
.\scripts\Test-Privacy-E2E.ps1 -BaseUrl https://pbiassure.pages.dev
```

This uses a local synthetic folder selection; it does not upload the fixture or mutate the deployment.

### Manual offline verification

1. Use a clean browser profile where practical and load PBI Assure fully.
2. Wait until the project picker and **Run analysis** control are usable.
3. Open browser developer tools, select **Network**, clear the log, then enable Offline mode.
4. Select the synthetic privacy fixture and run the scan.
5. Review the browser results and download the HTML report and semantic-usage CSV.
6. Open the downloaded standalone HTML file and exercise its navigation/filter controls.
7. Confirm the complete processing/export workflow succeeds while the browser remains offline.

The app's **Open HTML report** button loads the same-origin report-viewer shell before transferring the
locally generated report to it. That shell is deliberately served with `Cache-Control: no-cache`, so
opening a new viewer tab is not part of the offline guarantee. The online test verifies that viewer route,
its restrictive CSP and the local `postMessage` transfer separately.

This demonstrates that project processing and output generation do not require a remote service once the
application is loaded. It does not prove that an online version can never transmit data, that deployed
bytes always match reviewed source, that browser extensions/proxies/endpoint tooling do nothing, or that
every future code path remains local.

### Manual online network verification

1. Use a clean browser profile where practical, open developer tools and load PBI Assure fully.
2. In **Network**, wait for startup requests to finish, clear the log and enable **Preserve log**.
3. Select the synthetic privacy fixture and run the complete scan.
4. Review results, use **Open HTML report**, then download HTML and CSV outputs.
5. Inspect HTTP(S), WebSocket and EventSource activity, including request URLs, headers and payloads where
   the browser exposes them. Check for beacon traffic as well.
6. Search captured request data for the fixture canary prefix `PBIASSURE_CANARY_7F3C2A` and the project
   canary `PBIASSURE_PRIVACY_PROJECT_7F3C2A`.
7. Optionally export a HAR for an authorised evidence review.

HAR files can contain cookies, headers, URLs and content from other browsing activity. Capture them only
with synthetic data, store them securely and do not commit them to the repository.

The automated and manual checks establish that no observable browser network request occurred during the
tested scan/export workflow beyond the expected local/same-origin report-viewer shell. They do not cover
every theoretical browser side channel or software outside PBI Assure's page context.

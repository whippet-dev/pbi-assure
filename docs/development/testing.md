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
- any investigation request other than expected same-origin production viewer assets;
- any fixture canary in an observable outbound URL, request headers or request body;
- a missing or weakened application/investigation CSP;
- a scan, investigation, portable HTML interaction, HTML download or CSV download failure;
- application-origin cookies, session storage, IndexedDB databases or service-worker registrations,
  or local storage other than a valid appearance preference.

Investigation opens load only the same-origin production viewer modules and assets. ViewerSnapshot
1.0 is delivered locally with postMessage, not included in those requests. Tests exercise Overview,
the integrated Apparently unused review and Analysis scope separately from portable downloads.

Compact JSON evidence and the generated synthetic HTML/CSV outputs are written under
`artifacts/privacy-e2e/`. The directory is ignored by Git. Large traffic logs and HAR files are not retained
by default. Evidence filenames distinguish local and deployed runs. Each record identifies the source
revision, whether the verification checkout had changes, the displayed application build and browser
version. CI uploads only the compact JSON evidence with a 30-day retention period, including on failure
when a completed workflow record exists; generated report files and raw traffic are not uploaded.

An optional read-only smoke run can apply the same workflow to a deployed site without making production
availability part of the normal test suite:

```powershell
.\scripts\Test-Privacy-E2E.ps1 -BaseUrl https://pbiassure.pages.dev
```

This uses a local synthetic folder selection; it does not upload the fixture or mutate the deployment.

### Manual offline verification

1. Use a clean browser profile where practical and load PBI Project Lens fully.
2. Wait until the project picker and **Run analysis** control are usable.
3. Open browser developer tools, select **Network**, clear the log, then enable Offline mode.
4. Select the synthetic privacy fixture and run the scan.
5. Review the browser results and download both HTML reports and all three CSV exports.
6. Open both downloaded standalone HTML files and exercise their navigation/filter controls.
7. Confirm the complete processing/export workflow succeeds while the browser remains offline.

Open investigation loads same-origin assets with revalidation, so opening a new viewer tab is not
part of the offline guarantee. Online tests verify the production viewer, CSP and in-memory handoff.
Offline tests verify analysis and the standalone downloads without needing that hosted viewer.

This demonstrates that project processing and output generation do not require a remote service once the
application is loaded. It does not prove that an online version can never transmit data, that deployed
bytes always match reviewed source, that browser extensions/proxies/endpoint tooling do nothing, or that
every future code path remains local.

### Manual online network verification

1. Use a clean browser profile where practical, open developer tools and load PBI Project Lens fully.
2. In **Network**, wait for startup requests to finish, clear the log and enable **Preserve log**.
3. Select the synthetic privacy fixture and run the complete scan.
4. Open the investigation and its Apparently unused focused review, then download both HTML
   reports and all three CSV outputs.
5. Inspect HTTP(S), WebSocket and EventSource activity, including request URLs, headers and payloads where
   the browser exposes them. Check for beacon traffic as well.
6. Search captured request data for the fixture canary prefix `PBIASSURE_CANARY_7F3C2A` and the project
   canary `PBIASSURE_PRIVACY_PROJECT_7F3C2A`.
7. Optionally export a HAR for an authorised evidence review.

HAR files can contain cookies, headers, URLs and content from other browsing activity. Capture them only
with synthetic data, store them securely and do not commit them to the repository.

The automated and manual checks establish that no observable browser network request occurred during the
tested scan/export workflow beyond the expected same-origin investigation assets. They do not cover
every theoretical browser side channel or software outside PBI Project Lens's page context.

# Browser privacy and local processing

## User-facing statement

PBI Assure can accurately state:

> Your Power BI project is processed locally in your browser. Project files and analysis results are
> not uploaded to PBI Assure.

This statement describes project processing. It does not mean that loading the website creates no
network traffic.

## What happens locally

After the user grants folder access, the browser provides file handles or `File` objects for the chosen
directory. PBI Assure filters these to the project `.pbip`, `.Report` and `.SemanticModel` metadata,
copies the accepted files into the local WebAssembly runtime, and runs the shared Core and Reporting
code in the browser.

HTML and CSV results are built in browser memory. Downloads use temporary Blob URLs. **Open report**
loads a same-origin static viewer shell and transfers the generated HTML to it locally using
`postMessage`; report content is not placed in the request URL or sent to the host. The project-processing
code does not use `HttpClient`, `fetch`, XMLHttpRequest, WebSocket, SignalR, telemetry, analytics or an
error-reporting service. It does not store selected projects in a backend.

## Normal network activity

The browser must request PBI Assure's own HTML, CSS, JavaScript, WebAssembly runtime, .NET assemblies
and runtime data from the static host. The generated .NET loader uses same-origin fetches for some of
these assets while the application starts. The host can therefore receive ordinary request information
such as IP address, time, asset path and browser headers. These requests do not contain selected project
files or analysis results.

Once the application is ready, project selection, scanning, HTML/CSV generation and downloads do not
initiate network requests. **Open report** requests only the same-origin static viewer shell; selected
project data and generated report content pass between the two browser windows locally. The
source-controlled Content Security Policy permits the same-origin connections required for application
startup, gives the report viewer a separate tightly scoped policy, and blocks ordinary cross-origin
connections. The static-only hosting architecture, production configuration and network verification
remain separate parts of the assurance.

Links to Microsoft or W3C guidance in generated reports are passive. A network request occurs only if
the user chooses to open one.

Browser extensions, operating-system security products and organisation-managed browser services are
outside PBI Assure's control. Production hosting must apply and validate the CSP documented in
[Browser static hosting](browser-hosting.md).

## Generated outputs remain sensitive

The semantic-usage CSV excludes full M expressions, connection values and source paths, and neutralises
text that spreadsheet software could otherwise interpret as a formula.

The detailed HTML report is not a redacted export. It can include:

- report, page, visual, table, column and measure names;
- DAX and other model metadata;
- source and evidence paths;
- full Power Query M expressions;
- local paths, server names, URLs or hard-coded values contained in those expressions.

Users should review an HTML report before sharing it and protect it to the same standard as its source
project. Connector summaries minimise connection arguments, but that does not remove sensitive values
from the full M expression shown in query details.

## Source access and lifetime

The browser can access only the directory explicitly granted by the user. Canonical project-relative
paths cannot escape that directory. Selected file objects and the resulting inventory remain in browser
memory until another project is chosen or the page is closed or reloaded.

## Deployment boundary

The checked-in application contains no backend, upload endpoint, application analytics or telemetry.
Cloudflare account-level settings can add behaviour outside the application source. The production Pages
project, Workers or Functions, Web Analytics, Zaraz, Access, Transform Rules, domains and logging
settings must therefore be checked separately using the manual checklist in
[Browser static hosting](browser-hosting.md).

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
uses the canonical clean web publish, starts a temporary local static host that applies the checked-in
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
2. Wait until the project picker and **Run assurance** control are usable.
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

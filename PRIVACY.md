# Privacy

PBI Assure is designed to analyse Power BI projects locally. This document explains what the browser
application processes, what network activity still occurs, and how the local-processing claim can be
checked independently.

## What PBI Assure processes

You choose a Power BI Project (PBIP) that uses Power BI's structured PBIR report format and TMDL
semantic-model format. PBI Assure reads the project metadata needed for its checks, including report and
visual definitions, semantic-model metadata, tables, columns, measures, DAX, relationships, Power Query
M and relevant project resources.

PBI Assure does not inspect imported model rows as part of this metadata analysis.
Power Query expressions can nevertheless contain inline data, hard-coded credentials or other
sensitive values. Those expressions are project metadata and can appear in the detailed HTML report.

## Where processing happens

In the browser application, your Power BI project is processed locally in your browser's WebAssembly
process. The generated findings, interactive HTML report, Apparently Unused review, Data Catalogue,
Usage Mapping and Semantic Usage CSV files are created in browser memory.

PBI Assure's project-processing code does not upload selected project files, their contents, analysis
results or generated HTML/CSV files to PBI Assure, Cloudflare or another service. No account is required.

## Cloudflare and normal website requests

[Cloudflare Pages](https://pages.cloudflare.com/) serves the public browser application. When you load
the site, Cloudflare receives normal requests for application files such as HTML, CSS, JavaScript,
WebAssembly and .NET runtime assets, together with ordinary network metadata such as IP address, request
time and browser headers. These application-delivery requests do not contain the selected Power BI
project or its generated results.

Cloudflare infrastructure headers such as `NEL` or `Report-To` may be present in responses. They are
Cloudflare platform behaviour, not PBI Assure project telemetry. PBI Assure does not make claims here
about Cloudflare's contractual retention or processing beyond the behaviour that has been technically
verified.

## Analytics, cookies and browser storage

PBI Assure application code contains no analytics or telemetry. On 1 October 2026, a read-only
Cloudflare dashboard check confirmed that Web Analytics was disabled for the production Pages project
and Zaraz had no domains configured in the hosting account. The deployed application HTML contained no
Cloudflare Web Analytics or Zaraz script, and the tested project workflows produced no observable
cross-origin requests. Cloudflare account settings can change independently of this repository and
should be rechecked when deployment assurance is required.

PBI Assure does not require an account and does not create application-managed cookies. It does not use
`localStorage`, `sessionStorage` or IndexedDB to store selected projects or results. Project data is held
in browser memory while the page is open.

One non-project preference is stored. Choosing Light or Dark appearance writes the key
`pbiassure-appearance` to `localStorage` so the application and the reports it generates keep the
appearance you picked; choosing System removes it. It holds only the string `light` or `dark`, and no
project content or analysis result is written to browser storage. Selecting another project replaces the active project state,
and closing or reloading the page ends the application session. Browser and operating-system memory
cannot be guaranteed to be securely zeroised.

## Generated files

Generated HTML and CSV files can contain sensitive project metadata. Depending on the output, this may
include report, page, visual, table, column and measure names; DAX; Power Query M; source paths; and report
structure. Review generated files before sharing them. Handle them according to the sensitivity of the
source project and your organisation's information-handling requirements.

## External links

External documentation links do not make requests until you choose to open them.

## How local processing is enforced

The browser application is deployed as static files and has no project upload endpoint or
project-processing backend. Its main Content Security Policy restricts connections to the same origin
needed to load the WebAssembly application. The isolated generated-report viewer uses
`connect-src 'none'`.

The main application's same-origin connection permission is needed for application delivery. CSP is a
defence against unintended connections, not proof by itself that project content cannot be sent: the
source review and browser workflow tests establish the application behaviour.

Purpose-built Playwright privacy tests establish an application-ready network baseline, process a
synthetic PBIP fixture, fail on unexpected scan/export requests, search observable outbound requests for
synthetic canary values, and verify that processing and standalone output generation work after the
browser goes offline. Both HTML reports and all three CSV exports are exercised. The tests also check
that the application origin has no cookies, session storage, IndexedDB databases or service-worker
registrations, and that local storage contains only a valid appearance preference, if present.

The verified scan and export workflow produced no observable browser network requests after the
application-ready baseline. Opening a report used only the expected same-origin static viewer shell
requests; generated report content was transferred locally to that shell and was not included in those
requests.

### Latest deployed verification

The read-only Cloudflare dashboard check on 1 October 2026 confirmed the `pbiassure` Pages project
was linked to `whippet-dev/pbi-assure`, with production branch `master`, build command
`bash ./scripts/Publish-Web-Cloudflare.sh` and output directory `artifacts/web/wwwroot`.
No custom domain or resource binding was configured. The account's application inventory contained
two Pages projects and no Workers; its domain inventory was empty, and Zero Trust showed its initial
setup screen. The active deployment's build log
recorded no Functions directory, a clean publish and the expected embedded source revision.
These observations support the static-only deployment; they do not establish Cloudflare's internal
request-log access or retention policies.

On 1 October 2026, all four Privacy E2E tests passed against `https://pbiassure.pages.dev`, whose displayed
build was `49fe4d9f2bf6` (source commit `49fe4d9f2bf638a9c34de7f8984518438017adef`), using Chromium
`149.0.7827.55` and the repository's synthetic privacy-canary project. The verification tests included
uncommitted extensions for the second report and browser-storage checks; the deployed application was
not changed by the test.

- Project selection, scanning and export generation/download: zero observable network requests.
- Opening both reports: six expected same-origin static viewer requests, with no project content in
  observable request URLs, headers or bodies.
- Unexpected requests, cross-origin requests and detected canary leaks: zero.
- After startup, with networking disabled: scanning, both HTML downloads, all three CSV exports and
  interaction with both downloaded reports passed.
- Application storage checks and the deployed application/viewer header checks passed.

This workflow uses the alternate folder picker. It does not establish complete coverage of every
browser, project shape or primary folder-picker/rerun path. Raw project files and generated outputs are
not uploaded as verification evidence; CI retains compact JSON results from the synthetic tests.

## Verify it yourself

From a local checkout, run the deterministic privacy tests:

```powershell
.\scripts\Test-Privacy-E2E.ps1
```

Run the optional read-only smoke test against the deployed application:

```powershell
.\scripts\Test-Privacy-E2E.ps1 -BaseUrl https://pbiassure.pages.dev
```

The complete manual offline and online Network-panel procedure is in
[Browser privacy and local processing](docs/development/testing.md#reproducible-privacy-verification).

These checks demonstrate that no observable browser network request occurred during the tested
scan/export workflow beyond the expected same-origin report-viewer shell. They do not prove that every
theoretical browser side channel is impossible or that every future revision will behave identically.

## Environment boundary

PBI Assure cannot control browser extensions, enterprise proxies, endpoint monitoring, browser or
operating-system telemetry, compromised hosting or dependencies, or code changes outside the tested
revision. Organisations should evaluate those controls as part of their own environment and risk model.

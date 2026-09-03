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

## Where processing happens

In the browser application, your Power BI project is processed locally in your browser's WebAssembly
process. The generated findings, HTML report and semantic-usage CSV are created in browser memory.

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

PBI Assure application code contains no analytics or telemetry. For the production deployment verified
on 14 August 2026, Cloudflare Web Analytics was disabled and Zaraz was not configured. Cloudflare account
settings can change independently of this repository and should be rechecked when deployment assurance
is required.

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

Purpose-built Playwright privacy tests establish an application-ready network baseline, process a
synthetic PBIP fixture, fail on unexpected scan/export requests, search observable outbound requests for
synthetic canary values, and verify that processing and standalone output generation work after the
browser goes offline.

The verified scan and export workflow produced no observable browser network requests after the
application-ready baseline. Opening a report used only the expected same-origin static viewer shell
requests; generated report content was transferred locally to that shell and was not included in those
requests.

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
[Browser privacy and local processing](docs/browser-privacy.md#reproducible-privacy-verification).

These checks demonstrate that no observable browser network request occurred during the tested
scan/export workflow beyond the expected same-origin report-viewer shell. They do not prove that every
theoretical browser side channel is impossible or that every future revision will behave identically.

## Environment boundary

PBI Assure cannot control browser extensions, enterprise proxies, endpoint monitoring, browser or
operating-system telemetry, compromised hosting or dependencies, or code changes outside the tested
revision. Organisations should evaluate those controls as part of their own environment and risk model.

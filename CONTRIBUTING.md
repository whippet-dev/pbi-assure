# Contributing

This repository should remain understandable to Power BI specialists who may be newer to application development.

## Development workflow

1. Create a short-lived Git branch for one coherent change.
2. Add or update automated tests for behavioural changes.
3. Run `dotnet format --verify-no-changes`, `dotnet build`, and `dotnet test`. The solution test run includes the browser privacy end-to-end tests, which need Node.js and a Playwright Chromium build; see [Build and test](README.md#build-and-test).
4. Keep generated reports, real PBIP projects, secrets, and report data out of Git.
5. Record significant architectural decisions under `docs/decisions/`.
6. After user-facing desktop feature changes, refresh the Windows publish output with `dotnet publish src/PbiAssure.Desktop -c Release -o artifacts/desktop`.

## Visual design

The design system is shared by the browser application and the generated HTML report. Its tokens and
primitives live in `src/PbiAssure.Web/wwwroot/css/core.css`, the report's presentation layer in
`src/PbiAssure.Reporting/Styles/report.css`. After editing either, run
`node scripts/Sync-DesignTokens.mjs` to regenerate `src/PbiAssure.Reporting/DesignSystem.cs`;
`DesignSystemSourceTests` fails when the copies drift. See
[docs/design/visual-identity.md](docs/design/visual-identity.md).

## Design rules

- Keep `PbiAssure.Core` independent of the command line and any future UI.
- Parsing discovers facts; rules interpret those facts. Do not combine both responsibilities.
- Findings must contain evidence and a stable rule identifier.
- Do not modify an analysed report or model unless a future feature explicitly requests and confirms that operation.
- Avoid network access in the core analysis path.
- Use synthetic fixtures in tests. Never copy operational or organisation-specific report content into the test suite.

## Terminology

- **Artifact**: a Power BI report, semantic model, project, or a component within one.
- **Reference**: an observed link from one artifact to another.
- **Finding**: a rule result supported by evidence.
- **Analysed scope**: the exact local project or set of Fabric items included in a scan.
- **Apparently unused**: no inbound usage was found inside the analysed scope.

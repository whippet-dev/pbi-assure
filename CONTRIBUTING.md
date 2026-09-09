# Contributing

Keep the project understandable to Power BI specialists and occasional contributors. Make one coherent change at a time, and add or update focused tests for behaviour changes.

## Build and test

A Windows checkout supports the complete solution, including the Windows desktop application. Install Git, the .NET SDK pinned in `global.json`, and Node.js for the browser publish and privacy tests. The command-line and browser projects can also be built separately on other platforms.

```powershell
dotnet restore PbiAssure.slnx
dotnet build PbiAssure.slnx --no-restore
.\tests\PbiAssure.Privacy.E2E\bin\Debug\net10.0\playwright.ps1 install chromium
dotnet test PbiAssure.slnx --no-build
```

Use focused tests while developing; the solution run includes Privacy E2E. See [testing](docs/development/testing.md) for privacy verification and [hosting](docs/development/hosting.md) for clean Web publishing. Generated outputs belong in ignored `artifacts/` or local project output folders.

## Implementation guidance

- Keep Core independent of frontends. Parsing discovers facts; rules interpret them.
- Retain evidence and stable identifiers. Do not turn incomplete evidence into confident absence or invented dependencies.
- Keep analysis local and read-only. Do not add network calls to the analysis path.
- Use redistribution-safe synthetic projects or documented Desktop persistence fixtures. Preserve origin and sanitisation notes beside fixtures; never commit operational reports, credentials or private data.
- Update public claims when behaviour changes. Explain durable architectural choices in [architecture](docs/development/architecture.md), not a new status ledger.
- Contribute only material you have the right to share, and retain relevant copyright/licence notices.

## Visual design

Shared tokens and primitives live in `src/PbiAssure.Web/wwwroot/css/core.css`; report presentation lives in `src/PbiAssure.Reporting/Styles/report.css`. After editing either, run `node scripts/Sync-DesignTokens.mjs` to regenerate `src/PbiAssure.Reporting/DesignSystem.cs`. `DesignSystemSourceTests` detects drift. See [visual identity](docs/development/visual-identity.md).

After desktop UI changes, a local review publish can be created with:

```powershell
dotnet publish src/PbiAssure.Desktop -c Release -o artifacts/desktop
```

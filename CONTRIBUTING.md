# Contributing

This repository should remain understandable to Power BI specialists who may be newer to application development.

## Development workflow

1. Create a short-lived Git branch for one coherent change.
2. Add or update automated tests for behavioural changes.
3. Run `dotnet format --verify-no-changes`, `dotnet build`, and `dotnet test`.
4. Keep generated reports, real PBIP projects, secrets, and report data out of Git.
5. Record significant architectural decisions under `docs/decisions/`.

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

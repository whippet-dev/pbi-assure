# Security and data handling

## Default operating model

The initial scanner runs locally and performs no network requests. It reads metadata from a user-selected PBIP directory and does not query model data.

## Data minimisation

- Do not inspect imported model rows to perform metadata analysis.
- Do not include secrets, credentials, access tokens, connection-string secrets, or sample data in reports or logs.
- Treat DAX, Power Query M, source names, object names, descriptions, and report text as potentially sensitive metadata.
- Generated assurance reports inherit the sensitivity of their source and must be stored accordingly.
- Diagnostic logs should identify a file or property without copying full expressions unless the operator explicitly requests evidence-rich output.

## Network boundary

The core library should not make outbound network calls. Future Fabric or XMLA connectors must be separate adapters with explicit authentication and permission documentation. Telemetry must remain disabled unless organisational owners make a deliberate, documented decision to introduce it.

## Filesystem boundary

Analysis is read-only against source projects. Generated output is written only to an operator-selected path. Future remediation features must be separate from analysis, show an exact change preview, and require explicit confirmation.

## Test data

Tests use small synthetic PBIP-like directory structures. Real departmental reports, exported model definitions, tenant identifiers, workspace names, and production metadata must not enter source control.

## Supply chain

- Prefer the .NET standard library where it is sufficient.
- Pin direct dependency versions.
- Record dependency purpose and licence before adoption.
- Produce a software bill of materials for release builds.
- Review and update dependencies through an auditable process.

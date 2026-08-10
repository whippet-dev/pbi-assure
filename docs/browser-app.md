# Browser application

## Current architecture

`PbiAssure.Web` is a standalone Blazor WebAssembly application. It is a thin frontend over the same
shared projects used by the command-line and Windows applications:

```text
Browser directory picker
    -> canonical project-relative file manifest
    -> InMemoryProjectFileSource
    -> PbiAssure.Core analysis and assurance rules
    -> PbiAssure.Reporting HTML and semantic-usage CSV renderers
    -> local browser downloads
```

There is no backend, upload API, account, telemetry or analytics service. The static host serves only
the application assets. Selected project files and generated results remain in the browser process.
See [Browser privacy](browser-privacy.md) for the exact boundary and caveats.

## Selecting a project

Choose the folder that directly contains one root `.pbip` file. PBI Assure accepts metadata from the
immediate `.Report` and `.SemanticModel` directories in that folder. It rejects folders with no root
project, more than one root project, unsafe relative paths, or paths that collide when compared without
letter case.

The primary picker uses `showDirectoryPicker` where supported. **Use alternate folder picker** invokes
the existing directory-input fallback, including when an organisation policy blocks the primary API.

Initial browser safety limits are:

| Limit | Value |
|---|---:|
| Visited entries | 10,000 |
| Accepted metadata files | 5,000 |
| One metadata file | 25 MiB |
| Total accepted metadata | 100 MiB |
| Directory depth | 64 levels |

These are browser-operability limits, not Power BI format limits. The application stops with a short
message when one is exceeded.

## Browser support for the first beta

- **Supported:** current desktop Microsoft Edge and Google Chrome over HTTPS.
- **Best effort after explicit testing:** current Firefox and Safari using the alternate picker.
- **Not initially supported:** mobile browsers and older enterprise browser versions.

Managed-browser policies can independently block folder access, WebAssembly execution or downloads.
Release testing should cover primary and alternate picking, HTML and CSV downloads, the production CSP,
OneDrive placeholder files, and organisation download/security controls.

## Build and publish locally

```powershell
dotnet publish src/PbiAssure.Web -c Release -o artifacts/web
```

Serve `artifacts/web/wwwroot` over HTTP for local development or HTTPS for deployed use. Do not open
`index.html` directly through `file://` because Blazor must load its framework assets from a web origin.

For an identifiable release build, pass the current commit as `SourceRevisionId`:

```powershell
$revision = git rev-parse --short HEAD
dotnet publish src/PbiAssure.Web -c Release -o artifacts/web -p:SourceRevisionId=$revision
```

The footer always displays the application version and displays the revision only when the build
supplies one. It links to the public source repository.

Production hosting requires additional response headers, caching and MIME configuration. See
[Browser static hosting](browser-hosting.md).

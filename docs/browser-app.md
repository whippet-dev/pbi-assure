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
    -> local browser downloads or same-origin local report viewer
```

There is no backend, upload API, account, telemetry or analytics service. The static host serves only
the application assets. Selected project files and generated results remain in the browser process.
See [Browser privacy](browser-privacy.md) for the exact boundary and caveats.

## Selecting a project

PBI Assure provides full browser assurance for a PBIP project using PBIR and TMDL. If starting with a
PBIX, follow [Prepare a Power BI project for PBI Assure](preparing-power-bi-project.md), then choose the
folder that directly contains one root `.pbip` file. PBI Assure accepts metadata from the immediate
`.Report` and `.SemanticModel` directories in that folder. It rejects folders with no root project, more
than one root project, unsafe relative paths, or paths that collide when compared without letter case.

The primary picker uses `showDirectoryPicker` where supported. The **Having trouble selecting a folder?**
disclosure exposes the directory-input fallback when an organisation policy or browser blocks the primary
picker.

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
Release testing should cover primary and alternate picking, a styled and interactive Open report, HTML
and CSV downloads, the production CSP, OneDrive placeholder files, and organisation download/security
controls.

## Build and publish locally

```powershell
.\scripts\Publish-Web.cmd
```

The normal command refuses tracked source changes, removes only `artifacts/web`, publishes Release
output and embeds the full current Git commit. Serve `artifacts/web/wwwroot` over HTTP for local
development or HTTPS for deployed use. Do not open `index.html` directly through `file://` because
Blazor must load its framework assets from a web origin.

For a local review build while tracked changes are intentionally uncommitted:

```powershell
.\scripts\Publish-Web.cmd -AllowDirty
```

The footer labels review output with the first 12 characters of the current commit plus `-dirty`. A
production publish must not contain that suffix. Untracked build inputs in the Web, Core or Reporting
projects also produce a dirty build; unrelated untracked research documents do not affect the compiled
application.

The publish includes the source-controlled Cloudflare Pages `_headers` policy. The footer links the
displayed build identity to the public source repository.

Production hosting requires additional response headers, caching and MIME configuration. See
[Browser static hosting](browser-hosting.md).

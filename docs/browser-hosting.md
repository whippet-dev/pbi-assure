# Browser static hosting

## Deployment model

`PbiAssure.Web` publishes as static files. It requires no ASP.NET backend, API, authentication service,
database or upload endpoint. Deploy the contents of the published `wwwroot` directory from a clean,
versioned build directory rather than incrementally accumulating old fingerprinted assets.

The public site must use HTTPS because the primary directory-picker API requires a secure context.

## Required security headers

Configure the following Content Security Policy as an HTTP response header on the static host:

```text
default-src 'self';
base-uri 'self';
object-src 'none';
script-src 'self' 'wasm-unsafe-eval';
style-src 'self';
connect-src 'self';
img-src 'self' data:;
font-src 'self';
media-src 'none';
frame-src 'none';
worker-src 'none';
form-action 'none';
frame-ancestors 'none';
upgrade-insecure-requests;
```

Also send:

```text
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
Permissions-Policy: camera=(), microphone=(), geolocation=()
```

`'wasm-unsafe-eval'` is required by the client-side Blazor Mono runtime. `connect-src 'self'` is also
required: the .NET 10 WebAssembly loader performs same-origin fetches for runtime, assembly and data
assets while the application starts. `connect-src 'none'` prevents the current application from loading
and must not be used. The application has no project-processing API or telemetry connection, and Blob
downloads do not require `connect-src blob:`.

The remaining directives disable embedding, plugins, form submission, workers and media because the
current application does not use them. Camera, microphone and geolocation are disabled for the same
reason. No CSP reporting endpoint is configured because that would introduce an outbound reporting path.

`connect-src 'self'` blocks ordinary cross-origin fetch, XMLHttpRequest, WebSocket, EventSource and
beacon connections, but it does not prove that a same-origin upload endpoint is absent. That assurance
also depends on the static-only deployment, source review and network verification.

`frame-ancestors` must be sent as a response header; placing it in a CSP `<meta>` element is ineffective.
The complete Cloudflare Pages policy is source-controlled in
`src/PbiAssure.Web/wwwroot/_headers` and is copied into the root of a web publish.

If a future feature introduces a report iframe or a Web Worker, review and narrowly amend `frame-src`
or `worker-src` at that time. They are deliberately disabled now.

The generated standalone HTML report contains its own inline CSS and JavaScript. A Blob document opened
directly from the application inherits the main CSP and therefore cannot use that inline presentation.
PBI Assure instead opens the same-origin `report-viewer.html` shell and transfers the generated report
to it locally with `postMessage`. The selected project and report content are not included in the viewer
page request.

Cloudflare serves `.html` files at extensionless URLs, so `/report-viewer.html` redirects to
`/report-viewer`. Cloudflare's `_headers` matching rules are cumulative. Both viewer routes therefore
detach the main Content-Security-Policy before applying this narrow viewer-only policy:

```text
default-src 'none';
base-uri 'none';
object-src 'none';
script-src 'self' 'unsafe-inline';
style-src 'unsafe-inline';
connect-src 'none';
img-src 'self' data:;
font-src 'none';
media-src 'none';
frame-src 'none';
worker-src 'none';
form-action 'none';
frame-ancestors 'none';
```

Inline script and style are permitted only on the report-viewer document because they are intrinsic to
the self-contained generated report. The viewer cannot make fetch, WebSocket, beacon or other
CSP-governed connections. It accepts content only from its same-origin opener, clears the opener
relationship before rendering, and the main application policy remains unchanged.

## Clean, identifiable publish

Run the repository publish script from the repository root:

```powershell
.\scripts\Publish-Web.cmd
```

The script resolves the fixed generated output directory `artifacts/web`, refuses a normal production
publish when tracked changes are present, removes only that directory, publishes Release output with the
full current Git commit embedded as `SourceRevisionId`, and verifies the key output files.

For a local review of uncommitted tracked changes, use:

```powershell
.\scripts\Publish-Web.cmd -AllowDirty
```

That review build is visibly labelled with the current commit plus `-dirty` and must not be treated as
a reproducible production build. Untracked build inputs in the Web, Core or Reporting projects also
produce a dirty build; unrelated untracked research documents do not affect the compiled application.

The canonical implementation is the cross-platform `scripts/Publish-Web.mjs`; the `.cmd` and
PowerShell files are thin Windows launchers for the same workflow. The publish replaces a placeholder
in the small browser-helper URLs with a content-derived version.
The HTML and non-fingerprinted helper files use `Cache-Control: no-cache`, so browsers revalidate them
while fingerprinted framework assets retain their normal caching. This prevents a current WebAssembly
build from being combined with an older folder picker or report-opening script.

Cloudflare builds normally start from a clean checkout, but the current Pages v2/v3 build images do
not include the .NET SDK. The Cloudflare launcher installs the exact SDK pinned by `global.json` when
`dotnet` is unavailable, then hands off to the same clean cross-platform publisher used locally.
Configure this exact build command:

```bash
bash ./scripts/Publish-Web-Cloudflare.sh
```

Configure the build output directory as:

```text
artifacts/web/wwwroot
```

The launcher requires `CF_PAGES_COMMIT_SHA`, opts the .NET CLI out of telemetry, and installs the SDK only
as a build prerequisite. The canonical publisher then verifies that the SHA is a full commit matching the
checked-out revision, cleans only `artifacts/web`, publishes Release output, embeds that revision, versions
the browser helper assets and verifies the required output. The SDK download and NuGet restore are
build-time dependency requests; they do not add any network behaviour to the published browser app.

## Cloudflare dashboard verification

The repository cannot establish account-level Cloudflare settings. Before relying on the production
privacy statement, a Cloudflare administrator must confirm and record:

- the production Pages project and branch map to this repository and `master`;
- the build command performs a clean Release publish and embeds `CF_PAGES_COMMIT_SHA`;
- the output directory is `artifacts/web/wwwroot`;
- no Pages Functions or advanced-mode Worker is attached;
- no Worker route intercepts the Pages or custom domain;
- Web Analytics is disabled unless separately documented and approved;
- Zaraz is disabled;
- Cloudflare Access is absent, or its processing is separately documented;
- Transform Rules or other features do not inject scripts or rewrite HTML;
- every custom domain or proxy layer is known and reviewed;
- live response headers match the checked-in `_headers` file;
- logging availability, access and retention are acceptable.

Absence of these features from the repository is not proof that they are disabled in the Cloudflare
account.

## Static asset configuration

- Serve `.wasm` as `application/wasm`.
- Serve the published Brotli or Gzip variants with the corresponding `Content-Encoding`.
- Cache fingerprinted framework and application assets immutably.
- Revalidate `index.html`, or give it a short cache lifetime, so deployments do not retain an old asset map.
- Deploy a clean publish directory atomically so obsolete fingerprinted assemblies are not retained.
- Configure a fallback to `index.html` if client-side routes beyond `/` are introduced.
- Change `<base href="/">` when deploying below the origin root.

## Release verification

Before publishing a beta:

1. Build from a committed and identifiable revision with the clean publish script.
2. Confirm the footer shows the expected version and short revision without `-dirty`.
3. Confirm only one current set of fingerprinted PBI Assure application assemblies exists in the clean
   output.
4. Test the exact response headers in current desktop Edge and Chrome.
5. Test startup, primary and alternate folder selection, a complete controlled scan, result navigation,
   **Open report**, HTML download and CSV download. Confirm the opened report is styled and interactive.
6. Confirm the browser console contains no CSP or runtime errors.
7. Clear the Network panel after startup and confirm project selection, analysis and export cause no
   unexpected outbound requests.
8. Run the repository build, test and dependency-vulnerability checks.

No hosting provider is prescribed by this document.

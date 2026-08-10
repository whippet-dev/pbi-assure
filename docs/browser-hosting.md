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
connect-src 'none';
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
```

`'wasm-unsafe-eval'` is required by the client-side Blazor Mono runtime. `connect-src 'none'` is
intentional: the current application performs no API or telemetry connections. Blob downloads do not
require `connect-src blob:`.

`frame-ancestors` must be sent as a response header; placing it in a CSP `<meta>` element is ineffective.
This repository therefore documents the host requirement rather than embedding a partial production
policy in `index.html`.

If a future feature introduces a Blob-backed report iframe or a Web Worker, review and narrowly amend
`frame-src` or `worker-src` at that time. They are deliberately disabled now.

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

1. Build from a committed and identifiable revision.
2. Confirm the footer shows the expected version and short revision.
3. Test the exact response headers in current desktop Edge and Chrome.
4. Confirm the browser Network panel shows only static application requests during project selection,
   analysis and export.
5. Test primary and alternate folder selection.
6. Test HTML and CSV downloads under representative enterprise browser controls.
7. Run the repository build, test and dependency-vulnerability checks.

No hosting provider is prescribed by this document.

# Browser privacy and local processing

## User-facing statement

PBI Assure can accurately state:

> Your Power BI project is processed locally in your browser. Project files and analysis results are
> not uploaded to PBI Assure.

## What happens locally

After the user grants folder access, the browser provides file handles or `File` objects for the chosen
directory. PBI Assure filters these to the project `.pbip`, `.Report` and `.SemanticModel` metadata,
copies the accepted files into the local WebAssembly runtime, and runs the shared Core and Reporting
code in the browser.

HTML and CSV results are built in browser memory and downloaded through temporary Blob URLs. The
application does not use `HttpClient`, `fetch`, XMLHttpRequest, WebSocket, SignalR, telemetry, analytics
or an error-reporting service. It does not store selected projects in a backend.

## Normal network activity

The browser must request PBI Assure's own HTML, CSS, JavaScript, WebAssembly runtime and .NET assemblies
from the static host. The host can therefore receive ordinary request information such as IP address,
time, asset path and browser headers. These requests do not contain selected project files or analysis
results.

Links to Microsoft or W3C guidance in generated reports are passive. A network request occurs only if
the user chooses to open one.

Browser extensions, operating-system security products and organisation-managed browser services are
outside PBI Assure's control. Production hosting must apply the network-blocking CSP documented in
[Browser static hosting](browser-hosting.md).

## Generated outputs remain sensitive

The semantic-usage CSV excludes full M expressions, connection values and source paths, and neutralises
text that spreadsheet software could otherwise interpret as a formula.

The detailed HTML report is not a redacted export. It can include:

- report, page, visual, table, column and measure names;
- DAX and other model metadata;
- source and evidence paths;
- full Power Query M expressions;
- local paths, server names, URLs or hard-coded values contained in those expressions.

Users should review an HTML report before sharing it and protect it to the same standard as its source
project. Connector summaries minimise connection arguments, but that does not remove sensitive values
from the full M expression shown in query details.

## Source access and lifetime

The browser can access only the directory explicitly granted by the user. Canonical project-relative
paths cannot escape that directory. Selected file objects and the resulting inventory remain in browser
memory until another project is chosen or the page is closed or reloaded.

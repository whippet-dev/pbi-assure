# Product brand and compatibility

**PBI Project Lens** — **Understand your Power BI project.**

The product is a read-only investigation workspace for Power BI projects. Its previous name was
PBI Assure. This is a display-brand change, not a compatibility migration.

The rename audit separates:

- **User-facing branding:** current web/desktop labels, help, titles, diagnostic prose and portable
  HTML headings/footers use PBI Project Lens. Existing graphical marks remain unchanged.
- **Documentation/public description:** current introductions use the new name and investigation
  positioning. Dated deployment records and fixture provenance remain historical evidence.
- **Application metadata:** Product, Description and Desktop's display title use the new brand;
  package/application IDs and assembly names do not.
- **Internal identifiers:** `PbiAssure.*` namespaces, classes, assemblies, solution/project names and
  source paths remain stable.
- **Compatibility identifiers:** `pbiassure` CLI usage, output filenames, JS globals, message types,
  error prefixes, appearance-storage keys, configuration keys and schema identities remain stable.
  Public JSON is still 0.26; ViewerSnapshot is still 1.0.
- **Historical/development references:** fixture names, canary values and repository-locating test
  diagnostics may retain the old spelling. They are not product labels.

The repository remains `whippet-dev/pbi-assure` and existing hosting/documentation URLs stay intact.
Do not rename these technical identifiers as a cosmetic follow-up without assessing compatibility.

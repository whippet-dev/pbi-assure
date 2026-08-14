# Security

## Supported versions

PBI Assure is currently an early-stage project without versioned support branches. Security fixes are
made against the current `master` branch and the latest deployment at
[pbiassure.pages.dev](https://pbiassure.pages.dev/). Older source revisions, local builds and copied
deployments are not supported after a fix is available.

## Report a vulnerability privately

The preferred reporting route is GitHub Private Vulnerability Reporting:

1. Open the repository's **Security** tab.
2. Select **Advisories** and then **Report a vulnerability**.
3. Provide the information described below.

If **Report a vulnerability** is unavailable, private vulnerability reporting may not be enabled for the
repository. Do not post exploit details, project data, credentials or other sensitive evidence in a
public issue. Instead, open a minimal issue asking the maintainer to provide or enable a private reporting
route.

Repository maintainers can enable the preferred route under **Settings → Security → Code security and
analysis → Private vulnerability reporting**. Enabling that GitHub setting requires separate approval
and is not performed by this document.

## Useful information

Include what you can safely provide:

- the affected PBI Assure version, commit or deployed build revision;
- the affected frontend: browser, command line or Windows desktop;
- a concise description of the issue and its likely impact;
- reproducible steps using synthetic data where possible;
- relevant browser, operating-system and deployment details;
- a minimal proof of concept or redacted evidence;
- whether the issue is already public or being actively exploited.

Do not include a real Power BI project or sensitive generated report unless a secure exchange has been
agreed with the maintainer.

## Relevant security scope

Examples include unintended project-data transmission, unsafe local-file access or path handling,
cross-site scripting in generated output, sensitive-data exposure, bypass of browser security controls,
compromised build or deployment behaviour, and vulnerable dependencies with a practical PBI Assure
impact.

Normal assurance false positives, feature requests and Power BI product issues are not security
vulnerabilities and can use the repository's standard issue process.

## What to expect

Private reports will be reviewed and assessed, with clarification requested where needed. Fixes and
disclosure will be coordinated in proportion to the issue. No fixed response or resolution SLA is
currently promised. Please avoid public disclosure of exploitable details until the issue has been
reviewed and a coordinated disclosure approach has been discussed.

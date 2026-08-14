# Privacy canary fixture

`PBIASSURE_PRIVACY_PROJECT_7F3C2A` is a minimal synthetic PBIP/PBIR/TMDL project used only for
privacy regression testing. Its project, model, Power Query and visual names are unique canary values.
They contain no personal information, credentials, real service URLs or organisation-specific content.

The fixture is deliberately committed so a reviewer can reproduce the browser privacy checks without
using a private report. It exercises report parsing, a visual and measure reference, Power Query/model
processing, HTML generation and semantic-usage CSV generation.

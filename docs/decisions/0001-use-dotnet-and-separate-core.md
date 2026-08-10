# ADR 0001: Use .NET and separate the analysis core

- Status: Accepted for initial development
- Date: 2026-08-07

## Context

The tool must inspect Power BI project metadata locally, later integrate with the Tabular Object Model and XMLA endpoints, run in CI, and potentially support a Windows desktop interface. It should also remain maintainable as contributors and maintainers change over time.

## Decision

Use the supported .NET long-term-support toolchain available in the development environment. Put analysis behaviour in a UI-independent class library and expose it initially through a thin command-line project.

## Consequences

- The same core can serve a command line, tests, CI, and a later desktop application.
- .NET aligns with Microsoft Analysis Services and Tabular client libraries.
- Contributors need basic C# knowledge.
- Detailed PBIR parsing should initially use documented JSON and the standard JSON library, avoiding unnecessary package dependencies.
- UI technology remains an intentionally deferred decision.

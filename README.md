# PicoUI

**AOT-first UI & rendering toolkit for .NET** — server-rendered HTML/HTMX
tooling, terminal UI, and the markdown/mermaid rendering cores behind them.
Every module compiles to NativeAOT with zero runtime reflection.

## Modules

| Module | Role | Packages |
|---|---|---|
| **PicoMarkdown** | Zero-dependency, AOT-safe markdown parsing core (bounded AST + streaming metadata) | `PicoMarkdown` |
| **PicoMermaid** | Zero-dependency Mermaid diagram renderer (flowchart subset) | `PicoMermaid` |
| **PicoTui** | Terminal UI framework on PicoMarkdown + PicoMermaid | `PicoTui` |
| **PicoHtmx** | AOT-safe HTML + HTMX toolkit for PicoWeb (server-rendered components, htmx response helpers) | `PicoHtmx` |

Each module is **independent** — use one, some, or all. `PicoHtmx` is the only
module with an external dependency: `PicoWeb` (PicoNode repo, web-app layer).
Local development uses sibling project references when `../PicoNode` is
checked out; otherwise the published `PicoWeb` package is used.

## Quick Start

```bash
dotnet add package PicoMarkdown   # parse markdown to a bounded AST
dotnet add package PicoHtmx       # server-rendered htmx fragments on PicoWeb
dotnet add package PicoTui        # terminal UI apps
```

## AOT Contract

All modules: `<IsAotCompatible>true</IsAotCompatible>` +
`<IsTrimmable>true</IsTrimmable>` — zero reflection, zero expression trees,
zero runtime code generation. `PicoHtmx` attribute bags are compile-time
anonymous types only (see its design docs).

## Ecosystem

```
PicoInfra   infrastructure (DI / Cfg / Log / Aop / Mediator / Schedule)
PicoNode    network + web framework (PicoWeb ← PicoHtmx depends on it)
PicoUI      this repo — UI / rendering
PicoAgent   consumer product (PicoAgent.Htmx / PicoAgent.Tui samples)
```
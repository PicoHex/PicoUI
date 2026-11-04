# PicoUI

**AOT-first UI & rendering toolkit for .NET** — server-rendered HTML/HTMX
tooling, terminal UI, and the markdown/mermaid rendering cores behind them.
Every module compiles to NativeAOT. The only runtime reflection in the stack is
PicoHtmx's attribute writer, whose property preservation is guaranteed by
`[DynamicallyAccessedMembers]` at every call site (no reflection-based
serialization, no runtime code generation).

## Modules

| Module | Role | Packages |
|---|---|---|
| **PicoMarkdown** | Zero-dependency, AOT-safe markdown parsing core (bounded AST + streaming metadata) | `PicoMarkdown` |
| **PicoMermaid** | Zero-dependency Mermaid diagram renderer (flowchart subset) | `PicoMermaid` |
| **PicoTui** | Terminal UI framework on PicoMarkdown + PicoMermaid | `PicoTui` |
| **PicoHtmx** | AOT-safe HTML + HTMX toolkit for PicoWeb apps (server-rendered components, htmx response helpers) | `PicoHtmx` |

Each module is **independent** — use one, some, or all. `PicoHtmx` is the only
module with external dependencies: `PicoNode.Http` + `PicoNode.Web` (PicoNode
repo, HTTP/web layers; deliberately not `PicoWeb`, which injects source
generators into every consumer) plus `PicoJetson` for JSON writing. Local
development uses sibling project references when `../PicoNode` is checked out;
otherwise the published packages are used.

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

## Behaviour Changes

Two public behaviours were **corrected** in this release — these are not new
features, and both now match what the members' XML docs say:

- **`MarkdownUrl.Sanitize(null)` / `("")` / `("   ")` now return `""`** (was
  `about:blank`). "No destination" is no longer rewritten into a fallback URL;
  unsafe schemes still degrade to `MarkdownUrl.Fallback` (`about:blank`).
  Same for `H.Url` in PicoHtmx — the two are pinned together by a parity test.
- **Void elements with null/empty content self-close: `<br />`** (was `<br></br>`).
  An end tag for a void element is a *second* start tag, so the old pair rendered an
  extra break for `H.Tag("br", null)` and an extra request for `<img>`. Non-void
  elements are unchanged (`<div></div>`).

## Ecosystem

```
PicoInfra   infrastructure (DI / Cfg / Log / Aop / Mediator / Schedule)
PicoNode    network + web framework (PicoWeb ← PicoHtmx depends on it)
PicoUI      this repo — UI / rendering
PicoAgent   consumer product (PicoAgent.Htmx / PicoAgent.Tui samples)
```
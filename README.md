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

## URL Policy

`H.Url` decides every URL that reaches an attribute: relative references and the
`http`/`https`/`mailto`/`tel` schemes pass unchanged, every other scheme becomes
`about:blank`. Three narrow points:

- **`data:` is not allowed wholesale.** Only
  `data:image/(png|jpeg|jpg|gif|webp);base64,<payload>` passes — the form a favicon or
  an inline image is written in. `data:text/html`, `data:image/svg+xml`,
  `data:application/*` and anything without `;base64` right before the comma stay
  `about:blank`: those are documents that run script, not pixels.
- **`H.RawUrl(string)` is the explicit escape hatch** — a type marker like `H.Raw`,
  emitted without the scheme whitelist. Attribute escaping still applies; `null` is an
  empty value (`href=""`), not a fallback URL. Constants and values you sanitized
  yourself only: anything from a user, a request or a model belongs in a plain string.
- **Attribute bags are gated by rendered name**, whatever entry point produced the
  value. The set is closed: `href`, `src`, `action`, `formaction`, `poster`,
  `xlink:href`. `poster` is in it because the browser fetches and decodes it exactly
  like `src`.

## Behaviour Changes

Public behaviours **corrected** in this release — these are not new features, and
each now matches what its member's XML docs say:

- **`MarkdownUrl.Sanitize(null)` / `("")` / `("   ")` now return `""`** (was
  `about:blank`). "No destination" is no longer rewritten into a fallback URL;
  unsafe schemes still degrade to `MarkdownUrl.Fallback` (`about:blank`).
  Same for `H.Url` in PicoHtmx — the two are pinned together by a parity test.
- **Text is escaped by default; markup is explicit.** Every builder escapes its content
  (`H.A(x)` and `H.Span(x)` are byte-identical for the same `x`); only `H.Raw(html)`
  emits markup verbatim.
- **Void elements with null/empty content self-close: `<br />`** (was `<br></br>`).
  An end tag for a void element is a *second* start tag, so the old pair rendered an
  extra break for `H.Tag("br", null)` and an extra request for `<img>`. Non-void
  elements are unchanged (`<div></div>`).
- **`HtmxResult.WithRetry` is gone.** htmx has no retry response header, so the
  `HX-Retry` it wrote was silently ignored. Retries are client-side now: `HX-Trigger`
  (`WithTrigger`) plus an `hx-trigger` handler in the page, or
  `htmx.config.responseHandling` / `htmx:responseError`. `WithHeader` rejects unknown
  `HX-*` names.
- **`Ux.FormField(label, name, type: "textarea")` emits a real `<textarea>`** — value
  between the tags and `rows` (default 3) as an attribute, where the `<input>` branch
  puts the value in `value=""` and has no rows. `<input type="textarea">` is invalid and
  its Text state deletes newlines, so a multi-line prompt used to collapse onto one line.
- **SSE frames keep real blank lines.** `H.SseHtml` frames an interior `\n\n` as its own
  `data: ` field (dropping it glued markdown paragraphs back together), while a run of CRs
  is one terminator: `"a\r\rb"` is two payload lines.

## Ecosystem

```
PicoInfra   infrastructure (DI / Cfg / Log / Aop / Mediator / Schedule)
PicoNode    network + web framework (PicoWeb ← PicoHtmx depends on it)
PicoUI      this repo — UI / rendering
PicoAgent   consumer product (PicoAgent.Htmx / PicoAgent.Tui samples)
```
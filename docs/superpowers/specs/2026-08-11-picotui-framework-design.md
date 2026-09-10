# PicoTui Framework — Design Spec

> 2026-08-11 | Design (in progress) | A zero-dependency, AOT-safe TUI framework for the PicoAgent terminal front-end

## 1. Context & Positioning

PicoAgent ships one product with three entry points sharing one event-sourced core:

- **GUI** — `PicoAgent.Htmx` (HTMX web app, existing)
- **TUI** — `PicoAgent.Tui` (terminal UI, currently a stub) ← this spec
- **CLI** — print / json / rpc modes (non-interactive entry points of the same binary)

**PicoTui** is the general-purpose terminal UI framework that powers `PicoAgent.Tui`.
It is designed for generality but grown from the agent TUI's needs (agent-first).
It lives inside the PicoAgent repository for now:

```
PicoAgent/
├── src/PicoTui/             ← TUI framework (zero-dependency, AOT-safe)
├── src/PicoMarkdown/        ← markdown parsing core (AST, no rendering)
├── src/PicoMermaid/         ← mermaid → Unicode art (flowchart/sequenceDiagram subset)
├── samples/PicoAgent.Tui/   ← TUI product (sibling of PicoAgent.Htmx)
└── samples/PicoAgent.Htmx/
```

**Hard constraints**

- .NET 10, NativeAOT, full trimming, zero reflection
- Zero third-party runtime dependencies (pure P/Invoke to OS APIs only)
- One pattern everywhere: *draw into a cell buffer, diff it out*

**Reference implementations studied**

- `pi-tui` (earendil-works/pi) — differential rendering, Terminal interface
  abstraction, synchronized output (CSI 2026), Kitty keyboard protocol, native
  modifier helpers (win32/darwin)
- `codex-tui` (openai/codex) — ratatui + crossterm; event-driven state machine
  for the agent conversation flow (`app.rs` / `app_event.rs`)
- `markdown-tui-explorer` (leboiko/markdown-reader) — markdown + mermaid in the
  terminal; demonstrates both the Unicode-art path and the image path
- `grok-mermaid` (used by pi) / `mermaid-text` (Rust) — mermaid → Unicode
  box-drawing art, zero-dependency

## 2. Architecture Overview

```
PicoAgent.Tui (application layer: agent state machine + StreamEvent/block consumption)
    └── PicoTui (framework)
          ├── Terminal/   platform terminal abstraction
          ├── Screen/     cell buffer + differential rendering + sync output
          ├── Input/      key decoding + keyboard protocol negotiation
          ├── Layout/     layout nodes + components
          └── Loop/       event loop (input + async agent events)
```

Dependency direction: `PicoAgent.Tui → PicoTui + PicoMarkdown + PicoMermaid + PicoAgent(Host)`.
One-way, no cycles. The framework layer knows nothing about agents.

## 3. Terminal Layer

```csharp
public interface ITerminal
{
    void Start(Action<string> onInput, Action onResize);
    void Stop();                                  // restore raw mode, always
    void Write(string data);
    int Columns { get; }
    int Rows { get; }
    void MoveBy(int lines);
    void HideCursor(); void ShowCursor();
    void ClearLine(); void ClearFromCursor(); void ClearScreen();
    void SetTitle(string title);
    Task DrainInputAsync(int maxMs, int idleMs);  // drain before exit (SSH key-release leak)
}
```

| Implementation | Mechanism |
|---|---|
| `UnixTerminal` | termios raw mode + ioctl(TIOCGWINSZ) + ANSI escapes |
| `WindowsTerminal` | `SetConsoleMode` (ENABLE_VIRTUAL_TERMINAL_INPUT/PROCESSING) + `GetConsoleScreenBufferInfo` |
| `VirtualTerminal` | in-memory terminal model + scripted input → **testing** (pi's `@xterm/headless` role) |

Responsibilities: raw mode, alternative screen (`smcup`/`rmcup`), bracketed paste
(`\x1b[?2004h/l`), cursor hide/show, resize signals (SIGWINCH / console events).

## 4. Screen Layer — Differential Rendering

```
Cell { char, style, dirty }
Buffer { 2D grid }   ← double-buffered
DiffRenderer         ← compare old/new → minimal ANSI output
```

**Three rendering strategies** (from `TuiMainScreen`):

1. First render: full output, no scrollback clear
2. Width changed / content above changed: clear screen + full re-render
3. Normal: move cursor to first changed line → clear-to-end → write changed lines

**Synchronized output**: every update wrapped in `\x1b[?2026h ... \x1b[?2026l`
(CSI 2026) for atomic, flicker-free rendering.

**Dual renderers** (shared `TUI` interface, pi's design):

| Renderer | Buffer | Scrolling | Exit behavior | Use case |
|---|---|---|---|---|
| `MainScreen` | main buffer | terminal-owned (user wheel/scrollbar) | output stays in terminal history | short sessions, console-style |
| `AltScreen` | alternate buffer (`\x1b[?1049h`) | **application-owned** (SGR mouse, ScrollView, follow-end) | screen restores, output gone (vim behavior) | **default for agent TUI** — long sessions, follow streaming, collapsible thinking, tool animations |

Both wrap updates in synchronized output.

### 4.1 Data model

```csharp
readonly record struct Cell(char Ch, Style Style);
struct Style { bool Bold, Italic, Underline, Reverse; RgbColor? Fg; RgbColor? Bg; }
sealed class Line { Cell[] Cells; }
sealed class ScreenBuffer { Line[] Lines; }   // double-buffered: prev / next
```

### 4.2 Diff algorithm (line-level)

Rendering strategy per frame:

1. First frame → full output (no scrollback clear)
2. Width changed / content above viewport changed → clear + full re-render
3. Otherwise → compute diff; if the changed region covers >50% of rows, clear +
   full re-render instead; else incremental

Incremental diff:

```
1. Find first changed line (from top) and last changed line (from bottom)
2. No change → emit nothing
3. Position cursor at firstDiff row, col 0
4. For each line in [firstDiff..lastDiff]:
     a. RLE-encode cells → ANSI (consecutive same-style cells → one SGR run)
     b. write line content
     c. write \x1b[K (clear-to-end) to erase leftovers of a longer old line
5. Adjacent lines joined with \r\n; absolute positioning (\x1b[{r};{c}H)
   only for large jumps
```

Why line-level and not cell-level: terminal I/O is orders of magnitude slower
than in-memory comparison; almost all updates are whole-line (streaming
appends, content replacement). In-line fine edits (e.g. an input caret move)
are caught by the component re-rendering its line. Cell-level diffing adds
complexity with no perceivable gain.

Hardware cursor after render: if a focused component exposes a caret position
(pi's `CURSOR_MARKER` scan), position it and `\x1b[?25h`; else `\x1b[?25l`.

### 4.3 Color model — truecolor RGB + depth adapter

Terminal palette standards: 3-bit (8) / 4-bit (16) / 8-bit (256) / 24-bit
(truecolor); extended colors defined by ISO 8613-6 (ITU-T T.416).

**Model decision (pi/codex-proven):** the application styles in RGB truecolor;
the `Terminal` layer adapts on output based on the negotiated color depth.

```csharp
enum ColorDepth { TrueColor, Color256, Color16, Mono }
// negotiated at startup via: $COLORTERM, $TERM, DA query (\x1b[c / \x1b[>c)
// optional: OSC 11 background query + \x1b[?997;1n/2n for dark/light theme adaptation
```

- TrueColor → `38;2;r;g;b` directly
- Color256 → nearest of 6×6×6 cube + 24 grays
- Color16 → nearest of 16
- Mono → drop color

The adapter is a thin negotiated-once layer (~150 lines). Theme adaptation via
OSC 11 is a v1 optional (agent chat has dark/light theme needs).

## 5. Input Layer — Key Decoding

```
raw bytes → StdinBuffer (split batched input) → KeyDecoder → Key → event
                                        ↘ protocol-negotiation response interception
```

### 5.1 Key model

```csharp
readonly record struct Key(char Char, bool Ctrl, bool Alt, bool Shift);
// special keys: Enter / Escape / Tab / Backspace / Delete / Home / End /
//               ↑↓←→ / F1-F12 / PageUp / PageDown / Insert
bool MatchesKey(string seq, string spec);   // "ctrl+c", "shift+tab", "alt+left"...
```

### 5.2 StdinBuffer — sequence boundary splitting

Splits batched raw bytes into individual complete sequences (one event at a time):

- UTF-8 decoding (incl. surrogate pairs / emoji)
- Escape-sequence boundary: `ESC [` until final byte (`A-Za-z~`)
- Bracketed-paste markers (`\x1b[200~ … \x1b[201~`) → re-wrapped paste event
- **Kitty negotiation response interception** (not treated as keys while
  negotiating; split fragments buffered with 150ms flush)
- **ESC disambiguation**: bare `ESC` byte → wait ~150ms; if it extends into a
  known sequence → sequence; otherwise → Escape key

### 5.3 KeyDecoder — sequence → Key

| Input | Meaning |
|---|---|
| `0x00-0x1F` control chars | Ctrl+A=0x01, Enter=0x0D, Tab=0x09, Esc=0x1B |
| `ESC [ A/B/C/D` | ↑ ↓ → ← |
| `ESC [ 1;5A` | Ctrl+↑ (CSI with modifiers) |
| `ESC [ 27;m;c~` | modifyOtherKeys modified key |
| `ESC [ n;m u` | **Kitty keyboard protocol**: real key + modifiers (incl. press/release) |
| `ESC O A` | arrow keys on some terminals (SS3) |
| `ESC x` | Alt+x (legacy meta) |

### 5.4 Keyboard protocol negotiation (progressive enhancement, pi-proven)

```
startup query: \x1b[>7u\x1b[?u\x1b[c    (flags 7 = disambiguate + report event
                                             types + report alternate keys)
  ├─ response \x1b[?7u     → Kitty protocol active → keys as \x1b[n;m u (strongest)
  ├─ response \x1b[?...c   → no Kitty → enable modifyOtherKeys: \x1b[>4;2m
  │                          → modified keys as \x1b[27;m;c~
  └─ neither               → native helpers fallback
on exit (must clean up): \x1b[<u + \x1b[>4;0m
```

**v1 scope decision: full model** — Kitty + modifyOtherKeys + native helpers
(no subset). This is the cross-terminal input-feel foundation, and pi's
implementation is a proven reference.

### 5.5 Native helpers (P/Invoke, zero third-party)

| Platform | Helper | Solves |
|---|---|---|
| Windows | `SetConsoleMode` + `ENABLE_VIRTUAL_TERMINAL_INPUT` (0x200) | console sends VT sequences (Shift+Tab=`\x1b[Z`); `ReadConsoleInput` drops modifier info |
| Windows/macOS | modifier-key state detection (`GetKeyState` etc.) | **Shift+Enter ambiguity**: some terminals send `\r` for both Shift+Enter and Enter — check physical modifier state |

### 5.6 Wide-character width table

- Built-in East Asian Width table (static lookup generated from Unicode data at
  build time, not a dependency)
- Drives cursor positioning / line width (`CJK`=2 cols, emoji variants, combining
  chars=0)
- Shared with the Screen layer (`VisibleWidth` / `TruncateToWidth` utilities)

### 5.7 Edge cases

| Scenario | Handling |
|---|---|
| Batched input coalescing | StdinBuffer dispatches one sequence at a time |
| ESC ambiguity | ~150ms timeout |
| Kitty response split across chunks | negotiation buffer reassembly + timeout flush |
| Fast key repeat | Kitty flags 2 distinguishes press/repeat/release |
| Large paste | folded into `[paste #N +M lines]` markers |
| Slow SSH key-release leak | `DrainInputAsync` before exit |

### 5.8 Testing

`VirtualTerminal` scripted input → KeyDecoder assertions: one golden set per
terminal protocol (legacy / CSI / SS3 / modifyOtherKeys / Kitty), ESC-ambiguity
timing tests, coalescing split tests, width-table tests (CJK/emoji/combining).

## 6. Layout & Components

### 6.1 Component contract (pi's minimal model)

```csharp
public interface IComponent
{
    string[] Render(int width);    // render full content lines (unbounded height)
    void HandleInput(string seq);  // receives keyboard when focused
    void Invalidate();             // clear cache, re-render on next frame
}
```

Layout nodes (VStack/HStack) are IComponents too, but they run an internal
layout pass over `(width, availableHeight)` — the height comes from the parent
allocation, not from the one-argument contract (see 6.2).

Core principle: `Render(width)` returns the **unbounded full content**; whoever
needs to clip does the clipping (ScrollView slices the viewport, layout nodes
allocate heights). Main screen = whole document; Alt screen = layout tree
allocates constrained regions.

### 6.2 Flexbox-subset layout (pi's basis/grow/shrink model)

```csharp
public readonly record struct LayoutItem(
    IComponent Component,
    int Basis = 0,                        // 0 = auto (natural height); >0 = fixed
    int Grow = 0,                         // share of free space
    int Shrink = 1,                       // shrink weight when overflowing
    int MinSize = 0,
    int MaxSize = int.MaxValue,
    Func<int,int,bool>? Visible = null);  // responsive visibility (in v1)

public sealed class VStack : IComponent { void Add(LayoutItem item); ... }
public sealed class HStack : IComponent { /* same, horizontal */ }
```

**Vertical flex allocation** (VStack's internal layout pass over `(width, availableHeight)`):

```
1. invisible (Visible callback false) → return []
2. natural heights: for Basis==0 items, render child at width → count lines
3. fixed items (Basis>0) take their basis (clamped by min/max)
4. auto items take natural height (clamped)
5. remaining = availableHeight − allocated
6. remaining > 0 → distribute by Grow weight among Grow>0 items (clamp MaxSize)
7. remaining < 0 (overflow) → shrink by Shrink weight, down to MinSize
8. per item: render child content, take the allocated slice
```

Typical agent layout:

```
VStack:
  ├─ LayoutItem(ScrollView(messages), basis: 0, grow: 1, min: 1)  ← fills and scrolls
  └─ LayoutItem(HStack(Editor, StatusBar), basis: 0, shrink: 1)   ← fixed at bottom
```

### 6.3 ScrollView — application-owned scrolling (Alt-screen core)

```csharp
public sealed class ScrollView : IComponent
{
    ScrollView(IComponent child, ScrollOptions opts);
    // opts: Follow("end"|"manual"), Primary(bool), Overscroll("chain"|"none")
}
```

```
child.render(width) → unbounded content lines
viewport = lines [scrollOffset, scrollOffset + visibleHeight)
follow-end: after render, if pinned to bottom → scrollOffset = contentHeight − visibleHeight
mouse wheel (SGR) → scrollOffset ± n (Primary=true also captures wheel over non-scroll regions)
keyboard navigation (PageUp/Down, arrows)
```

### 6.4 Focus & input routing

```
tui.SetFocus(component) → keyboard events routed to the focused component's HandleInput
Focusable (IME): component embeds CURSOR_MARKER (zero-width APC) in its rendered
  lines → TUI scans to position the hardware cursor (IME candidate-window placement)
Overlay focus capture: a visible capturing overlay claims input; focus falls back
  to the previous target when it closes
```

### 6.5 Overlay (dialogs/menus)

```csharp
tui.ShowOverlay(component, options);
// width/height: absolute number or "50%" percentage
// anchor: center / top-right / bottom-left / ... (nine-region grid)
// row/col: absolute or percentage (overrides anchor)
// margin: safe distance from terminal edges
// nonCapturing: don't claim focus
```

Rendered on top of the composed viewport lines; multiple overlays stack. Resolution
order: `minWidth` floor > absolute row/col > percentage > anchor > margin clamp.

### 6.6 Theme model (truecolor)

```csharp
public sealed record Theme(
    RgbColor Border, RgbColor Text, RgbColor Accent, RgbColor Muted,
    RgbColor Warning, RgbColor Error, RgbColor Selection, ...);
// dark / light variants; OSC 11 background query selects at startup
// applied via theme.Fg(color, line) style functions
```

### 6.7 Caching & streaming integration (ties the layers together)

```
components cache (width → rendered lines)
Invalidate() clears the cache
streaming: only the changed-tail component invalidates → re-renders its lines
  → PicoMarkdown progressive disclosure: closed nodes frozen, open tail plain
  → ScrollView follow-end slides the viewport; only newly exposed lines render
```

The Screen layer's diff only sees the viewport line array; the Layout layer
controls which lines each component contributes; caching ensures only the
streaming tail is ever recomputed.

### 6.8 Built-in components (v1)

| Component | Description |
|---|---|
| `Text` | multi-line + word wrap + padding |
| `TruncatedText` | single-line truncation (status bar) |
| `Input` | single-line + horizontal scroll |
| `Editor` | multi-line + autocomplete + undo + large-paste folding |
| `SelectList` | keyboard-navigated selection list |
| `StatusBar` | model / tokens / session name |
| `Loader` | animated spinner |
| `ThinkingBlock` | collapsible thinking |
| `Markdown` | wraps PicoMarkdown → ANSI renderer |
| `Mermaid` | wraps PicoMermaid → Unicode art |

### 6.9 Testing

```
Layout: given (child line counts, availableHeight) → assert per-item allocated
  height (golden scenario table)
ScrollView: follow-end behavior, wheel, overflow/underflow
Focus/IME: CURSOR_MARKER placement assertions
Overlay: percentage/absolute/anchor positioning + focus capture
Responsive visible: narrow-width scenarios hide/restore components
```

## 7. Event Loop

### 7.1 Unified event source

```
input reader thread ──┐
agent stream thread ──┼→ Channel<UiEvent> → UI thread: drain a batch → update state
timer ticks ──────────┘                      → render on demand (throttled ~30fps)

sealed record UiEvent(EventKind Kind, string? Input, ActorOutputEvent? Agent, long Tick);
// ActorOutputEvent = the runtime's output-channel event: (Type, Data, ToolCallId, ToolName, TurnId)
enum EventKind { Input, Agent, Resize, Tick, Signal }
```

### 7.2 Threading model — serial core + async periphery

**Principle: only what must be serialized is serialized; everything else is pure async.**

| Work | Home | Why |
|---|---|---|
| state mutation (message tree / focus / scroll) | **serial** (UI thread) | rendering reads it; must be consistent |
| layout allocation (VStack/ScrollView) | **serial** | depends on live state, no snapshot |
| diff + ANSI terminal writes | **serial** | sole writer to the terminal |
| input acquisition | async (reader thread → Channel) | touches neither state nor terminal |
| agent event acquisition | async (stream thread → Channel) | same |
| PicoMarkdown parse | **serial** (UI thread) | sub-millisecond for typical <10 KB messages — well inside the frame budget (§8.2); never blocks display |
| PicoMermaid layout | **async** (heaviest computation) | pure: source → cached art |
| large-document render derivation | async | same (the only async markdown path, §8.2) |

**Async derivation contract**: off-thread work must be a pure function over an
immutable snapshot; results carry a `(messageId, version)` key and the UI thread
only accepts results with `version >= current`, discarding stale ones.

- mermaid satisfies this naturally (input = source string, output = cacheable art); the long-document markdown fallback uses the same contract
- layout does NOT (depends on live state) → stays serial (the flip side: what must be serialized stays serial)

### 7.3 Throttled rendering (coalescing)

```
after processing a batch of events:
  if now − lastRender ≥ frameInterval (30fps ≈ 33ms) → render
  else → wait for the next frame
```

High-frequency streaming deltas merge into the frame; `Screen.diff` sees only
the last frame's state — no per-event redraws.

### 7.4 Lifecycle & terminal restore (top correctness requirement)

```
Run():
  terminal.Start(onInput, onResize)
  enter alt screen (\x1b[?1049h) + hide cursor + raw mode
  enable protocols (Kitty / modifyOtherKeys / bracketed paste)
  try:
    loop { await channel.ReadAsync; handle; maybeRender }
  catch/finally:                      ← every exit path
    Cleanup() → terminal.Stop()
```

Cleanup order (pi's `stop()` proven, order-sensitive):

```
1. disable Kitty protocol      (\x1b[<u)
2. disable modifyOtherKeys     (\x1b[>4;0m)
3. disable bracketed paste     (\x1b[?2004l)
4. clear progress indicator    (OSC 9;4;0)
5. DrainInputAsync             (prevent key-release leak to parent shell over slow SSH)
6. pause stdin                 (prevent Ctrl+D re-interpretation after restore)
7. restore raw mode
8. restore main screen + show cursor (\x1b[?1049l, \x1b[?25h)
9. remove event handlers
```

Signals: SIGINT/SIGTERM → push Signal event → set stop flag → loop exits →
finally restores (never `Environment.Exit` directly). SIGWINCH → push Resize
event → re-query size → full re-render. In raw mode Ctrl+C is a normal input
byte, handled by
KeyDecoder; the app decides (agent TUI = abort current turn).

### 7.5 Testing

```
VirtualTerminal:
  - scripted input sequences → assert final screen state (deterministic)
  - lifecycle: simulate signal/exception → assert terminal restored (virtual terminal records final mode state)
  - throttling: burst 100 events → assert render count ≤ frame budget per second
  - thread safety: concurrent agent-thread pushes → assert no reordering/races
Async derivation (separate tests):
  - pure Parse/AST tests
  - stale-version result discard logic
```

## 8. PicoMarkdown — Parsing Core

**Responsibility boundary** (matches the domain-core philosophy):

```
markdown source (string)
   → PicoMarkdown.Parse (pure, no rendering)
   → MarkdownDocument
   → renderers (separate consumers)
        ├─ PicoTui: AST → ANSI
        └─ PicoAgent.Htmx (future): AST → HTML (replaces browser-side marked.js)
```

```csharp
public static class Markdown
{
    public static MarkdownDocument Parse(string source);  // pure, never throws
}

public sealed record MarkdownDocument(
    IReadOnlyList<BlockNode> Blocks,
    int LastStableLine);   // ← progressive-rendering anchor
```

**`LastStableLine`**: the parser knows which blocks are closed (blank line closes
a paragraph, closing fence closes a code block) and reports the last line index
up to which the structure is stable. The application freezes any block with
`block.EndLine <= LastStableLine`. No app-side guessing.

**Block nodes** (`BlockNode` carries `StartLine`/`EndLine`):

```csharp
Heading(int Level, IReadOnlyList<InlineNode> Inlines)
Paragraph(IReadOnlyList<InlineNode> Inlines)
FencedCode(string Lang, string Code)
List(bool Ordered, int StartNumber, IReadOnlyList<ListItem> Items)
ListItem(IReadOnlyList<BlockNode> Blocks)
BlockQuote(IReadOnlyList<BlockNode> Blocks)
Hr
Table(IReadOnlyList<TableRow> Rows)          // GFM subset, in v1
TableRow(IReadOnlyList<TableCell> Cells, bool IsHeader)
TableCell(IReadOnlyList<InlineNode> Inlines)
```

**Inline nodes**:

```csharp
Text(string Value)
Bold / Italic / StrikeThrough (IReadOnlyList<InlineNode> Children)
InlineCode(string Code)
Link(string Url, string? Title, IReadOnlyList<InlineNode> Children)
Image(string Url, string Alt)
SoftBreak / HardBreak
```

**Design decisions**

| Decision | Choice | Reason |
|---|---|---|
| Type model | sealed records + pattern matching | AOT-safe, immutable, testable |
| Parse failure | never throws; unparseable → text | streaming text is often incomplete |
| Line info | block nodes carry `StartLine`/`EndLine` | freeze judgment for progressive rendering |
| Progressive anchor | `LastStableLine` reported by parser | app doesn't guess closure boundaries |
| Bounded subset | no setext headings, no full HTML blocks, no autolinks, limited inline nesting depth | restraint — chat doesn't need them |
| Tables | **in v1** | agent output frequently contains comparison tables; bounded work |

### 8.1 Parser architecture & implementation (competitor-grounded)

Reference implementations studied: pi uses `marked` (tree-building, JS); codex
uses `pulldown-cmark` (event-driven SAX, Rust). Both converge on the same
progressive-rendering metadata; our architecture unifies the two:

**Tree-building AST + single-pass progressive metadata collection**

```
block parse: line-driven state machine
  scan lines: blank-line separation / ``` fence state / list indentation /
  table rows; collect progressive metadata in the SAME pass (no second traversal)

inline parse: delimiter stack (CommonMark-style)
  **bold** / *italic* / `code` / [text](url) / ~~strike~~ — stack-matched,
  nesting depth capped (bomb guard)
```

**Progressive metadata** (built into the parser, validated against codex's
`StreamingMarkdownRender.last_top_level_block_start`):

```csharp
sealed record MarkdownDocument(
    IReadOnlyList<BlockNode> Blocks,
    int LastStableLine);         // == codex's last_top_level_block_start
                                 // no HasReferenceLinks flag — see below
```

**Reference links excluded from v1** (the one retroactive case): codex tracks
`has_reference_link_definition` because a later `[foo]: url` definition can
retroactively change an earlier `[foo]` render. v1 supports only inline links
`[text](url)`, so the record carries no `HasReferenceLinks` field and
`LastStableLine` stays purely line-based — progressive rendering is always
correct. If reference links are added later, the flag reappears.

**Partial closing fences trimmed during streaming** (pi's
`trimPartialClosingFences`): the closing ``` arrives char by char while
streaming; an incomplete fence must be trimmed from rendered code content or
the code block shrinks/flickers. This is built into the open-tail plain-text
treatment.

**Tables**: v1 uses pi's approach (natural/min column widths + cell wrapping +
box-drawing borders). codex's column classification
(Narrative/TokenHeavy/Compact) with key/value transpose fallback is a later
enhancement.

Additional decisions (from this research):

| Decision | Choice | Reason |
|---|---|---|
| Parse model | tree-building AST + single-pass metadata | AST suits progressive caching; single pass avoids double traversal (codex pattern) |
| Reference links | excluded from v1 | eliminates retroactive re-rendering; keeps LastStableLine line-based (codex's `has_reference_link_definition` lesson) |
| Partial closing fences | trimmed from open tail | pi's `trimPartialClosingFences` — prevents code-block flicker while the final fence streams in |
| Table rendering | pi-style column widths + wrapping; codex-style classification deferred | restraint |

### 8.2 Progressive rendering (application-layer strategy)

SSE delivers `text_delta` chunks. The TUI pipeline:

```
chunk arrives → append raw text to display immediately (zero cost)
              → same frame: Parse(accumulated) → Render → diff → update changed rows
```

- **Incrementality is temporal** (per chunk), not spatial (inside one parse).
  Each chunk triggers a full Parse of the accumulated buffer; the parse is
  sub-millisecond for typical messages (<10 KB), well inside a 30fps frame budget.
- **Block/node-level progressive disclosure**: closed nodes render formatted and
  freeze (cached lines); the open streaming tail renders as plain text until it
  closes. No layout jumps.
- **Long-document fallback** (pathological, not v1): stable-prefix render cache +
  reduced throttle, parsed off-thread under §7.2's versioned-result contract.
  Never blocks text display on parsing.

## 9. PicoMermaid — v1: Unicode Box-Drawing Art

**Model**: mermaid subset → Unicode/box-drawing terminal art (same model as
`grok-mermaid` JS and `mermaid-text` Rust — both production-proven, zero-dep).

```
mermaid source → PicoMermaid.Render (subset parser + layout)
              → MermaidArt { Span[] rows, width, warnings }
              → themed spans (border/text/edge/edgeLabel/title/none classes)
```

**v1 scope**

| Item | Decision |
|---|---|
| Diagram types | `flowchart` + `sequenceDiagram` subset; **unsupported types → fallback to raw code + warning** |
| Output model | `Span { text, cls }` rows, theme-styled |
| Width | diagram wider than available width → fallback to raw code |
| Render modes | `off` / `on` / `streaming` (streaming default: fall back to raw code while in-flight, render when complete) |
| Failure | never crash; always fallback + warning |

**Deferred — image path** (not v1): full-fidelity rendering via terminal image
protocols (Kitty/iTerm2) requires an SVG rasterizer. `markdown-tui-explorer`
proves this in Rust via `mermaid-rs-renderer` → `resvg` → `ratatui-image`. In
pure .NET with zero third-party deps this means writing an SVG rasterizer
(unrealistic) or accepting a native dependency (violates zero-dep). **Deferred
decision point**: image rendering vs. browser handoff.

## 10. Application Layer — Agent State Machine (PicoAgent.Tui)

Organization modeled on codex's `app.rs`/`app_event.rs` — an explicit event-driven
state machine for the conversation flow:

```
Idle → StreamingText → ToolExecuting → ... → Idle
          ↘ Thinking (collapsible)
          ↘ SteeringMessage (queued input)
```

**Block lifecycle events** — consumed from the existing `RuntimeActor` block
event model (aligned with `2026-07-31-picoagent-block-rendering-design.md`):

| Event | TUI action |
|---|---|
| `thinking_start`/`thinking_delta`/`thinking_end` | ThinkingBlock (collapsible, no markdown) |
| `content_start`/`text_delta`/`content_end` | markdown-rendered message content (PicoMarkdown) |
| `tool_call_start`/`tool_call_delta`/`tool_call_end` | tool call widget |
| `tool_execution_start`/`tool_execution_end` | tool execution status |
| `new_turn` | next iteration anchor |
| `done` / `error` | finalize / error bubble (+ render partial blocks on mid-iteration failure — not on user abort, see 10.3) |

**Input submit** → `RuntimeActor.RunTurnCmd`; **Ctrl+C/Esc** → abort / clear editor.
### 10.1 Layering: block machine in RuntimeActor, turn machine in the TUI

The block-level state machine (thinking/content/tool start/end) is already
implemented by `RuntimeActor` (see `2026-07-31-picoagent-block-rendering-design.md`).
The TUI application layer only owns the **turn-level** state machine — it consumes
block lifecycle events, manages UI state, steering, and abort.

### 10.2 Turn-level state machine

```
                 submit
   ┌─────────┐ ──────────► ┌────────────┐
   │  Idle   │             │ Streaming  │──done──► Completed ──► Idle
   └─────────┘ ◄────────── └─────┬──────┘
     ▲                           │ error
     │                           ▼
     │                         Failed ──(render partial)──► Idle
     │                           ▲
     │      Esc / Ctrl+C ────────┤
     │          (while streaming)│
     │                           ▼
     └── any state + new input ──┘     Aborting ──(cancel runtime stream)──► Idle
```

States: `Idle` / `Streaming` (thinking/content/tool sub-states live in UI
components) / `Aborting` / `Completed` / `Failed`.

### 10.3 Event → state/UI mapping

| Event | Transition | UI action |
|---|---|---|
| submit input | Idle → Streaming | push user message → `RunTurnCmd(msg, turnId)` |
| `thinking_start/delta/end` | Streaming | ThinkingBlock (collapsible, no markdown) |
| `content_start/text_delta/content_end` | Streaming | Markdown message area (progressive) |
| `tool_call_*` | Streaming | tool-call widget (parsing args) |
| `tool_execution_*` | Streaming | tool-execution status (progress/result) |
| `new_turn` | Streaming | next-iteration anchor, reset block UI state |
| `done` | → Completed | finalize + status-bar update → Idle |
| Esc / Ctrl+C (streaming) | → Aborting | call `RuntimeActor.RequestCancel()` (the runtime's cross-thread cancel) → wait for stream end → Idle |
| `error` / `done` while Aborting | Aborting → Idle | abort completed — discard the still-open partial block, keep finalized blocks (§10.5) |
| `error` (not aborting) | → Failed | render partial blocks first (block design doc: completion events precede the error on mid-iteration failure) → error bubble → Idle |
| Esc / Ctrl+C (Idle) | — | clear editor; second press → exit app |
| input (streaming) | stay | **steering message queued** (RuntimeActor steering handoff via TurnId) |

### 10.4 Steering semantics (aligned with pi)

```
input while streaming → queue (steeringMode: one-at-a-time / all)
  → injected after the current turn's tool calls finish → new turn
  → hooks into RuntimeActor's existing steering handoff; TUI owns the queue UI
```

### 10.5 Abort semantics

```
Aborting: call RuntimeActor.RequestCancel() → wait for the runtime's
error/done to arrive (not a hard kill) → discard the still-open partial block,
keep finalized blocks → Idle. The TUI distinguishes the abort-response from a
genuine failure via its own Aborting state: an `error` event arriving while
Aborting is consumed as abort completion, never routed through Failed.
Guarantee: the terminal always responds to keys (UI thread never blocked by
abort — async-periphery principle)
```

### 10.6 Status bar (derived per frame)

Model / tokens (input↑ output↓ cache R/W) / session name / thinking level /
current-state indicator (thinking spinner).

### 10.7 Testing

```
State machine: event-sequence driven → assert transition sequence
  (table-driven: given event stream → expected state sequence)
Steering: input while streaming → assert queued + injected at turn end
Abort: streaming + Esc → assert Aborting → cancelled → Idle (VirtualTerminal
  asserts restored rendering)
Error partial: thinking mid-stream + error → assert partial rendered + error bubble
```

## 11. Terminology — Alignment with Htmx

Three nested levels; the word "block" belongs to the domain layer only:

| Level | Term | Definition |
|---|---|---|
| Domain | `ContentBlock` (existing) | message structure: thinking / content / tool / image |
| Markdown parse | `AstNode` (concretely `BlockNode`/`InlineNode` under `MarkdownDocument`) | formatting structure inside a text ContentBlock (paragraph/heading/code/...) |
| Render | row | differential-rendering update unit |

One message = `ContentBlock[]` (domain skeleton); each text ContentBlock's
content = markdown → `AstNode[]`. The TUI and Htmx consume the same domain
contract (`2026-07-31` block events), so thinking/tool block behavior stays
consistent across front-ends.

## 12. Key Decisions

| # | Decision | Choice | Reason |
|---|---|---|---|
| 1 | Framework form | independent core layer in-repo (`src/PicoTui`), not a separate package yet | focus; extraction deferred |
| 2 | Default renderer | `AltScreen` (application-owned scrolling) | long agent sessions, follow-end streaming |
| 3 | Sync output | CSI 2026 mandatory | flicker-free is a TUI quality floor |
| 4 | Markdown | bounded-subset `AstNode` core + separate renderers | generality + restraint |
| 5 | Tables | in v1 (incl. CJK column-width) | high chat value |
| 6 | Mermaid | v1 = Unicode art (flowchart + sequenceDiagram), image path deferred | proven model (grok-mermaid/mermaid-text); zero-dep |
| 7 | Keyboard | Kitty protocol → modifyOtherKeys → native helpers | cross-terminal correctness |
| 8 | Conversation flow | explicit state machine | codex-proven; agent flow too complex for scattered callbacks |
| 9 | Streaming text | raw text first, AST upgrade same-frame | never block display on parsing |
| 10 | Color model | truecolor RGB + depth adapter (256/16/mono fallback) | pi/codex-proven; all modern terminals support truecolor |
| 11 | Diff granularity | line-level diff + 50% full-redraw threshold | terminal I/O is the bottleneck; line-level is sufficient |
| 12 | Keyboard scope | full model: Kitty + modifyOtherKeys + native helpers | cross-terminal input-feel foundation; pi-proven reference |
| 13 | Responsive layout | flexbox-subset layout with `Visible` callbacks (basis/grow/shrink) | narrow-terminal usability; ~50 lines, pi-proven |
| 14 | Async boundary | serial-minimal core (screen state + terminal writes) + pure async periphery (input/agent/mermaid; markdown parse is serial on the UI thread — §7.2) with versioned results | only what must be serialized is serial; correctness without concurrency complexity |
| 15 | Parser model | tree-building AST + single-pass progressive metadata; reference links excluded from v1; partial closing fences trimmed | validated against codex's `last_top_level_block_start` and pi's `trimPartialClosingFences`; keeps `LastStableLine` line-based |
| 16 | Turn machine layering | block machine stays in RuntimeActor; TUI owns only the turn-level state machine (Idle/Streaming/Aborting/Completed/Failed) | reuses the existing block event contract; no duplicated block logic |

## 13. Out of Scope / Deferred

- Mermaid image rendering (SVG rasterizer / native-dep decision)
- PicoMarkdown HTML renderer (Htmx keeps marked.js for now)
- Full CommonMark compliance (bounded subset by design)
- Syntax highlighting for code blocks (syntect-like; deferred)
- Mouse support beyond alt-screen scrolling (SGR mode) — v1 optional
- PicoTui extraction to a standalone package

## 14. Work Estimate (rough)

| Layer | Lines |
|---|---|
| Terminal (3 platforms + virtual) | ~600 |
| Screen (buffer + diff + sync output) | ~500 |
| Input (decoding + keyboard negotiation + width table) | ~800 |
| Layout + components | ~1000 |
| Loop | ~300 |
| PicoMarkdown (parser + AST) | ~700 |
| PicoMermaid (subset parser + layout + spans) | ~800 |
| Application layer (state machine + agent widgets) | ~1500 |
| **Total** | **~6200** |

## 15. Open Questions

1. PicoMermaid diagram types beyond the v1 flowchart + sequenceDiagram subset (e.g. classDiagram / erDiagram / gantt) — add in v1 or defer?
2. AltScreen default confirmed? (`--main-screen` escape hatch for history-preserving users)
3. PicoTui extraction timing to a standalone PicoHex package.

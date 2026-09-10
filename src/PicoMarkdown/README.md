# PicoMarkdown

A zero-dependency, AOT-safe markdown parsing core for the PicoHex stack: `Markdown.Parse(string)` returns a bounded AST (`MarkdownDocument`) with single-pass progressive metadata (`LastStableLine`) so streaming consumers can freeze closed nodes while the tail is still arriving.

- **Bounded subset**: headings, paragraphs, fenced code, lists, blockquotes, hr, GFM tables; inline bold/italic/strikethrough/code/link/image. No reference links, no setext headings, no HTML blocks, no autolinks.
- **Pure & tolerant**: same input → same AST; never throws (unparseable input → literal text). CRLF normalized at entry.
- **Progressive**: `LastStableLine` reports the last source line up to which block structure is final — the app freezes `block.EndLine <= LastStableLine`, no guessing.
- **AOT-safe**: records + pattern matching, zero reflection, `IsAotCompatible`/`IsTrimmable`.

Consumed by **PicoTui** (the terminal front-end's `Markdown` component); an Htmx HTML renderer is future work.

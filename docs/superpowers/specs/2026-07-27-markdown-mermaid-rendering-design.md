# Chat Markdown + Mermaid Rendering — Design Spec

**Date:** 2026-07-27
**Status:** Design approved

## 1. Problem

AI responses contain markdown and Mermaid diagrams. `MessageBubble` renders everything as plain escaped text. All formatting is lost.

## 2. Scope

1. **Client-side markdown** — marked.js on Assistant + ToolResult messages
2. **Client-side Mermaid** — mermaid.js on `language-mermaid` code blocks, only after message complete
3. **XSS defense** — double layer: `H.E()` server-side → `textContent` → marked → DOMPurify
4. **Progressive streaming** — `htmx:afterSettle` re-renders on every SSE chunk; partial markdown progressively improves
5. **Static assets** — marked + DOMPurify + mermaid in `wwwroot/lib/`

## 3. Design

### 3.1 Libraries

| Library | Local path |
|---------|------------|
| marked v15 | `wwwroot/lib/marked.min.js` |
| DOMPurify v3 | `wwwroot/lib/purify.min.js` |
| mermaid v11 | `wwwroot/lib/mermaid.min.js` |

### 3.2 Server-Side — ChatView

Add `data-markdown` to Assistant and ToolResult containers:

```csharp
// AssistantContainer / MessageBubble for Role=Assistant:
new { @class = "msg assistant", id = id, data_markdown = "" }

// MessageBubble for Role=ToolResult:
new { @class = "message tool-result", data_markdown = "" }
```

User and System messages are plain text — no `data-markdown`.

### 3.3 Server-Side — Done Marker

When streaming completes, a hidden `<span data-done="">` is appended to the assistant container so the JS knows the message is finished and can run Mermaid:

```csharp
// ChatView:
public static string DoneMarker(string assistantId) =>
    H.Tag("hx-partial",
        H.Span("", new { data_done = "", style = "display:none" }),
        new { hx_target = $"#{assistantId}", hx_swap = "beforeend" }
    );
```

Emitted from `ChatEndpoint.RunSseStream` after the `await foreach` loop completes.

### 3.4 Client-Side — `wwwroot/js/chat-renderer.js`

```javascript
(function () {
    marked.setOptions({ breaks: true, gfm: true });

    const purifyOpts = {
        ALLOWED_TAGS: DOMPurify.sanitizeDefaults.ALLOWED_TAGS.concat(
            'pre', 'code', 'span', 'div'
        ),
        ALLOWED_ATTR: DOMPurify.sanitizeDefaults.ALLOWED_ATTR.concat(
            'class', 'id'
        ),
    };

    document.body.addEventListener('htmx:afterSettle', () => {
        const els = document.querySelectorAll('[data-markdown]');
        for (const el of els) {
            const childCount = el.childNodes.length;
            if (el.dataset.mdCount === String(childCount)) continue;
            el.dataset.mdCount = String(childCount);

            const raw = el.textContent || '';
            if (!raw.trim()) continue;

            el.innerHTML = DOMPurify.sanitize(marked.parse(raw), purifyOpts);
            el.style.whiteSpace = 'normal';

            if (el.querySelector('[data-done]')) renderMermaid(el);
        }
    });

    function renderMermaid(container) {
        const blocks = container.querySelectorAll('code.language-mermaid');
        for (const block of blocks) {
            const pre = block.closest('pre');
            if (!pre) continue;
            const id = 'mermaid-' + Math.random().toString(36).slice(2, 8);
            const div = document.createElement('div');
            div.className = 'mermaid';
            div.id = id;
            div.textContent = block.textContent;
            pre.replaceWith(div);
            try { mermaid.run({ nodes: [div] }); }
            catch (e) {
                div.innerHTML = DOMPurify.sanitize(
                    `<pre class="mermaid-error">${e.message}</pre>`, purifyOpts
                );
            }
        }
    }
})();
```

### 3.5 Streaming Mechanics

```
SSE text_delta → <span>token</span> appended → htmx:afterSettle
    → childCount changed (3→4) → re-render with marked → partial markdown shown

SSE done (after loop) → <span data-done> appended → htmx:afterSettle
    → childCount changed → re-render → data-done found → Mermaid runs
```

No streaming-specific JS. No inline scripts in SSE frames. Just `childCount` comparison + `data-done` marker.

### 3.6 ShellView — Script Includes

Added to `<head>` in the app shell template:

```html
<script src="/lib/marked.min.js" defer></script>
<script src="/lib/purify.min.js" defer></script>
<script src="/lib/mermaid.min.js" defer></script>
<script src="/js/chat-renderer.js" defer></script>
```

All `defer` — load in order, execute after DOM ready, don't block rendering.

## 4. Files Changed

| File | Change |
|------|--------|
| `wwwroot/lib/marked.min.js` | New |
| `wwwroot/lib/purify.min.js` | New |
| `wwwroot/lib/mermaid.min.js` | New |
| `wwwroot/js/chat-renderer.js` | New |
| `samples/PicoAgent.Htmx/Views/ChatView.cs` | `data-markdown` on Assistant/ToolResult, `DoneMarker` method |
| `samples/PicoAgent.Htmx/Views/ShellView.cs` | `<script>` includes |
| `samples/PicoAgent.Htmx/Controllers/ChatEndpoint.cs` | Emit DoneMarker after streaming |

## 5. Out of Scope

- Code syntax highlighting (highlight.js)
- Math rendering (KaTeX)
- Mermaid during streaming (always on Done only)
- Markdown in User/System messages

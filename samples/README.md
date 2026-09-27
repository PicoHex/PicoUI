# PicoUI samples

Runnable samples, one per module — they double as the AOT publish validation
targets in CI (`PublishAot=true` on all five RIDs):

| Sample | Shows |
|---|---|
| `PicoMarkdown.Sample` | parse markdown → print the block outline (`Markdown.Parse`, `LastStableLine`) |
| `PicoMermaid.Sample` | render a flowchart subset to Unicode art (`Mermaid.Render`, warnings) |
| `PicoTui.Sample` | 3-second terminal UI on `VirtualTerminal`/`UiLoop` (needs a TTY; exits early on pipes) |
| `PicoHtmx.Sample` | PicoWeb app serving a full page + an htmx fragment (`Layout.Page`, `H.*`, `Htmx.Html`) |

```bash
dotnet run --project samples/PicoMarkdown.Sample
dotnet run --project samples/PicoMermaid.Sample
dotnet run --project samples/PicoTui.Sample        # TTY required
dotnet run --project samples/PicoHtmx.Sample       # prints its listen URL
```

The consuming product (`PicoAgent.Htmx` / `PicoAgent.Tui`) carries the larger
end-to-end samples; these four exist to keep every module buildable, runnable
and AOT-verifiable inside this repo.

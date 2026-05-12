# PicoTUI

An **AOT-first** Terminal User Interface (TUI) framework for .NET, written in C#.

PicoTUI is designed from the ground up to be compatible with [.NET Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) — no reflection, no runtime code generation, just fast and predictable behaviour.

## Features

- ✅ **AOT-compatible** — marked `IsAotCompatible`, no reflection
- ✅ **Double-buffered renderer** — only writes cells that changed, minimising terminal I/O
- ✅ **ANSI color support** — named 16-color, 256-color palette, and 24-bit RGB
- ✅ **Raw-mode input** — captures keystrokes without line buffering (cross-platform)
- ✅ **Alternate screen** — does not pollute the user's scrollback buffer
- ✅ **Extensible widget system** — derive from `Widget` to create custom components
- ✅ **Built-in widgets**: `Label`, `Block` (border/title), `Paragraph`, `Gauge`, `List`

## Solution structure

```
src/
  PicoTUI.Core/          Core library (AOT-compatible class library)
samples/
  HelloWorld/            Interactive demo application
tests/
  PicoTUI.Core.Tests/    Unit tests (xUnit)
```

## Quick start

```csharp
using PicoTUI;
using PicoTUI.Events;
using PicoTUI.Widgets;

using var app = new Application();

var block = new Block
{
    Title = " Hello, PicoTUI! ",
    TitleAlignment = Alignment.Center,
    BorderSet = BorderSet.Rounded,
    BorderStyle = new Style(Color.Cyan, Color.Default, TextAttributes.Bold)
};

var label = new Label
{
    Text = "Press Q to quit.",
    Style = new Style(Color.White, Color.Default, TextAttributes.None),
    Alignment = Alignment.Center
};

app.Add(block).Add(label);

app.OnRender = canvas =>
{
    block.Bounds = new Rect(0, 0, canvas.Width, canvas.Height);
    label.Bounds = block.InnerBounds with { Height = 1 };
};

app.OnEvent = ev =>
{
    if (ev is KeyEvent k && (k.Key == Key.Escape || k.Character == 'q'))
        return false;          // stop the loop
    return true;
};

app.Run();
```

## Building

```bash
dotnet build PicoTUI.slnx
```

## Running tests

```bash
dotnet test tests/PicoTUI.Core.Tests
```

## Running the sample

```bash
dotnet run --project samples/HelloWorld
```

## AOT publish

```bash
dotnet publish samples/HelloWorld -r linux-x64 -c Release
```

## Core concepts

| Type | Description |
|------|-------------|
| `Canvas` | Virtual screen buffer; widgets draw into it each frame |
| `Renderer` | Diffs consecutive canvases and emits ANSI sequences for changed cells |
| `Terminal` | Static helpers: raw mode, alternate screen, cursor, ANSI output |
| `Style` | Foreground color + background color + `TextAttributes` flags |
| `Color` | Named (16), indexed (256), or RGB (24-bit) color |
| `BorderSet` | Named border character sets: `Single`, `Double`, `Rounded`, `Thick`, `Ascii` |
| `Widget` | Abstract base — implement `Render(Canvas)` to create custom widgets |
| `Application` | Manages the event loop, renderer, and widget tree |
| `Event` | `KeyEvent`, `MouseEvent`, `ResizeEvent` |

## License

See [LICENSE](LICENSE).

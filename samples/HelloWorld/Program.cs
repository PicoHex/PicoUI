using PicoTUI;
using PicoTUI.Events;
using PicoTUI.Widgets;

// ── Widgets ───────────────────────────────────────────────────────────────────

var outerBlock = new Block
{
    Title = " PicoTUI Hello World ",
    TitleAlignment = Alignment.Center,
    BorderSet = BorderSet.Rounded,
    BorderStyle = new Style(Color.Cyan, Color.Default, TextAttributes.Bold)
};

var welcomeLabel = new Label
{
    Text = "Welcome to PicoTUI — an AOT-first C# TUI framework!",
    Style = new Style(Color.Green, Color.Default, TextAttributes.Bold),
    Alignment = Alignment.Center
};

var hintLabel = new Label
{
    Text = "Press Q or Ctrl+C to quit. Press UP/DOWN to move the list.",
    Style = new Style(Color.DarkGray, Color.Default, TextAttributes.None),
    Alignment = Alignment.Center
};

var featureList = new List()
    .AddItems(["AOT-compatible (no reflection)", "Double-buffered renderer", "Raw-mode input", "ANSI 16 / 256 / RGB colors", "Extensible widget system"]);

var listBlock = new Block
{
    Title = " Features ",
    BorderSet = BorderSet.Single,
    BorderStyle = new Style(Color.Yellow, Color.Default, TextAttributes.None)
};

var gauge = new Gauge
{
    Ratio = 0.0,
    Label = "0%",
    FilledStyle = new Style(Color.Black, Color.Cyan, TextAttributes.None),
    EmptyStyle = new Style(Color.White, Color.DarkGray, TextAttributes.None)
};

var gaugeBlock = new Block
{
    Title = " Loading ",
    BorderSet = BorderSet.Single,
    BorderStyle = new Style(Color.Magenta, Color.Default, TextAttributes.None)
};

// ── Application ───────────────────────────────────────────────────────────────

double gaugeValue = 0.0;
const double gaugeStep = 0.05;

using var app = new Application();

app.Add(outerBlock);
app.Add(welcomeLabel);
app.Add(hintLabel);
app.Add(listBlock);
app.Add(featureList);
app.Add(gaugeBlock);
app.Add(gauge);

app.OnRender = canvas =>
{
    // Layout widgets based on current canvas size
    int w = canvas.Width;
    int h = canvas.Height;

    outerBlock.Bounds = new Rect(0, 0, w, h);
    var inner = outerBlock.InnerBounds;

    welcomeLabel.Bounds = new Rect(inner.X, inner.Y, inner.Width, 1);
    hintLabel.Bounds = new Rect(inner.X, inner.Y + 1, inner.Width, 1);

    int listTop = inner.Y + 3;
    int listHeight = Math.Max(3, inner.Height - 7);
    listBlock.Bounds = new Rect(inner.X, listTop, inner.Width, listHeight);
    featureList.Bounds = listBlock.InnerBounds;

    int gaugeTop = listTop + listHeight + 1;
    gaugeBlock.Bounds = new Rect(inner.X, gaugeTop, inner.Width, 3);
    gauge.Bounds = gaugeBlock.InnerBounds;

    // Animate the gauge
    gaugeValue = (gaugeValue + gaugeStep) % 1.05;
    double ratio = Math.Min(gaugeValue, 1.0);
    gauge.Ratio = ratio;
    gauge.Label = $"{(int)(ratio * 100)}%";
};

app.OnEvent = ev =>
{
    if (ev is KeyEvent key)
    {
        if (key.Key == Key.Up) featureList.MoveUp();
        else if (key.Key == Key.Down) featureList.MoveDown();
        else if (key.Key == Key.Escape) return false;
        else if (key.Key == Key.Char && (key.Character == 'q' || key.Character == 'Q')) return false;
        else if (key.IsCtrl('c')) return false;
    }
    return true;
};

app.Run();


namespace PicoHtmx;

public static class Ux
{
    public static string ListPage<T>(
        string title,
        IEnumerable<T> items,
        Func<T, string> renderItem,
        string emptyMsg = "No items"
    )
    {
        var rendered = new List<string>();
        foreach (var item in items)
            rendered.Add(renderItem(item));
        // renderItem returns markup, so the joined list is injected verbatim.
        var list =
            rendered.Count > 0
                ? string.Join("", rendered)
                : H.Div(emptyMsg, new { @class = "empty" });
        return $"""
            <div class="list-page">
                <div class="list-header">{H.E(title)}</div>
                <div class="list-items">{list}</div>
            </div>
            """;
    }

    public static string Form(
        string action,
        string method = "post",
        string? content = null,
        string? error = null
    )
    {
        var errorHtml = error is null ? "" : H.Div(error, new { @class = "form-error" });
        return $"""
            <form action="{H.E(H.Url(action))}" method="{H.E(
                method
            )}" hx-target="this" hx-swap="outerHTML">
                {errorHtml}
                {content}
            </form>
            """;
    }

    /// <summary>Renders one labelled form control. <paramref name="type"/> =
    /// <c>"textarea"</c> emits a real <c>&lt;textarea&gt;</c> (RCDATA — the value
    /// goes between the tags, never in a <c>value</c> attribute): an
    /// <c>&lt;input type="textarea"&gt;</c> is an invalid type, so the browser falls
    /// back to the Text state, whose value sanitization DELETES newlines — editing
    /// a multi-line prompt silently collapsed it onto one line and saved that.
    /// </summary>
    public static string FormField(
        string label,
        string name,
        string? value = null,
        string? type = "text",
        bool required = false,
        int rows = 3
    )
    {
        var reqAttr = required ? " required" : "";
        var labelHtml = $"""
            <label for="{H.E(name)}">{H.E(label)}</label>
            """;
        if (string.Equals(type, "textarea", StringComparison.OrdinalIgnoreCase))
        {
            return $"""
                <div class="form-group">
                    {labelHtml}
                    <textarea id="{H.E(name)}" name="{H.E(name)}" rows="{rows}"{reqAttr}>{H.E(value)}</textarea>
                </div>
                """;
        }

        return $"""
            <div class="form-group">
                {labelHtml}
                <input type="{H.E(type)}" id="{H.E(name)}" name="{H.E(name)}"
                       value="{H.E(value)}"{reqAttr} />
            </div>
            """;
    }

    public static string ConfirmDialog(string message, string confirmUrl)
    {
        return $"""
            <div class="confirm-dialog">
                <p>{H.E(message)}</p>
                <button class="btn-danger" hx-post="{H.E(H.Url(confirmUrl))}"
                        hx-target="closest .confirm-dialog" hx-swap="outerHTML">Confirm</button>
                <button class="btn-secondary" onclick="this.closest('.confirm-dialog').remove()">Cancel</button>
            </div>
            """;
    }

    public static string Toast(string message, bool isError = false)
    {
        var cls = isError ? "toast error" : "toast success";
        return H.Div(message, new { @class = cls });
    }

    public static string Spinner()
    {
        return H.Div("", new { @class = "spinner" });
    }
}

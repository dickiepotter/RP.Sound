using System.Collections.Specialized;
using System.Globalization;
using System.Web;

namespace RP.Sound.Showcase;

/// <summary>
/// The query string of one demo request, read the same way whether it arrived over HTTP at the
/// ASP.NET showcase or through a JavaScript call into the WebAssembly build. It follows ASP.NET's
/// own binding: names are case-insensitive and numbers are culture-invariant. A value that is present
/// but unreadable, including an empty one, is an error rather than a silent fallback to the default.
/// </summary>
public sealed class DemoParameters
{
    private readonly NameValueCollection _values;

    private DemoParameters(NameValueCollection values) => _values = values;

    /// <summary>Parses <c>a=1&amp;b=two</c>, with or without a leading <c>?</c>.</summary>
    public static DemoParameters FromQueryString(string? query) =>
        new(HttpUtility.ParseQueryString(query ?? ""));

    public string String(string name, string fallback) =>
        Present(name, out string? text) ? text : fallback;

    public double Double(string name, double fallback) =>
        Read(name, fallback, (string text, out double value) =>
            double.TryParse(text, CultureInfo.InvariantCulture, out value) && double.IsFinite(value));

    public int Int(string name, int fallback) =>
        Read(name, fallback, (string text, out int value) =>
            int.TryParse(text, CultureInfo.InvariantCulture, out value));

    public bool Bool(string name, bool fallback) =>
        Read(name, fallback, bool.TryParse);

    private bool Present(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? text)
    {
        text = _values[name];
        return text is not null;
    }

    private delegate bool Parser<T>(string text, out T value);

    private T Read<T>(string name, T fallback, Parser<T> parse)
    {
        if (!Present(name, out string? text)) return fallback;
        return parse(text, out T value)
            ? value
            : throw new FormatException($"Parameter '{name}' has the unreadable value '{text}'.");
    }
}

using System.Globalization;

namespace RP.Sound.Showcase;

/// <summary>
/// The query string of one demo request, read the same way whether it arrived over HTTP at the
/// ASP.NET showcase or through a JavaScript call into the WebAssembly build. Names are
/// case-insensitive and numbers are culture-invariant, matching ASP.NET's own binding; a value that
/// is present but unreadable is an error rather than a silent fallback to the default.
/// </summary>
public sealed class DemoParameters
{
    private readonly Dictionary<string, string> _values;

    private DemoParameters(Dictionary<string, string> values) => _values = values;

    /// <summary>Parses <c>a=1&amp;b=two</c>, with or without a leading <c>?</c>.</summary>
    public static DemoParameters FromQueryString(string? query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in (query ?? "").TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=');
            string name = Decode(equals < 0 ? pair : pair[..equals]);
            values[name] = equals < 0 ? "" : Decode(pair[(equals + 1)..]);
        }
        return new DemoParameters(values);

        static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
    }

    public string String(string name, string fallback) =>
        _values.TryGetValue(name, out string? value) ? value : fallback;

    public double Double(string name, double fallback) =>
        Read(name, fallback, (string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value));

    public int Int(string name, int fallback) =>
        Read(name, fallback, (string text, out int value) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value));

    public bool Bool(string name, bool fallback) =>
        Read(name, fallback, bool.TryParse);

    private delegate bool Parser<T>(string text, out T value);

    private T Read<T>(string name, T fallback, Parser<T> parse)
    {
        if (!_values.TryGetValue(name, out string? text)) return fallback;
        return parse(text, out T value)
            ? value
            : throw new FormatException($"Parameter '{name}' has the unreadable value '{text}'.");
    }
}

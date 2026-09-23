using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using RP.Sound.Showcase;

// The browser build of the showcase: the same catalog the ASP.NET server maps onto /api/, exported
// to JavaScript so a static host such as GitHub Pages can play every demo with no server at all.
// Main has nothing to do; the runtime stays loaded and JavaScript calls Render as the cards need it.
return;

[SupportedOSPlatform("browser")]
public static partial class ShowcaseExports
{
    /// <summary>
    /// Renders the sound at <paramref name="path"/> (for example <c>physics/impact</c>) with the
    /// parameters in <paramref name="query"/> (a URL query string) and returns the WAV bytes. An
    /// unknown path or a malformed parameter throws, which JavaScript receives as a rejected call.
    /// </summary>
    [JSExport]
    public static byte[] Render(string path, string query) =>
        ShowcaseCatalog.TryRender(path, DemoParameters.FromQueryString(query), out byte[] wav)
            ? wav
            : throw new ArgumentException($"No sound at /api/{path}.");
}

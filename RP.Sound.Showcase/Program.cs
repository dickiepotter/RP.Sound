using RP.Sound.Showcase;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// ---- Metadata for the client UI ----
app.MapGet("/api/meta", () => Results.Json(ShowcaseCatalog.Meta()));

// ---- Every sound: the catalog is shared with the WebAssembly build, so this server and the
// static GitHub Pages demo render exactly the same thing for the same query. ----
app.MapGet("/api/{**path}", (string path, HttpRequest request) =>
{
    try
    {
        return ShowcaseCatalog.TryRender(path, DemoParameters.FromQueryString(request.QueryString.Value), out byte[] wav)
            ? Results.Bytes(wav, "audio/wav")
            : Results.Text($"No sound at /api/{path}.", statusCode: StatusCodes.Status404NotFound);
    }
    catch (Exception error) when (error is FormatException or ArgumentException)
    {
        return Results.Text(error.Message, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.Run();

using System.Net;
using System.Reflection;
using System.Text;

namespace Unisono.UI.Catalog;

/// <summary>
/// Renders a UI document into one self-contained HTML page: the catalog renderer plus the
/// document as JSON. No external scripts, fonts or assets, so the MCP Apps CSP can stay empty.
/// </summary>
public static class UnisonoUiRenderer
{
    private const string HtmlResource = "Unisono.UI.Catalog.renderer.html";
    private const string ScriptResource = "Unisono.UI.Catalog.renderer.js";

    /// <summary>Returns the self-contained HTML for <paramref name="document"/>.</summary>
    public static string Render(UnisonoUiDocument document, string? title = null)
    {
        UnisonoUiDocumentValidator.ThrowIfInvalid(document);

        var template = ReadResource(HtmlResource);
        var script = ReadResource(ScriptResource);

        // A literal "</" inside the JSON can close the script element early; JSON allows "<\/".
        var json = UnisonoUiDocumentJson.Serialize(document).Replace("</", "<\\/", StringComparison.Ordinal);
        var encodedTitle = WebUtility.HtmlEncode(title ?? document.CreateSurface.SurfaceId);

        return template
            .Replace("__UNISONO_TITLE__", encodedTitle, StringComparison.Ordinal)
            .Replace("__UNISONO_CATALOG_ID__", WebUtility.HtmlEncode(UnisonoUiCatalog.Id), StringComparison.Ordinal)
            .Replace("__UNISONO_CATALOG_VERSION__", WebUtility.HtmlEncode(UnisonoUiCatalog.Version), StringComparison.Ordinal)
            .Replace("__UNISONO_DOCUMENT__", json, StringComparison.Ordinal)
            .Replace("__UNISONO_SCRIPT__", script.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase), StringComparison.Ordinal);
    }

    internal static string ReadResource(string name)
    {
        var assembly = typeof(UnisonoUiRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Unisono UI catalog: embedded resource '{name}' is missing. " +
                $"Found: {string.Join(", ", assembly.GetManifestResourceNames())}.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

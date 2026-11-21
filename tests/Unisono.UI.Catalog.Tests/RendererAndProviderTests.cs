using System.Text;
using Unisono.McpApps;

namespace Unisono.UI.Catalog.Tests;

public sealed class RendererTests
{
    private static UnisonoUiDocument TextDocument(string text, string? title = null)
    {
        var document = UnisonoUiDocument.Surface("surface", [
            new UnisonoUiComponent { Id = "root", Component = "Column", Children = ["text"] },
            new UnisonoUiComponent { Id = "text", Component = "Text", Text = UnisonoUiValue.Of(text) },
        ]);

        return title is null
            ? document
            : new UnisonoUiDocument
            {
                CreateSurface = new UnisonoUiSurface
                {
                    SurfaceId = title,
                    CatalogId = document.CreateSurface.CatalogId,
                    Components = document.CreateSurface.Components,
                },
            };
    }

    [Fact]
    public void Renders_one_self_contained_html_page()
    {
        var document = UnisonoUiDocumentBuilder.For(TestRegistry.Create(), "calculate_pupusas").Build();
        var html = UnisonoUiRenderer.Render(document, "Pupusa Party");

        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script src=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link ", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__UNISONO_DOCUMENT__", html, StringComparison.Ordinal);
        Assert.DoesNotContain("__UNISONO_SCRIPT__", html, StringComparison.Ordinal);
        Assert.Contains("Pupusa Party", html, StringComparison.Ordinal);
        Assert.Contains(UnisonoUiCatalog.Id, html, StringComparison.Ordinal);
        Assert.Contains("\"@path\":\"/quantity\"", html, StringComparison.Ordinal);
        Assert.Contains("ui/initialize", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Cannot_break_out_of_the_json_script_element()
    {
        var html = UnisonoUiRenderer.Render(TextDocument("</script><script>alert(1)</script>"));

        Assert.DoesNotContain("</script><script>alert(1)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        // System.Text.Json escapes '<' as \u003C inside the document, and the provider also rewrites "</".
        Assert.Contains("\\u003C/script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Encodes_the_page_title()
    {
        var html = UnisonoUiRenderer.Render(TextDocument("hi", "<b>Pupusas</b>"), "<b>Pupusas</b>");

        Assert.Contains("&lt;b&gt;Pupusas&lt;/b&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<title><b>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_an_invalid_document()
    {
        var document = UnisonoUiDocument.Surface("surface", [
            new UnisonoUiComponent { Id = "root", Component = "Video" },
        ]);

        var error = Assert.Throws<InvalidOperationException>(() => UnisonoUiRenderer.Render(document));
        Assert.Contains("unknown type 'Video'", error.Message);
    }
}

public sealed class CatalogUiProviderTests
{
    private static readonly UnisonoUiDefinition Definition = new("calculate_pupusas", "ui://test/tool", "catalog");

    private static CatalogUiProvider ProviderWithTool()
    {
        var provider = new CatalogUiProvider();
        provider.AddFromTool(TestRegistry.Create(), "calculate_pupusas", "ui://test/tool");
        return provider;
    }

    [Fact]
    public async Task Serves_the_rendered_document_as_an_mcp_app()
    {
        var provider = ProviderWithTool();
        var asset = await provider.GetAssetAsync(Definition);

        Assert.Equal("ui://test/tool", asset.ResourceUri);
        Assert.Equal("text/html;profile=mcp-app", asset.MimeType);
        var html = Encoding.UTF8.GetString(asset.Content.Span);
        Assert.Contains("Calculate pupusas", html, StringComparison.Ordinal);
        Assert.Contains("calculate_pupusas", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_document_for_inspection()
    {
        var provider = ProviderWithTool();

        Assert.True(provider.TryGetDocument("ui://test/tool", out var document));
        Assert.Equal("calculate_pupusas", document!.CreateSurface.SurfaceId);
        Assert.False(provider.TryGetDocument("ui://other", out _));
    }

    [Fact]
    public async Task Throws_for_an_unknown_resource_or_framework()
    {
        var provider = ProviderWithTool();

        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await provider.GetAssetAsync(new UnisonoUiDefinition("calculate_pupusas", "ui://missing", "catalog")));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await provider.GetAssetAsync(new UnisonoUiDefinition("calculate_pupusas", "ui://test/tool", "react")));
    }

    [Fact]
    public void Rejects_duplicates_and_bad_uris()
    {
        var provider = ProviderWithTool();
        var document = UnisonoUiDocumentBuilder.For(TestRegistry.Create(), "calculate_pupusas").Build();

        Assert.Throws<InvalidOperationException>(() => provider.Add("ui://test/tool", document));
        Assert.Throws<ArgumentException>(() => provider.Add("https://example.com", document));
    }

    [Fact]
    public void Rejects_an_invalid_document_at_registration()
    {
        var provider = new CatalogUiProvider();
        var document = UnisonoUiDocument.Surface("surface", [
            new UnisonoUiComponent { Id = "root", Component = "Column", Children = ["missing"] },
        ]);

        Assert.Throws<InvalidOperationException>(() => provider.Add("ui://test/tool", document));
    }
}

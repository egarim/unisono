using Unisono.UI.Catalog;

namespace Unisono.UI.Catalog.Tests;

public sealed class DocumentValidatorTests
{
    private static UnisonoUiDocument Document(params UnisonoUiComponent[] components)
        => UnisonoUiDocument.Surface("surface", components);

    private static UnisonoUiComponent Root(params string[] children)
        => new() { Id = "root", Component = "Column", Children = children };

    [Fact]
    public void Accepts_a_minimal_document()
    {
        var document = Document(Root("text"), new UnisonoUiComponent { Id = "text", Component = "Text", Text = UnisonoUiValue.Of("Hi") });

        Assert.Empty(UnisonoUiDocumentValidator.Validate(document));
    }

    [Fact]
    public void Rejects_unknown_components()
    {
        var document = Document(
            new UnisonoUiComponent { Id = "root", Component = "Column", Children = ["video"] },
            new UnisonoUiComponent { Id = "video", Component = "Video" });

        Assert.Contains(UnisonoUiDocumentValidator.Validate(document), error => error.Contains("unknown type 'Video'"));
    }

    [Fact]
    public void Rejects_a_missing_root()
    {
        var document = Document(new UnisonoUiComponent { Id = "text", Component = "Text", Text = UnisonoUiValue.Of("Hi") });

        Assert.Contains(UnisonoUiDocumentValidator.Validate(document), error => error.Contains("id 'root'"));
    }

    [Fact]
    public void Rejects_duplicate_ids_and_dangling_children()
    {
        var document = Document(
            new UnisonoUiComponent { Id = "root", Component = "Column", Children = ["ghost"] },
            new UnisonoUiComponent { Id = "root", Component = "Text", Text = UnisonoUiValue.Of("duplicate") });

        var errors = UnisonoUiDocumentValidator.Validate(document);
        Assert.Contains(errors, error => error.Contains("Duplicate component id 'root'"));
        Assert.Contains(errors, error => error.Contains("missing child 'ghost'"));
    }

    [Fact]
    public void Rejects_an_unknown_catalog()
    {
        var document = UnisonoUiDocument.Surface("surface", [Root()]);
        var copy = new UnisonoUiDocument
        {
            CreateSurface = new UnisonoUiSurface
            {
                SurfaceId = document.CreateSurface.SurfaceId,
                CatalogId = "https://example.com/other",
                Components = document.CreateSurface.Components,
            },
        };

        Assert.Contains(UnisonoUiDocumentValidator.Validate(copy), error => error.Contains("Unknown catalogId"));
    }

    [Fact]
    public void Rejects_incomplete_components()
    {
        var document = Document(
            new UnisonoUiComponent { Id = "root", Component = "Column", Children = ["select", "button", "table"] },
            new UnisonoUiComponent { Id = "select", Component = "Select", Label = "Pick" },
            new UnisonoUiComponent { Id = "button", Component = "Button", Label = "Go" },
            new UnisonoUiComponent { Id = "table", Component = "Table" });

        var errors = UnisonoUiDocumentValidator.Validate(document);
        Assert.Contains(errors, error => error.Contains("(Select) needs options"));
        Assert.Contains(errors, error => error.Contains("(Select) needs a value binding"));
        Assert.Contains(errors, error => error.Contains("(Button) needs an action"));
        Assert.Contains(errors, error => error.Contains("(Table) needs rows"));
    }

    [Fact]
    public void Rejects_malformed_pointers_loaded_from_json()
    {
        const string json = """
        {
          "version": "1.0",
          "createSurface": {
            "surfaceId": "s",
            "catalogId": "https://unisono.dev/ui/catalog/v1",
            "components": [
              { "id": "root", "component": "Column", "children": ["text"] },
              { "id": "text", "component": "Text", "text": { "@path": "no-slash" } }
            ]
          }
        }
        """;

        // The converter fails fast, so an invalid binding never reaches the validator.
        var error = Assert.Throws<ArgumentException>(() => UnisonoUiDocumentJson.Deserialize(json));
        Assert.Contains("JSON Pointer", error.Message);
    }

    [Fact]
    public void ThrowIfInvalid_lists_every_problem()
    {
        var document = Document(new UnisonoUiComponent { Id = "root", Component = "Nope" });

        var error = Assert.Throws<InvalidOperationException>(() => UnisonoUiDocumentValidator.ThrowIfInvalid(document));
        Assert.Contains("unknown type 'Nope'", error.Message);
        Assert.StartsWith("Invalid Unisono UI document:", error.Message);
    }
}

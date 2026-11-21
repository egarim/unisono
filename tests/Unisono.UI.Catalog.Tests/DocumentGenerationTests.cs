using System.Text.Json;
using Unisono.UI.Catalog;

namespace Unisono.UI.Catalog.Tests;

public sealed class DocumentGenerationTests
{
    private readonly ToolRegistry _tools = TestRegistry.Create();

    [Fact]
    public void Builds_a_form_from_the_tool_input_schema()
    {
        var document = UnisonoUiDocumentBuilder.For(_tools, "calculate_pupusas").Build();
        var surface = document.CreateSurface;

        Assert.Equal(UnisonoUiCatalog.Version, document.Version);
        Assert.Equal(UnisonoUiCatalog.Id, surface.CatalogId);
        Assert.Equal("calculate_pupusas", surface.SurfaceId);
        Assert.Equal("Calculate pupusas", FirstText(document, "title"));

        var quantity = Component(document, "field-quantity");
        Assert.Equal("NumberInput", quantity.Component);
        Assert.Equal("Number of pupusas", quantity.Label);
        Assert.Equal(1d, quantity.Step);
        Assert.Equal("/quantity", quantity.Value!.Path);

        var price = Component(document, "field-priceEach");
        Assert.Equal("NumberInput", price.Component);
        Assert.Equal(0.01d, price.Step);

        var submit = Component(document, "submit");
        Assert.Equal("Button", submit.Component);
        Assert.Equal("Run", submit.Label);
        Assert.Equal("calculate_pupusas", submit.Action!.Name);
        Assert.Equal("/quantity", submit.Action.Arguments!["quantity"].Path);

        Assert.Contains(Component(document, "root").Children!, id => id == "field-quantity");
        Assert.Contains(surface.Components, component => component.Id == "result-table" && component.Component == "Table");
        Assert.Empty(surface.DataModel!);
    }

    [Fact]
    public void Carries_required_flags_defaults_enums_and_formats()
    {
        var document = UnisonoUiDocumentBuilder.For(_tools, "add_task").Build();

        var title = Component(document, "field-title");
        Assert.True(title.Required);
        Assert.Equal("/title", title.Value!.Path);

        var due = Component(document, "field-due");
        Assert.Equal("date", due.Format);
        Assert.Equal("YYYY-MM-DD", due.Placeholder);

        var priority = Component(document, "field-priority");
        Assert.Equal("Select", priority.Component);
        Assert.Equal(new[] { "Low", "Normal", "High" }, priority.Options!.Select(option => option.Label).ToArray());
        Assert.Equal("normal", document.CreateSurface.DataModel!["priority"]!.GetValue<string>());

        var estimate = Component(document, "field-estimate");
        Assert.Equal("NumberInput", estimate.Component);
        Assert.False(estimate.Required);
    }

    [Fact]
    public void Skips_a_description_that_only_repeats_the_title()
    {
        var document = UnisonoUiDocumentBuilder.For(_tools, "list_tasks").Build();

        Assert.DoesNotContain(document.CreateSurface.Components, component => component.Id == "description");
        Assert.Equal("List tasks", FirstText(document, "title"));
    }

    [Fact]
    public void Overrides_title_submit_label_and_field_label()
    {
        var document = UnisonoUiDocumentBuilder
            .For(_tools, "calculate_pupusas")
            .Title("Pupusa Party")
            .SubmitLabel("Calculate")
            .Label("quantity", "How many?")
            .Build();

        Assert.Equal("Pupusa Party", FirstText(document, "title"));
        Assert.Equal("Calculate", Component(document, "submit").Label);
        Assert.Equal("How many?", Component(document, "field-quantity").Label);
    }

    [Fact]
    public void Rejects_parameter_types_the_catalog_cannot_render()
    {
        var error = Assert.Throws<NotSupportedException>(() => UnisonoUiDocumentBuilder.For(_tools, "search_notes").Build());
        Assert.Contains("tags", error.Message);
        Assert.Contains("React, Blazor or Uno", error.Message);
    }

    [Fact]
    public void Throws_for_an_unknown_tool()
    {
        var error = Assert.Throws<KeyNotFoundException>(() => UnisonoUiDocumentBuilder.For(_tools, "missing").Build());
        Assert.Contains("calculate_pupusas", error.Message);
    }

    [Fact]
    public void Generated_documents_serialize_bindings_as_path_objects()
    {
        var document = UnisonoUiDocumentBuilder.For(_tools, "calculate_pupusas").Build();
        var json = UnisonoUiDocumentJson.Serialize(document);

        Assert.Contains("\"@path\":\"/quantity\"", json);
        Assert.Equal(0, UnisonoUiDocumentValidator.Validate(document).Count);
    }

    [Fact]
    public void Documents_round_trip_through_json()
    {
        var document = UnisonoUiDocumentBuilder.For(_tools, "add_task").Build();
        var copy = UnisonoUiDocumentJson.Deserialize(UnisonoUiDocumentJson.Serialize(document));

        Assert.Equal(UnisonoUiDocumentJson.Serialize(document), UnisonoUiDocumentJson.Serialize(copy));
        Assert.Equal(0, UnisonoUiDocumentValidator.Validate(copy).Count);
    }

    [Fact]
    public void Bindings_must_be_json_pointers()
    {
        Assert.Throws<ArgumentException>(() => UnisonoUiValue.At("quantity"));
        Assert.Equal("/quantity", UnisonoUiValue.At("/quantity").Path);
        Assert.False(UnisonoUiValue.Of("literal").IsPath);
    }

    private static UnisonoUiComponent Component(UnisonoUiDocument document, string id)
        => document.CreateSurface.Components.Single(component => component.Id == id);

    private static string FirstText(UnisonoUiDocument document, string id)
        => Component(document, id).Text!.Literal!.GetValue<string>();
}

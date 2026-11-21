namespace Unisono.UI.Catalog;

/// <summary>
/// Checks a document against the v1 catalog before it is served or rendered: known components,
/// a single root, resolvable child references, the properties each component needs, and
/// well-formed data bindings. The renderer repeats the important checks for untrusted input.
/// </summary>
public static class UnisonoUiDocumentValidator
{
    /// <summary>Returns every problem found; empty when the document is valid.</summary>
    public static IReadOnlyList<string> Validate(UnisonoUiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var errors = new List<string>();

        if (!string.Equals(document.Version, UnisonoUiCatalog.Version, StringComparison.Ordinal))
            errors.Add($"Unsupported document version '{document.Version}'. Expected '{UnisonoUiCatalog.Version}'.");

        var surface = document.CreateSurface;
        if (surface is null)
        {
            errors.Add("Missing createSurface.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(surface.SurfaceId))
            errors.Add("createSurface.surfaceId is required.");
        if (!string.Equals(surface.CatalogId, UnisonoUiCatalog.Id, StringComparison.Ordinal))
            errors.Add($"Unknown catalogId '{surface.CatalogId}'. Expected '{UnisonoUiCatalog.Id}'.");

        var components = surface.Components;
        if (components is null || components.Count == 0)
        {
            errors.Add("createSurface.components is empty.");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var component in components)
        {
            if (component is null)
            {
                errors.Add("createSurface.components contains a null entry.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(component.Id))
                errors.Add("A component has an empty id.");
            else if (!ids.Add(component.Id))
                errors.Add($"Duplicate component id '{component.Id}'.");

            if (!UnisonoUiCatalog.IsComponent(component.Component))
                errors.Add($"Component '{component.Id}' uses unknown type '{component.Component}'.");
        }

        if (!ids.Contains("root"))
            errors.Add("No component with id 'root'.");

        foreach (var component in components)
        {
            if (component is null)
                continue;

            ValidateComponent(component, ids, errors);
            ValidateValue(component.Text, $"{component.Id}.text", errors);
            ValidateValue(component.Value, $"{component.Id}.value", errors);
            ValidateValue(component.Rows, $"{component.Id}.rows", errors);
            ValidateValue(component.Items, $"{component.Id}.items", errors);

            if (component.Action is { } action)
            {
                if (string.IsNullOrWhiteSpace(action.Name))
                    errors.Add($"Component '{component.Id}' has an action without a tool name.");
                if (action.Arguments is not null)
                {
                    foreach (var argument in action.Arguments)
                    {
                        if (string.IsNullOrWhiteSpace(argument.Key))
                            errors.Add($"Component '{component.Id}' has an action argument with an empty name.");
                        ValidateValue(argument.Value, $"{component.Id}.action.arguments.{argument.Key}", errors);
                    }
                }
            }
        }

        return errors;
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> listing every problem.</summary>
    public static void ThrowIfInvalid(UnisonoUiDocument document)
    {
        var errors = Validate(document);
        if (errors.Count == 0)
            return;

        throw new InvalidOperationException(
            "Invalid Unisono UI document:" + Environment.NewLine + " - " + string.Join(Environment.NewLine + " - ", errors));
    }

    private static void ValidateComponent(UnisonoUiComponent component, HashSet<string> ids, List<string> errors)
    {
        switch (component.Component)
        {
            case "Column":
            case "Row":
                if (component.Children is null)
                {
                    errors.Add($"Component '{component.Id}' ({component.Component}) needs children.");
                    break;
                }

                foreach (var child in component.Children)
                {
                    if (string.IsNullOrWhiteSpace(child))
                        errors.Add($"Component '{component.Id}' has an empty child reference.");
                    else if (!ids.Contains(child))
                        errors.Add($"Component '{component.Id}' references missing child '{child}'.");
                }

                break;

            case "Text":
                if (component.Text is null)
                    errors.Add($"Component '{component.Id}' (Text) needs text.");
                break;

            case "TextInput":
            case "NumberInput":
                if (string.IsNullOrWhiteSpace(component.Label))
                    errors.Add($"Component '{component.Id}' ({component.Component}) needs a label.");
                if (component.Value is null)
                    errors.Add($"Component '{component.Id}' ({component.Component}) needs a value binding.");
                break;

            case "Select":
                if (string.IsNullOrWhiteSpace(component.Label))
                    errors.Add($"Component '{component.Id}' (Select) needs a label.");
                if (component.Options is null || component.Options.Count == 0)
                    errors.Add($"Component '{component.Id}' (Select) needs options.");
                if (component.Value is null)
                    errors.Add($"Component '{component.Id}' (Select) needs a value binding.");
                break;

            case "Checkbox":
                if (string.IsNullOrWhiteSpace(component.Label))
                    errors.Add($"Component '{component.Id}' (Checkbox) needs a label.");
                if (component.Value is null)
                    errors.Add($"Component '{component.Id}' (Checkbox) needs a value binding.");
                break;

            case "Button":
                if (string.IsNullOrWhiteSpace(component.Label))
                    errors.Add($"Component '{component.Id}' (Button) needs a label.");
                if (component.Action is null)
                    errors.Add($"Component '{component.Id}' (Button) needs an action.");
                break;

            case "Table":
                if (component.Rows is null)
                    errors.Add($"Component '{component.Id}' (Table) needs rows.");
                if (component.Columns is not null)
                {
                    foreach (var column in component.Columns)
                    {
                        if (string.IsNullOrWhiteSpace(column.Key) || string.IsNullOrWhiteSpace(column.Label))
                            errors.Add($"Component '{component.Id}' has a table column without key or label.");
                    }
                }

                break;

            case "List":
                if (component.Items is null)
                    errors.Add($"Component '{component.Id}' (List) needs items.");
                break;

            case "Image":
                if (string.IsNullOrWhiteSpace(component.Src))
                    errors.Add($"Component '{component.Id}' (Image) needs src.");
                break;
        }
    }

    private static void ValidateValue(UnisonoUiValue? value, string path, List<string> errors)
    {
        if (value is { IsPath: true } && !IsValidPointer(value.Path!))
            errors.Add($"{path} is not a valid JSON Pointer: '{value.Path}'.");
    }

    private static bool IsValidPointer(string pointer)
    {
        if (pointer.Length == 0 || pointer[0] != '/')
            return false;

        for (var i = 0; i < pointer.Length; i++)
        {
            if (pointer[i] != '~')
                continue;

            if (i + 1 >= pointer.Length || (pointer[i + 1] != '0' && pointer[i + 1] != '1'))
                return false;
            i++;
        }

        return true;
    }
}

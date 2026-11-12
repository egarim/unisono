namespace Unisono;

/// <summary>
/// Optional hint for the Swift generator. On a class that implements <see cref="IEntityQuery{T}"/>, names the entity
/// type the way the registry registers it (default: the DTO's type name without a Dto/Model/Record suffix). On a
/// string tool parameter, says which entity type that id belongs to when the name alone ("id", "taskId") is ambiguous.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class UnisonoEntityAttribute(string typeName) : Attribute
{
    public string TypeName { get; } = typeName;
}

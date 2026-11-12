using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;

namespace Unisono.AppIntents.Build;

/// <summary>
/// Writes the Swift App Intents layer for an app from its compiled assembly: one <c>AppIntent</c> per
/// <c>[McpServerTool]</c> method, <c>AppEnum</c>s for the enums they use, <c>Codable</c> structs for the records they
/// return, an <c>AppEntity</c> + <c>EntityStringQuery</c> per <c>IEntityQuery&lt;T&gt;</c>, and an
/// <c>AppShortcutsProvider</c> with a default phrase per intent. Anything the app already declares by hand in its
/// AppIntents folder (a struct, enum or an AppShortcutsProvider with the same name) is left to the hand-written file.
/// Reads metadata only (MetadataLoadContext), so it runs on the Mac at build time without loading iOS code.
/// </summary>
public class UnisonoGenerateSwift : Microsoft.Build.Utilities.Task
{
    [Required] public string AppAssembly { get; set; } = "";
    [Required] public ITaskItem[] References { get; set; } = Array.Empty<ITaskItem>();
    public ITaskItem[]? HandWrittenSwift { get; set; }
    [Required] public string OutputDir { get; set; } = "";
    public int MaxAppShortcuts { get; set; } = 10;
    [Output] public ITaskItem[] GeneratedFiles { get; set; } = Array.Empty<ITaskItem>();

    // ---- model -------------------------------------------------------------------------------------------------

    sealed class ToolInfo
    {
        public string Name = ""; public string Title = ""; public string Description = ""; public bool Destructive;
        public List<ParamInfo> Parameters = new(); public Type? ReturnType; public string SwiftName = "";
    }
    sealed class ParamInfo
    {
        public string Name = ""; public Type Type = null!; public bool Optional; public object? Default; public bool HasDefault;
        public string Description = ""; public string? EntityType; public string? ExplicitEntity;
    }
    sealed class EntityInfo { public string TypeName = ""; public Type Dto = null!; }

    readonly Dictionary<string, Type> _enums = new();
    readonly Dictionary<string, Type> _dtos = new();
    readonly List<EntityInfo> _entities = new();
    readonly HashSet<string> _handWritten = new(StringComparer.Ordinal);
    bool _handWrittenProvider;

    static readonly HashSet<string> SwiftKeywords = new(StringComparer.Ordinal)
    { "associatedtype","class","deinit","enum","extension","fileprivate","func","import","init","inout","internal","let","open",
      "operator","private","precedencegroup","protocol","public","rethrows","static","struct","subscript","typealias","var",
      "break","case","catch","continue","default","defer","do","else","fallthrough","for","guard","if","in","repeat","return",
      "throw","switch","where","while","Any","as","await","false","is","nil","self","Self","super","throws","true","try" };
    static readonly HashSet<string> IntentMembers = new(StringComparer.Ordinal)
    { "title","description","openAppWhenRun","parameterSummary","isDiscoverable","perform","authenticationPolicy","systemImageName" };

    public override bool Execute()
    {
        try
        {
            Directory.CreateDirectory(OutputDir);
            ScanHandWritten();
            var paths = References.Select(r => Path.GetFullPath(r.ItemSpec)).Where(File.Exists).ToList();
            paths.Add(Path.GetFullPath(AppAssembly));
            var core = paths.Any(p => Path.GetFileName(p).Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase)) ? "System.Runtime"
                     : paths.Any(p => Path.GetFileName(p).Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase)) ? "mscorlib" : null;
            using var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths), core);
            var asm = mlc.LoadFromAssemblyPath(Path.GetFullPath(AppAssembly));
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }

            var tools = new List<ToolInfo>();
            foreach (var type in types)
            {
                foreach (var iface in SafeInterfaces(type))
                {
                    if (iface.IsGenericType && iface.Name == "IEntityQuery`1")
                    {
                        var dto = iface.GetGenericArguments()[0];
                        var name = AttrString(type.GetCustomAttributesData(), "UnisonoEntityAttribute", 0) ?? EntityNameFor(dto);
                        if (!_entities.Any(e => e.TypeName == name)) _entities.Add(new EntityInfo { TypeName = name, Dto = dto });
                        RegisterDto(dto);
                    }
                }
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    var attr = m.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "McpServerToolAttribute");
                    if (attr is null) continue;
                    var tool = new ToolInfo
                    {
                        Name = NamedArg(attr, "Name") as string ?? m.Name,
                        Title = NamedArg(attr, "Title") as string ?? Humanize(m.Name),
                        Destructive = NamedArg(attr, "Destructive") is bool d && d,
                        Description = AttrString(m.GetCustomAttributesData(), "DescriptionAttribute", 0) ?? "",
                    };
                    tool.SwiftName = Pascal(tool.Title) + "Intent";
                    tool.Parameters = m.GetParameters().Where(p => p.ParameterType.Name != "CancellationToken").Select(p => new ParamInfo
                    {
                        Name = p.Name ?? "value",
                        Type = p.ParameterType,
                        HasDefault = p.HasDefaultValue,
                        Default = p.HasDefaultValue ? p.RawDefaultValue : null,
                        Optional = p.HasDefaultValue || IsNullable(p.ParameterType) || IsAnnotatedNullable(p, m),
                        Description = AttrString(p.GetCustomAttributesData(), "DescriptionAttribute", 0) ?? "",
                        ExplicitEntity = AttrString(p.GetCustomAttributesData(), "UnisonoEntityAttribute", 0),
                    }).ToList();
                    tool.ReturnType = UnwrapReturn(m.ReturnType);
                    tools.Add(tool);
                }
            }
            foreach (var t in tools)
            {
                foreach (var p in t.Parameters) RegisterUses(Unwrap(p.Type));
                if (t.ReturnType is { } rt) RegisterUses(ElementOf(rt) ?? rt);
            }
            foreach (var t in tools)
                foreach (var p in t.Parameters)
                    p.EntityType = ResolveEntityParam(p);

            var files = new List<string>();
            var written = new List<string>();
            void Emit(string file, string content)
            {
                var path = Path.Combine(OutputDir, file);
                if (!File.Exists(path) || File.ReadAllText(path) != content) File.WriteAllText(path, content);
                written.Add(path);
            }
            var enums = GenerateEnums(); if (enums is not null) Emit("Unisono.Enums.generated.swift", enums);
            var typesSwift = GenerateTypes(); if (typesSwift is not null) Emit("Unisono.Types.generated.swift", typesSwift);
            var intents = GenerateIntents(tools, out var generatedIntents); if (intents is not null) Emit("Unisono.Intents.generated.swift", intents);
            var shortcuts = GenerateShortcuts(tools, generatedIntents); if (shortcuts is not null) Emit("Unisono.Shortcuts.generated.swift", shortcuts);
            foreach (var stale in Directory.GetFiles(OutputDir, "*.generated.swift"))
                if (!written.Contains(stale)) File.Delete(stale);
            GeneratedFiles = written.Select(w => (ITaskItem)new Microsoft.Build.Utilities.TaskItem(w)).ToArray();
            Log.LogMessage(MessageImportance.High, $"Unisono: generated Swift for {generatedIntents.Count} intent(s), {_entities.Count} entity type(s), {_enums.Count} enum(s); {tools.Count - generatedIntents.Count} intent(s) hand-written.");
            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, true);
            return false;
        }
    }

    // ---- scanning helpers ---------------------------------------------------------------------------------------

    void ScanHandWritten()
    {
        foreach (var item in HandWrittenSwift ?? Array.Empty<ITaskItem>())
        {
            if (!File.Exists(item.ItemSpec)) continue;
            var text = File.ReadAllText(item.ItemSpec);
            foreach (Match m in Regex.Matches(text, @"\b(?:struct|enum|class|final class)\s+([A-Za-z_][A-Za-z0-9_]*)"))
                _handWritten.Add(m.Groups[1].Value);
            if (text.Contains("AppShortcutsProvider")) _handWrittenProvider = true;
        }
    }

    static IEnumerable<Type> SafeInterfaces(Type t) { try { return t.GetInterfaces(); } catch { return Array.Empty<Type>(); } }

    static object? NamedArg(CustomAttributeData attr, string name)
        => attr.NamedArguments.FirstOrDefault(a => a.MemberName == name).TypedValue.Value;

    static string? AttrString(IEnumerable<CustomAttributeData> attrs, string attributeName, int ctorIndex)
    {
        var a = attrs.FirstOrDefault(x => x.AttributeType.Name == attributeName);
        if (a is null || a.ConstructorArguments.Count <= ctorIndex) return null;
        return a.ConstructorArguments[ctorIndex].Value as string;
    }

    /// <summary>Enum members as (name, underlying value), in declaration order, from the literal fields.</summary>
    static List<(string Name, long Value)> EnumMembers(Type t)
        => t.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (f.Name, Convert.ToInt64(f.GetRawConstantValue())))
            .ToList();

    static bool IsNullable(Type t) => t.IsGenericType && t.GetGenericTypeDefinition().Name == "Nullable`1";
    static Type Unwrap(Type t) => IsNullable(t) ? t.GetGenericArguments()[0] : t;

    static bool IsAnnotatedNullable(ParameterInfo p, MethodInfo m)
    {
        if (p.ParameterType.IsValueType) return false;
        var flag = p.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "NullableAttribute");
        if (flag is not null && flag.ConstructorArguments.Count > 0)
        {
            var v = flag.ConstructorArguments[0].Value;
            if (v is byte b) return b == 2;
            if (v is IReadOnlyCollection<CustomAttributeTypedArgument> arr && arr.Count > 0) return arr.First().Value is byte b2 && b2 == 2;
        }
        var ctx = m.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "NullableContextAttribute")
               ?? m.DeclaringType?.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "NullableContextAttribute");
        return ctx is not null && ctx.ConstructorArguments.Count > 0 && ctx.ConstructorArguments[0].Value is byte c && c == 2;
    }

    /// <summary>Return type after unwrapping Task/ValueTask; null for void.</summary>
    static Type? UnwrapReturn(Type t)
    {
        if (t.FullName == "System.Void") return null;
        if (t.IsGenericType && (t.GetGenericTypeDefinition().Name == "Task`1" || t.GetGenericTypeDefinition().Name == "ValueTask`1"))
            t = t.GetGenericArguments()[0];
        if (t.Name == "Task" || t.Name == "ValueTask") return null;
        return t;
    }

    static Type? ElementOf(Type t)
    {
        if (t.IsArray) return t.GetElementType();
        if (t.IsGenericType)
        {
            var def = t.GetGenericTypeDefinition().Name;
            if (def is "IReadOnlyList`1" or "IList`1" or "List`1" or "IEnumerable`1" or "IReadOnlyCollection`1" or "ICollection`1")
                return t.GetGenericArguments()[0];
        }
        return null;
    }

    void RegisterUses(Type t)
    {
        t = Unwrap(t);
        if (ElementOf(t) is { } el) { RegisterUses(el); return; }
        if (t.IsEnum) { _enums[t.Name] = t; return; }
        if (IsRecordLike(t)) RegisterDto(t);
    }

    void RegisterDto(Type t)
    {
        if (_dtos.ContainsKey(t.Name)) return;
        _dtos[t.Name] = t;
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            RegisterUses(p.PropertyType);
    }

    static bool IsRecordLike(Type t)
        => !t.IsPrimitive && !t.IsEnum && t.FullName is not null && !t.FullName.StartsWith("System.", StringComparison.Ordinal)
           && (t.IsClass || (t.IsValueType && !t.IsEnum)) && t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Length > 0;

    static string EntityNameFor(Type dto)
    {
        var n = dto.Name;
        foreach (var suffix in new[] { "Dto", "Model", "Record" })
            if (n.EndsWith(suffix, StringComparison.Ordinal) && n.Length > suffix.Length) return n[..^suffix.Length];
        return n;
    }

    string? ResolveEntityParam(ParamInfo p)
    {
        if (Unwrap(p.Type).FullName != "System.String") return null;
        if (p.ExplicitEntity is not null) return _entities.Any(e => e.TypeName == p.ExplicitEntity) ? p.ExplicitEntity : null;
        if (_entities.Count == 0) return null;
        if (p.Name == "id") return _entities.Count == 1 ? _entities[0].TypeName : null;
        if (p.Name.EndsWith("Id", StringComparison.Ordinal))
        {
            var prefix = p.Name[..^2];
            return _entities.FirstOrDefault(e => e.TypeName.Equals(prefix, StringComparison.OrdinalIgnoreCase))?.TypeName;
        }
        return null;
    }

    // ---- naming ------------------------------------------------------------------------------------------------

    static string Humanize(string s)
    {
        s = s.Replace('_', ' ');
        s = Regex.Replace(s, "([a-z0-9])([A-Z])", "$1 $2");
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        return string.Join(" ", words);
    }
    static string Pascal(string s) => string.Concat(Humanize(s).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    static string Camel(string s) { var p = Pascal(s); return p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]; }
    static string Ident(string s) => SwiftKeywords.Contains(s) ? "`" + s + "`" : s;
    static string SwiftString(string s) => "\"" + EscapeForSwift(s) + "\"";
    static string EscapeForSwift(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>Swift type for a C# type (parameters and DTO properties); null when unsupported.</summary>
    string? SwiftType(Type t, bool forEntity)
    {
        var optional = IsNullable(t);
        var inner = Unwrap(t);
        if (ElementOf(inner) is { } el)
        {
            var e = SwiftType(el, forEntity);
            return e is null ? null : "[" + e + "]" + (optional ? "?" : "");
        }
        string? core = inner.FullName switch
        {
            "System.String" => "String",
            "System.Boolean" => "Bool",
            "System.Int32" or "System.Int64" or "System.Int16" or "System.Byte" or "System.UInt16" or "System.UInt32" or "System.SByte" => "Int",
            "System.Double" or "System.Single" or "System.Decimal" => "Double",
            "System.DateTime" or "System.DateTimeOffset" or "System.DateOnly" => "Date",
            "System.Guid" => "String",
            "System.Uri" => "URL",
            _ => null,
        };
        if (core is null && inner.IsEnum) core = inner.Name;
        if (core is null && !forEntity && _dtos.ContainsKey(inner.Name)) core = inner.Name;
        if (core is null) return null;
        return core + (optional ? "?" : "");
    }

    string? SwiftDefault(ParamInfo p)
    {
        if (!p.HasDefault || p.Default is null) return null;
        var t = Unwrap(p.Type);
        if (t.IsEnum)
        {
            // MetadataLoadContext types reject GetEnumValues/GetEnumNames; the literal fields carry the same facts.
            var wanted = Convert.ToInt64(p.Default);
            foreach (var (name, value) in EnumMembers(t))
                if (value == wanted) return "." + Ident(Camel(name));
            return null;
        }
        return p.Default switch
        {
            bool b => b ? "true" : "false",
            string s => SwiftString(s),
            int or long or short or byte => p.Default.ToString(),
            double or float or decimal => Convert.ToDouble(p.Default).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    // ---- generation --------------------------------------------------------------------------------------------

    string? GenerateEnums()
    {
        var sb = new StringBuilder();
        foreach (var (name, t) in _enums.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (_handWritten.Contains(name)) continue;
            var members = EnumMembers(t).Select(m => m.Name).ToArray();
            sb.Append("/// Generated from the C# enum ").Append(t.FullName).Append(". Raw values match camelCase JSON.\n");
            sb.Append("enum ").Append(name).Append(": String, AppEnum, Codable {\n");
            sb.Append("    case ").Append(string.Join(", ", members.Select(m => Ident(Camel(m))))).Append('\n');
            sb.Append("\n    static var typeDisplayRepresentation: TypeDisplayRepresentation = ").Append(SwiftString(Humanize(name))).Append('\n');
            sb.Append("\n    static var caseDisplayRepresentations: [").Append(name).Append(": DisplayRepresentation] = [\n");
            sb.Append(string.Join(",\n", members.Select(m => "        ." + Ident(Camel(m)) + ": " + SwiftString(Humanize(m)))));
            sb.Append("\n    ]\n}\n\n");
        }
        if (sb.Length == 0) return null;
        return Header() + sb.ToString();
    }

    string? GenerateTypes()
    {
        var sb = new StringBuilder();
        foreach (var (name, t) in _dtos.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            if (_handWritten.Contains(name)) continue;
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead).ToList();
            sb.Append("/// Generated from the C# type ").Append(t.FullName).Append(" (camelCase JSON).\n");
            sb.Append("struct ").Append(name).Append(": Codable {\n");
            foreach (var p in props)
            {
                var st = SwiftType(p.PropertyType, false);
                if (st is null) { Log.LogWarning($"Unisono: {t.Name}.{p.Name} has a type Swift cannot carry ({p.PropertyType.Name}); skipped."); continue; }
                // JSON null for a missing optional is fine; every non-value property is optional in Swift to be safe
                if (!st.EndsWith("?") && !p.PropertyType.IsValueType && p.PropertyType.FullName != "System.String") st += "?";
                sb.Append("    var ").Append(Ident(Camel(p.Name))).Append(": ").Append(st).Append('\n');
            }
            sb.Append("}\n\n");
        }
        foreach (var e in _entities.OrderBy(e => e.TypeName, StringComparer.Ordinal))
        {
            var entityName = e.TypeName + "Entity";
            var queryName = e.TypeName + "EntityQuery";
            var props = e.Dto.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead).ToList();
            var idProp = props.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) && Unwrap(p.PropertyType).FullName is "System.String" or "System.Guid");
            if (idProp is null) { Log.LogWarning($"Unisono: entity {e.TypeName} ({e.Dto.Name}) has no string Id property; no AppEntity generated."); continue; }
            var titleProp = props.FirstOrDefault(p => new[] { "Title", "Name", "Subject", "Label", "Summary" }.Contains(p.Name, StringComparer.OrdinalIgnoreCase) && Unwrap(p.PropertyType).FullName == "System.String");
            if (!_handWritten.Contains(entityName))
            {
                sb.Append("/// Generated entity for ").Append(e.Dto.FullName).Append(", registered as \"").Append(e.TypeName).Append("\".\n");
                sb.Append("struct ").Append(entityName).Append(": AppEntity {\n");
                sb.Append("    static var defaultQuery = ").Append(queryName).Append("()\n");
                sb.Append("    static var typeDisplayRepresentation: TypeDisplayRepresentation = ").Append(SwiftString(Humanize(e.TypeName))).Append("\n\n");
                sb.Append("    var id: String\n");
                var inits = new List<string> { "        self.id = dto." + Ident(Camel(idProp.Name)) };
                foreach (var p in props)
                {
                    if (p == idProp) continue;
                    var st = SwiftType(p.PropertyType, true);
                    if (st is null) continue;
                    if (!st.EndsWith("?") && !p.PropertyType.IsValueType && p.PropertyType.FullName != "System.String") st += "?";
                    var pn = Ident(Camel(p.Name));
                    sb.Append("\n    @Property(title: ").Append(SwiftString(Humanize(p.Name))).Append(")\n    var ").Append(pn).Append(": ").Append(st).Append('\n');
                    inits.Add("        self." + pn + " = dto." + pn);
                }
                var titleExpr = titleProp is null ? "id" : Ident(Camel(titleProp.Name));
                sb.Append("\n    var displayRepresentation: DisplayRepresentation { DisplayRepresentation(title: \"\\(").Append(titleExpr).Append(")\") }\n");
                sb.Append("\n    init(_ dto: ").Append(e.Dto.Name).Append(") {\n").Append(string.Join("\n", inits)).Append("\n    }\n}\n\n");
            }
            if (!_handWritten.Contains(queryName))
            {
                sb.Append("/// Generated query: resolves \"").Append(e.TypeName).Append("\" through the C# IEntityQuery registered under that name.\n");
                sb.Append("struct ").Append(queryName).Append(": EntityStringQuery {\n");
                sb.Append("    func entities(for identifiers: [String]) async throws -> [").Append(entityName).Append("] {\n");
                sb.Append("        try await UnisonoTools.entities(").Append(SwiftString(e.TypeName)).Append(", ids: identifiers, as: ").Append(e.Dto.Name).Append(".self).map(").Append(entityName).Append(".init)\n    }\n");
                sb.Append("    func entities(matching string: String) async throws -> IntentItemCollection<").Append(entityName).Append("> {\n");
                sb.Append("        IntentItemCollection(items: try await UnisonoTools.search(").Append(SwiftString(e.TypeName)).Append(", string, as: ").Append(e.Dto.Name).Append(".self).map(").Append(entityName).Append(".init))\n    }\n");
                sb.Append("    func suggestedEntities() async throws -> IntentItemCollection<").Append(entityName).Append("> {\n");
                sb.Append("        IntentItemCollection(items: try await UnisonoTools.suggested(").Append(SwiftString(e.TypeName)).Append(", as: ").Append(e.Dto.Name).Append(".self).map(").Append(entityName).Append(".init))\n    }\n}\n\n");
            }
        }
        if (sb.Length == 0) return null;
        return Header() + sb.ToString();
    }

    string? GenerateIntents(List<ToolInfo> tools, out List<ToolInfo> generated)
    {
        generated = new List<ToolInfo>();
        var sb = new StringBuilder();
        foreach (var tool in tools.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            if (_handWritten.Contains(tool.SwiftName)) continue;
            var body = GenerateIntent(tool);
            if (body is null) continue;
            sb.Append(body).Append('\n');
            generated.Add(tool);
        }
        if (sb.Length == 0) return null;
        return Header() + sb.ToString();
    }

    string? GenerateIntent(ToolInfo tool)
    {
        var sb = new StringBuilder();
        var paramLines = new List<string>();
        var argAssignments = new List<string>();
        var argFields = new List<string>();
        var summaryParts = new List<string>();
        foreach (var p in tool.Parameters)
        {
            var varName = Camel(p.Name);
            if (IntentMembers.Contains(varName)) varName += "Value";
            varName = Ident(varName);
            if (p.EntityType is not null)
            {
                var entityVar = Camel(p.EntityType);
                if (tool.Parameters.Any(o => o != p && Camel(o.Name) == entityVar) || IntentMembers.Contains(entityVar)) entityVar += "Entity";
                varName = Ident(entityVar);
                paramLines.Add($"    @Parameter(title: {SwiftString(Humanize(p.EntityType))})\n    var {varName}: {p.EntityType}Entity");
                argFields.Add($"var {Ident(Camel(p.Name))}: String");
                argAssignments.Add($"{Ident(Camel(p.Name))}: {varName}.id");
                summaryParts.Add(varName);
                continue;
            }
            var st = SwiftType(p.Type, false);
            if (st is null)
            {
                Log.LogWarning($"Unisono: tool '{tool.Name}' has a parameter '{p.Name}' of type {p.Type.Name} that Swift App Intents cannot carry; no intent generated. Write it by hand in AppIntents/ if you need it.");
                return null;
            }
            var optional = p.Optional && !st.EndsWith("?");
            var swiftDecl = st + (optional ? "?" : "");
            var def = SwiftDefault(p);
            var attrs = new List<string> { "title: " + SwiftString(Humanize(p.Name)) };
            if (def is not null && !swiftDecl.EndsWith("?")) attrs.Add("default: " + def);
            else if (def is not null && swiftDecl.EndsWith("?")) { swiftDecl = st.TrimEnd('?'); attrs.Add("default: " + def); }
            if (!swiftDecl.EndsWith("?") && def is null && (st is "String" or "Date" or "Int" or "Double"))
                attrs.Add("requestValueDialog: " + SwiftString(Humanize(p.Name) + "?"));
            paramLines.Add($"    @Parameter({string.Join(", ", attrs)})\n    var {varName}: {swiftDecl}");
            argFields.Add($"var {Ident(Camel(p.Name))}: {swiftDecl}");
            argAssignments.Add($"{Ident(Camel(p.Name))}: {varName}");
            summaryParts.Add(varName);
        }

        // return shape
        string resultType = "some IntentResult & ProvidesDialog";
        string invoke; string ret;
        var rt = tool.ReturnType;
        var elem = rt is null ? null : ElementOf(rt);
        var done = SwiftString(tool.Title + ": done.");
        if (rt is null)
        {
            invoke = $"        try await UnisonoTools.invoke({SwiftString(tool.Name)}, args)";
            ret = $"        return .result(dialog: {done})";
        }
        else if (elem is not null && EntityFor(elem) is { } listEntity)
        {
            resultType = $"some IntentResult & ReturnsValue<[{listEntity.TypeName}Entity]> & ProvidesDialog";
            invoke = $"        let items = try await UnisonoTools.invoke({SwiftString(tool.Name)}, args, returning: [{elem.Name}].self).map({listEntity.TypeName}Entity.init)";
            ret = $"        return .result(value: items, dialog: \"{EscapeForSwift(tool.Title)}: \\(items.count) \\(items.count == 1 ? {SwiftString(Humanize(listEntity.TypeName).ToLowerInvariant())} : {SwiftString(Humanize(listEntity.TypeName).ToLowerInvariant() + "s")})\")";
        }
        else if (elem is not null && SwiftType(elem, true) is { } listScalar && !_dtos.ContainsKey(elem.Name))
        {
            resultType = $"some IntentResult & ReturnsValue<[{listScalar}]> & ProvidesDialog";
            invoke = $"        let items = try await UnisonoTools.invoke({SwiftString(tool.Name)}, args, returning: [{listScalar}].self)";
            ret = $"        return .result(value: items, dialog: \"{EscapeForSwift(tool.Title)}: \\(items.count)\")";
        }
        else if (EntityFor(Unwrap(rt)) is { } entity)
        {
            var optional = IsNullable(rt) || !rt.IsValueType;
            resultType = $"some IntentResult & ReturnsValue<{entity.TypeName}Entity> & ProvidesDialog";
            invoke = $"        guard let dto = try await UnisonoTools.invoke({SwiftString(tool.Name)}, args, returning: {Unwrap(rt).Name}?.self) else {{ throw UnisonoError.failed({SwiftString(tool.Title + " returned no " + Humanize(entity.TypeName).ToLowerInvariant() + ".")}) }}\n        let entity = {entity.TypeName}Entity(dto)";
            ret = $"        return .result(value: entity, dialog: {done})";
        }
        else if (SwiftType(rt, true) is { } scalar && !_dtos.ContainsKey(Unwrap(rt).Name))
        {
            var bare = scalar.TrimEnd('?');
            resultType = $"some IntentResult & ReturnsValue<{bare}> & ProvidesDialog";
            if (scalar.EndsWith("?"))
                invoke = $"        guard let value = try await UnisonoTools.invoke({SwiftString(tool.Name)}, args, returning: {scalar}.self) else {{ throw UnisonoError.failed({SwiftString(tool.Title + " returned nothing.")}) }}";
            else
                invoke = $"        let value = try await UnisonoTools.invoke({SwiftString(tool.Name)}, args, returning: {bare}.self)";
            ret = bare == "Bool"
                ? $"        return .result(value: value, dialog: value ? {done} : {SwiftString(tool.Title + ": nothing changed.")})"
                : $"        return .result(value: value, dialog: \"{EscapeForSwift(tool.Title)}: \\(value)\")";
        }
        else
        {
            // a plain record without an entity: run it, report done, drop the value
            invoke = $"        _ = try await UnisonoTools.invokeRaw({SwiftString(tool.Name)}, try UnisonoTools.encode(args))";
            ret = $"        return .result(dialog: {done})";
        }

        sb.Append("/// Generated from the C# tool \"").Append(tool.Name).Append("\". Write a struct named ").Append(tool.SwiftName).Append(" in AppIntents/ to replace it.\n");
        sb.Append("struct ").Append(tool.SwiftName).Append(": AppIntent {\n");
        sb.Append("    static var title: LocalizedStringResource = ").Append(SwiftString(tool.Title)).Append('\n');
        if (tool.Description.Length > 0) sb.Append("    static var description = IntentDescription(").Append(SwiftString(tool.Description)).Append(")\n");
        sb.Append("    static var openAppWhenRun: Bool = false\n\n");
        foreach (var l in paramLines) sb.Append(l).Append("\n\n");
        if (summaryParts.Count > 0)
        {
            sb.Append("    static var parameterSummary: some ParameterSummary {\n        Summary(\"").Append(tool.Title);
            sb.Append(" \\(\\.$").Append(summaryParts[0]).Append(")\")");
            if (summaryParts.Count > 1) { sb.Append(" {\n"); foreach (var s in summaryParts.Skip(1)) sb.Append("            \\.$").Append(s).Append('\n'); sb.Append("        }"); }
            sb.Append("\n    }\n\n");
        }
        sb.Append("    struct Args: Encodable {").Append(argFields.Count == 0 ? "" : " " + string.Join("; ", argFields)).Append(" }\n\n");
        sb.Append("    func perform() async throws -> ").Append(resultType).Append(" {\n");
        if (tool.Destructive) sb.Append("        try await requestConfirmation(result: .result(dialog: ").Append(SwiftString(tool.Title + "?")).Append("))\n");
        sb.Append("        let args = Args(").Append(string.Join(", ", argAssignments)).Append(")\n");
        sb.Append(invoke).Append('\n').Append(ret).Append("\n    }\n}\n");
        return sb.ToString();
    }

    EntityInfo? EntityFor(Type t) => _entities.FirstOrDefault(e => e.Dto.Name == t.Name);

    string? GenerateShortcuts(List<ToolInfo> tools, List<ToolInfo> generated)
    {
        if (_handWrittenProvider) return null;
        var intents = tools.Where(t => generated.Contains(t) || _handWritten.Contains(t.SwiftName)).ToList();
        if (intents.Count == 0) return null;
        if (intents.Count > MaxAppShortcuts)
        {
            Log.LogWarning($"Unisono: {intents.Count} intents but iOS allows {MaxAppShortcuts} App Shortcuts; only the first {MaxAppShortcuts} get Siri phrases. Write your own AppShortcutsProvider in AppIntents/ to choose.");
            intents = intents.Take(MaxAppShortcuts).ToList();
        }
        var sb = new StringBuilder(Header());
        sb.Append("/// Generated phrases: \"<title> in <app name>\" for every intent. Declare an AppShortcutsProvider in AppIntents/ to replace this.\n");
        sb.Append("struct UnisonoGeneratedShortcuts: AppShortcutsProvider {\n    @AppShortcutsBuilder\n    static var appShortcuts: [AppShortcut] {\n");
        foreach (var t in intents)
        {
            sb.Append("        AppShortcut(\n            intent: ").Append(t.SwiftName).Append("(),\n            phrases: [\"").Append(t.Title).Append(" in \\(.applicationName)\"],\n");
            sb.Append("            shortTitle: ").Append(SwiftString(t.Title)).Append(",\n            systemImageName: ").Append(SwiftString(ImageFor(t.Title))).Append("\n        )\n");
        }
        sb.Append("    }\n}\n");
        return sb.ToString();
    }

    static string ImageFor(string title)
    {
        var first = title.Split(' ')[0].ToLowerInvariant();
        return first switch
        {
            "create" or "add" or "new" => "plus.circle",
            "delete" or "remove" or "clear" => "trash",
            "list" or "show" or "get" => "list.bullet",
            "search" or "find" or "look" => "magnifyingglass",
            "complete" or "finish" or "mark" or "check" => "checkmark.circle",
            "open" or "view" => "eye",
            "set" or "update" or "change" or "edit" => "pencil",
            "append" or "extract" => "text.badge.plus",
            _ => "bolt",
        };
    }

    static string Header() => "// Generated by Unisono.AppIntents from the app's C# tools. Do not edit; put your own Swift in AppIntents/.\nimport AppIntents\nimport Foundation\n\n";
}

# Unisono for .NET — one tool definition, exposed to Siri (App Intents) and to agents (MCP)

Design notes, 2026-11-10. Name: **Unisono** (chosen 2026-11-10; ASCII `Unisono` for ids and namespaces, *Unísono* in prose; bare NuGet id `Unisono` was free that day).

## 1. What is fixed by Apple and by .NET (the constraints)

| Fact | Consequence for the design |
|---|---|
| App Intents are discovered at **compile time**: Xcode's `appintentsmetadataprocessor` reads the Swift module and writes `Metadata.appintents` into the bundle. There is no Objective-C API and no runtime registration. | The intent *declarations* must be Swift. C# can only *implement* them behind a bridge. |
| .NET 10 for iOS can build an Xcode project as part of `dotnet build` (`<XcodeProject>` item → `_BuildXcodeProjects` target). Redth's sample proves the full chain: Xcode framework → binding project → `Metadata.appintents` copied into the .app → Siri works. | We do not need Swift-to-C# interop magic. We need **code generation** of the Swift layer plus MSBuild glue. |
| Siri AI Developer Preview (iOS/macOS/visionOS 27.3 beta, since 2026-11-03): `@AppIntent(.tool)` promotes an intent to a `ToolIntent` that Siri's model can call. Siri picks tools from the intent `title` + `description` and each `@Parameter(title:description:)`. Entities can be `@AppEntity(.generable)` (Siri fills them from the request). Multi-step guidance goes in `Skills.skillsbundle/Skills/<name>/SKILL.md` (a subset of the Agent Skills spec). App Store submissions open spring 2027. | A Siri "tool" and an MCP "tool" are **the same contract**: name, description, typed parameters with descriptions, typed result. That contract is what we define once in C#. |
| MCP in the OS itself: only beta-code hints (iOS 26.1, Sep 2025). Nothing official as of today. | Do not depend on it. If Apple ships "App Intents → MCP", correctly-described tool intents get it for free. |
| MCP C# SDK 2.4.0 (`ModelContextProtocol.Core`, netstandard2.0/net8+): tools are `AIFunction`s (`McpServerTool.Create`, `[McpServerToolType]`, `[McpServerTool]`, `[Description]`). `StreamableHttpServerTransport` lives in **Core**, depends only on `Stream`/channels, has `HandlePostRequestAsync(JsonRpcMessage, Stream)` / `HandleGetRequestAsync(Stream)` and a `Stateless` mode. | We can host MCP over HTTP inside a MAUI app **without ASP.NET Core/Kestrel**, on `HttpListener`. Stdio works for a macOS companion process. |
| swift-dotnet-bindings (SwiftBindings.Sdk, .NET 10 Swift interop) can *call* Swift from C#, but nothing lets a C# type conform to `AppIntent` in a way the metadata processor sees. | Confirms: generate Swift, don't bind Swift. |

## 2. The core idea

```
            ┌────────────── C# (single source of truth) ──────────────┐
            │ [McpServerTool] + [Description] + [SiriTool] attributes │
            │ on ordinary methods; records/enums for parameters       │
            └───────────────┬─────────────────────┬───────────────────┘
                            │ codegen (build time) │
          ┌─────────────────▼───────┐    ┌─────────▼──────────────────┐
          │ Swift: AppIntent structs │    │ C#: ToolDescriptor table   │
          │ @AppIntent(.tool), @Para │    │ (name, JSON schema, typed  │
          │ AppEntity/AppEnum/Query  │    │ invoker, dialog formatter) │
          │ AppShortcutsProvider     │    │                            │
          │ Skills.skillsbundle      │    │                            │
          └───────────┬─────────────┘    └──────────┬─────────────────┘
                      │ perform() → JSON             │
                      ▼                              ▼
   Siri / Shortcuts / Spotlight ──► @objc bridge ──► IToolRegistry.InvokeAsync(name, args)
                                                       ▲              ▲
   Claude Desktop / Claude Code / any MCP client ──────┘              │
        (Streamable HTTP on localhost, or stdio)                      │
   In-app agent (Microsoft.Extensions.AI IChatClient, same AIFunctions)┘
```

Everything converges on one runtime object, the **tool registry**. Siri and MCP are two *projections* of it. A third projection (in-app agent via `IChatClient` tools) is free because the MCP SDK already models tools as `AIFunction`.

## 3. Developer experience we are designing for

```csharp
// In the shared MAUI project — no platform code
[McpServerToolType]
public sealed class CartTools(ICartService cart)
{
    [McpServerTool(Name = "add_to_cart")]
    [Description("Adds a product to the shopping cart.")]
    [SiriTool(Title = "Add to Cart",
              Phrases = ["Add to my cart in \(.applicationName)"],
              Dialog = "Added {quantity} × {result.Title} to your cart")]
    public async Task<CartLine> AddToCart(
        [Description("The product to add")] ProductRef item,
        [Description("How many to add, 1-99")] int quantity = 1)
        => await cart.AddAsync(item.Id, quantity);
}

[AppEntity(TypeName = "Product", Query = typeof(ProductQuery))]
public record ProductRef(string Id, [property: Description("Product name")] string Title);

public sealed class ProductQuery : IEntityQuery<ProductRef>
{
    public Task<IReadOnlyList<ProductRef>> ByIdsAsync(IReadOnlyList<string> ids, CancellationToken ct);
    public Task<IReadOnlyList<ProductRef>> SearchAsync(string text, CancellationToken ct);
    public Task<IReadOnlyList<ProductRef>> SuggestedAsync(CancellationToken ct);
}

// MauiProgram.cs
builder.Services.AddUnisono(o =>
{
    o.AddToolsFromAssembly();              // scans [McpServerToolType]
    o.AddMcpHttp(port: 0, loopbackOnly: true, requireToken: true);
    o.AddSiri();                           // no-op on Android/Windows
});
```

Only `[SiriTool]`, `[AppEntity]`, `IEntityQuery<T>` are ours. The rest is the MCP SDK's own attribute set, so an MCP-only app needs zero extra annotations and a Siri-enabled app adds one attribute per tool.

## 4. Packages (what ships on NuGet)

1. **Unisono.Core** — attributes, `IToolRegistry`, `ToolDescriptor`, JSON (System.Text.Json) invocation, dialog formatting. Targets netstandard2.0/net10. Depends on `ModelContextProtocol.Core` + `Microsoft.Extensions.AI.Abstractions`.
2. **Unisono.Mcp** — hosts the registry as an MCP server. Transports: `StreamableHttpServerTransport` on `System.Net.HttpListener` (loopback or LAN, bearer token per install, QR/pairing screen), `StdioServerTransport` for a macOS companion CLI, in-process `StreamServerTransport` for tests and for an in-app agent.
3. **Unisono.AppIntents.Binding** (net10-ios, net10-maccatalyst) — a **static** `IsBindingProject` with `ApiDefinition.cs` for the generic bridge (see §5). Because the bridge surface never changes, this binding is written once and shipped; only the Swift intents are generated per app.
4. **Unisono.AppIntents.Build** — MSBuild `.targets` + the `unisono-codegen` tool + a templated Xcode project (`UnisonoIntents.xcodeproj` with the bridge Swift files).
5. **Unisono.Analyzers** — Roslyn analyzers: missing `[Description]` on a tool or parameter (Siri needs it), unsupported parameter type, phrase without `\(.applicationName)`, duplicate tool names, `[SiriTool]` on a method whose return type cannot be an entity.

## 5. The generic bridge (why it beats per-intent protocols)

Redth's sample bridges each intent with its own `@objc` method (`createTaskWithTitle:priorityRawValue:…`). That works but means regenerating a binding per app. Instead, one fixed `@objc` protocol:

```swift
// Ships in the NuGet, never regenerated
@objc(UnisonoHost) public protocol UnisonoHost: AnyObject {
    /// argumentsJSON → resultJSON; error → (nil, message)
    func invoke(_ tool: String, argumentsJSON: String,
                completion: @escaping (String?, String?) -> Void)
    func entities(_ type: String, ids: [String],
                  completion: @escaping (String?) -> Void)       // JSON array
    func search(_ type: String, text: String,
                completion: @escaping (String?) -> Void)
    func suggested(_ type: String, completion: @escaping (String?) -> Void)
}

@objc(UnisonoBridge) public class UnisonoBridge: NSObject {
    @objc public static let shared = UnisonoBridge()
    @objc public weak var host: UnisonoHost?
}

// Swift helper used by generated code
extension UnisonoHost {
    func invokeAsync(_ tool: String, _ json: String) async throws -> String { … withCheckedThrowingContinuation … }
}
```

C# implements `UnisonoHost` once (in the Core package's iOS target), deserialises `argumentsJSON` to the tool's parameter record, and calls the registry. `@objc` protocols cannot be `async`, hence completion blocks; the Swift side wraps them in `async` continuations so `perform()` stays idiomatic.

Generated intent (what `unisono-codegen` emits for `AddToCart`):

```swift
import AppIntents

@AppIntent(.tool)                       // only when [SiriTool(Tool = true)]
struct AddToCartIntent: AppIntent {
    static let title: LocalizedStringResource = "Add to Cart"
    static let description = IntentDescription("Adds a product to the shopping cart.")
    static let openAppWhenRun = false

    @Parameter(title: "Item", description: "The product to add")
    var item: ProductEntity
    @Parameter(title: "Quantity", description: "How many to add, 1-99", default: 1)
    var quantity: Int

    func perform() async throws -> some IntentResult & ReturnsValue<CartLineEntity> & ProvidesDialog {
        guard let host = UnisonoBridge.shared.host else { throw UnisonoError.appNotReady }
        let args = AddToCartArgs(item: item.id, quantity: quantity)     // Codable, generated
        let json = try await host.invokeAsync("add_to_cart", encode(args))
        let line = try decode(CartLineEntity.self, json)
        return .result(value: line, dialog: IntentDialog(full: line.dialog, supporting: line.title))
    }
}

struct ProductEntity: AppEntity {                      // from [AppEntity] record
    static let typeDisplayRepresentation: TypeDisplayRepresentation = "Product"
    static let defaultQuery = ProductEntityQuery()
    var id: String
    @Property(title: "Title", description: "Product name") var title: String
    var displayRepresentation: DisplayRepresentation { .init(title: "\(title)") }
}

struct ProductEntityQuery: EntityStringQuery {         // calls host.entities/search/suggested
    func entities(for identifiers: [String]) async throws -> [ProductEntity] { … }
    func entities(matching string: String) async throws -> IntentItemCollection<ProductEntity> { … }
    func suggestedEntities() async throws -> IntentItemCollection<ProductEntity> { … }
}

struct UnisonoShortcuts: AppShortcutsProvider {       // from Phrases
    static var appShortcuts: [AppShortcut] {
        AppShortcut(intent: AddToCartIntent(), phrases: ["Add to my cart in \(.applicationName)"],
                    shortTitle: "Add to Cart", systemImageName: "cart")
    }
}
```

## 6. Type mapping (one table drives both emitters)

| C# | JSON Schema (MCP) | Swift `@Parameter` / property |
|---|---|---|
| `string` | `string` | `String` |
| `int`, `long` | `integer` | `Int` |
| `double`, `decimal` | `number` | `Double` |
| `bool` | `boolean` | `Bool` |
| `DateTime`, `DateTimeOffset` | `string` `format: date-time` | `Date` |
| `TimeSpan` | `string` (ISO 8601 duration) | `Duration` (iOS 17+) |
| `enum` | `enum` of names | `AppEnum` with `caseDisplayRepresentations` |
| record/class, **input only** | `object` | `@AppEntity(.generable) struct: TransientAppEntity` — Siri can fill it from the request |
| record with `[AppEntity]` + `IEntityQuery<T>` | `object` (`id` + properties) | `AppEntity` + `EntityStringQuery` that calls back into C# |
| `T[]`, `List<T>` | `array` | `[T]` |
| `T?` | optional / nullable | `T?` |
| `Uri` | `string` `format: uri` | `URL` |
| Dictionaries, polymorphic bases, nested generics | allowed for MCP | **analyzer error** when `[SiriTool]` is present |

Results: MCP gets `structuredContent` (JSON) plus a text block built from `Dialog`; Siri gets `ReturnsValue<Entity>` plus `ProvidesDialog` from the same `Dialog` template. One formatter, two outputs.

## 7. Build pipeline (the risky part)

```
dotnet build  (net10.0-ios / net10.0-maccatalyst)
 ├─ 1. unisono-codegen  (BeforeTargets=CoreCompile)
 │     Roslyn syntax walk over the app's *.cs → tool model
 │     → obj/Unisono/Unisono.g.cs      (ToolDescriptor table, typed invokers, no reflection → AOT/trim safe)
 │     → obj/Unisono/Swift/*.swift      (intents, entities, enums, shortcuts)
 │     → obj/Unisono/Skills.skillsbundle (from [AppSkill] markdown files, validated: name regex, ≤1024-char description, ≤10 000-char body)
 ├─ 2. template Xcode project copied to obj/Unisono/Xcode/, generated Swift dropped into Sources/Generated/
 ├─ 3. <XcodeProject> item → _BuildXcodeProjects → xcarchive + xcframework (device + simulator)
 ├─ 4. AfterTargets=_BuildXcodeProjects: pull Metadata.appintents out of the xcarchive   (Redth's ExtractAppIntentsMetadata)
 ├─ 5. normal .NET link of the xcframework (ForceLoad=true, SmartLink=false)
 └─ 6. AfterTargets=_CopyResourcesToBundle: copy Metadata.appintents + Skills.skillsbundle into the .app   (Redth's CopyAppIntentsMetadata; trailing slash on $(AppBundleDir) matters)
```

Why a separate codegen executable instead of a Roslyn source generator: source generators can only add C# to the compilation, they cannot write `.swift` files. One Roslyn-based tool with two emitters keeps Swift and C# generated from the same model and makes the C# side reflection-free (good for MAUI trimming/AOT).

**Open risk:** steps 1–3 inject an `XcodeProject` item into the *app* project at build time, whereas Redth builds it in a separate binding project. The .NET for iOS SDK resolves native references early in the build. This must be prototyped first. **Fallback for the MVP:** a `dotnet unisono generate` CLI (same model as `dotnet ef`) that writes the Xcode project into `Platforms/iOS/Unisono/` and a tiny `Unisono.Intents.csproj` binding project next to the app, both checked in. The developer builds normally; the Swift stays generated-but-committed. Automate later.

## 8. MCP hosting inside the app

* **macOS / Mac Catalyst** is the real use case: Claude Desktop, Claude Code or any MCP client talks to the *running* app at `http://127.0.0.1:<port>/mcp`. Bind loopback only; require a bearer token the app shows in a settings page; write a small `unisono-proxy` stdio→HTTP shim for clients that can only spawn processes.
* **iPhone / iPad:** a LAN-reachable endpoint while the app is in the foreground (developer and testing story), and the in-process path for an in-app agent (Foundation Models or a cloud model via `IChatClient`). Do not promise a background MCP server on iOS; iOS suspends the listener.
* **Windows / Android:** the same tools over MCP, Siri part compiles to nothing. This is a selling point: one annotation set gives Siri on Apple and Copilot/Claude on Windows.
* Implementation: `HttpListener` → for each POST, parse `JsonRpcMessage`, call `transport.HandlePostRequestAsync(message, responseStream)`; for GET (stateful mode) `HandleGetRequestAsync`. `McpServer.Create(transport, options)` with `options.ToolCollection` filled from the registry. ~200 lines; no ASP.NET Core in the app.

## 9. Runtime details that bite

* **Process model:** keep intents **in-app** (`openAppWhenRun = false` still runs inside the app process; the system launches the app in the background if needed). An App Intents *extension* target has no .NET runtime. Wire `UnisonoBridge.shared.host` in `FinishedLaunching` before anything else, and make `invoke` wait on a "services ready" gate so a cold launch from Siri does not race DI.
* **Threads:** `perform()` arrives on a background actor; the bridge must not touch UI; tools that need UI post to the main thread themselves.
* **Entitlements / signing:** `com.apple.developer.siri` in `Entitlements.plist`; wildcard provisioning profiles lack Siri, so a dedicated App ID + profile; simulator builds use `-p:CodesignEntitlements=""`. Intents appear in Shortcuts a few minutes after first launch.
* **Siri AI preview specifics:** test on a physical device in a supported region/language (Apple Intelligence is region-gated; unavailable in the EU on iOS/iPadOS for now). Skills only work on device/TestFlight. Build with Xcode 27.3 beta. Expect API churn until the spring 2027 App Store window.
* **Descriptions are the product.** Siri's and Claude's model both choose tools from `description` text. The analyzer should refuse a `[SiriTool]` without descriptions on the method and every parameter, and lint for units/format hints ("1-99", "IATA code").

## 10. Milestones

| # | Goal | Proof |
|---|---|---|
| M0 (**done 2026-11-10**, branch `m0/ios-prototype`) | Fork Redth's sample; add `Unisono.Mcp` with the HTTP transport and expose its 6 intents as MCP tools through one shared handler table | Verified on the iPhone 17 Pro simulator: `create_task` over MCP shows up in the Shortcuts "List Tasks" intent and the UI; "Create Task" from Shortcuts comes back from `search_tasks` over MCP. Learned: iOS suspends the MCP socket while the app is in the background; the installed iOS workload needs `-p:ValidateXcodeVersion=false` with Xcode 27. |
| M1 | Generic JSON bridge + static binding NuGet | Redth's per-intent protocol deleted; Swift intents call `host.invoke` |
| M2 | `unisono-codegen`: attributes → Swift intents/entities/enums/shortcuts + C# descriptor table | Zero hand-written Swift for a new tool |
| M3 | MSBuild automation (or the `dotnet unisono generate` fallback), analyzers, Skills bundle | `dotnet new maui` + 1 NuGet + 1 attribute = working Siri action |
| M4 | Siri AI: `.tool` trait, generable entities, `Transferable` entities, Siri Evaluations on 27.3 beta | Siri AI chooses the right tool from a free-form request; evaluation script passes repeatedly |

## 11. Sources

* Apple, `ToolIntent` (beta, 27.3): https://developer.apple.com/documentation/appintents/toolintent
* Apple, "Bringing custom actions and content to Siri": https://developer.apple.com/documentation/appintents/bringing-custom-actions-and-content-to-siri
* Apple, "Orchestrating complex actions with app skills": https://developer.apple.com/documentation/appintents/orchestrating-complex-actions-with-app-skills
* Apple Newsroom, WWDC26 developer frameworks (2026-06-08): https://www.apple.com/newsroom/2026/06/apple-aids-app-development-with-new-intelligence-frameworks-and-advanced-tools/
* The Register on the 27.3 Siri developer preview (2026-11-04): https://www.theregister.com/ai-and-ml/2026/11/04/hey-siri-plug-into-my-app-like-you-said-youd-do-way-back-in-2024/5307659
* Siri AI developer preview walkthrough: https://www.aighten.com/en/blog/apple-siri-ai-developer-preview-app-intents-ios-27-3
* Redth, .NET MAUI + App Intents sample: https://github.com/Redth/Maui.Apple.PlatformFeature.Samples/blob/main/AppIntents/README.md
* 9to5Mac, MCP hints in iOS 26.1 beta (2025-09-22): https://9to5mac.com/2025/09/22/macos-tahoe-26-1-beta-1-mcp-integration/
* MCP C# SDK: https://github.com/modelcontextprotocol/csharp-sdk — `ModelContextProtocol.Core` 2.4.0: https://www.nuget.org/packages/ModelContextProtocol.Core — `StreamableHttpServerTransport` source: https://github.com/modelcontextprotocol/csharp-sdk/blob/main/src/ModelContextProtocol.Core/Server/StreamableHttpServerTransport.cs
* swift-dotnet-bindings overview: https://dev.to/wojo/swift-net-bindings-the-objective-sharpie-replacement-for-net-maui-and-ios-88f

## 12. How to describe it (positioning)

**One line:** *Declare an app action once in C#; every assistant and agent on the device can call it: Siri on Apple, Gemini on Android, Copilot on Windows, and any MCP client anywhere.*

**One paragraph:** Unisono is a .NET library plus a build-time generator for MAUI and .NET apps. You annotate ordinary C# methods as tools, with descriptions the way you would for MCP. At build time the generator projects those tools onto each platform's native "app tool" surface (Swift App Intents on Apple, AppFunctions metadata and service on Android, App Actions JSON on Windows) and at runtime hosts the same tools as an MCP server, in-process on desktop and optionally as a cloud endpoint. One registry, one handler per tool, several front doors.

**What it is not:** it is not a Swift or Kotlin interop layer, and it does not try to make Siri or Gemini "run C#". The native layers stay thin and generated; all logic stays in .NET.

## 13. The same idea on Android, Windows and other assistants

The registry-plus-projections model holds everywhere. Every platform has converged on the same shape: *the app declares typed, described functions in static metadata, the OS indexes them, a system LLM calls them.* Only the declaration format and the dispatch mechanism differ.

| Surface | Native declaration | Who calls it | Status (Nov 2026) | How a .NET projection would work |
|---|---|---|---|---|
| **Apple: Siri AI, Shortcuts, Spotlight** | App Intents in Swift; `@AppIntent(.tool)`; compile-time metadata | Siri, Shortcuts, Spotlight, Apple Intelligence | 27.3 developer preview; App Store spring 2027 | Generated Swift + generic `@objc` bridge (sections 5–7). Hardest platform: needs an Xcode build step. |
| **Android: Gemini and OEM assistants** | **AppFunctions**: platform API `android.app.appfunctions` (API 36/37); Jetpack `androidx.appfunctions` 1.0.0-beta01 with KSP generating a service subclass plus an XML asset. Manifest: `<service>` with `BIND_APP_FUNCTION_SERVICE`, intent action `android.app.appfunctions.AppFunctionService`, a `<property>` pointing at the XML. XML root `<appfunctions>` with `<appfunction><id>…</id><enabledByDefault/><schemaName/><schemaVersion/>…`. Optional predefined schemas (Google's analogue of Apple's App Schemas). | Callers holding `EXECUTE_APP_FUNCTIONS`: Gemini, OEM assistants, Google-approved apps. Gemini integration was a private preview as of May 2026; Google widened Gemini task automation to 40+ apps in July 2026 and says more comes with Android 17. | Beta. Verify locally with `adb shell cmd app_function list-app-functions` and `execute-app-function`. | **Easier than iOS.** The metadata is plain XML and the dispatch is a plain Android service. The .NET 10 API 36 binding already contains `Android.App.AppFunctions` (`AppFunctionService`, `AppFunctionManager`, `ExecuteAppFunctionRequest/Response`, `AppFunctionException`). So: the generator writes the XML asset and manifest entries; a C# subclass of `AppFunctionService` deserialises the request's `GenericDocument` parameters to JSON and calls the same registry. No Kotlin, no KSP, no Gradle. Open point: match the exact v2 XML the Jetpack compiler emits for parameter and response types (the `app_functions_schema.xsd` the manifest references) rather than only the platform minimum. |
| **Android legacy: Google Assistant App Actions** | `shortcuts.xml` capabilities bound to Google's built-in intents (`actions.xml` is dead) | Assistant, being replaced by Gemini | Maintained, not strategic | Trivial from C#: generate `shortcuts.xml` and handle the incoming intent. Only useful for the fixed catalogue of built-in intents; not a free-form tool surface. Low priority. |
| **Windows: Copilot, Click to Do, other apps** | **App Actions on Windows**: JSON action definitions (id, description, typed entity inputs/outputs) shipped in the MSIX package and declared in the package manifest; provider via URI activation or COM `IActionProvider` (`Windows.AI.Actions`). Requires package identity. | Windows, Copilot, other apps | Documented (Oct 2026) | Straightforward: generator writes the action JSON and a C# provider class; needs MSIX identity on the MAUI Windows target. Entity types are Windows' own (Document, Photo, Text…) so the mapping is narrower than JSON Schema. |
| **Windows: any agent via MCP** | **MCP on Windows**: On-device Agent Registry (ODR); servers register through MSIX or `odr.exe`; containment, per-agent consent, Intune control; "agent connectors" | Copilot, VS Code/Visual Studio agent mode, Microsoft Agent Framework apps | Prerelease | Our in-app MCP host is exactly what the ODR registers. Add an ODR registration step to the Windows target. This is the one platform where the OS vendor itself unifies "app actions" and "MCP server". |
| **Amazon Alexa+** | **MCP toolkit**: Alexa+ is an MCP *client* to your hosted MCP server over Streamable HTTP (spec 2025-11-25; MCP Apps for UI); add-ons deployed with the Alexa AI CLI | Alexa+ | Available | Cloud-side, not on-device: run the same registry in a server build of `Unisono.Mcp` (ASP.NET Core host) behind HTTPS and OAuth. The tool definitions are identical; only hosting differs. |
| **Samsung Bixby** | LLM-based Bixby since March 2026 ("functions as callable agents"); Perplexity/DeepSeek models reported; legacy capsules/Marketplace | Bixby | No public developer SDK confirmed | Watch. On Galaxy devices the practical path is Gemini plus AppFunctions. |
| **Desktop agents (Claude Desktop/Code, ChatGPT, Cursor, …)** | MCP | — | — | Already covered by the in-app MCP host. |

**Consequences for the design**

1. The package list in §4 gains two cheap projections: `Unisono.AppFunctions` (Android: XML generator + `AppFunctionService` subclass) and `Unisono.WinActions` (Windows: JSON generator + `IActionProvider`), plus an ODR registration helper. Android is the natural **second** platform because it needs no native toolchain step, so it also makes a good first proof that the projection model generalises.
2. The analyzer should define a **portable type subset** (string, int, double, bool, date, enum, flat record, array, optional) that maps cleanly to App Intents, AppFunctions and MCP JSON Schema, and warn when a tool steps outside it on a platform where it is enabled.
3. Both Apple and Google have **schema catalogues** (Apple App Schemas, Android predefined AppFunction schemas). Keep an optional per-platform `[AppleSchema("…")]` / `[AndroidSchema("…")]` hint; do not try to unify the catalogues.
4. The runtime "front door" list becomes: Siri → bridge; Gemini → `AppFunctionService`; Copilot → `IActionProvider`; MCP clients → in-app host (ODR-registered on Windows); Alexa+ and cloud agents → server host. All call `IToolRegistry.InvokeAsync`.

Additional sources: [Android: Add the AppFunctions API](https://developer.android.com/ai/appfunctions/add-appfunctions), [`AppFunctionMetadata` (platform XML format)](https://developer.android.com/reference/android/app/appfunctions/AppFunctionMetadata), [`AppFunctionService` (platform)](https://developer.android.com/reference/android/app/appfunctions/AppFunctionService), [App Actions on Windows overview](https://learn.microsoft.com/en-us/windows/ai/app-actions/), [MCP on Windows overview](https://learn.microsoft.com/en-us/windows/ai/mcp/overview), [Alexa+ MCP toolkit](https://developer.amazon.com/docs/alexaplus/add-ons/mcp-toolkit-overview.html), [Android agent-readiness in 2026 (third-party)](https://dev.to/mr_manushukla/make-your-android-app-agent-ready-in-2026-appfunctions-app-actions-and-geminis-40-app-wave-22a6).

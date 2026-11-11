# Unisono.AppFunctions

Android projection for [Unisono](https://github.com/egarim/unisono): the tools in a `ToolRegistry` become Android
**AppFunctions**, the surface Gemini and other assistants call on API 36+. One `AppFunctionService` subclass dispatches
every function to the same C# handlers that serve MCP (and, on Apple platforms, App Intents).

- `UnisonoAppFunctions.Attach(registry)` once at application start.
- Subclass `UnisonoAppFunctionService` in the app and declare it in the manifest with
  `android.permission.BIND_APP_FUNCTION_SERVICE`, the `android.app.appfunctions.AppFunctionService` intent action and
  the `android.app.appfunctions` property pointing at the XML that lists the functions.
- Parameters arrive as an AppSearch `GenericDocument` and are converted to the tool's JSON arguments; the result goes
  back as a document under `androidAppFunctionReturnValue`.

Verify on a device or emulator with `adb shell cmd app_function list-app-functions` and `execute-app-function`.

Notes: the manifest `<property android:name="android.app.appfunctions" android:value="app_functions.xml"/>` names an
**assets** path (API 36 reads only that form); the v1 XML carries `function_id` and `enabled_by_default` per function and
no parameter schema, so parameters are read by name at run time. When calling from `adb shell`, quote the whole
command so the JSON keeps its quotes on the phone. A top-level `id` key in that JSON becomes the document id, which
`GenericDocumentJson` passes on to a tool parameter named `id`. Verified on a Pixel 7 running Android 17.

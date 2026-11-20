# Uno Pupusas — C# / XAML UI adapter example 🫓

This directory contains the **Uno UI page** (XAML and code-behind) to insert into an Uno Platform WebAssembly application. It is intentionally a drop-in example rather than a full Uno app scaffold: generate a fresh Uno project with the current official `unoapp` template so its SDK pin and WebAssembly bootstrap match your installed Uno version.

1. Install the official Uno templates and create a browser/WebAssembly Uno application. Use the Uno project wizard or `dotnet new unoapp`; choose WebAssembly as the target.
2. Name the project **UnoPupusas**, or adjust the namespace and `x:Class` in the two source files.
3. Replace the generated `MainPage.xaml` and `MainPage.xaml.cs` with the files in this directory.
4. Publish the WebAssembly target with `dotnet publish -f net10.0-browserwasm -c Release -o ./publish`.
5. Deploy the entire `publish/wwwroot` folder to HTTPS, for example `https://static.example.com/uno-pupusas/`.
6. Register `UnoUiProvider` with the matching local published `wwwroot` directory and absolute public asset URL.

The provider wraps the published `index.html` as a `ui://` resource and rewrites its `<base>` to point at your asset host. See [UI adapter guide](../../docs/uno-blazor-ui-providers.md).

**Important:** This is a working *design scaffold*, not a tested Uno build. Host-side MCP Apps delivery, CSP configuration and the JavaScript MCP Apps bridge are separate future changes. The sample calculates prices locally and does not invoke the MCP tool.

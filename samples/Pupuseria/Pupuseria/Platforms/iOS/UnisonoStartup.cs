using Unisono;
using Unisono.AppIntents;
using Unisono.Mcp;

namespace Pupuseria;

/// <summary>Two front doors, one registry: Siri through the generated App Intents, agents through MCP. Shared by iOS and Mac Catalyst.</summary>
public static class UnisonoStartup
{
	public static void Wire()
	{
		var services = IPlatformApplication.Current?.Services;
		try
		{
			if (services?.GetService<ToolRegistry>() is { } registry)
			{
				UnisonoAppIntents.Attach(registry);
				Console.WriteLine($"[Unisono] Siri bridge attached. Tools: {string.Join(", ", registry.Names)}. Entities: {string.Join(", ", registry.EntityTypes)}");
			}
			var host = services?.GetService<UnisonoMcpHttpHost>();
			host?.Start();
			Console.WriteLine($"[Unisono] MCP server at {host?.Url}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[Unisono] ERROR during startup wiring: {ex}");
		}
	}
}

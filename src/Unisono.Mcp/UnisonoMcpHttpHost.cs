using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Unisono.Mcp;

public sealed class UnisonoMcpHttpOptions
{
    /// <summary>TCP port to listen on. Use 0 to pick a free port (reported through <see cref="UnisonoMcpHttpHost.Url"/>).</summary>
    public int Port { get; set; } = 8787;

    /// <summary>Request path of the MCP endpoint.</summary>
    public string Path { get; set; } = "/mcp";

    /// <summary>When true (default) only 127.0.0.1 is bound. False binds every interface, for LAN testing from another machine.</summary>
    public bool LoopbackOnly { get; set; } = true;

    public string ServerName { get; set; } = "unisono";
    public string ServerVersion { get; set; } = "0.1.0";

    /// <summary>Optional instructions returned to clients at initialize; a good place for "call X before Y" hints.</summary>
    public string? Instructions { get; set; }
}

/// <summary>
/// Hosts a <see cref="ToolRegistry"/> as an MCP server over Streamable HTTP, using
/// <see cref="HttpListener"/> so it runs inside a MAUI app without ASP.NET Core.
/// Stateful sessions (initialize + Mcp-Session-Id) are supported, and a POST without a session is
/// served statelessly with a per-request transport, mirroring the SDK's ASP.NET Core handler.
/// </summary>
public sealed class UnisonoMcpHttpHost : IAsyncDisposable
{
    private const string SessionHeader = "Mcp-Session-Id";

    private readonly ToolRegistry _registry;
    private readonly UnisonoMcpHttpOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _log;
    private readonly McpServerPrimitiveCollection<McpServerTool> _tools;
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private int _port;

    public UnisonoMcpHttpHost(ToolRegistry registry, UnisonoMcpHttpOptions? options = null, ILoggerFactory? loggerFactory = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _options = options ?? new UnisonoMcpHttpOptions();
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _log = _loggerFactory.CreateLogger<UnisonoMcpHttpHost>();
        _port = _options.Port;

        _tools = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.Ordinal);
        foreach (var tool in registry.McpTools)
            _tools.Add(tool);
    }

    /// <summary>The URL MCP clients should connect to.</summary>
    public string Url => $"http://127.0.0.1:{_port}{_options.Path}";

    public bool IsRunning => _listener?.IsListening == true;

    public void Start()
    {
        if (_listener is not null)
            return;

        if (_port == 0)
            _port = FindFreePort();

        var listener = new HttpListener();
        var host = _options.LoopbackOnly ? "127.0.0.1" : "*";
        listener.Prefixes.Add($"http://{host}:{_port}/");
        listener.Start();

        _listener = listener;
        _cts = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, _cts.Token));
        _log.LogInformation("Unisono MCP server listening at {Url} with tools: {Tools}", Url, string.Join(", ", _registry.Names));
    }

    public async Task StopAsync()
    {
        var listener = _listener;
        if (listener is null)
            return;

        _listener = null;
        _cts?.Cancel();
        try { listener.Stop(); listener.Close(); } catch { /* shutting down */ }

        Session[] sessions;
        lock (_gate)
        {
            sessions = _sessions.Values.ToArray();
            _sessions.Clear();
        }
        foreach (var session in sessions)
            await session.DisposeAsync().ConfigureAwait(false);

        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.ConfigureAwait(false); } catch { /* cancelled */ }
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (ct.IsCancellationRequested || !listener.IsListening)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Unisono MCP: accept failed");
                continue;
            }

            _ = Task.Run(() => HandleAsync(context, ct), ct);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken ct)
    {
        var request = context.Request;
        var response = context.Response;
        try
        {
            var path = (request.Url?.AbsolutePath ?? "/").TrimEnd('/');
            if (!string.Equals(path, _options.Path.TrimEnd('/'), StringComparison.Ordinal))
            {
                await WriteTextAsync(response, 404, "text/plain", "Not found. MCP endpoint is " + _options.Path).ConfigureAwait(false);
                return;
            }

            switch (request.HttpMethod)
            {
                case "POST":
                    await HandlePostAsync(request, response, ct).ConfigureAwait(false);
                    break;
                case "GET":
                    await HandleGetAsync(request, response, ct).ConfigureAwait(false);
                    break;
                case "DELETE":
                    await HandleDeleteAsync(request, response).ConfigureAwait(false);
                    break;
                default:
                    response.AddHeader("Allow", "GET, POST, DELETE");
                    await WriteTextAsync(response, 405, "text/plain", "Method not allowed").ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Unisono MCP: request failed");
            try { await WriteErrorAsync(response, 500, -32603, ex.Message).ConfigureAwait(false); } catch { /* response already started */ }
        }
        finally
        {
            try { response.Close(); } catch { /* already closed */ }
        }
    }

    private async Task HandlePostAsync(HttpListenerRequest request, HttpListenerResponse response, CancellationToken ct)
    {
        JsonRpcMessage? message;
        try
        {
            message = await JsonSerializer.DeserializeAsync<JsonRpcMessage>(request.InputStream, McpJsonUtilities.DefaultOptions, ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            await WriteErrorAsync(response, 400, -32700, "Parse error: " + ex.Message).ConfigureAwait(false);
            return;
        }

        if (message is null)
        {
            await WriteErrorAsync(response, 400, -32600, "Empty request body").ConfigureAwait(false);
            return;
        }

        var sessionId = request.Headers[SessionHeader];
        Session? session;
        var ephemeral = false;

        if (!string.IsNullOrEmpty(sessionId))
        {
            if (!TryGetSession(sessionId, out session))
            {
                await WriteErrorAsync(response, 404, -32001, "Session not found").ConfigureAwait(false);
                return;
            }
        }
        else if (message is JsonRpcRequest { Method: "initialize" })
        {
            session = CreateSession(stateless: false);
            response.AddHeader(SessionHeader, session.Id);
        }
        else
        {
            // No session and not an initialize request: serve it with a throw-away stateless session.
            session = CreateSession(stateless: true);
            ephemeral = true;
        }

        try
        {
            PrepareEventStream(response);
            var wroteResponse = await session.Transport.HandlePostRequestAsync(message, response.OutputStream, ct).ConfigureAwait(false);
            if (!wroteResponse)
            {
                // Notifications and client responses get 202 Accepted with no body.
                response.ContentType = null;
                response.SendChunked = false;
                response.StatusCode = 202;
            }
        }
        finally
        {
            if (ephemeral)
                await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task HandleGetAsync(HttpListenerRequest request, HttpListenerResponse response, CancellationToken ct)
    {
        var sessionId = request.Headers[SessionHeader];
        if (string.IsNullOrEmpty(sessionId) || !TryGetSession(sessionId, out var session))
        {
            await WriteErrorAsync(response, 404, -32001, "Session not found").ConfigureAwait(false);
            return;
        }

        PrepareEventStream(response);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, session.Closed.Token);
        await session.Transport.HandleGetRequestAsync(response.OutputStream, linked.Token).ConfigureAwait(false);
    }

    private async Task HandleDeleteAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var sessionId = request.Headers[SessionHeader];
        Session? session = null;
        if (!string.IsNullOrEmpty(sessionId))
        {
            lock (_gate)
            {
                if (_sessions.Remove(sessionId, out session))
                    _log.LogInformation("Unisono MCP: session {SessionId} ended by client", sessionId);
            }
        }

        if (session is not null)
            await session.DisposeAsync().ConfigureAwait(false);

        response.StatusCode = 200;
        response.ContentLength64 = 0;
    }

    private Session CreateSession(bool stateless)
    {
        var id = stateless ? string.Empty : NewSessionId();
        var transport = new StreamableHttpServerTransport(_loggerFactory)
        {
            SessionId = stateless ? null : id,
            Stateless = stateless,
        };

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation { Name = _options.ServerName, Version = _options.ServerVersion },
            ServerInstructions = _options.Instructions,
            ToolCollection = _tools,
        };

        var server = McpServer.Create(transport, options, _loggerFactory, null);
        var session = new Session(id, transport, server);

        session.RunTask = Task.Run(async () =>
        {
            try
            {
                await server.RunAsync(session.Closed.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Unisono MCP: session {SessionId} ended with an error", id);
            }
            finally
            {
                if (!stateless)
                {
                    lock (_gate) _sessions.Remove(id);
                }
            }
        });

        if (!stateless)
        {
            lock (_gate) _sessions[id] = session;
            _log.LogInformation("Unisono MCP: new session {SessionId}", id);
        }

        return session;
    }

    private bool TryGetSession(string id, out Session session)
    {
        lock (_gate)
        {
            return _sessions.TryGetValue(id, out session!);
        }
    }

    private static void PrepareEventStream(HttpListenerResponse response)
    {
        response.StatusCode = 200;
        response.ContentType = "text/event-stream";
        response.AddHeader("Cache-Control", "no-cache,no-store");
        response.SendChunked = true;
    }

    private static Task WriteErrorAsync(HttpListenerResponse response, int status, int code, string message)
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = null,
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        };
        return WriteTextAsync(response, status, "application/json", body.ToJsonString());
    }

    private static async Task WriteTextAsync(HttpListenerResponse response, int status, string contentType, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static string NewSessionId()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static int FindFreePort()
    {
        var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return port;
    }

    private sealed class Session(string id, StreamableHttpServerTransport transport, McpServer server) : IAsyncDisposable
    {
        public string Id { get; } = id;
        public StreamableHttpServerTransport Transport { get; } = transport;
        public McpServer Server { get; } = server;
        public Task? RunTask { get; set; }
        public CancellationTokenSource Closed { get; } = new();

        public async ValueTask DisposeAsync()
        {
            Closed.Cancel();
            try { await Transport.DisposeAsync().ConfigureAwait(false); } catch { }
            try { await Server.DisposeAsync().ConfigureAwait(false); } catch { }
            if (RunTask is not null)
            {
                try { await RunTask.ConfigureAwait(false); } catch { }
            }
            Closed.Dispose();
        }
    }
}

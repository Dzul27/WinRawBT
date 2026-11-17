using System.Net;
using System.Net.WebSockets;
using System.Text;
using WinRawBT.Helpers;

namespace WinRawBT.Services;

/// <summary>
/// Local HTTP + WebSocket print bridge bound to http://localhost:{port}/ (TASK-08/09/10).
/// Decodes payloads and forwards them to <see cref="BluetoothPrinterService"/>.
/// </summary>
public sealed class PrintBridgeServer : IDisposable
{
    private readonly BluetoothPrinterService _printer;
    private readonly Action<string> _log;

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public PrintBridgeServer(BluetoothPrinterService printer, Action<string>? logger = null)
    {
        _printer = printer;
        _log = logger ?? (_ => { });
    }

    public bool IsRunning { get; private set; }
    public int Port { get; private set; }

    /// <summary>
    /// The MAC of the printer that print jobs are routed to. Set by the UI before Start.
    /// </summary>
    public string? TargetMac { get; set; }

    /// <summary>
    /// When true, ESC @ (1B 40, initialize printer) is prepended to every job whose decoded payload does not
    /// already start with it. Default off (payloads are sent exactly as the POS made them); set via
    /// <c>PrependInit</c> in config.json.
    /// </summary>
    public bool PrependInit { get; set; }

    /// <summary>
    /// Browser origins (e.g. <c>https://pos.example.com</c>) that may talk to the bridge. A request that carries an
    /// <c>Origin</c> header not listed here is rejected with 403 before anything is printed. Requests without an
    /// <c>Origin</c> header (curl, native apps) are always accepted. Set via <c>AllowedOrigins</c> in config.json.
    /// </summary>
    public IReadOnlyList<string> AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>
    /// When true, every origin is accepted (<c>Access-Control-Allow-Origin: *</c>). Any website open in a browser on
    /// this PC can then print. Set via <c>AllowAnyOrigin</c> in config.json.
    /// </summary>
    public bool AllowAnyOrigin { get; set; }

    private bool IsOriginAllowed(string? origin)
    {
        if (string.IsNullOrEmpty(origin) || AllowAnyOrigin)
        {
            return true;
        }

        string normalized = origin!.TrimEnd('/');
        foreach (string allowed in AllowedOrigins)
        {
            if (string.Equals(allowed.Trim().TrimEnd('/'), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Bytes of each decoded job shown as hex + ASCII in the Activity Log.</summary>
    private const int PreviewBytes = 48;

    private int _jobCounter;

    /// <summary>
    /// Start the listener. Throws <see cref="HttpListenerException"/> (e.g. port in use)
    /// so the caller can surface a modal error (edge-case matrix).
    /// </summary>
    public void Start(int port)
    {
        if (IsRunning)
        {
            return;
        }

        Port = port;
        _listener = new HttpListener();
        // Bind strictly to localhost (NFR-2).
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();

        _cts = new CancellationTokenSource();
        IsRunning = true;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));

        _log($"[INFO] Service started on http://localhost:{port}/");
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _listener?.Stop(); } catch { /* ignore */ }
        try { _listener?.Close(); } catch { /* ignore */ }

        _listener = null;
        _log("[INFO] Service stopped.");
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (IsRunning && _listener != null && !token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (token.IsCancellationRequested || !IsRunning)
            {
                break; // normal shutdown
            }
            catch (Exception ex)
            {
                _log($"[WARN] Listener error: {ex.Message}");
                continue;
            }

            // Fire-and-forget per connection; jobs still serialize in the Bluetooth layer.
            _ = Task.Run(() => HandleContextAsync(context, token));
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context, CancellationToken token)
    {
        try
        {
            string? origin = context.Request.Headers["Origin"];
            if (!IsOriginAllowed(origin))
            {
                _log($"[WARN] Rejected request from origin \"{origin}\" (not in AllowedOrigins). " +
                     "Add it to \"AllowedOrigins\" in config.json and restart the service, or set \"AllowAnyOrigin\": true.");
                await WriteJsonAsync(context.Response, 403, ErrorJson("Origin not allowed")).ConfigureAwait(false);
                return;
            }

            if (context.Request.IsWebSocketRequest)
            {
                await HandleWebSocketAsync(context, token).ConfigureAwait(false);
                return;
            }

            AddCorsHeaders(context.Request, context.Response);

            string method = context.Request.HttpMethod.ToUpperInvariant();
            switch (method)
            {
                case "OPTIONS":
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    break;

                case "GET":
                    await WriteJsonAsync(context.Response, 200,
                        "{\"status\":\"OK\",\"service\":\"WinRawBT\",\"port\":" + Port + ",\"running\":true}")
                        .ConfigureAwait(false);
                    break;

                case "POST":
                    await HandlePostAsync(context).ConfigureAwait(false);
                    break;

                default:
                    await WriteJsonAsync(context.Response, 405,
                        ErrorJson("Method not allowed"))
                        .ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log($"[ERROR] Request handling failed: {ex.Message}");
            try
            {
                await WriteJsonAsync(context.Response, 500,
                    ErrorJson(ex.Message)).ConfigureAwait(false);
            }
            catch { /* response already closed */ }
        }
    }

    // TASK-09: POST / print endpoint
    private async Task HandlePostAsync(HttpListenerContext context)
    {
        byte[] body;
        using (var ms = new MemoryStream())
        {
            await context.Request.InputStream.CopyToAsync(ms).ConfigureAwait(false);
            body = ms.ToArray();
        }

        HttpListenerRequest request = context.Request;
        string source = $"HTTP POST {request.Url?.AbsolutePath ?? "/"} from {request.RemoteEndPoint}" +
                        DescribeClient(request);

        var outcome = await ProcessJobAsync(source, request.ContentType ?? "(none)", request.ContentType, body)
            .ConfigureAwait(false);
        await WriteJsonAsync(context.Response, outcome.Status, outcome.Json).ConfigureAwait(false);
    }

    /// <summary>
    /// Decode one print job (HTTP body or WebSocket message), log what arrived and how it was decoded
    /// (never the whole payload), and send it to the printer. Returns the HTTP status and JSON reply.
    /// </summary>
    private async Task<(int Status, string Json)> ProcessJobAsync(
        string source, string contentTypeLabel, string? decoderContentType, byte[] body)
    {
        int job = Interlocked.Increment(ref _jobCounter);
        string tag = $"[INFO] Job #{job}";

        DecodeResult decoded = PayloadDecoder.Decode(body, decoderContentType);
        byte[] payload = decoded.Bytes;

        _log($"{tag} {source} | Content-Type: {contentTypeLabel} | body {body.Length} B -> {payload.Length} B | decode: {decoded.Path}");
        if (decoded.Transformed || decoded.Error != null || payload.Length == 0)
        {
            _log($"{tag} body as received: {PayloadDecoder.FormatBodyHead(body)}");
        }

        if (decoded.Error != null)
        {
            _log($"[WARN] Job #{job} rejected: {decoded.Error}");
            return (400, ErrorJson(decoded.Error));
        }

        if (payload.Length == 0)
        {
            _log($"[WARN] Job #{job} rejected: empty payload.");
            return (400, ErrorJson("Empty payload or invalid Base64"));
        }

        _log($"{tag} decoded first {Math.Min(payload.Length, PreviewBytes)} of {payload.Length} B (hex | ascii): " +
             PayloadDecoder.FormatPreview(payload, PreviewBytes));
        _log($"{tag} {PayloadDecoder.SummarizeEscPos(payload)}");

        if (PrependInit && !PayloadDecoder.StartsWithInit(payload))
        {
            var withInit = new byte[payload.Length + 2];
            withInit[0] = 0x1B;
            withInit[1] = 0x40;
            Buffer.BlockCopy(payload, 0, withInit, 2, payload.Length);
            payload = withInit;
            _log($"{tag} PrependInit is ON and the payload did not start with ESC @: prepended 1B 40 ({payload.Length} B now).");
        }

        bool ok = await SendToPrinterAsync(payload).ConfigureAwait(false);
        return ok
            ? (200, OkJson(payload.Length))
            : (500, ErrorJson("Bluetooth connection failed"));
    }

    /// <summary>" (User-Agent: ..., Origin: ...)" of the caller, to tell which POS app is sending.</summary>
    private static string DescribeClient(HttpListenerRequest request)
    {
        string? agent = request.UserAgent;
        string? origin = request.Headers["Origin"];
        if (string.IsNullOrEmpty(agent) && string.IsNullOrEmpty(origin))
        {
            return string.Empty;
        }

        if (agent != null && agent.Length > 60)
        {
            agent = agent.Substring(0, 60) + "...";
        }

        return $" (User-Agent: {agent ?? "-"}, Origin: {origin ?? "-"})";
    }

    // TASK-10: WebSocket upgrade handler
    private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken token)
    {
        HttpListenerWebSocketContext wsContext;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log($"[ERROR] WebSocket upgrade failed: {ex.Message}");
            context.Response.StatusCode = 500;
            context.Response.Close();
            return;
        }

        WebSocket socket = wsContext.WebSocket;
        _log("[INFO] WebSocket client connected.");

        var buffer = new byte[8192];
        try
        {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                using var frame = new MemoryStream();
                WebSocketReceiveResult result;
                WebSocketMessageType messageType;
                do
                {
                    result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), token).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            "bye", CancellationToken.None).ConfigureAwait(false);
                        _log("[INFO] WebSocket client disconnected.");
                        return;
                    }

                    frame.Write(buffer, 0, result.Count);
                    messageType = result.MessageType;
                }
                while (!result.EndOfMessage);

                byte[] raw = frame.ToArray();
                if (raw.Length == 0)
                {
                    continue;
                }

                // Text frames go through the full decoder (Base64, RawBT intent URL, JSON ...); binary frames are raw bytes.
                bool isText = messageType == WebSocketMessageType.Text;
                var outcome = await ProcessJobAsync(
                    $"WS {(isText ? "text" : "binary")} frame from {context.Request.RemoteEndPoint}" +
                    DescribeClient(context.Request),
                    isText ? "n/a (WS text frame)" : "n/a (WS binary frame)",
                    isText ? "text/plain" : "application/octet-stream",
                    raw).ConfigureAwait(false);

                await SendWsResponseAsync(socket, outcome.Json, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            _log($"[WARN] WebSocket error: {ex.Message}");
        }
        finally
        {
            try { socket.Dispose(); } catch { /* ignore */ }
        }
    }

    private async Task<bool> SendToPrinterAsync(byte[] payload)
    {
        if (string.IsNullOrWhiteSpace(TargetMac))
        {
            _log("[ERROR] No target printer selected.");
            return false;
        }

        return await _printer.SendRawBytesAsync(TargetMac!, payload).ConfigureAwait(false);
    }

    private static string OkJson(int bytesSent) =>
        "{\"status\":\"OK\",\"bytesSent\":" + bytesSent + "}";

    private static string ErrorJson(string message) =>
        "{\"status\":\"ERROR\",\"message\":\"" + EscapeJson(message) + "\"}";

    private static string EscapeJson(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }

        return sb.ToString();
    }

    private void AddCorsHeaders(HttpListenerRequest request, HttpListenerResponse response)
    {
        string? origin = request.Headers["Origin"];
        if (AllowAnyOrigin)
        {
            response.Headers["Access-Control-Allow-Origin"] = "*";
        }
        else if (!string.IsNullOrEmpty(origin))
        {
            response.Headers["Access-Control-Allow-Origin"] = origin;
            response.Headers["Vary"] = "Origin";
        }

        response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "*";
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, string body)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        response.OutputStream.Close();
        response.Close();
    }

    private static async Task SendWsResponseAsync(WebSocket socket, string body, CancellationToken token)
    {
        if (socket.State != WebSocketState.Open)
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(body);
        await socket.SendAsync(new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text, true, token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

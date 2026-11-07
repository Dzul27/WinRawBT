using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InTheHand.Net;
using WinRawBT.Helpers;

namespace WinRawBT.Services;

/// <summary>
/// Local Print Bridge Server - HTTP and WebSocket listener (TASK-08, TASK-09, TASK-10)
/// </summary>
public class PrintBridgeServer
{
    private readonly BluetoothPrinterService _printerService;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationToken;
    private bool _isRunning;
    private int _port;
    private BluetoothAddress? _selectedPrinterAddress;

    public event EventHandler<LogEventArgs>? LogMessage;
    public event EventHandler<PrintJobEventArgs>? PrintJobCompleted;

    public bool IsRunning => _isRunning;
    public int Port => _port;

    public PrintBridgeServer(BluetoothPrinterService printerService)
    {
        _printerService = printerService;
    }

    /// <summary>
    /// Set the selected printer for print jobs
    /// </summary>
    public void SetSelectedPrinter(BluetoothAddress address)
    {
        _selectedPrinterAddress = address;
    }

    /// <summary>
    /// Start the HTTP listener (TASK-08)
    /// </summary>
    public bool Start(int port)
    {
        if (_isRunning)
        {
            Log("Server is already running");
            return false;
        }

        try
        {
            _port = port;
            _cancellationToken = new CancellationTokenSource();
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{port}/");

            _httpListener.Start();
            _isRunning = true;

            Log($"Service started on http://localhost:{port}/");

            // Start processing requests in background
            _ = Task.Run(() => ProcessRequestsAsync(_cancellationToken.Token));

            return true;
        }
        catch (HttpListenerException ex)
        {
            Log($"Failed to start server: {ex.Message}");
            Log($"Port {port} may already be in use or requires elevation");
            return false;
        }
        catch (Exception ex)
        {
            Log($"Failed to start server: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Stop the HTTP listener (TASK-08)
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        try
        {
            _cancellationToken?.Cancel();
            _httpListener?.Stop();
            _httpListener?.Close();
            _isRunning = false;

            Log("Service stopped");
        }
        catch (Exception ex)
        {
            Log($"Error stopping server: {ex.Message}");
        }
    }

    /// <summary>
    /// Process incoming HTTP and WebSocket requests
    /// </summary>
    private async Task ProcessRequestsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _httpListener != null)
        {
            try
            {
                var context = await _httpListener.GetContextAsync().ConfigureAwait(false);

                // Handle WebSocket upgrade (TASK-10)
                if (context.Request.IsWebSocketRequest)
                {
                    _ = HandleWebSocketAsync(context, token);
                }
                else
                {
                    // Handle HTTP requests
                    _ = HandleHttpRequestAsync(context, token);
                }
            }
            catch (HttpListenerException)
            {
                // Expected when stopping the listener
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Error processing request: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Handle HTTP requests (TASK-09)
    /// </summary>
    private async Task HandleHttpRequestAsync(HttpListenerContext context, CancellationToken token)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Add CORS headers (TASK-08)
            AddCorsHeaders(response);

            // Handle OPTIONS preflight (TASK-08)
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            // Only accept POST requests on /
            if (request.HttpMethod != "POST" || request.Url?.AbsolutePath != "/")
            {
                await SendJsonResponseAsync(response, 405, new
                {
                    status = "ERROR",
                    message = "Method not allowed. Use POST /"
                });
                return;
            }

            // Check if printer is selected
            if (_selectedPrinterAddress == null)
            {
                await SendJsonResponseAsync(response, 500, new
                {
                    status = "ERROR",
                    message = "No printer selected"
                });
                return;
            }

            // Read request body
            byte[] rawBody;
            using (var ms = new System.IO.MemoryStream())
            {
                await request.InputStream.CopyToAsync(ms, token);
                rawBody = ms.ToArray();
            }

            // Decode payload (TASK-07)
            byte[] printData;
            try
            {
                printData = PayloadDecoder.DecodePayload(rawBody, request.ContentType);
            }
            catch (Exception ex)
            {
                await SendJsonResponseAsync(response, 400, new
                {
                    status = "ERROR",
                    message = $"Invalid payload: {ex.Message}"
                });
                return;
            }

            Log($"Incoming print job ({printData.Length} bytes)");

            // Send to printer (TASK-09)
            try
            {
                await _printerService.SendRawBytesAsync(_selectedPrinterAddress, printData);

                Log($"Print job completed successfully");

                PrintJobCompleted?.Invoke(this, new PrintJobEventArgs
                {
                    BytesSent = printData.Length,
                    Success = true
                });

                await SendJsonResponseAsync(response, 200, new
                {
                    status = "OK",
                    bytesSent = printData.Length
                });
            }
            catch (Exception ex)
            {
                Log($"Print job failed: {ex.Message}");

                PrintJobCompleted?.Invoke(this, new PrintJobEventArgs
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });

                await SendJsonResponseAsync(response, 500, new
                {
                    status = "ERROR",
                    message = ex.Message
                });
            }
        }
        catch (Exception ex)
        {
            Log($"Error handling HTTP request: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle WebSocket connections (TASK-10)
    /// </summary>
    private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken token)
    {
        try
        {
            var wsContext = await context.AcceptWebSocketAsync(null);
            var webSocket = wsContext.WebSocket;

            Log("WebSocket client connected");

            var buffer = new byte[8192];

            while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", token);
                    Log("WebSocket client disconnected");
                    break;
                }

                // Check if printer is selected
                if (_selectedPrinterAddress == null)
                {
                    Log("WebSocket print failed: No printer selected");
                    continue;
                }

                // Extract the received data
                byte[] payload = new byte[result.Count];
                Array.Copy(buffer, payload, result.Count);

                // Decode payload if it's text (Base64)
                byte[] printData;
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(payload);
                    printData = PayloadDecoder.DecodePayload(payload, "text/plain");
                }
                else
                {
                    // Binary data - use as-is
                    printData = payload;
                }

                Log($"WebSocket print job ({printData.Length} bytes)");

                // Send to printer
                try
                {
                    await _printerService.SendRawBytesAsync(_selectedPrinterAddress, printData);
                    Log($"WebSocket print job completed successfully");
                }
                catch (Exception ex)
                {
                    Log($"WebSocket print job failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"WebSocket error: {ex.Message}");
        }
    }

    /// <summary>
    /// Add CORS headers to response (TASK-08)
    /// </summary>
    private void AddCorsHeaders(HttpListenerResponse response)
    {
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "*");
        response.ContentType = "application/json";
    }

    /// <summary>
    /// Send JSON response
    /// </summary>
    private async Task SendJsonResponseAsync(HttpListenerResponse response, int statusCode, object data)
    {
        response.StatusCode = statusCode;
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        response.Close();
    }

    /// <summary>
    /// Log message event
    /// </summary>
    private void Log(string message)
    {
        LogMessage?.Invoke(this, new LogEventArgs { Message = message });
    }
}

public class LogEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
}

public class PrintJobEventArgs : EventArgs
{
    public int BytesSent { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

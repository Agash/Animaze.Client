using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Animaze.Client;

/// <summary>
/// Client for the Animaze API: a WebSocket on which each request names an <c>action</c> and carries an
/// <c>id</c> the reply echoes, and on which Animaze pushes events to every connected client. The API is
/// off until enabled in Animaze under Settings, Animaze API.
/// </summary>
public sealed partial class AnimazeClient : IAsyncDisposable
{
    private readonly AnimazeClientOptions _options;
    private readonly ILogger<AnimazeClient> _logger;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new(
        StringComparer.Ordinal
    );
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _receiveStop;
    private Task? _receiveLoop;
    private long _nextId;
    private bool _disposed;

    /// <summary>A client; call <see cref="ConnectAsync"/> to open it.</summary>
    /// <param name="options">Endpoint and timing.</param>
    /// <param name="logger">Where the client logs.</param>
    public AnimazeClient(
        AnimazeClientOptions? options = null,
        ILogger<AnimazeClient>? logger = null
    )
    {
        _options = options ?? new AnimazeClientOptions();
        _logger = logger ?? NullLogger<AnimazeClient>.Instance;
    }

    /// <summary>Whether the socket is open.</summary>
    public bool IsConnected => _socket is { State: WebSocketState.Open };

    /// <summary>Raised when the loaded avatar changes, from any source.</summary>
    public event EventHandler<AvatarChangedEventArgs>? AvatarChanged;

    /// <summary>
    /// Raised when ChatPal starts speaking. Animaze's timing depends on the voice engine and can be up to a second
    /// early.
    /// </summary>
    public event EventHandler? ChatPalSpeechStarted;

    /// <summary>Raised when ChatPal stops speaking; up to a second late, as <see cref="ChatPalSpeechStarted"/>.</summary>
    public event EventHandler? ChatPalSpeechEnded;

    /// <summary>Raised for every event Animaze pushes, including ones this client has no typed event for.</summary>
    public event EventHandler<AnimazeEventArgs>? EventReceived;

    /// <summary>Raised when the connection ends, whoever ended it.</summary>
    public event EventHandler? Disconnected;

    /// <summary>Opens the connection.</summary>
    /// <param name="ct">Cancels the connect.</param>
    /// <exception cref="AnimazeException">Animaze is not running, or its API is not enabled.</exception>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsConnected)
        {
            return;
        }

        ClientWebSocket socket = new();
        try
        {
            await socket.ConnectAsync(_options.Endpoint, ct).ConfigureAwait(false);
        }
        catch (WebSocketException ex)
        {
            socket.Dispose();
            throw new AnimazeException(
                $"Could not connect to Animaze at {_options.Endpoint}; is its API enabled?",
                ex
            );
        }

        _socket = socket;
        _receiveStop = new CancellationTokenSource();
        _receiveLoop = Task.Run(
            () => ReceiveAsync(socket, _receiveStop.Token),
            CancellationToken.None
        );
        LogConnected(_logger, _options.Endpoint);
    }

    /// <summary>Closes the connection.</summary>
    /// <param name="ct">Cancels the close handshake.</param>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        ClientWebSocket? socket = Interlocked.Exchange(ref _socket, null);
        if (socket is null)
        {
            return;
        }

        if (socket.State is WebSocketState.Open)
        {
            // Only the client's half of the close: a server that never answers it must not hold the caller,
            // so the read loop is stopped below rather than waited on for the reply (RFC 6455 section 7.1.1
            // lets the client close the connection once it has sent its Close frame).
            using CancellationTokenSource closeTimeout =
                CancellationTokenSource.CreateLinkedTokenSource(ct);
            closeTimeout.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                await socket
                    .CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "closing",
                        closeTimeout.Token
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
            {
                LogCloseFailed(_logger, ex);
            }
        }

        if (_receiveStop is not null)
        {
            await _receiveStop.CancelAsync().ConfigureAwait(false);
        }

        if (_receiveLoop is not null)
        {
            await _receiveLoop.ConfigureAwait(false);
        }

        _receiveStop?.Dispose();
        _receiveStop = null;
        _receiveLoop = null;
        socket.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        _sendLock.Dispose();
    }

    /// <summary>Sends any action and returns Animaze's reply as it came.</summary>
    /// <param name="action">The action name, for example <c>LoadAvatar</c>.</param>
    /// <param name="arguments">The action's fields, or null for none.</param>
    /// <param name="ct">Cancels the wait for the reply.</param>
    /// <returns>The reply, without its <c>action</c> and <c>id</c>.</returns>
    /// <exception cref="AnimazeException">Animaze answered with an error, or did not answer in time.</exception>
    public async Task<JsonElement> SendAsync(
        string action,
        JsonObject? arguments = null,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ObjectDisposedException.ThrowIf(_disposed, this);
        ClientWebSocket socket = _socket is { State: WebSocketState.Open } open
            ? open
            : throw new AnimazeException("The Animaze client is not connected.");

        string id = Interlocked.Increment(ref _nextId).ToString(CultureInfo.InvariantCulture);
        JsonObject request = arguments?.DeepClone().AsObject() ?? [];
        request["action"] = action;
        request["id"] = id;
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            request,
            AnimazeJsonContext.Default.JsonObject
        );

        TaskCompletionSource<JsonElement> reply = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _pending[id] = reply;
        try
        {
            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await socket
                    .SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, ct)
                    .ConfigureAwait(false);
            }
            finally
            {
                _ = _sendLock.Release();
            }

            JsonElement answer;
            try
            {
                answer = await reply
                    .Task.WaitAsync(_options.RequestTimeout, _options.TimeProvider, ct)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                throw new AnimazeException(
                    $"Animaze did not answer '{action}' within {_options.RequestTimeout.TotalSeconds:0.#} s.",
                    ex
                );
            }

            return
                answer.TryGetProperty("error", out JsonElement error)
                && error.ValueKind is JsonValueKind.String
                ? throw new AnimazeException($"Animaze refused '{action}': {error.GetString()}")
                {
                    Action = action,
                }
                : answer;
        }
        finally
        {
            _ = _pending.TryRemove(id, out _);
        }
    }

    private async Task ReceiveAsync(ClientWebSocket socket, CancellationToken ct)
    {
        byte[] buffer = new byte[_options.ReceiveBufferSize];
        using MemoryStream message = new();
        try
        {
            while (!ct.IsCancellationRequested && socket.State is WebSocketState.Open)
            {
                ValueWebSocketReceiveResult received = await socket
                    .ReceiveAsync(buffer.AsMemory(), ct)
                    .ConfigureAwait(false);
                if (received.MessageType is WebSocketMessageType.Close)
                {
                    break;
                }

                message.Write(buffer, 0, received.Count);
                if (!received.EndOfMessage)
                {
                    continue;
                }

                Dispatch(message.GetBuffer().AsMemory(0, (int)message.Length));
                message.SetLength(0);
            }
        }
        catch (OperationCanceledException)
        {
            // Deliberately not logged: DisconnectAsync stopped the loop.
        }
        catch (WebSocketException ex)
        {
            LogConnectionLost(_logger, ex);
        }
        finally
        {
            foreach (TaskCompletionSource<JsonElement> waiting in _pending.Values)
            {
                _ = waiting.TrySetException(
                    new AnimazeException("The connection to Animaze closed before it answered.")
                );
            }

            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Dispatch(ReadOnlyMemory<byte> utf8)
    {
        JsonElement root;
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8);
            root = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            LogUnreadable(_logger, ex);
            return;
        }

        if (root.ValueKind is not JsonValueKind.Object)
        {
            return;
        }

        if (
            root.TryGetProperty("event", out JsonElement name)
            && name.ValueKind is JsonValueKind.String
        )
        {
            string eventName = name.GetString()!;
            EventReceived?.Invoke(this, new AnimazeEventArgs(eventName, root));
            switch (eventName)
            {
                case "AvatarChanged":
                    AvatarChanged?.Invoke(
                        this,
                        new AvatarChangedEventArgs(Read.String(root, "new_avatar") ?? string.Empty)
                    );
                    break;
                case "ChatbotSpeechStarted":
                    ChatPalSpeechStarted?.Invoke(this, EventArgs.Empty);
                    break;
                case "ChatbotSpeechEnded":
                    ChatPalSpeechEnded?.Invoke(this, EventArgs.Empty);
                    break;
            }

            return;
        }

        if (
            root.TryGetProperty("id", out JsonElement id)
            && id.ValueKind is JsonValueKind.String
            && _pending.TryGetValue(id.GetString()!, out TaskCompletionSource<JsonElement>? waiting)
        )
        {
            _ = waiting.TrySetResult(root);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Connected to Animaze at {Endpoint}."
    )]
    private static partial void LogConnected(ILogger logger, Uri endpoint);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "The connection to Animaze was lost."
    )]
    private static partial void LogConnectionLost(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Animaze sent a message that is not JSON; it was ignored."
    )]
    private static partial void LogUnreadable(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Closing the Animaze connection failed; it is dropped."
    )]
    private static partial void LogCloseFailed(ILogger logger, Exception exception);
}

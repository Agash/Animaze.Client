using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Animaze.Client.Tests;

[TestClass]
public sealed class AnimazeClientTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetEmotesAsync_ReadsNamesAndItemNames()
    {
        await using FakeAnimaze animaze = await FakeAnimaze.StartAsync(static request =>
            request["action"]!.GetValue<string>() switch
            {
                "GetEmotes" => new JsonObject
                {
                    ["emotes"] = new JsonArray(
                        new JsonObject
                        {
                            ["friendlyName"] = "Hearts",
                            ["itemName"] = "emote_hearts",
                        }
                    ),
                },
                _ => [],
            }
        );
        await using AnimazeClient client = await animaze.ConnectAsync(
            TestContext.CancellationToken
        );

        var emotes = await client.GetEmotesAsync(TestContext.CancellationToken);

        Assert.HasCount(1, emotes);
        Assert.AreEqual(new AnimazeItem("Hearts", "emote_hearts"), emotes[0]);
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task TriggerSpecialActionAsync_SendsTheIndex()
    {
        await using FakeAnimaze animaze = await FakeAnimaze.StartAsync(static _ => []);
        await using AnimazeClient client = await animaze.ConnectAsync(
            TestContext.CancellationToken
        );

        await client.TriggerSpecialActionAsync(3, TestContext.CancellationToken);

        JsonObject sent = animaze.Received.Single();
        Assert.AreEqual("TriggerSpecialAction", sent["action"]!.GetValue<string>());
        Assert.AreEqual(3, sent["index"]!.GetValue<int>());
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SendAsync_ErrorReply_Throws()
    {
        await using FakeAnimaze animaze = await FakeAnimaze.StartAsync(static _ => new JsonObject
        {
            ["error"] = "Avatar not found",
        });
        await using AnimazeClient client = await animaze.ConnectAsync(
            TestContext.CancellationToken
        );

        AnimazeException error = await Assert.ThrowsExactlyAsync<AnimazeException>(() =>
            client.LoadAvatarAsync("Nobody", TestContext.CancellationToken)
        );

        Assert.AreEqual("LoadAvatar", error.Action);
        Assert.Contains("Avatar not found", error.Message);
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task AvatarChangedEvent_IsRaised()
    {
        await using FakeAnimaze animaze = await FakeAnimaze.StartAsync(static _ => []);
        await using AnimazeClient client = await animaze.ConnectAsync(
            TestContext.CancellationToken
        );
        TaskCompletionSource<string> changed = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        client.AvatarChanged += (_, e) => changed.TrySetResult(e.AvatarName);

        await animaze.PushAsync(
            new JsonObject { ["event"] = "AvatarChanged", ["new_avatar"] = "AvatarBomb" },
            TestContext.CancellationToken
        );

        Assert.AreEqual(
            "AvatarBomb",
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken)
        );
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SendAsync_NoReply_TimesOut()
    {
        await using FakeAnimaze animaze = await FakeAnimaze.StartAsync(static _ => null);
        await using AnimazeClient client = await animaze.ConnectAsync(
            TestContext.CancellationToken,
            TimeSpan.FromMilliseconds(200)
        );

        _ = await Assert.ThrowsExactlyAsync<AnimazeException>(() =>
            client.CalibrateTrackerAsync(TestContext.CancellationToken)
        );
    }

    /// <summary>A loopback stand-in for Animaze that answers each request through a reply function.</summary>
    private sealed class FakeAnimaze : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Func<JsonObject, JsonObject?> _reply;
        private readonly TaskCompletionSource<WebSocket> _socket = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private readonly CancellationTokenSource _stop = new();
        private readonly List<JsonObject> _received = [];
        private Task? _serve;

        private FakeAnimaze(Func<JsonObject, JsonObject?> reply) => _reply = reply;

        public Uri Endpoint { get; private set; } = null!;

        public IReadOnlyList<JsonObject> Received
        {
            get
            {
                lock (_received)
                {
                    return [.. _received];
                }
            }
        }

        public static Task<FakeAnimaze> StartAsync(Func<JsonObject, JsonObject?> reply)
        {
            FakeAnimaze animaze = new(reply);
            int port = FreePort();
            animaze._listener.Prefixes.Add($"http://localhost:{port}/");
            animaze._listener.Start();
            animaze.Endpoint = new Uri($"ws://localhost:{port}/");
            animaze._serve = animaze.ServeAsync();
            return Task.FromResult(animaze);
        }

        public async Task<AnimazeClient> ConnectAsync(
            CancellationToken ct,
            TimeSpan? timeout = null
        )
        {
            AnimazeClient client = new(
                new AnimazeClientOptions
                {
                    Endpoint = Endpoint,
                    RequestTimeout = timeout ?? TimeSpan.FromSeconds(10),
                }
            );
            await client.ConnectAsync(ct);
            _ = await _socket.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            return client;
        }

        public async Task PushAsync(JsonObject message, CancellationToken ct)
        {
            WebSocket socket = await _socket.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            await socket.SendAsync(
                Encoding.UTF8.GetBytes(message.ToJsonString()),
                WebSocketMessageType.Text,
                true,
                ct
            );
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            if (_serve is not null)
            {
                try
                {
                    await _serve;
                }
                catch (Exception ex)
                    when (ex
                            is OperationCanceledException
                                or HttpListenerException
                                or WebSocketException
                                or ObjectDisposedException
                    )
                {
                    // The listener stopped under the serve loop, which is how the fake ends.
                }
            }

            _listener.Close();
            _stop.Dispose();
        }

        private async Task ServeAsync()
        {
            HttpListenerContext context = await _listener.GetContextAsync();
            HttpListenerWebSocketContext accepted = await context.AcceptWebSocketAsync(null);
            WebSocket socket = accepted.WebSocket;
            _ = _socket.TrySetResult(socket);
            byte[] buffer = new byte[16 * 1024];
            while (!_stop.IsCancellationRequested && socket.State is WebSocketState.Open)
            {
                WebSocketReceiveResult received = await socket.ReceiveAsync(buffer, _stop.Token);
                if (received.MessageType is WebSocketMessageType.Close)
                {
                    await socket.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "bye",
                        CancellationToken.None
                    );
                    return;
                }

                JsonObject request = JsonNode.Parse(buffer.AsSpan(0, received.Count))!.AsObject();
                lock (_received)
                {
                    _received.Add(request);
                }

                if (_reply(request) is not { } reply)
                {
                    continue;
                }

                reply["action"] = request["action"]!.GetValue<string>();
                reply["id"] = request["id"]!.GetValue<string>();
                await socket.SendAsync(
                    Encoding.UTF8.GetBytes(reply.ToJsonString()),
                    WebSocketMessageType.Text,
                    true,
                    _stop.Token
                );
            }
        }

        private static int FreePort()
        {
            using TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
    }
}

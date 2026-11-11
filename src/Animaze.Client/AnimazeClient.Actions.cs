using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Animaze.Client;

/// <summary>An entry in one of Animaze's lists: what it is called and how a trigger names it.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Id">The value the matching trigger takes: an item name or an index.</param>
public sealed record AnimazeItem(string Name, string Id);

/// <summary>A position and rotation in Animaze's scene space.</summary>
/// <param name="Position">X, Y, Z.</param>
/// <param name="Rotation">Quaternion X, Y, Z, W.</param>
public sealed record AnimazeTransform(
    ImmutableArray<double> Position,
    ImmutableArray<double> Rotation
);

public sealed partial class AnimazeClient
{
    /// <summary>The avatars Animaze can load.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Their names.</returns>
    public async Task<ImmutableArray<AnimazeItem>> GetAvatarsAsync(
        CancellationToken ct = default
    ) => Read.Items(await SendAsync("GetAvatars", ct: ct).ConfigureAwait(false), "avatars");

    /// <summary>Loads an avatar by name.</summary>
    /// <param name="name">As <see cref="GetAvatarsAsync"/> lists it.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task LoadAvatarAsync(string name, CancellationToken ct = default) =>
        SendAsync("LoadAvatar", new JsonObject { ["name"] = name }, ct);

    /// <summary>The scenes Animaze can load.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Their names.</returns>
    public async Task<ImmutableArray<AnimazeItem>> GetScenesAsync(CancellationToken ct = default) =>
        Read.Items(await SendAsync("GetScenes", ct: ct).ConfigureAwait(false), "scenes");

    /// <summary>Loads a scene by name.</summary>
    /// <param name="name">As <see cref="GetScenesAsync"/> lists it.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task LoadSceneAsync(string name, CancellationToken ct = default) =>
        SendAsync("LoadScene", new JsonObject { ["name"] = name }, ct);

    /// <summary>The saved quickscenes.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Their names and indexes.</returns>
    public async Task<ImmutableArray<AnimazeItem>> GetQuickscenesAsync(
        CancellationToken ct = default
    ) => Read.Items(await SendAsync("GetQuickscenes", ct: ct).ConfigureAwait(false), "quickscenes");

    /// <summary>Loads a quickscene.</summary>
    /// <param name="index">As <see cref="GetQuickscenesAsync"/> lists it.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task LoadQuicksceneAsync(int index, CancellationToken ct = default) =>
        SendAsync("LoadQuickscene", new JsonObject { ["index"] = index }, ct);

    /// <summary>The emotes the current avatar has.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Their names and item names.</returns>
    public async Task<ImmutableArray<AnimazeItem>> GetEmotesAsync(CancellationToken ct = default) =>
        Read.Items(await SendAsync("GetEmotes", ct: ct).ConfigureAwait(false), "emotes");

    /// <summary>Plays an emote.</summary>
    /// <param name="itemName">The emote's item name.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task TriggerEmoteAsync(string itemName, CancellationToken ct = default) =>
        SendAsync("TriggerEmote", new JsonObject { ["itemName"] = itemName }, ct);

    /// <summary>The current avatar's special actions.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Their animation names and indexes.</returns>
    public async Task<ImmutableArray<AnimazeItem>> GetSpecialActionsAsync(
        CancellationToken ct = default
    ) =>
        Read.Items(
            await SendAsync("GetSpecialActions", ct: ct).ConfigureAwait(false),
            "specialActions"
        );

    /// <summary>Plays a special action.</summary>
    /// <param name="index">As <see cref="GetSpecialActionsAsync"/> lists it.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task TriggerSpecialActionAsync(int index, CancellationToken ct = default) =>
        SendAsync("TriggerSpecialAction", new JsonObject { ["index"] = index }, ct);

    /// <summary>The current avatar's poses.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Their names and indexes.</returns>
    public async Task<ImmutableArray<AnimazeItem>> GetPosesAsync(CancellationToken ct = default) =>
        Read.Items(await SendAsync("GetPoses", ct: ct).ConfigureAwait(false), "poses");

    /// <summary>Strikes a pose.</summary>
    /// <param name="index">As <see cref="GetPosesAsync"/> lists it.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task TriggerPoseAsync(int index, CancellationToken ct = default) =>
        SendAsync("TriggerPose", new JsonObject { ["index"] = index }, ct);

    /// <summary>The current avatar's idle animations.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Their names and indexes.</returns>
    public async Task<ImmutableArray<AnimazeItem>> GetIdleAnimationsAsync(
        CancellationToken ct = default
    ) => Read.Items(await SendAsync("GetIdleAnims", ct: ct).ConfigureAwait(false), "idleAnims");

    /// <summary>Switches the idle animation.</summary>
    /// <param name="index">As <see cref="GetIdleAnimationsAsync"/> lists it.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task TriggerIdleAnimationAsync(int index, CancellationToken ct = default) =>
        SendAsync("TriggerIdle", new JsonObject { ["index"] = index }, ct);

    /// <summary>An item's icon.</summary>
    /// <param name="name">The avatar, scene or item.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The image bytes.</returns>
    public async Task<byte[]> GetItemIconAsync(string name, CancellationToken ct = default) =>
        Read.String(
            await SendAsync("GetItemIcon", new JsonObject { ["name"] = name }, ct)
                .ConfigureAwait(false),
            "image"
        )
            is { } image
            ? Convert.FromBase64String(image)
            : [];

    /// <summary>The camera's position and rotation.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The transform.</returns>
    public async Task<AnimazeTransform> GetCameraTransformAsync(CancellationToken ct = default) =>
        Read.Transform(await SendAsync("GetCameraTransform", ct: ct).ConfigureAwait(false));

    /// <summary>Moves the camera.</summary>
    /// <param name="transform">Where to.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task SetCameraTransformAsync(AnimazeTransform transform, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return SendAsync("SetCameraTransform", Transform(transform), ct);
    }

    /// <summary>The camera's field of view in degrees.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The field of view.</returns>
    public async Task<double> GetCameraFieldOfViewAsync(CancellationToken ct = default) =>
        Read.Number(await SendAsync("GetCameraFOV", ct: ct).ConfigureAwait(false), "fov") ?? 0d;

    /// <summary>Sets the camera's field of view.</summary>
    /// <param name="degrees">Field of view in degrees.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task SetCameraFieldOfViewAsync(double degrees, CancellationToken ct = default) =>
        SendAsync("SetCameraFOV", new JsonObject { ["fov"] = degrees }, ct);

    /// <summary>The avatar's position and rotation.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The transform.</returns>
    public async Task<AnimazeTransform> GetAvatarTransformAsync(CancellationToken ct = default) =>
        Read.Transform(await SendAsync("GetAvatarTransform", ct: ct).ConfigureAwait(false));

    /// <summary>Moves the avatar.</summary>
    /// <param name="transform">Where to.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task SetAvatarTransformAsync(AnimazeTransform transform, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return SendAsync("SetAvatarTransform", Transform(transform), ct);
    }

    /// <summary>Switches a behaviour override, such as <c>Auto Blink</c> or <c>Look At Camera</c>.</summary>
    /// <param name="behavior">The behaviour's name as Animaze spells it.</param>
    /// <param name="enabled">On or off.</param>
    /// <param name="settings">The behaviour's own settings (for example <c>frequency</c>), or null.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task SetOverrideAsync(
        string behavior,
        bool enabled,
        IReadOnlyDictionary<string, double>? settings = null,
        CancellationToken ct = default
    )
    {
        JsonObject request = new() { ["behavior"] = behavior, ["value"] = enabled };
        foreach ((string key, double value) in settings ?? new Dictionary<string, double>())
        {
            request[key] = value;
        }

        return SendAsync("SetOverride", request, ct);
    }

    /// <summary>Turns broadcasting (Animaze's virtual camera) on or off.</summary>
    /// <param name="on">On or off.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task SetBroadcastAsync(bool on, CancellationToken ct = default) =>
        SendAsync("Broadcast", new JsonObject { ["toggle"] = on }, ct);

    /// <summary>Recalibrates face tracking.</summary>
    /// <param name="ct">Cancels the request.</param>
    public Task CalibrateTrackerAsync(CancellationToken ct = default) =>
        SendAsync("CalibrateTracker", ct: ct);

    /// <summary>
    /// Sends a prompt to Animaze's ChatPal. The avatar speaks ChatPal's reply, and the reply is returned.
    /// </summary>
    /// <param name="message">The prompt.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>ChatPal's reply, or an empty string when Animaze sends none.</returns>
    public async Task<string> SendChatPalMessageAsync(
        string message,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(message);
        JsonElement reply = await SendAsync(
                "ChatbotSendMessage",
                new JsonObject { ["message"] = message },
                ct
            )
            .ConfigureAwait(false);
        return Read.String(reply, "response") ?? string.Empty;
    }

    /// <summary>
    /// Has the avatar speak <paramref name="text"/> word for word through ChatPal's voice, without asking the AI.
    /// Animaze's API documents this as a ChatPal message starting with <c>-echo</c>.
    /// </summary>
    /// <param name="text">What to say.</param>
    /// <param name="ct">Cancels the request.</param>
    public Task SpeakAsync(string text, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return SendAsync(
            "ChatbotSendMessage",
            new JsonObject { ["message"] = "-echo " + text },
            ct
        );
    }

    private static JsonObject Transform(AnimazeTransform transform) =>
        new()
        {
            ["position"] = new JsonArray([.. transform.Position.Select(static v => (JsonNode)v)]),
            ["rotation"] = new JsonArray([.. transform.Rotation.Select(static v => (JsonNode)v)]),
        };
}

/// <summary>Lenient readers for Animaze's replies, whose list entries vary between strings and objects.</summary>
internal static class Read
{
    public static string? String(JsonElement root, string key) =>
        root.TryGetProperty(key, out JsonElement value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    public static double? Number(JsonElement root, string key) =>
        root.TryGetProperty(key, out JsonElement value) && value.ValueKind is JsonValueKind.Number
            ? value.GetDouble()
            : null;

    public static ImmutableArray<AnimazeItem> Items(JsonElement root, string key)
    {
        if (
            !root.TryGetProperty(key, out JsonElement list)
            || list.ValueKind is not JsonValueKind.Array
        )
        {
            return [];
        }

        ImmutableArray<AnimazeItem>.Builder items = ImmutableArray.CreateBuilder<AnimazeItem>();
        int position = 0;
        foreach (JsonElement entry in list.EnumerateArray())
        {
            if (entry.ValueKind is JsonValueKind.String)
            {
                string text = entry.GetString() ?? string.Empty;
                items.Add(new AnimazeItem(text, text));
            }
            else if (entry.ValueKind is JsonValueKind.Object)
            {
                string name =
                    String(entry, "friendlyName")
                    ?? String(entry, "name")
                    ?? String(entry, "animName")
                    ?? String(entry, "itemName")
                    ?? string.Empty;
                string id =
                    String(entry, "itemName")
                    ?? (
                        entry.TryGetProperty("index", out JsonElement index)
                        && index.ValueKind is JsonValueKind.Number
                            ? index
                                .GetInt32()
                                .ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : position.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    );
                items.Add(new AnimazeItem(name, id));
            }

            position++;
        }

        return items.ToImmutable();
    }

    public static AnimazeTransform Transform(JsonElement root) =>
        new(Numbers(root, "position"), Numbers(root, "rotation"));

    private static ImmutableArray<double> Numbers(JsonElement root, string key) =>
        root.TryGetProperty(key, out JsonElement array) && array.ValueKind is JsonValueKind.Array
            ?
            [
                .. array
                    .EnumerateArray()
                    .Where(static v => v.ValueKind is JsonValueKind.Number)
                    .Select(static v => v.GetDouble()),
            ]
            : [];
}

/// <summary>Source-generated JSON metadata.</summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(JsonObject))]
internal sealed partial class AnimazeJsonContext
    : System.Text.Json.Serialization.JsonSerializerContext;

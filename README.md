# Animaze.Client

.NET client for the [Animaze](https://www.animaze.us/) API: load avatars and scenes, play emotes, poses,
idle animations and special actions, move the camera and the avatar, switch behaviour overrides, and react
to avatar changes. Native AOT compatible, System.Text.Json source-generated, `net10.0` and `net11.0`.

## Install

```shell
dotnet add package Animaze.Client
dotnet add package Animaze.Client.DependencyInjection   # optional
```

Enable the API in Animaze first: Settings, Animaze API, Enabled. It listens on port 9000 unless changed there.

## Use

```csharp
await using AnimazeClient animaze = new();
await animaze.ConnectAsync();

foreach (AnimazeItem emote in await animaze.GetEmotesAsync())
{
    Console.WriteLine($"{emote.Name}: {emote.Id}");
}

await animaze.TriggerEmoteAsync("emote_hearts");
animaze.AvatarChanged += (_, e) => Console.WriteLine($"now {e.AvatarName}");
```

Every documented action has a typed method. `SendAsync(action, arguments)` sends any other action and
returns Animaze's reply as JSON; replies with an `error` field throw `AnimazeException`.

## Packages

| Package | Contents |
|---|---|
| `Animaze.Client` | The client |
| `Animaze.Client.DependencyInjection` | `services.AddAnimazeClient(options => ...)` |

## License

MIT. Animaze is a product of Holotech Studios; this project is not affiliated with it.

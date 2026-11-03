using Animaze.Client;

// Lists what the running Animaze offers, and plays an emote when one is named:
//   Animaze.Client.Sample [emote item name] [ws://host:port]
Uri endpoint = args.Length > 1 ? new Uri(args[1]) : new Uri("ws://localhost:9000");
await using AnimazeClient client = new(new AnimazeClientOptions { Endpoint = endpoint });
client.AvatarChanged += (_, e) => Console.WriteLine($"avatar changed to {e.AvatarName}");

try
{
    await client.ConnectAsync();
}
catch (AnimazeException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

Print("avatars", await client.GetAvatarsAsync());
Print("scenes", await client.GetScenesAsync());
Print("emotes", await client.GetEmotesAsync());
Print("special actions", await client.GetSpecialActionsAsync());

if (args.Length > 0)
{
    await client.TriggerEmoteAsync(args[0]);
    Console.WriteLine($"played {args[0]}");
}

return 0;

static void Print(string title, IEnumerable<AnimazeItem> items)
{
    Console.WriteLine($"{title}:");
    foreach (AnimazeItem item in items)
    {
        Console.WriteLine($"  {item.Name} ({item.Id})");
    }
}

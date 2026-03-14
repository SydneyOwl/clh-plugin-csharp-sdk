using ClhPluginSdk;

var pipePath = args.Length > 0 ? args[0] : SdkDefaults.DefaultPipePath;

var manifest = new PluginManifest
{
    Uuid = $"csharp-demo-{Guid.NewGuid():N}",
    Name = "basic-csharp-demo",
    Version = "0.1.0",
    Description = "CLH plugin C# SDK demo",
    Metadata = new Dictionary<string, string>
    {
        ["source"] = "clh-plugin-csharp-sdk/examples/BasicDemo"
    }
};

var options = new ClientOptions
{
    PipePath = pipePath,
    HeartbeatInterval = TimeSpan.FromSeconds(3),
    RequestTimeout = TimeSpan.FromSeconds(8),
    OnMessage = message =>
    {
        Console.WriteLine($"[callback] kind={message.Kind} ts={message.TimestampUtc:O}");
    }
};

await using var client = new ClhClient(manifest, options);

Console.WriteLine($"Connecting to {pipePath} ...");
var register = await client.ConnectAsync();
Console.WriteLine($"Connected instance={register.InstanceId} version={register.ServerInfo.Version}");

var serverInfo = await client.QueryServerInfoAsync();
Console.WriteLine($"Server uptime={serverInfo.UptimeSec}s connected_plugins={serverInfo.ConnectedPluginCount}");

var subResult = await client.SubscribeEventsAsync(new EventSubscription
{
    Topics =
    {
        EnvelopeTopic.EventServerStatus,
        EnvelopeTopic.EventPluginLifecycle,
        EnvelopeTopic.EventWsjtxDecodeBatch
    }
});
Console.WriteLine($"Subscribed topics={subResult.Topics.Count}");

using (var waitCts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
{
    try
    {
        Console.WriteLine("Waiting one inbound message (10s timeout)...");
        var message = await client.WaitMessageAsync(waitCts.Token);
        Console.WriteLine($"WaitMessage received kind={message.Kind} ts={message.TimestampUtc:O}");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("WaitMessage timeout reached.");
    }
}

await client.CloseAsync();
Console.WriteLine("Client closed.");

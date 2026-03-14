# CLH Plugin C# SDK

Cloudlog Helper plugin SDK for C#, aligned with `clh-proto` `v20260312`.

## Project Layout

- `src/ClhPluginSdk`: SDK library
- `examples/BasicDemo`: minimal console example

## Features

- Plugin register / heartbeat / graceful deregister
- Typed query wrappers
- Typed command wrappers
- `RawQueryAsync` / `RawCommandAsync`
- Dual inbound model: callback (`ClientOptions.OnMessage`) + pull (`WaitMessageAsync`)
- Inbound `Any` payload auto-converted to SDK models when possible

## Quick Start

```csharp
using ClhPluginSdk;

var client = new ClhClient(
    new PluginManifest
    {
        Uuid = "your-plugin-uuid",
        Name = "demo-plugin",
        Version = "1.0.0"
    },
    new ClientOptions
    {
        PipePath = SdkDefaults.DefaultPipePath,
        HeartbeatInterval = TimeSpan.FromSeconds(3),
        RequestTimeout = TimeSpan.FromSeconds(8)
    });

var register = await client.ConnectAsync();
var info = await client.QueryServerInfoAsync();
await client.CloseAsync();
```

## Run Demo

```bash
dotnet run --project clh-plugin-csharp-sdk/examples/BasicDemo
```

Optional pipe path argument:

```bash
dotnet run --project clh-plugin-csharp-sdk/examples/BasicDemo -- "/tmp/clh.plugin"
```

## Command Notes

- `TriggerQsoReuploadAsync` uses attribute key `qsoIds` (multiple ids joined by `;;;`).

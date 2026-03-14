# CLH Plugin C# SDK

Cloudlog Helper plugin SDK for C#, aligned with `clh-proto` `v20260312`.

## Install

`dotnet add package ClhPluginSdk --version 0.3.2`

or add to csproj:

`<PackageReference Include="ClhPluginSdk" Version="0.3.2" />`

## Project Layout

- `src/ClhPluginSdk`: SDK library
- `examples/AvaloniaCapabilityDemo`: Avalonia full capability demo

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

## Run Avalonia Capability Demo

```bash
dotnet run --project clh-plugin-csharp-sdk/examples/AvaloniaCapabilityDemo
```

Optional pipe path argument:

```bash
dotnet run --project clh-plugin-csharp-sdk/examples/AvaloniaCapabilityDemo -- --pipe "/tmp/clh.plugin"
```

The Avalonia demo includes:

- All typed queries and commands
- Event subscription management
- Inbound handling by `ClientOptions.OnMessage`, `MessageReceived`, and `WaitMessageAsync`
- `RawQueryAsync` / `RawCommandAsync`
- Full JSON log output for request/response and inbound messages

## Command Notes

- `TriggerQsoReuploadAsync` uses attribute key `qsoIds` (multiple ids joined by `;;;`).


## Cheatsheet
• Feature-to-API Cheat Sheet (Cloudlog Helper Plugin System)

### 1) Connect / lifecycle

| Feature | Go SDK | C# SDK | Notes |                                                                                                                                                                                             
  |---|---|---|---|                                                                                                                                                                                                                 
| Create client | NewClient(PluginManifest, ...options) | new ClhClient(PluginManifest, ClientOptions) | Uuid/Name/Version are required |
| Connect/register | Connect(ctx) | ConnectAsync() | Registers plugin with CLH |                                                                                                                                                  
| Heartbeat | Auto via WithHeartbeatInterval(...) | Auto via ClientOptions.HeartbeatInterval | Keepalive timeout is enforced by CLH |                                                                                             
| Graceful close | Close(ctx) | CloseAsync() | Sends deregister request |                                                                                                                                                         
| Inbound callback | WithMessageHandler(func(Message){...}) | ClientOptions.OnMessage / MessageReceived | Push mode |                                                                                                             
| Inbound pull | WaitMessage(ctx) | WaitMessageAsync(...) | Pull mode |                                                                                                                                                           

### 2) Query APIs (read state)

| Feature | Protocol topic | Go SDK | C# SDK |                                                                                                                                                                                    
  |---|---|---|---|                                                                                                                                                                                                                 
| Server info | QueryServerInfo | QueryServerInfo(ctx) | QueryServerInfoAsync() |                                                                                                                                                 
| Connected plugins | QueryConnectedPlugins | QueryConnectedPlugins(ctx) | QueryConnectedPluginsAsync() |                                                                                                                         
| Runtime snapshot (all-in-one) | QueryRuntimeSnapshot | QueryRuntimeSnapshot(ctx) | QueryRuntimeSnapshotAsync() |                                                                                                                
| Rig snapshot | QueryRigSnapshot | QueryRigSnapshot(ctx) | QueryRigSnapshotAsync() |                                                                                                                                             
| UDP snapshot | QueryUdpSnapshot | QueryUDPSnapshot(ctx) | QueryUdpSnapshotAsync() |                                                                                                                                             
| QSO queue snapshot | QueryQsoQueueSnapshot | QueryQSOQueueSnapshot(ctx) | QueryQsoQueueSnapshotAsync() |                                                                                                                        
| Settings snapshot | QuerySettingsSnapshot | QuerySettingsSnapshot(ctx) | QuerySettingsSnapshotAsync() |                                                                                                                         
| Plugin telemetry | QueryPluginTelemetry | QueryPluginTelemetry(ctx, pluginUUID) | QueryPluginTelemetryAsync(pluginUuid?) |                                                                                                      

### 3) Command APIs (control/actions)

| Feature | Protocol topic | Go SDK | C# SDK | Required input |                                                                                                                                                                   
  |---|---|---|---|---|                                                                                                                                                                                                             
| Subscribe event topics | CommandSubscribeEvents | SubscribeEvents(ctx, sub) | SubscribeEventsAsync(sub) | topics[] |                                                                                                            
| Show main window | CommandShowMainWindow | ShowMainWindow(ctx) | ShowMainWindowAsync() | - |                                                                                                                                    
| Hide main window | CommandHideMainWindow | HideMainWindow(ctx) | HideMainWindowAsync() | - |                                                                                                                                    
| Open specific window | CommandOpenWindow | OpenWindow(ctx, window, asDialog) | OpenWindowAsync(window, asDialog) | window, asDialog |                                                                                           
| Send in-app notification | CommandSendNotification | SendNotification(ctx, NotificationCommand) | SendNotificationAsync(command) | level, message |                                                                             
| Toggle UDP server | CommandToggleUdpServer | ToggleUDPServer(ctx, enabled*) | ToggleUdpServerAsync(enabled?) | optional enabled |                                                                                               
| Toggle rig backend polling | CommandToggleRigBackend | ToggleRigBackend(ctx, enabled*) | ToggleRigBackendAsync(enabled?) | optional enabled |                                                                                   
| Switch rig backend | CommandSwitchRigBackend | SwitchRigBackend(ctx, backend) | SwitchRigBackendAsync(backend) | Hamlib/FLRig/OmniRig |                                                                                         
| Upload external QSO(s) via ADIF | CommandUploadExternalQso | UploadExternalQSO(ctx, adifLogs) | UploadExternalQsoAsync(adifLogs) | attribute adifLogs |                                                                         
| Trigger QSO reupload | CommandTriggerQsoReupload | TriggerQSOReupload(ctx, attrs) | TriggerQsoReuploadAsync(...) | qsoIds (use ;;; separator) |
| Update settings | CommandUpdateSettings | UpdateSettings(ctx, patch) | UpdateSettingsAsync(patch) | SettingsPatch.Values |                                                                                                      
| Raw request escape hatch | any topic | RawQuery, RawCommand | RawQueryAsync, RawCommandAsync | advanced use |                                                                                                                   

### 4) Event topics you can subscribe to

| Topic | Payload type |                                                                                                                                                                                                          
  |---|---|                                                                                                                                                                                                                         
| EventServerStatus | ClhServerStatusChanged |                                                                                                                                                                                    
| EventPluginLifecycle | ClhPluginLifecycleChanged |                                                                                                                                                                              
| EventWsjtxMessage | WsjtxMessage |                                                                                                                                                                                              
| EventWsjtxDecodeRealtime | WsjtxMessage (decode payload) |                                                                                                                                                                      
| EventWsjtxDecodeBatch | PackedDecodeMessage |                                                                                                                                                                                   
| EventRigData | RigData |                                                                                                                                                                                                        
| EventQsoUploadStatus | ClhQSOUploadStatusChanged |                                                                                                                                                                              
| EventQsoQueueStatus | ClhQsoQueueStatusChanged |                                                                                                                                                                                
| EventSettingsChanged | ClhSettingsChanged |
| EventPluginTelemetry | ClhPluginTelemetryChanged |      

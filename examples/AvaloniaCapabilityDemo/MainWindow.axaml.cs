using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClhPluginSdk;

namespace AvaloniaCapabilityDemo;

public partial class MainWindow : Window
{
    private static readonly EnvelopeTopic[] DefaultEventTopics = Enum.GetValues<EnvelopeTopic>()
        .Where(IsEventTopic)
        .ToArray();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private ClhClient? _client;
    private CancellationTokenSource? _waitLoopCts;
    private Task? _waitLoopTask;
    private int _optionCallbackCount;
    private int _eventHandlerCount;
    private int _pullLoopCount;

    public MainWindow()
    {
        InitializeComponent();
        InitializeUiDefaults();
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            DisconnectInternalAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore close-time exceptions.
        }

        base.OnClosed(e);
    }

    private void InitializeUiDefaults()
    {
        PipePathBox.Text = string.IsNullOrWhiteSpace(Program.StartupPipePath)
            ? SdkDefaults.DefaultPipePath
            : Program.StartupPipePath;
        PluginUuidBox.Text = $"avalonia-demo-{Guid.NewGuid():N}";
        PluginNameBox.Text = "avalonia-capability-demo";
        PluginVersionBox.Text = "1.0.0";
        PluginDescriptionBox.Text = "Avalonia example for CLH Plugin C# SDK capabilities";
        SubscriptionTopicsBox.Text = string.Join(",", DefaultEventTopics.Select(t => t.ToString()));

        NotificationLevelBox.ItemsSource = Enum.GetValues<NotificationLevel>();
        NotificationLevelBox.SelectedItem = NotificationLevel.Info;
        NotificationTitleBox.Text = "SDK Demo";
        NotificationMessageBox.Text = "Message from Avalonia capability demo.";

        WindowNameBox.Text = ControllableWindowNames.Settings;

        RigBackendBox.ItemsSource = new[]
        {
            RigBackendNames.Hamlib,
            RigBackendNames.FlRig,
            RigBackendNames.OmniRig
        };
        RigBackendBox.SelectedIndex = 0;

        RawKindBox.ItemsSource = new[] { EnvelopeKind.Query, EnvelopeKind.Command };
        RawKindBox.SelectedItem = EnvelopeKind.Query;
        RawTopicBox.ItemsSource = Enum.GetValues<EnvelopeTopic>();
        RawTopicBox.SelectedItem = EnvelopeTopic.QueryServerInfo;

        AdifLogsBox.Text = "<call:5>BA1ABC <gridsquare:4>OL12 <mode:3>FT8 <rst_sent:3>-17 <rst_rcvd:3>-13 <qso_date:8>20240930 <time_on:6>024231 <qso_date_off:8>20240930 <time_off:6>024314 <band:2>6m <freq:9>50.314044 <station_callsign:6>BA2ABC <my_gridsquare:4>OL34 <eor>";
        ReuploadQsoIdsBox.Text = "qso-1;;;qso-2";
        ReuploadAttributesBox.Text = "qsoIds=qso-1;;;qso-2";
        SettingsKeyBox.Text = "instance_name";
        SettingsValueBox.Text = "CLH-Demo";

        ConnectionStateText.Text = "Disconnected";
        InstanceIdText.Text = "-";
        ServerVersionText.Text = "-";
        UpdateMessageCounters();

        AppendLog("Avalonia capability demo is ready.");
    }

    private static bool IsEventTopic(EnvelopeTopic topic)
    {
        var value = (int)topic;
        return value >= 1 && value < 100;
    }

    private PluginManifest BuildManifest()
    {
        var uuid = (PluginUuidBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(uuid))
        {
            uuid = $"avalonia-demo-{Guid.NewGuid():N}";
            PluginUuidBox.Text = uuid;
        }

        return new PluginManifest
        {
            Uuid = uuid,
            Name = (PluginNameBox.Text ?? string.Empty).Trim(),
            Version = (PluginVersionBox.Text ?? string.Empty).Trim(),
            Description = (PluginDescriptionBox.Text ?? string.Empty).Trim(),
            Metadata = new Dictionary<string, string>
            {
                ["example"] = "AvaloniaCapabilityDemo",
                ["ui"] = "Avalonia",
                ["created_at"] = DateTimeOffset.UtcNow.ToString("O")
            }
        };
    }

    private ClientOptions BuildClientOptions()
    {
        var options = new ClientOptions
        {
            PipePath = (PipePathBox.Text ?? string.Empty).Trim(),
            HeartbeatInterval = TimeSpan.FromSeconds(3),
            RequestTimeout = TimeSpan.FromSeconds(8),
            WaitBufferSize = 512
        };

        if (UseOptionCallbackCheckBox.IsChecked == true)
        {
            options.OnMessage = HandleOptionCallbackMessage;
        }

        return options;
    }

    private async Task ConnectInternalAsync()
    {
        if (_client is not null && _client.IsConnected)
        {
            AppendLog("[ConnectAsync] skipped: already connected.");
            return;
        }

        await DisconnectInternalAsync();

        var client = new ClhClient(BuildManifest(), BuildClientOptions());
        if (UseEventHandlerCheckBox.IsChecked == true)
        {
            client.MessageReceived += HandleEventMessage;
        }

        try
        {
            var register = await client.ConnectAsync();
            _client = client;
            ConnectionStateText.Text = "Connected";
            InstanceIdText.Text = register.InstanceId;
            ServerVersionText.Text = register.ServerInfo.Version;
            AppendLog("[ConnectAsync] success:");
            AppendLog(ToJson(register));

            if (StartWaitLoopCheckBox.IsChecked == true)
            {
                StartWaitLoop(client);
            }
        }
        catch
        {
            client.MessageReceived -= HandleEventMessage;
            await client.CloseAsync();
            throw;
        }
    }

    private async Task DisconnectInternalAsync()
    {
        await StopWaitLoopAsync();

        var client = _client;
        _client = null;
        if (client is not null)
        {
            client.MessageReceived -= HandleEventMessage;
            try
            {
                await client.CloseAsync();
                AppendLog("[CloseAsync] completed.");
            }
            catch (Exception ex)
            {
                AppendLog($"[CloseAsync] failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        ConnectionStateText.Text = "Disconnected";
        InstanceIdText.Text = "-";
        ServerVersionText.Text = "-";
    }

    private void StartWaitLoop(ClhClient client)
    {
        _waitLoopCts?.Cancel();
        _waitLoopCts = new CancellationTokenSource();
        var token = _waitLoopCts.Token;
        _waitLoopTask = Task.Run(async () =>
        {
            AppendLogThreadSafe("[WaitMessageAsync] pull loop started.");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var message = await client.WaitMessageAsync(token);
                    HandlePullLoopMessage(message);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (ClientClosedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppendLogThreadSafe($"[WaitMessageAsync] loop stopped: {ex.GetType().Name}: {ex.Message}");
                    break;
                }
            }

            AppendLogThreadSafe("[WaitMessageAsync] pull loop exited.");
        });
    }

    private async Task StopWaitLoopAsync()
    {
        if (_waitLoopCts is null)
        {
            return;
        }

        _waitLoopCts.Cancel();
        try
        {
            if (_waitLoopTask is not null)
            {
                await _waitLoopTask;
            }
        }
        catch
        {
            // Ignore pull-loop exceptions during shutdown.
        }
        finally
        {
            _waitLoopCts.Dispose();
            _waitLoopCts = null;
            _waitLoopTask = null;
        }
    }

    private void HandleOptionCallbackMessage(Message message)
    {
        HandleInboundMessage("OnMessage", MessageSource.Option, message);
    }

    private void HandleEventMessage(Message message)
    {
        HandleInboundMessage("MessageReceived", MessageSource.Event, message);
    }

    private void HandlePullLoopMessage(Message message)
    {
        HandleInboundMessage("WaitMessageAsync", MessageSource.Pull, message);
    }

    private void HandleInboundMessage(string source, MessageSource messageSource, Message message)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => HandleInboundMessage(source, messageSource, message));
            return;
        }

        switch (messageSource)
        {
            case MessageSource.Option:
                _optionCallbackCount++;
                break;
            case MessageSource.Event:
                _eventHandlerCount++;
                break;
            case MessageSource.Pull:
                _pullLoopCount++;
                break;
        }

        UpdateMessageCounters();

        AppendLog($"[{source}] {SummarizeMessage(message)}");
        if (VerboseMessageCheckBox.IsChecked == true)
        {
            AppendLog(ToJson(message));
        }
    }

    private void UpdateMessageCounters()
    {
        OptionCallbackCountText.Text = _optionCallbackCount.ToString();
        EventHandlerCountText.Text = _eventHandlerCount.ToString();
        PullLoopCountText.Text = _pullLoopCount.ToString();
    }

    private async Task ExecuteWithClientAsync(string actionName, Func<ClhClient, Task> action)
    {
        var client = _client;
        if (client is null || !client.IsConnected)
        {
            AppendLog($"[{actionName}] skipped: client is not connected.");
            return;
        }

        try
        {
            await action(client);
            AppendLog($"[{actionName}] success.");
        }
        catch (Exception ex)
        {
            AppendException(actionName, ex);
        }
    }

    private async Task ExecuteWithClientAsync<T>(string actionName, Func<ClhClient, Task<T>> action)
    {
        var client = _client;
        if (client is null || !client.IsConnected)
        {
            AppendLog($"[{actionName}] skipped: client is not connected.");
            return;
        }

        try
        {
            var result = await action(client);
            AppendLog($"[{actionName}] success:");
            AppendLog(ToJson(result));
        }
        catch (Exception ex)
        {
            AppendException(actionName, ex);
        }
    }

    private async Task RunFullDemoAsync()
    {
        AppendLog("[RunFullDemo] started.");
        await ExecuteWithClientAsync("QueryServerInfoAsync", c => c.QueryServerInfoAsync());
        await ExecuteWithClientAsync("QueryConnectedPluginsAsync", c => c.QueryConnectedPluginsAsync());
        await ExecuteWithClientAsync("QueryRuntimeSnapshotAsync", c => c.QueryRuntimeSnapshotAsync());
        await ExecuteWithClientAsync("QueryRigSnapshotAsync", c => c.QueryRigSnapshotAsync());
        await ExecuteWithClientAsync("QueryUdpSnapshotAsync", c => c.QueryUdpSnapshotAsync());
        await ExecuteWithClientAsync("QueryQsoQueueSnapshotAsync", c => c.QueryQsoQueueSnapshotAsync());
        await ExecuteWithClientAsync("QuerySettingsSnapshotAsync", c => c.QuerySettingsSnapshotAsync());
        await ExecuteWithClientAsync("QueryPluginTelemetryAsync", c => c.QueryPluginTelemetryAsync(TelemetryPluginUuidBox.Text));
        await ExecuteWithClientAsync("SubscribeEventsAsync(default)", c => c.SubscribeEventsAsync(BuildDefaultEventSubscription()));
        await ExecuteWithClientAsync("SendNotificationAsync", c => c.SendNotificationAsync(new NotificationCommand
        {
            Level = NotificationLevel.Info,
            Title = "CLH SDK Demo",
            Message = "RunFullDemo triggered typed + raw API showcase."
        }));
        await ExecuteWithClientAsync("RawQueryAsync(QueryServerInfo)", c => c.RawQueryAsync(EnvelopeTopic.QueryServerInfo));
        await ExecuteWithClientAsync("RawCommandAsync(CommandSubscribeEvents)", c =>
            c.RawCommandAsync(EnvelopeTopic.CommandSubscribeEvents, null, null, BuildDefaultEventSubscription()));
        AppendLog("[RunFullDemo] completed.");
    }

    private static EventSubscription BuildDefaultEventSubscription()
    {
        return new EventSubscription
        {
            Topics = DefaultEventTopics.ToList()
        };
    }

    private EventSubscription ParseSubscriptionOrThrow(string input)
    {
        var topics = ParseTopics(input);
        if (topics.Count == 0)
        {
            throw new FormatException("no valid topic specified");
        }

        return new EventSubscription { Topics = topics };
    }

    private static List<EnvelopeTopic> ParseTopics(string? input)
    {
        var output = new List<EnvelopeTopic>();
        if (string.IsNullOrWhiteSpace(input))
        {
            return output;
        }

        var separators = new[] { ",", ";", "\r", "\n" };
        foreach (var rawToken in input.Split(separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = rawToken.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            EnvelopeTopic topic;
            if (Enum.TryParse(token, true, out topic))
            {
                output.Add(topic);
                continue;
            }

            if (int.TryParse(token, out var intTopic) &&
                Enum.IsDefined(typeof(EnvelopeTopic), intTopic))
            {
                output.Add((EnvelopeTopic)intTopic);
                continue;
            }

            throw new FormatException($"invalid topic token: {token}");
        }

        return output.Distinct().ToList();
    }

    private static Dictionary<string, string>? ParseAttributes(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var separators = new[] { ";", "\r", "\n" };
        foreach (var rawToken in input.Split(separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = rawToken.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var index = token.IndexOf('=');
            if (index <= 0 || index == token.Length - 1)
            {
                throw new FormatException($"invalid attribute token: {token}");
            }

            var key = token[..index].Trim();
            var value = token[(index + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new FormatException($"invalid attribute key: {token}");
            }

            attributes[key] = value;
        }

        return attributes.Count == 0 ? null : attributes;
    }

    private static string[] ParseQsoIds(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Array.Empty<string>();
        }

        var separators = new[] { ";", ",", " ", "\r", "\n", "\t" };
        return input.Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string SummarizeMessage(Message message)
    {
        return message.Kind switch
        {
            InboundKind.RigData when message.RigData is not null =>
                $"RigData provider={message.RigData.Provider} rig={message.RigData.RigName} freq={message.RigData.Frequency}",
            InboundKind.ClhInternal when message.ClhInternal is not null =>
                $"ClhInternal {SummarizeInternal(message.ClhInternal)}",
            InboundKind.Envelope when message.Envelope is not null =>
                $"Envelope kind={message.Envelope.Kind} topic={message.Envelope.Topic} success={message.Envelope.Success}",
            InboundKind.ConnectionClosed =>
                "ConnectionClosed",
            InboundKind.Unknown when message.Unknown is not null =>
                $"Unknown type={message.Unknown.TypeUrl}",
            _ => message.Kind.ToString()
        };
    }

    private static string SummarizeInternal(ClhInternalMessage internalMessage)
    {
        if (internalMessage.QsoUploadStatus is not null) return nameof(internalMessage.QsoUploadStatus);
        if (internalMessage.PluginLifecycle is not null) return nameof(internalMessage.PluginLifecycle);
        if (internalMessage.ServerStatus is not null) return nameof(internalMessage.ServerStatus);
        if (internalMessage.QsoQueueStatus is not null) return nameof(internalMessage.QsoQueueStatus);
        if (internalMessage.SettingsChanged is not null) return nameof(internalMessage.SettingsChanged);
        if (internalMessage.PluginTelemetry is not null) return nameof(internalMessage.PluginTelemetry);
        return "empty";
    }

    private string ToJson(object? value)
    {
        return JsonSerializer.Serialize(value, _jsonOptions);
    }

    private void AppendException(string actionName, Exception exception)
    {
        if (exception is RemoteError remoteError)
        {
            AppendLog(
                $"[{actionName}] remote error: topic={remoteError.Topic}, code={remoteError.Code}, correlation={remoteError.CorrelationId}, message={remoteError.MessageText}");
            return;
        }

        AppendLog($"[{actionName}] failed: {exception.GetType().Name}: {exception.Message}");
    }

    private void AppendLogThreadSafe(string text)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            AppendLog(text);
            return;
        }

        Dispatcher.UIThread.Post(() => AppendLog(text));
    }

    private void AppendLog(string text)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {text}{Environment.NewLine}";
        var current = (LogBox.Text ?? string.Empty) + line;
        const int maxLength = 240_000;
        if (current.Length > maxLength)
        {
            current = current[^maxLength..];
        }

        LogBox.Text = current;
        LogBox.CaretIndex = current.Length;
    }

    private async void ConnectButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ConnectInternalAsync();
        }
        catch (Exception ex)
        {
            AppendException("ConnectAsync", ex);
        }
    }

    private async void DisconnectButton_Click(object? sender, RoutedEventArgs e)
    {
        await DisconnectInternalAsync();
    }

    private async void RunFullDemoButton_Click(object? sender, RoutedEventArgs e)
    {
        await RunFullDemoAsync();
    }

    private async void SubscribeEventsButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var subscription = ParseSubscriptionOrThrow(SubscriptionTopicsBox.Text ?? string.Empty);
            await ExecuteWithClientAsync("SubscribeEventsAsync", c => c.SubscribeEventsAsync(subscription));
        }
        catch (Exception ex)
        {
            AppendException("SubscribeEventsAsync", ex);
        }
    }

    private void SubscribeDefaultEventsButton_Click(object? sender, RoutedEventArgs e)
    {
        SubscriptionTopicsBox.Text = string.Join(",", DefaultEventTopics.Select(t => t.ToString()));
        AppendLog("[SubscribeEventsAsync] default event topics populated.");
    }

    private async void QueryServerInfoButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("QueryServerInfoAsync", c => c.QueryServerInfoAsync());
    }

    private async void QueryConnectedPluginsButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("QueryConnectedPluginsAsync", c => c.QueryConnectedPluginsAsync());
    }

    private async void QueryRuntimeSnapshotButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("QueryRuntimeSnapshotAsync", c => c.QueryRuntimeSnapshotAsync());
    }

    private async void QueryRigSnapshotButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("QueryRigSnapshotAsync", c => c.QueryRigSnapshotAsync());
    }

    private async void QueryUdpSnapshotButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("QueryUdpSnapshotAsync", c => c.QueryUdpSnapshotAsync());
    }

    private async void QueryQsoQueueSnapshotButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("QueryQsoQueueSnapshotAsync", c => c.QueryQsoQueueSnapshotAsync());
    }

    private async void QuerySettingsSnapshotButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("QuerySettingsSnapshotAsync", c => c.QuerySettingsSnapshotAsync());
    }

    private async void QueryPluginTelemetryButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("QueryPluginTelemetryAsync",
            c => c.QueryPluginTelemetryAsync(TelemetryPluginUuidBox.Text));
    }

    private async void ShowMainWindowButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("ShowMainWindowAsync", c => c.ShowMainWindowAsync());
    }

    private async void HideMainWindowButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("HideMainWindowAsync", c => c.HideMainWindowAsync());
    }

    private async void OpenWindowButton_Click(object? sender, RoutedEventArgs e)
    {
        var window = (WindowNameBox.Text ?? string.Empty).Trim();
        var asDialog = OpenAsDialogCheckBox.IsChecked == true;
        await ExecuteWithClientAsync("OpenWindowAsync", c => c.OpenWindowAsync(window, asDialog));
    }

    private async void SendNotificationButton_Click(object? sender, RoutedEventArgs e)
    {
        var notificationLevel = NotificationLevelBox.SelectedItem is NotificationLevel value
            ? value
            : NotificationLevel.Info;
        var command = new NotificationCommand
        {
            Level = notificationLevel,
            Title = (NotificationTitleBox.Text ?? string.Empty).Trim(),
            Message = (NotificationMessageBox.Text ?? string.Empty).Trim()
        };

        await ExecuteWithClientAsync("SendNotificationAsync", c => c.SendNotificationAsync(command));
    }

    private async void ToggleUdpAutoButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("ToggleUdpServerAsync()", c => c.ToggleUdpServerAsync());
    }

    private async void ToggleUdpOnButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("ToggleUdpServerAsync(true)", c => c.ToggleUdpServerAsync(true));
    }

    private async void ToggleUdpOffButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("ToggleUdpServerAsync(false)", c => c.ToggleUdpServerAsync(false));
    }

    private async void ToggleRigAutoButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("ToggleRigBackendAsync()", c => c.ToggleRigBackendAsync());
    }

    private async void ToggleRigOnButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("ToggleRigBackendAsync(true)", c => c.ToggleRigBackendAsync(true));
    }

    private async void ToggleRigOffButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("ToggleRigBackendAsync(false)", c => c.ToggleRigBackendAsync(false));
    }

    private async void StartRigButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("StartRigBackendAsync", c => c.StartRigBackendAsync());
    }

    private async void StopRigButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("StopRigBackendAsync", c => c.StopRigBackendAsync());
    }

    private async void RestartRigButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExecuteWithClientAsync("RestartRigBackendAsync", c => c.RestartRigBackendAsync());
    }

    private async void SwitchRigBackendButton_Click(object? sender, RoutedEventArgs e)
    {
        var backend = RigBackendBox.SelectedItem as string ?? RigBackendNames.Hamlib;
        await ExecuteWithClientAsync("SwitchRigBackendAsync", c => c.SwitchRigBackendAsync(backend));
    }

    private async void UploadExternalQsoButton_Click(object? sender, RoutedEventArgs e)
    {
        var adifLogs = (AdifLogsBox.Text ?? string.Empty).Trim();
        await ExecuteWithClientAsync("UploadExternalQsoAsync", c => c.UploadExternalQsoAsync(adifLogs));
    }

    private async void TriggerQsoReuploadIdsButton_Click(object? sender, RoutedEventArgs e)
    {
        var qsoIds = ParseQsoIds(ReuploadQsoIdsBox.Text);
        await ExecuteWithClientAsync("TriggerQsoReuploadAsync(ids)", c => c.TriggerQsoReuploadAsync(qsoIds));
    }

    private async void TriggerQsoReuploadAttrsButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var attributes = ParseAttributes(ReuploadAttributesBox.Text) ?? new Dictionary<string, string>();
            await ExecuteWithClientAsync("TriggerQsoReuploadAsync(attributes)",
                c => c.TriggerQsoReuploadAsync(attributes));
        }
        catch (Exception ex)
        {
            AppendException("TriggerQsoReuploadAsync(attributes)", ex);
        }
    }

    private async void UpdateSettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var key = (SettingsKeyBox.Text ?? string.Empty).Trim();
        var value = (SettingsValueBox.Text ?? string.Empty).Trim();
        var patch = new SettingsPatch();
        patch.Values[key] = value;
        await ExecuteWithClientAsync("UpdateSettingsAsync", c => c.UpdateSettingsAsync(patch));
    }

    private async void RawExecuteButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var kind = RawKindBox.SelectedItem is EnvelopeKind selectedKind
                ? selectedKind
                : EnvelopeKind.Query;
            var topic = RawTopicBox.SelectedItem is EnvelopeTopic selectedTopic
                ? selectedTopic
                : EnvelopeTopic.QueryServerInfo;
            var attributes = ParseAttributes(RawAttributesBox.Text);

            switch (kind)
            {
                case EnvelopeKind.Query:
                    await ExecuteWithClientAsync("RawQueryAsync",
                        c => c.RawQueryAsync(topic, attributes));
                    break;
                case EnvelopeKind.Command:
                    await ExecuteWithClientAsync("RawCommandAsync",
                        c => c.RawCommandAsync(topic, attributes));
                    break;
                default:
                    AppendLog($"[RawExecute] unsupported kind: {kind}");
                    break;
            }
        }
        catch (Exception ex)
        {
            AppendException("RawExecute", ex);
        }
    }

    private void ClearLogButton_Click(object? sender, RoutedEventArgs e)
    {
        LogBox.Text = string.Empty;
    }

    private enum MessageSource
    {
        Option,
        Event,
        Pull
    }
}

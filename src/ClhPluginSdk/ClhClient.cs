using System.Collections.Concurrent;
using System.Threading.Channels;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Proto = SydneyOwl.CLHProto.Plugin;

namespace ClhPluginSdk;

public sealed class ClhClient : IAsyncDisposable, IDisposable
{
    private readonly PluginManifest _manifest;
    private readonly ClientOptions _options;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Proto.PipeEnvelope?>> _pending = new();
    private readonly Channel<Message> _waitChannel;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _streamLock = new();

    private Stream? _stream;
    private CancellationTokenSource? _sessionCts;
    private Task? _readLoopTask;
    private Task? _heartbeatTask;
    private RegisterResponse _registerResponse = new();

    private long _requestSequence;
    private int _finished;
    private bool _connected;
    private bool _closed;

    public ClhClient(PluginManifest manifest, ClientOptions? options = null)
    {
        _manifest = manifest ?? throw new InvalidManifestException("manifest is required");
        _options = options ?? new ClientOptions();

        NormalizeManifest(_manifest);
        ValidateOptions(_options);

        _waitChannel = Channel.CreateBounded<Message>(new BoundedChannelOptions(_options.WaitBufferSize)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public event MessageHandler? MessageReceived;

    public bool IsConnected => _connected && !_closed;

    public RegisterResponse RegisterResponse => _registerResponse;

    public async Task<RegisterResponse> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_closed || _finished != 0)
        {
            throw new ClientClosedException();
        }

        if (_connected)
        {
            return _registerResponse;
        }

        var stream = await PipeDialer.DialAsync(_options.PipePath, cancellationToken).ConfigureAwait(false);

        try
        {
            var request = PbConvert.ToPbManifest(_manifest);
            await DelimitedProtoCodec.WriteAsync(stream, request, cancellationToken).ConfigureAwait(false);

            var response = await DelimitedProtoCodec.ReadAsync(stream, Proto.PipeRegisterPluginResp.Parser, cancellationToken)
                .ConfigureAwait(false);
            var modelResponse = PbConvert.FromPbRegisterResponse(response);
            if (!response.Success)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(modelResponse.Message)
                    ? "register failed"
                    : modelResponse.Message);
            }

            lock (_streamLock)
            {
                _stream = stream;
            }

            _sessionCts = new CancellationTokenSource();
            _connected = true;
            _registerResponse = modelResponse;

            _readLoopTask = Task.Run(() => ReadLoopAsync(_sessionCts.Token));
            if (_options.HeartbeatInterval > TimeSpan.Zero)
            {
                _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_sessionCts.Token));
            }

            return modelResponse;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public async Task<Message> WaitMessageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _waitChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            throw new ClientClosedException();
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_closed)
        {
            await WaitBackgroundTasksAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (_connected)
        {
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(TimeSpan.FromSeconds(2));
                await SendAnyMessageAsync(
                    new Proto.PipeDeregisterPluginReq
                    {
                        Uuid = _manifest.Uuid,
                        Reason = "client-close",
                        Timestamp = PbConvert.NowTimestamp()
                    },
                    linkedCts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Best effort only.
            }
        }

        Finish();
        await WaitBackgroundTasksAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ServerInfo> QueryServerInfoAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Query,
            EnvelopeTopic.QueryServerInfo,
            null,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipeServerInfo>(response);
        return PbConvert.FromPbServerInfo(payload);
    }

    public async Task<PluginList> QueryConnectedPluginsAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Query,
            EnvelopeTopic.QueryConnectedPlugins,
            null,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipePluginList>(response);
        return PbConvert.FromPbPluginList(payload);
    }

    public async Task<RuntimeSnapshot> QueryRuntimeSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Query,
            EnvelopeTopic.QueryRuntimeSnapshot,
            null,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipeRuntimeSnapshot>(response);
        return PbConvert.FromPbRuntimeSnapshot(payload);
    }

    public async Task<RigSnapshot> QueryRigSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Query,
            EnvelopeTopic.QueryRigSnapshot,
            null,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipeRigStatusSnapshot>(response);
        return PbConvert.FromPbRigSnapshot(payload);
    }

    public async Task<UdpSnapshot> QueryUdpSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Query,
            EnvelopeTopic.QueryUdpSnapshot,
            null,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipeUdpStatusSnapshot>(response);
        return PbConvert.FromPbUdpSnapshot(payload);
    }

    public async Task<QsoQueueSnapshot> QueryQsoQueueSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Query,
            EnvelopeTopic.QueryQsoQueueSnapshot,
            null,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipeQsoQueueSnapshot>(response);
        return PbConvert.FromPbQsoQueueSnapshot(payload);
    }

    public async Task<SettingsSnapshot> QuerySettingsSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Query,
            EnvelopeTopic.QuerySettingsSnapshot,
            null,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipeMainSettingsSnapshot>(response);
        return PbConvert.FromPbSettingsSnapshot(payload);
    }

    public async Task<PluginTelemetry> QueryPluginTelemetryAsync(
        string? pluginUuid = null,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, string>? attributes = null;
        if (!string.IsNullOrWhiteSpace(pluginUuid))
        {
            attributes = new Dictionary<string, string>
            {
                ["plugin_uuid"] = pluginUuid
            };
        }

        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Query,
            EnvelopeTopic.QueryPluginTelemetry,
            attributes,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipePluginTelemetry>(response);
        return PbConvert.FromPbPluginTelemetry(payload);
    }

    public async Task<EventSubscription> SubscribeEventsAsync(
        EventSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var payload = PbConvert.ToPbEventSubscription(subscription);
        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            EnvelopeTopic.CommandSubscribeEvents,
            null,
            payload,
            subscription,
            cancellationToken).ConfigureAwait(false);

        var pbSub = DecodeResponsePayload<Proto.PipeEventSubscription>(response);
        return PbConvert.FromPbEventSubscription(pbSub);
    }

    public async Task ShowMainWindowAsync(CancellationToken cancellationToken = default)
    {
        await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            EnvelopeTopic.CommandShowMainWindow,
            null,
            null,
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task HideMainWindowAsync(CancellationToken cancellationToken = default)
    {
        await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            EnvelopeTopic.CommandHideMainWindow,
            null,
            null,
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task OpenWindowAsync(
        string window,
        bool asDialog,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(window))
        {
            throw new ArgumentException("window is required", nameof(window));
        }

        var attributes = new Dictionary<string, string>
        {
            ["window"] = window,
            ["asDialog"] = asDialog ? "true" : "false"
        };

        await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            EnvelopeTopic.CommandOpenWindow,
            attributes,
            null,
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SendNotificationAsync(
        NotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            EnvelopeTopic.CommandSendNotification,
            null,
            PbConvert.ToPbNotification(command),
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<UdpSnapshot> ToggleUdpServerAsync(
        bool? enabled = null,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, string>? attributes = null;
        if (enabled.HasValue)
        {
            attributes = new Dictionary<string, string>
            {
                ["enabled"] = enabled.Value ? "true" : "false"
            };
        }

        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            EnvelopeTopic.CommandToggleUdpServer,
            attributes,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipeUdpStatusSnapshot>(response);
        return PbConvert.FromPbUdpSnapshot(payload);
    }

    public async Task<RigSnapshot> ToggleRigBackendAsync(
        bool? enabled = null,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, string>? attributes = null;
        if (enabled.HasValue)
        {
            attributes = new Dictionary<string, string>
            {
                ["enabled"] = enabled.Value ? "true" : "false"
            };
        }

        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            EnvelopeTopic.CommandToggleRigBackend,
            attributes,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipeRigStatusSnapshot>(response);
        return PbConvert.FromPbRigSnapshot(payload);
    }

    public async Task<RigSnapshot> SwitchRigBackendAsync(
        string backend,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backend))
        {
            throw new ArgumentException("backend is required", nameof(backend));
        }

        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            EnvelopeTopic.CommandSwitchRigBackend,
            new Dictionary<string, string>
            {
                ["backend"] = backend
            },
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipeRigStatusSnapshot>(response);
        return PbConvert.FromPbRigSnapshot(payload);
    }

    public async Task<Envelope> UploadExternalQsoAsync(
        string adifLogs,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adifLogs))
        {
            throw new ArgumentException("adifLogs is required", nameof(adifLogs));
        }

        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            EnvelopeTopic.CommandUploadExternalQso,
            new Dictionary<string, string>
            {
                ["adifLogs"] = adifLogs
            },
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        return PbConvert.FromPbEnvelope(response);
    }

    public Task<RigSnapshot> StartRigBackendAsync(CancellationToken cancellationToken = default)
    {
        return ToggleRigBackendAsync(true, cancellationToken);
    }

    public Task<RigSnapshot> StopRigBackendAsync(CancellationToken cancellationToken = default)
    {
        return ToggleRigBackendAsync(false, cancellationToken);
    }

    public async Task<RigSnapshot> RestartRigBackendAsync(CancellationToken cancellationToken = default)
    {
        await StopRigBackendAsync(cancellationToken).ConfigureAwait(false);
        return await StartRigBackendAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Envelope> TriggerQsoReuploadAsync(
        Dictionary<string, string> attributes,
        CancellationToken cancellationToken = default)
    {
        if (attributes is null ||
            !attributes.TryGetValue("qsoIds", out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("qsoIds is required", nameof(attributes));
        }

        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            EnvelopeTopic.CommandTriggerQsoReupload,
            attributes,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        return PbConvert.FromPbEnvelope(response);
    }

    public Task<Envelope> TriggerQsoReuploadAsync(
        IEnumerable<string> qsoIds,
        CancellationToken cancellationToken = default)
    {
        if (qsoIds is null)
        {
            throw new ArgumentNullException(nameof(qsoIds));
        }

        var normalized = qsoIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("at least one qso id is required", nameof(qsoIds));
        }

        return TriggerQsoReuploadAsync(new Dictionary<string, string>
        {
            ["qsoIds"] = string.Join(";;;", normalized)
        }, cancellationToken);
    }

    public async Task<SettingsSnapshot> UpdateSettingsAsync(
        SettingsPatch patch,
        CancellationToken cancellationToken = default)
    {
        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            EnvelopeTopic.CommandUpdateSettings,
            null,
            PbConvert.ToPbSettingsPatch(patch),
            null,
            cancellationToken).ConfigureAwait(false);

        var payload = DecodeResponsePayload<Proto.PipeMainSettingsSnapshot>(response);
        return PbConvert.FromPbSettingsSnapshot(payload);
    }

    public async Task<Envelope> RawQueryAsync(
        EnvelopeTopic topic,
        Dictionary<string, string>? attributes = null,
        IMessage? payload = null,
        CancellationToken cancellationToken = default)
    {
        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Query,
            topic,
            attributes,
            payload,
            null,
            cancellationToken).ConfigureAwait(false);

        return PbConvert.FromPbEnvelope(response);
    }

    public async Task<Envelope> RawCommandAsync(
        EnvelopeTopic topic,
        Dictionary<string, string>? attributes = null,
        IMessage? payload = null,
        EventSubscription? subscription = null,
        CancellationToken cancellationToken = default)
    {
        var response = await RequestExpectSuccessAsync(
            EnvelopeKind.Command,
            topic,
            attributes,
            payload,
            subscription,
            cancellationToken).ConfigureAwait(false);

        return PbConvert.FromPbEnvelope(response);
    }

    public void Dispose()
    {
        CloseAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Any anyMessage;
                try
                {
                    var stream = GetStreamOrThrow();
                    anyMessage = await DelimitedProtoCodec.ReadAsync(stream, Any.Parser, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                var inbound = PbConvert.FromAnyMessage(anyMessage);
                if (inbound.ProtoMessage is Proto.PipeEnvelope response &&
                    response.Kind == Proto.PipeEnvelopeKind.Response)
                {
                    ResolvePending(response);
                }

                DispatchMessage(inbound.ModelMessage);

                if (inbound.ProtoMessage is Proto.PipeConnectionClosed)
                {
                    break;
                }
            }
        }
        finally
        {
            Finish();
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        if (_options.HeartbeatInterval <= TimeSpan.Zero)
        {
            return;
        }

        using var timer = new PeriodicTimer(_options.HeartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await SendAnyMessageAsync(new Proto.PipeHeartbeat
                    {
                        Uuid = _manifest.Uuid,
                        Timestamp = PbConvert.NowTimestamp()
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal when closing.
        }
    }

    private async Task SendAnyMessageAsync(IMessage message, CancellationToken cancellationToken)
    {
        if (_closed)
        {
            throw new ClientClosedException();
        }

        if (!_connected)
        {
            throw new NotConnectedException();
        }

        var stream = GetStreamOrThrow();
        var packed = Any.Pack(message);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DelimitedProtoCodec.WriteAsync(stream, packed, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<Proto.PipeEnvelope> RequestRawAsync(
        EnvelopeKind kind,
        EnvelopeTopic topic,
        Dictionary<string, string>? attributes,
        IMessage? payload,
        EventSubscription? subscription,
        CancellationToken cancellationToken)
    {
        if (_closed)
        {
            throw new ClientClosedException();
        }

        if (!_connected)
        {
            throw new NotConnectedException();
        }

        using var timeoutCts = _options.RequestTimeout > TimeSpan.Zero
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        if (timeoutCts is not null)
        {
            timeoutCts.CancelAfter(_options.RequestTimeout);
        }

        var requestToken = timeoutCts?.Token ?? cancellationToken;
        var requestId = NextRequestId();
        var request = new Proto.PipeEnvelope
        {
            Id = requestId,
            Kind = (Proto.PipeEnvelopeKind)kind,
            Topic = (Proto.PipeEnvelopeTopic)topic,
            Success = true,
            Message = "request",
            Timestamp = PbConvert.NowTimestamp()
        };

        if (attributes is not null)
        {
            foreach (var item in attributes)
            {
                request.Attributes[item.Key] = item.Value;
            }
        }

        if (subscription is not null)
        {
            request.Subscription = PbConvert.ToPbEventSubscription(subscription);
        }

        if (payload is not null)
        {
            request.Payload = Any.Pack(payload);
        }

        var responseSource = new TaskCompletionSource<Proto.PipeEnvelope?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(requestId, responseSource))
        {
            throw new InvalidOperationException("duplicate request id");
        }

        try
        {
            await SendAnyMessageAsync(request, requestToken).ConfigureAwait(false);
            var response = await responseSource.Task.WaitAsync(requestToken).ConfigureAwait(false);
            if (response is null)
            {
                throw new ClientClosedException();
            }

            return response;
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    private async Task<Proto.PipeEnvelope> RequestExpectSuccessAsync(
        EnvelopeKind kind,
        EnvelopeTopic topic,
        Dictionary<string, string>? attributes,
        IMessage? payload,
        EventSubscription? subscription,
        CancellationToken cancellationToken)
    {
        var response = await RequestRawAsync(kind, topic, attributes, payload, subscription, cancellationToken)
            .ConfigureAwait(false);
        if (!response.Success)
        {
            throw new RemoteError(topic, response.ErrorCode, response.Message, response.CorrelationId);
        }

        return response;
    }

    private static T DecodeResponsePayload<T>(Proto.PipeEnvelope response)
        where T : class, IMessage<T>, new()
    {
        if (response.Payload is null)
        {
            throw new InvalidDataException("response payload is empty");
        }

        return response.Payload.Unpack<T>();
    }

    private string NextRequestId()
    {
        var seq = Interlocked.Increment(ref _requestSequence);
        return $"{_manifest.Uuid}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{seq}";
    }

    private void ResolvePending(Proto.PipeEnvelope response)
    {
        var key = string.IsNullOrWhiteSpace(response.CorrelationId) ? response.Id : response.CorrelationId;
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (_pending.TryGetValue(key, out var source))
        {
            source.TrySetResult(response);
        }
    }

    private void RejectAllPending()
    {
        foreach (var item in _pending)
        {
            if (_pending.TryRemove(item.Key, out var source))
            {
                source.TrySetResult(null);
            }
        }
    }

    private void DispatchMessage(Message message)
    {
        var optionHandler = _options.OnMessage;
        if (optionHandler is not null)
        {
            _ = Task.Run(() => optionHandler(message));
        }

        var eventHandler = MessageReceived;
        if (eventHandler is not null)
        {
            _ = Task.Run(() => eventHandler(message));
        }

        _waitChannel.Writer.TryWrite(message);
    }

    private Stream GetStreamOrThrow()
    {
        lock (_streamLock)
        {
            return _stream ?? throw new NotConnectedException();
        }
    }

    private async Task WaitBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        if (_readLoopTask is not null)
        {
            tasks.Add(_readLoopTask);
        }

        if (_heartbeatTask is not null)
        {
            tasks.Add(_heartbeatTask);
        }

        if (tasks.Count == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Ignore background loop exceptions on shutdown.
        }
    }

    private void Finish()
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        _connected = false;
        _closed = true;

        try
        {
            _sessionCts?.Cancel();
        }
        catch
        {
            // Ignore cancellation race.
        }

        _sessionCts?.Dispose();
        _sessionCts = null;

        lock (_streamLock)
        {
            _stream?.Dispose();
            _stream = null;
        }

        RejectAllPending();
        _waitChannel.Writer.TryComplete();
    }

    private static void NormalizeManifest(PluginManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Uuid) ||
            string.IsNullOrWhiteSpace(manifest.Name) ||
            string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidManifestException("invalid plugin manifest: uuid/name/version are required");
        }

        manifest.Metadata ??= new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(manifest.SdkName))
        {
            manifest.SdkName = SdkDefaults.SdkName;
        }

        if (string.IsNullOrWhiteSpace(manifest.SdkVersion))
        {
            manifest.SdkVersion = SdkDefaults.SdkVersion;
        }
    }

    private static void ValidateOptions(ClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PipePath))
        {
            throw new ArgumentException("pipe path cannot be empty", nameof(options));
        }

        if (options.HeartbeatInterval < TimeSpan.Zero)
        {
            throw new ArgumentException("heartbeat interval cannot be negative", nameof(options));
        }

        if (options.RequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("request timeout must be positive", nameof(options));
        }

        if (options.WaitBufferSize <= 0)
        {
            throw new ArgumentException("wait buffer size must be greater than 0", nameof(options));
        }
    }
}


namespace ClhPluginSdk;

public enum EnvelopeKind
{
    Unspecified = 0,
    Event = 1,
    Query = 2,
    Command = 3,
    Response = 4
}

public enum EnvelopeTopic
{
    Unspecified = 0,
    EventServerStatus = 1,
    EventPluginLifecycle = 2,
    EventWsjtxMessage = 3,
    EventWsjtxDecodeRealtime = 4,
    EventWsjtxDecodeBatch = 5,
    EventRigData = 6,
    EventQsoUploadStatus = 7,
    EventQsoQueueStatus = 10,
    EventSettingsChanged = 11,
    EventPluginTelemetry = 12,
    QueryServerInfo = 100,
    QueryConnectedPlugins = 101,
    QueryRuntimeSnapshot = 102,
    QueryRigSnapshot = 103,
    QueryUdpSnapshot = 104,
    QueryQsoQueueSnapshot = 105,
    QuerySettingsSnapshot = 106,
    QueryPluginTelemetry = 107,
    CommandShowMainWindow = 200,
    CommandHideMainWindow = 201,
    CommandOpenWindow = 202,
    CommandSendNotification = 203,
    CommandToggleUdpServer = 204,
    CommandToggleRigBackend = 205,
    CommandSwitchRigBackend = 206,
    CommandUploadExternalQso = 207,
    CommandTriggerQsoReupload = 208,
    CommandUpdateSettings = 209,
    CommandSubscribeEvents = 210
}

public enum NotificationLevel
{
    Unspecified = 0,
    Info = 1,
    Success = 2,
    Warning = 3,
    Error = 4
}

public enum WsjtxMessageType
{
    Heartbeat = 0,
    Status = 1,
    Decode = 2,
    Clear = 3,
    Reply = 4,
    QsoLogged = 5,
    Close = 6,
    Replay = 7,
    HaltTx = 8,
    FreeText = 9,
    WsprDecode = 10,
    Location = 11,
    LoggedAdif = 12,
    HighlightCallsign = 13,
    SwitchConfiguration = 14,
    Configure = 15
}

public enum SpecialOperationMode
{
    None = 0,
    NaVhf = 1,
    EuVhf = 2,
    FieldDay = 3,
    RttyRu = 4,
    WwDigi = 5,
    Fox = 6,
    Hound = 7
}

public enum ClearWindow
{
    BandActivity = 0,
    RxFrequency = 1,
    Both = 2
}

public enum UploadStatus
{
    Unspecified = 0,
    Pending = 1,
    Uploading = 2,
    Success = 3,
    Fail = 4,
    Ignored = 5
}

public enum PluginLifecycleEventType
{
    Unspecified = 0,
    Connected = 1,
    Disconnected = 2,
    Timeout = 3,
    Replaced = 4
}

public enum ServiceRunStatus
{
    Unspecified = 0,
    Starting = 1,
    Running = 2,
    Stopped = 3,
    Error = 4
}

public static class RigBackendNames
{
    public const string Hamlib = "Hamlib";
    public const string FlRig = "FLRig";
    public const string OmniRig = "OmniRig";
}

public static class ControllableWindowNames
{
    public const string Settings = "SettingsWindow";
    public const string About = "AboutWindow";
    public const string QsoAssistant = "QSOAssistantWindow";
    public const string StationStats = "StationStatisticWindow";
    public const string PolarChart = "PolarChartWindow";
}

public sealed class PluginManifest
{
    public string Uuid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public EventSubscription? EventSubscription { get; set; }
    public string SdkName { get; set; } = string.Empty;
    public string SdkVersion { get; set; } = string.Empty;
}

public sealed class EventSubscription
{
    public List<EnvelopeTopic> Topics { get; set; } = new();
}

public sealed class NotificationCommand
{
    public NotificationLevel Level { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class SettingsPatch
{
    public Dictionary<string, string> Values { get; set; } = new();
}

public sealed class PluginTelemetry
{
    public string PluginUuid { get; set; } = string.Empty;
    public ulong ReceivedMessageCount { get; set; }
    public ulong SentMessageCount { get; set; }
    public ulong ControlRequestCount { get; set; }
    public ulong ControlErrorCount { get; set; }
    public uint LastRoundtripMs { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class RigSnapshot
{
    public string Provider { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public bool ServiceRunning { get; set; }
    public string RigModel { get; set; } = string.Empty;
    public ulong TxFrequencyHz { get; set; }
    public ulong RxFrequencyHz { get; set; }
    public string TxMode { get; set; } = string.Empty;
    public string RxMode { get; set; } = string.Empty;
    public bool Split { get; set; }
    public uint Power { get; set; }
    public DateTime SampledAtUtc { get; set; }
}

public sealed class UdpSnapshot
{
    public bool ServerRunning { get; set; }
    public string BindAddress { get; set; } = string.Empty;
    public DateTime SampledAtUtc { get; set; }
}

public sealed class QsoQueueSnapshot
{
    public List<QsoDetail> Details { get; set; } = new();
    public DateTime SampledAtUtc { get; set; }
}

public sealed class SettingsSnapshot
{
    public string InstanceName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public bool EnablePlugin { get; set; }
    public bool DisableAllCharts { get; set; }
    public string MyMaidenheadGrid { get; set; } = string.Empty;
    public bool AutoQsoUploadEnabled { get; set; }
    public bool AutoRigUploadEnabled { get; set; }
    public bool EnableUdpServer { get; set; }
    public DateTime SampledAtUtc { get; set; }
}

public sealed class RuntimeSnapshot
{
    public ServerInfo ServerInfo { get; set; } = new();
    public RigSnapshot RigSnapshot { get; set; } = new();
    public UdpSnapshot UdpSnapshot { get; set; } = new();
    public SettingsSnapshot SettingsSnapshot { get; set; } = new();
    public List<PluginTelemetry> PluginTelemetry { get; set; } = new();
    public DateTime SampledAtUtc { get; set; }
}

public sealed class PluginInfo
{
    public string Uuid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime RegisteredAtUtc { get; set; }
    public DateTime LastHeartbeatUtc { get; set; }
    public EventSubscription EventSubscription { get; set; } = new();
    public PluginTelemetry Telemetry { get; set; } = new();
}

public sealed class PluginList
{
    public List<PluginInfo> Plugins { get; set; } = new();
}

public sealed class ServerInfo
{
    public string InstanceId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public uint KeepaliveTimeoutSec { get; set; }
    public uint ConnectedPluginCount { get; set; }
    public ulong UptimeSec { get; set; }
}

public sealed class RegisterResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public ServerInfo ServerInfo { get; set; } = new();
    public DateTime TimestampUtc { get; set; }
}

public enum InboundKind
{
    Unknown,
    RigData,
    ClhInternal,
    Envelope,
    ConnectionClosed
}

public sealed class Message
{
    public InboundKind Kind { get; set; }
    public DateTime TimestampUtc { get; set; }
    public RigData? RigData { get; set; }
    public ClhInternalMessage? ClhInternal { get; set; }
    public Envelope? Envelope { get; set; }
    public ConnectionClosed? ConnectionClosed { get; set; }
    public UnknownMessage? Unknown { get; set; }
}

public sealed class UnknownMessage
{
    public string TypeUrl { get; set; } = string.Empty;
    public byte[] Raw { get; set; } = Array.Empty<byte>();
}

public sealed class ConnectionClosed
{
    public DateTime TimestampUtc { get; set; }
}

public sealed class Envelope
{
    public string Id { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public EnvelopeKind Kind { get; set; }
    public EnvelopeTopic Topic { get; set; }
    public bool Success { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public Dictionary<string, string> Attributes { get; set; } = new();
    public EventSubscription? Subscription { get; set; }
    public object? Payload { get; set; }
    public DateTime TimestampUtc { get; set; }
}

public sealed class RigData
{
    public string Uuid { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string RigName { get; set; } = string.Empty;
    public ulong Frequency { get; set; }
    public string Mode { get; set; } = string.Empty;
    public ulong FrequencyRx { get; set; }
    public string ModeRx { get; set; } = string.Empty;
    public bool Split { get; set; }
    public uint Power { get; set; }
    public DateTime TimestampUtc { get; set; }
}

public sealed class WsjtxMessage
{
    public WsjtxMessageHeader Header { get; set; } = new();
    public WsjtxHeartbeat? Heartbeat { get; set; }
    public WsjtxStatus? Status { get; set; }
    public WsjtxDecode? Decode { get; set; }
    public WsjtxClear? Clear { get; set; }
    public WsjtxReply? Reply { get; set; }
    public WsjtxQsoLogged? QsoLogged { get; set; }
    public WsjtxClose? Close { get; set; }
    public WsjtxHaltTx? HaltTx { get; set; }
    public WsjtxFreeText? FreeText { get; set; }
    public WsjtxWsprDecode? WsprDecode { get; set; }
    public WsjtxLocation? Location { get; set; }
    public WsjtxLoggedAdif? LoggedAdif { get; set; }
    public WsjtxHighlightCallsign? HighlightCallsign { get; set; }
    public WsjtxSwitchConfiguration? SwitchConfiguration { get; set; }
    public WsjtxConfigure? Configure { get; set; }
    public DateTime TimestampUtc { get; set; }
}

public sealed class WsjtxMessageHeader
{
    public uint MagicNumber { get; set; }
    public uint SchemaNumber { get; set; }
    public WsjtxMessageType Type { get; set; }
    public string Id { get; set; } = string.Empty;
}

public sealed class WsjtxHeartbeat
{
    public uint MaxSchemaNumber { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? Revision { get; set; }
}

public sealed class WsjtxStatus
{
    public ulong DialFrequency { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string DxCall { get; set; } = string.Empty;
    public string Report { get; set; } = string.Empty;
    public string TxMode { get; set; } = string.Empty;
    public bool TxEnabled { get; set; }
    public bool Transmitting { get; set; }
    public bool Decoding { get; set; }
    public uint RxDf { get; set; }
    public uint TxDf { get; set; }
    public string DeCall { get; set; } = string.Empty;
    public string DeGrid { get; set; } = string.Empty;
    public string DxGrid { get; set; } = string.Empty;
    public bool TxWatchdog { get; set; }
    public string SubMode { get; set; } = string.Empty;
    public bool FastMode { get; set; }
    public SpecialOperationMode? SpecialOpMode { get; set; }
    public uint? FrequencyTolerance { get; set; }
    public uint? TrPeriod { get; set; }
    public string? ConfigName { get; set; }
    public string? TxMessage { get; set; }
}

public sealed class WsjtxDecode
{
    public bool IsNew { get; set; }
    public DateTime TimeUtc { get; set; }
    public int Snr { get; set; }
    public double DeltaTime { get; set; }
    public uint DeltaFrequency { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public bool LowConfidence { get; set; }
    public bool OffAir { get; set; }
}

public sealed class WsjtxClear
{
    public ClearWindow Window { get; set; }
}

public sealed class WsjtxReply
{
    public DateTime TimeUtc { get; set; }
    public int Snr { get; set; }
    public double DeltaTime { get; set; }
    public uint DeltaFrequency { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public bool LowConfidence { get; set; }
    public uint Modifiers { get; set; }
}

public sealed class WsjtxQsoLogged
{
    public DateTime DateTimeOffUtc { get; set; }
    public string DxCall { get; set; } = string.Empty;
    public string DxGrid { get; set; } = string.Empty;
    public ulong TxFrequency { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string ReportSent { get; set; } = string.Empty;
    public string ReportReceived { get; set; } = string.Empty;
    public string TxPower { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public DateTime DateTimeOnUtc { get; set; }
    public string OperatorCall { get; set; } = string.Empty;
    public string MyCall { get; set; } = string.Empty;
    public string MyGrid { get; set; } = string.Empty;
    public string? ExchangeSent { get; set; }
    public string? ExchangeReceived { get; set; }
    public string? AdifPropagationMode { get; set; }
}

public sealed class WsjtxClose{}

public sealed class WsjtxHaltTx
{
    public bool AutoTxOnly { get; set; }
}

public sealed class WsjtxFreeText
{
    public string Text { get; set; } = string.Empty;
    public bool Send { get; set; }
}

public sealed class WsjtxWsprDecode
{
    public bool IsNew { get; set; }
    public DateTime TimeUtc { get; set; }
    public int Snr { get; set; }
    public double DeltaTime { get; set; }
    public ulong Frequency { get; set; }
    public int Drift { get; set; }
    public string Callsign { get; set; } = string.Empty;
    public string Grid { get; set; } = string.Empty;
    public int Power { get; set; }
    public bool? OffAir { get; set; }
}

public sealed class WsjtxLocation
{
    public string Location { get; set; } = string.Empty;
}

public sealed class WsjtxLoggedAdif
{
    public string AdifText { get; set; } = string.Empty;
}

public sealed class WsjtxHighlightCallsign
{
    public string Callsign { get; set; } = string.Empty;
    public uint BackgroundColor { get; set; }
    public uint ForegroundColor { get; set; }
    public bool HighlightLast { get; set; }
}

public sealed class WsjtxSwitchConfiguration
{
    public string ConfigName { get; set; } = string.Empty;
}

public sealed class WsjtxConfigure
{
    public string Mode { get; set; } = string.Empty;
    public uint FrequencyTolerance { get; set; }
    public string SubMode { get; set; } = string.Empty;
    public bool FastMode { get; set; }
    public uint TrPeriod { get; set; }
    public uint RxDf { get; set; }
    public string DxCall { get; set; } = string.Empty;
    public string DxGrid { get; set; } = string.Empty;
    public bool GenerateMessages { get; set; }
}

public sealed class PackedDecodeMessage
{
    public List<WsjtxDecode> Messages { get; set; } = new();
    public DateTime TimestampUtc { get; set; }
}

public sealed class ClhInternalMessage
{
    public QsoUploadStatusChanged? QsoUploadStatus { get; set; }
    public PluginLifecycleChanged? PluginLifecycle { get; set; }
    public ServerStatusChanged? ServerStatus { get; set; }
    public QsoQueueStatusChanged? QsoQueueStatus { get; set; }
    public SettingsChanged? SettingsChanged { get; set; }
    public PluginTelemetryChanged? PluginTelemetry { get; set; }
    public DateTime TimestampUtc { get; set; }
}

public sealed class QsoDetail
{
    public Dictionary<string, bool> UploadedServices { get; set; } = new();
    public Dictionary<string, string> UploadedServicesErrorMessage { get; set; } = new();
    public string OriginalCountryName { get; set; } = string.Empty;
    public int CqZone { get; set; }
    public int ItuZone { get; set; }
    public string Continent { get; set; } = string.Empty;
    public float Latitude { get; set; }
    public float Longitude { get; set; }
    public float GmtOffset { get; set; }
    public string Dxcc { get; set; } = string.Empty;
    public DateTime DateTimeOffUtc { get; set; }
    public string DxCall { get; set; } = string.Empty;
    public string DxGrid { get; set; } = string.Empty;
    public ulong TxFrequencyHz { get; set; }
    public string TxFrequencyMeters { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string ParentMode { get; set; } = string.Empty;
    public string ReportSent { get; set; } = string.Empty;
    public string ReportReceived { get; set; } = string.Empty;
    public string TxPower { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime DateTimeOnUtc { get; set; }
    public string OperatorCall { get; set; } = string.Empty;
    public string MyCall { get; set; } = string.Empty;
    public string MyGrid { get; set; } = string.Empty;
    public string ExchangeSent { get; set; } = string.Empty;
    public string ExchangeReceived { get; set; } = string.Empty;
    public string AdifPropagationMode { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string RawData { get; set; } = string.Empty;
    public string FailReason { get; set; } = string.Empty;
    public UploadStatus UploadStatus { get; set; }
    public bool ForcedUpload { get; set; }
    public string Uuid { get; set; } = string.Empty;
}

public sealed class QsoUploadStatusChanged
{
    public QsoDetail? Detail { get; set; }
}

public sealed class PluginLifecycleChanged
{
    public string PluginUuid { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string PluginVersion { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public PluginLifecycleEventType EventType { get; set; }
    public DateTime EventTimeUtc { get; set; }
}

public sealed class ServerStatusChanged
{
    public string InstanceId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public uint ConnectedPluginCount { get; set; }
    public DateTime EventTimeUtc { get; set; }
}

public sealed class QsoQueueStatusChanged
{
    public uint PendingCount { get; set; }
    public ulong UploadedTotal { get; set; }
    public ulong FailedTotal { get; set; }
    public DateTime EventTimeUtc { get; set; }
}

public sealed class SettingsChanged
{
    public string ChangedPart { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime EventTimeUtc { get; set; }
}

public sealed class PluginTelemetryChanged
{
    public string PluginUuid { get; set; } = string.Empty;
    public ulong ReceivedMessageCount { get; set; }
    public ulong SentMessageCount { get; set; }
    public ulong ControlRequestCount { get; set; }
    public ulong ControlErrorCount { get; set; }
    public uint LastRoundtripMs { get; set; }
    public DateTime EventTimeUtc { get; set; }
}

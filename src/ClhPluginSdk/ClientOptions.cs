namespace ClhPluginSdk;

public delegate void MessageHandler(Message message);

public static class SdkDefaults
{
    public const string SdkName = "clh-plugin-csharp-sdk";
    public const string SdkVersion = "v20260312";
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);
    public const int WaitBufferSize = 256;

    public static string DefaultPipePath => OperatingSystem.IsWindows()
        ? @"\\.\pipe\clh.plugin"
        : "/tmp/clh.plugin";
}

public sealed class ClientOptions
{
    public string PipePath { get; set; } = SdkDefaults.DefaultPipePath;
    public TimeSpan HeartbeatInterval { get; set; } = SdkDefaults.HeartbeatInterval;
    public TimeSpan RequestTimeout { get; set; } = SdkDefaults.RequestTimeout;
    public int WaitBufferSize { get; set; } = SdkDefaults.WaitBufferSize;
    public MessageHandler? OnMessage { get; set; }
}

namespace ClhPluginSdk;

public sealed class RemoteError : Exception
{
    public RemoteError(EnvelopeTopic topic, string code, string message, string correlationId)
        : base($"remote error topic={(int)topic} code={code} message={message} correlation_id={correlationId}")
    {
        Topic = topic;
        Code = code;
        MessageText = message;
        CorrelationId = correlationId;
    }

    public EnvelopeTopic Topic { get; }
    public string Code { get; }
    public string MessageText { get; }
    public string CorrelationId { get; }
}

public sealed class ClientClosedException : InvalidOperationException
{
    public ClientClosedException()
        : base("client is closed")
    {
    }
}

public sealed class NotConnectedException : InvalidOperationException
{
    public NotConnectedException()
        : base("client is not connected")
    {
    }
}

public sealed class InvalidManifestException : ArgumentException
{
    public InvalidManifestException(string message)
        : base(message)
    {
    }
}

using System.IO.Pipes;
using System.Net.Sockets;

namespace ClhPluginSdk;

internal static class PipeDialer
{
    public static async Task<Stream> DialAsync(string pipePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pipePath))
        {
            throw new ArgumentException("pipe path cannot be empty", nameof(pipePath));
        }

        if (OperatingSystem.IsWindows())
        {
            var pipeName = NormalizePipeName(pipePath);
            var stream = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await stream.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return stream;
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(pipePath), cancellationToken)
                .ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static string NormalizePipeName(string pipePath)
    {
        const string prefix = @"\\.\pipe\";
        if (pipePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return pipePath[prefix.Length..];
        }

        return pipePath;
    }
}

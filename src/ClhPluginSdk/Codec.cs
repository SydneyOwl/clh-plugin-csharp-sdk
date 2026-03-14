using Google.Protobuf;

namespace ClhPluginSdk;

internal static class DelimitedProtoCodec
{
    private const int MaxDelimitedMessageSize = 16 << 20;

    public static async Task WriteAsync(Stream stream, IMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);

        var payload = message.ToByteArray();
        var lengthBuffer = new byte[10];
        var lengthSize = WriteUVarint((ulong)payload.Length, lengthBuffer);

        await stream.WriteAsync(lengthBuffer.AsMemory(0, lengthSize), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<T> ReadAsync<T>(Stream stream, MessageParser<T> parser, CancellationToken cancellationToken)
        where T : class, IMessage<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(parser);

        var size = await ReadUVarintAsync(stream, cancellationToken).ConfigureAwait(false);
        if (size > MaxDelimitedMessageSize)
        {
            throw new InvalidDataException("delimited protobuf message too large");
        }

        var payload = new byte[size];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return parser.ParseFrom(payload);
    }

    private static int WriteUVarint(ulong value, Span<byte> output)
    {
        var index = 0;
        while (value >= 0x80)
        {
            output[index++] = (byte)(value | 0x80);
            value >>= 7;
        }

        output[index++] = (byte)value;
        return index;
    }

    private static async Task<ulong> ReadUVarintAsync(Stream stream, CancellationToken cancellationToken)
    {
        ulong result = 0;
        var shift = 0;
        var buffer = new byte[1];

        for (var i = 0; i < 10; i++)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            var current = buffer[0];
            result |= (ulong)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
        }

        throw new InvalidDataException("varint overflow");
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }
}

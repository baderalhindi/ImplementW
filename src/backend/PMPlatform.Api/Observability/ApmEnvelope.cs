using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PMPlatform.Application.Common.Observability;

namespace PMPlatform.Api.Observability;

/// <summary>
/// Redacts a Sentry-protocol envelope — the only thing the APM integration ever sends — with the <see cref="RedactionRegistry"/>.
/// An envelope is a header line, then items, each an item-header line and a payload that is either newline-terminated or
/// <c>length</c> bytes long. Every JSON payload (events, transactions, sessions, client reports) is redacted; an item whose payload
/// is not JSON cannot be checked and is dropped. The envelope header (event id, DSN, SDK) is passed on as it is.
/// </summary>
internal static class ApmEnvelope
{
    private const byte NewLine = (byte)'\n';

    public static byte[] Redact(ReadOnlySpan<byte> envelope)
    {
        using MemoryStream output = new(envelope.Length);
        int position = 0;
        output.Write(ReadLine(envelope, ref position));
        output.WriteByte(NewLine);

        while (position < envelope.Length)
        {
            ReadOnlySpan<byte> itemHeaderLine = ReadLine(envelope, ref position);
            if (itemHeaderLine.IsEmpty)
            {
                continue;
            }

            JsonObject itemHeader = JsonNode.Parse(itemHeaderLine) as JsonObject
                ?? throw new FormatException("An envelope item header is not a JSON object.");
            ReadOnlySpan<byte> payload = itemHeader["length"] is JsonValue length
                ? ReadExactly(envelope, ref position, length.GetValue<int>())
                : ReadLine(envelope, ref position);

            if (RedactPayload(payload) is not { } redacted)
            {
                continue;
            }

            if (itemHeader.ContainsKey("length"))
            {
                itemHeader["length"] = redacted.Length;
            }

            output.Write(Encoding.UTF8.GetBytes(itemHeader.ToJsonString()));
            output.WriteByte(NewLine);
            output.Write(redacted);
            output.WriteByte(NewLine);
        }

        return output.ToArray();
    }

    private static byte[]? RedactPayload(ReadOnlySpan<byte> payload)
    {
        JsonNode? document;
        try
        {
            document = JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            return null;
        }

        return Encoding.UTF8.GetBytes(TelemetryRedactor.Redact(document)?.ToJsonString() ?? "null");
    }

    private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> envelope, ref int position)
    {
        int end = envelope[position..].IndexOf(NewLine);
        ReadOnlySpan<byte> line = end < 0 ? envelope[position..] : envelope.Slice(position, end);
        position += end < 0 ? line.Length : end + 1;
        return line;
    }

    private static ReadOnlySpan<byte> ReadExactly(ReadOnlySpan<byte> envelope, ref int position, int length)
    {
        ReadOnlySpan<byte> payload = envelope.Slice(position, length);
        position += length;
        if (position < envelope.Length && envelope[position] == NewLine)
        {
            position++;
        }

        return payload;
    }
}

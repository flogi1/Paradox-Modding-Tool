using System.Buffers.Binary;

namespace ParadoxModTool.SteamIntegration;

/// <summary>
/// Liest die Ergebnis-Strukturen der Steamworks-Aufrufe aus einem Byte-Puffer. Die Ausrichtung (Packing) von <c>CreateItemResult_t</c> hängt von der Steamworks-Version ab
/// (ID bei Byte 4 oder 8); es gilt die erste Anordnung mit plausibler ID (größer 0, kleiner 2^40).
/// </summary>
public static class SteamworksResults
{
    public const int CreateItemCallbackId = 3403;
    public const int SubmitItemUpdateCallbackId = 3404;
    public const int BufferSize = 64;

    private const ulong MaxPlausibleId = 1UL << 40;

    public static CreateItemResult? ParseCreateItem(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 5)
        {
            return null;
        }

        var result = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        if (result != 1)
        {
            return new CreateItemResult(result, 0, false);
        }

        if (TryReadId(buffer, 4, out var id))
        {
            return new CreateItemResult(result, id, buffer.Length > 12 && buffer[12] != 0);
        }

        if (TryReadId(buffer, 8, out id))
        {
            return new CreateItemResult(result, id, buffer.Length > 16 && buffer[16] != 0);
        }

        return null;
    }

    public static SubmitItemResult? ParseSubmitItem(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 5)
        {
            return null;
        }

        var result = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        var needsAgreement = buffer[4] != 0;
        var id = buffer.Length >= 16 ? BinaryPrimitives.ReadUInt64LittleEndian(buffer[8..]) : 0UL;
        return new SubmitItemResult(result, id, needsAgreement);
    }

    private static bool TryReadId(ReadOnlySpan<byte> buffer, int offset, out ulong id)
    {
        id = 0;
        if (buffer.Length < offset + 8)
        {
            return false;
        }

        id = BinaryPrimitives.ReadUInt64LittleEndian(buffer[offset..]);
        return id != 0 && id < MaxPlausibleId;
    }
}

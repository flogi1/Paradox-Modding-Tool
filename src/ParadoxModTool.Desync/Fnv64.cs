namespace ParadoxModTool.Desync;

/// <summary>64-Bit-FNV-1a über eine Bytefolge; <see cref="Create"/> beginnt mit der Offset-Basis, <c>default</c> nicht.</summary>
public struct Fnv64
{
    private const ulong OffsetBasis = 14695981039346656037;
    private const ulong Prime = 1099511628211;

    private ulong _value;

    public readonly ulong Value => _value;

    public static Fnv64 Create() => new() { _value = OffsetBasis };

    public void Append(ReadOnlySpan<byte> data)
    {
        var hash = _value;
        foreach (var b in data)
        {
            hash ^= b;
            hash *= Prime;
        }

        _value = hash;
    }

    /// <summary>Hängt die acht Bytes des Werts an, niedrigstes zuerst.</summary>
    public void Append(ulong value)
    {
        var hash = _value;
        for (var i = 0; i < sizeof(ulong); i++)
        {
            hash ^= (byte)value;
            hash *= Prime;
            value >>= 8;
        }

        _value = hash;
    }
}

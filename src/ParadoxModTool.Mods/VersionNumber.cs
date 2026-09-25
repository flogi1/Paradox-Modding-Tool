namespace ParadoxModTool.Mods;

/// <summary>
/// Eine Release-Nummer wie <c>2.0.5</c> oder <c>1.2.0.1</c>. Verglichen wird zahlenweise (<c>2.0.10</c> ist neuer als <c>2.0.9</c>); fehlende Teile zählen als 0,
/// also sind <c>1.0</c> und <c>1.0.0</c> gleich. <see cref="Text"/> behält die Schreibweise, wie sie im Changelog oder in den Metadaten steht.
/// </summary>
public readonly struct VersionNumber : IComparable<VersionNumber>, IEquatable<VersionNumber>
{
    private readonly int[]? _parts;

    private VersionNumber(string text, int[] parts)
    {
        Text = text;
        _parts = parts;
    }

    /// <summary>Die Nummer, wie sie geschrieben wurde (ohne umgebende Leerzeichen).</summary>
    public string Text { get; }

    public static bool TryParse(string? text, out VersionNumber version)
    {
        version = default;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        var segments = trimmed.Split('.');
        var parts = new int[segments.Length];
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length == 0 || !segments[i].All(char.IsAsciiDigit) || !int.TryParse(segments[i], out parts[i]))
            {
                return false;
            }
        }

        version = new VersionNumber(trimmed, parts);
        return true;
    }

    public int CompareTo(VersionNumber other)
    {
        var a = _parts ?? Array.Empty<int>();
        var b = other._parts ?? Array.Empty<int>();
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var left = i < a.Length ? a[i] : 0;
            var right = i < b.Length ? b[i] : 0;
            if (left != right)
            {
                return left.CompareTo(right);
            }
        }

        return 0;
    }

    public bool Equals(VersionNumber other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is VersionNumber other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        var parts = _parts ?? Array.Empty<int>();
        var length = parts.Length;
        while (length > 0 && parts[length - 1] == 0)
        {
            length--;
        }

        for (var i = 0; i < length; i++)
        {
            hash.Add(parts[i]);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => Text ?? string.Empty;
}

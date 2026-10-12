using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Die wirksame Fassung einer Adresse aus Sicht einer Seite.</summary>
public sealed class Effective
{
    public static readonly Effective Absent = new();

    /// <summary>Einträge der obersten Ebene (Schlüssel oder Dateirest). Leer = nicht vorhanden.</summary>
    public IReadOnlyList<ScriptEntry> Entries { get; init; } = Array.Empty<ScriptEntry>();

    /// <summary>Nur bei <see cref="AddressKind.WholeFile"/>: SHA-256 der Datei.</summary>
    public string? FileHash { get; init; }

    /// <summary>Datei, aus der die wirksame Fassung stammt (bei mehreren Beiträgen der zuletzt angewendete).</summary>
    public string? RelativePath { get; init; }

    public string? FullPath { get; init; }

    public MergeSide? Side { get; init; }

    public bool Uncertain { get; init; }

    /// <summary>Operatoren der Beiträge, die in diese Fassung eingeflossen sind (einfache Definition = None).</summary>
    public IReadOnlyList<OverrideOperator> Operators { get; init; } = Array.Empty<OverrideOperator>();

    public bool Exists => Entries.Count > 0 || FileHash is not null;
}

public sealed class AddressInfo
{
    public required EntryAddress Address { get; init; }

    public required OverrideBehavior Behavior { get; init; }

    public required bool Certain { get; init; }

    /// <summary>Präfixe, die im Ordner wirken (aus der Regel); null = die des Spiels (<see cref="CompatchInput.KeyOperators"/>).</summary>
    public IReadOnlyList<string>? Operators { get; init; }

    /// <summary>Hinweis für die Anzeige, z. B. "nicht lesbar" oder "keine Skriptdatei".</summary>
    public string? Note { get; init; }

    public Effective Vanilla { get; init; } = Effective.Absent;

    public Effective A { get; init; } = Effective.Absent;

    public Effective B { get; init; } = Effective.Absent;

    /// <summary>Was das Spiel mit Vanilla + A + B ohne Kompatch lädt.</summary>
    public Effective NoPatch { get; init; } = Effective.Absent;

    public bool Uncertain => !Certain || A.Uncertain || B.Uncertain || NoPatch.Uncertain;
}

public sealed record CompatchUnreadable(MergeSide Side, string RelativePath, string Error);

public sealed class ResolvedEntries
{
    public Dictionary<EntryAddress, AddressInfo> Addresses { get; } = new();

    public List<SkippedFile> Skipped { get; } = new();

    public List<CompatchUnreadable> Unreadable { get; } = new();

    /// <summary>Je relativem Pfad die Datei, die das Spiel ohne Patch lädt (höchster Rang).</summary>
    public Dictionary<string, (MergeSide Side, string FullPath)> NoPatchFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool BLoadsAfterA { get; init; } = true;
}

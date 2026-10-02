using System.Text.Json;

namespace ParadoxModTool.GameProfiles;

/// <summary>Lädt ein <see cref="GameProfile"/> aus einer JSON-Datei bzw. -Zeichenkette.</summary>
public static class GameProfileLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static GameProfile LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    public static GameProfile LoadFromJson(string json)
    {
        var profile = JsonSerializer.Deserialize<GameProfile>(json, Options);
        return profile ?? throw new InvalidDataException(GameProfileTexts.EmptyResult);
    }
}

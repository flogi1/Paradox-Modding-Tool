using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.GuiDesign.Blocks;

/// <summary>
/// Liest eine <c>*.guiblock</c>-Datei: Kopf aus Kommentarzeilen, danach der Rumpf als GUI-Code mit <c>$schlüssel$</c>, <c>$repeat x$</c> … <c>$end x$</c>
/// und <c>$$</c>. Wirft nicht, sondern sammelt Fehler mit Zeilennummer; ein Baustein mit Fehlern lässt sich nicht einfügen.
/// </summary>
public static class GuiBlockParser
{
    public const string IndexKey = "index";

    private static readonly Regex HeaderLine = new(@"^\s*#\s*(block|group|description|param|repeat|requires)\s*:\s*(.*)$", RegexOptions.CultureInvariant);

    private static readonly Regex KeyPattern = new(@"^[a-z0-9_]+(\.[a-z0-9_]+)*$", RegexOptions.CultureInvariant);

    private static readonly Regex MarkerOnlyLine = new(@"^\s*\$(repeat|end) ([^$\s]+)\$\s*$", RegexOptions.CultureInvariant);

    private static readonly Regex Marker = new(@"^(repeat|end) (.+)$", RegexOptions.CultureInvariant);

    public static GuiBlock Parse(string text, GuiBlockLayer layer, string? sourcePath)
    {
        if (text.StartsWith('﻿'))
        {
            text = text[1..];
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var state = new ParseState();
        var bodyStart = ReadHeader(lines, state);
        if (state.Name is null)
        {
            state.Error(1, GuiBlockErrorKind.MissingBlock, string.Empty);
        }

        var body = ReadBody(lines, bodyStart, state);
        var name = state.Name ?? (sourcePath is null ? "?" : Path.GetFileNameWithoutExtension(sourcePath));
        return new GuiBlock(
            name,
            state.Group ?? GuiTexts.BlocksDefaultGroup,
            state.Description ?? string.Empty,
            state.BuildParameters(string.Empty),
            state.BuildRepeats(string.Empty),
            state.Requires,
            body,
            layer,
            sourcePath,
            state.Errors);
    }

    /// <summary>Liest die Kopfzeilen und gibt den Index der ersten Rumpfzeile zurück.</summary>
    private static int ReadHeader(string[] lines, ParseState state)
    {
        var first = true;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0)
            {
                continue;
            }

            var match = HeaderLine.Match(lines[i]);
            if (!match.Success)
            {
                return i;
            }

            var line = i + 1;
            var value = match.Groups[2].Value.Trim();
            switch (match.Groups[1].Value)
            {
                case "block":
                    if (state.Name is not null)
                    {
                        state.Error(line, GuiBlockErrorKind.DuplicateBlock, value);
                    }
                    else
                    {
                        if (!first)
                        {
                            state.Error(line, GuiBlockErrorKind.BlockNotFirst, value);
                        }

                        state.Name = value;
                    }

                    break;
                case "group":
                    state.Group = value.Length == 0 ? null : value;
                    break;
                case "description":
                    state.Description = value;
                    break;
                case "param":
                    ReadParameter(value, line, state);
                    break;
                case "repeat":
                    ReadRepeat(value, line, state);
                    break;
                case "requires":
                    state.Requires.AddRange(value.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0));
                    break;
            }

            first = false;
        }

        return lines.Length;
    }

    private static void ReadParameter(string value, int line, ParseState state)
    {
        var fields = value.Split('|').Select(f => f.Trim()).ToArray();
        if (fields.Length is < 3 or > 4)
        {
            state.Error(line, GuiBlockErrorKind.InvalidHeader, value);
            return;
        }

        var kind = GuiBlockParameterKind.Text;
        if (fields.Length == 4 && !TryParseKind(fields[3], out kind))
        {
            state.Error(line, GuiBlockErrorKind.InvalidHeader, fields[3]);
            return;
        }

        if (!state.TryDeclare(fields[0], line))
        {
            return;
        }

        var parent = ParentOf(fields[0]);
        var parts = ParseInline(fields[2], line, state, placeholder => CheckDefaultReference(placeholder, parent, line, state));
        state.Parameters.Add(new GuiBlockParameter(fields[0], fields[1], fields[2], kind, line) { DefaultParts = parts });
    }

    private static void ReadRepeat(string value, int line, ParseState state)
    {
        var fields = value.Split('|').Select(f => f.Trim()).ToArray();
        if (fields.Length != 3 || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            state.Error(line, GuiBlockErrorKind.InvalidHeader, value);
            return;
        }

        if (state.TryDeclare(fields[0], line))
        {
            state.RepeatHeaders.Add((fields[0], fields[1], count, line));
        }
    }

    private static bool TryParseKind(string text, out GuiBlockParameterKind kind)
    {
        switch (text.ToLowerInvariant())
        {
            case "text":
                kind = GuiBlockParameterKind.Text;
                return true;
            case "modifier":
                kind = GuiBlockParameterKind.Modifier;
                return true;
            default:
                kind = GuiBlockParameterKind.Text;
                return false;
        }
    }

    /// <summary>Vorgaben sehen nur früher deklarierte Parameter ihrer Ebene und der äußeren Ebenen, dazu <c>$index$</c> innerhalb einer Wiederholung.</summary>
    private static void CheckDefaultReference(GuiBlockPlaceholder placeholder, string parent, int line, ParseState state)
    {
        if (placeholder.Key == IndexKey)
        {
            if (parent.Length == 0)
            {
                state.Error(line, GuiBlockErrorKind.OutsideRepeat, placeholder.Key);
            }

            return;
        }

        var known = state.Parameters.FirstOrDefault(p => p.Key == placeholder.Key);
        if (known is null)
        {
            state.Error(line, GuiBlockErrorKind.UnknownPlaceholder, placeholder.Key);
        }
        else if (!IsSameOrAncestor(ParentOf(known.Key), parent))
        {
            state.Error(line, GuiBlockErrorKind.OutsideRepeat, placeholder.Key);
        }
    }

    private static List<GuiBlockPart> ReadBody(string[] lines, int start, ParseState state)
    {
        var root = new Frame(string.Empty, 0);
        var stack = new Stack<Frame>();
        stack.Push(root);
        for (var i = start; i < lines.Length; i++)
        {
            var line = i + 1;
            var content = lines[i];
            var newline = i < lines.Length - 1 ? "\n" : string.Empty;
            if (MarkerOnlyLine.Match(content) is { Success: true } marker)
            {
                HandleMarker(marker.Groups[1].Value, marker.Groups[2].Value, line, stack, state);
                continue;
            }

            ScanLine(content + newline, line, stack, state);
        }

        while (stack.Count > 1)
        {
            var open = stack.Pop();
            state.Error(open.Line, GuiBlockErrorKind.MissingEnd, open.Key);
            stack.Peek().Add(open.ToSection());
        }

        root.Flush();
        return root.Parts;
    }

    private static void ScanLine(string text, int line, Stack<Frame> stack, ParseState state)
    {
        foreach (var token in Tokenize(text))
        {
            switch (token.Kind)
            {
                case GuiBlockTokenKind.Text:
                    stack.Peek().Text.Append(token.Value);
                    break;
                case GuiBlockTokenKind.Placeholder:
                    var placeholder = new GuiBlockPlaceholder(token.Value, line);
                    CheckBodyReference(placeholder, stack, state);
                    stack.Peek().Add(placeholder);
                    break;
                case GuiBlockTokenKind.RepeatStart:
                    HandleMarker("repeat", token.Value, line, stack, state);
                    break;
                case GuiBlockTokenKind.RepeatEnd:
                    HandleMarker("end", token.Value, line, stack, state);
                    break;
                case GuiBlockTokenKind.InvalidKey:
                    state.Error(line, GuiBlockErrorKind.InvalidKey, token.Value);
                    break;
                case GuiBlockTokenKind.LoneDollar:
                    state.Error(line, GuiBlockErrorKind.LoneDollar, text.TrimEnd('\n'));
                    stack.Peek().Text.Append(token.Value);
                    break;
            }
        }
    }

    /// <summary>
    /// Zerlegt eine Rumpfzeile (mit ihrem Zeilenumbruch) in Text (<c>$$</c> schon aufgelöst), Platzhalter und Wiederholungsmarken. Ein <c>$</c> ohne
    /// schließendes <c>$</c> in derselben Zeile ergibt <see cref="GuiBlockTokenKind.LoneDollar"/> mit dem Rest der Zeile und beendet die Zeile.
    /// </summary>
    internal static IEnumerable<GuiBlockToken> Tokenize(string text)
    {
        var plain = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c != '$')
            {
                plain.Append(c);
                i++;
                continue;
            }

            if (i + 1 < text.Length && text[i + 1] == '$')
            {
                plain.Append('$');
                i += 2;
                continue;
            }

            if (plain.Length > 0)
            {
                yield return new GuiBlockToken(GuiBlockTokenKind.Text, plain.ToString());
                plain.Clear();
            }

            var close = text.IndexOf('$', i + 1);
            var newlineAt = text.IndexOf('\n', i + 1);
            if (close < 0 || (newlineAt >= 0 && newlineAt < close))
            {
                yield return new GuiBlockToken(GuiBlockTokenKind.LoneDollar, text[i..]);
                yield break;
            }

            var inner = text[(i + 1)..close];
            if (Marker.Match(inner) is { Success: true } marker)
            {
                var kind = marker.Groups[1].Value == "repeat" ? GuiBlockTokenKind.RepeatStart : GuiBlockTokenKind.RepeatEnd;
                yield return new GuiBlockToken(kind, marker.Groups[2].Value);
            }
            else if (KeyPattern.IsMatch(inner))
            {
                yield return new GuiBlockToken(GuiBlockTokenKind.Placeholder, inner);
            }
            else
            {
                yield return new GuiBlockToken(GuiBlockTokenKind.InvalidKey, inner);
            }

            i = close + 1;
        }

        if (plain.Length > 0)
        {
            yield return new GuiBlockToken(GuiBlockTokenKind.Text, plain.ToString());
        }
    }

    private static void HandleMarker(string kind, string key, int line, Stack<Frame> stack, ParseState state)
    {
        if (kind == "repeat")
        {
            if (!state.RepeatHeaders.Any(r => r.Key == key))
            {
                state.Error(line, GuiBlockErrorKind.UnknownPlaceholder, key);
            }
            else
            {
                var parent = ParentOf(key);
                var enclosing = stack.Select(f => f.Key).ToList();
                if ((parent.Length > 0 && !enclosing.Contains(parent)) || enclosing.Contains(key))
                {
                    state.Error(line, GuiBlockErrorKind.OutsideRepeat, key);
                }
            }

            stack.Peek().Flush();
            stack.Push(new Frame(key, line));
            return;
        }

        if (stack.Count == 1 || stack.Peek().Key != key)
        {
            state.Error(line, GuiBlockErrorKind.MismatchedEnd, key);
            return;
        }

        var section = stack.Pop();
        stack.Peek().Add(section.ToSection());
    }

    private static void CheckBodyReference(GuiBlockPlaceholder placeholder, Stack<Frame> stack, ParseState state)
    {
        var enclosing = stack.Where(f => f.Key.Length > 0).Select(f => f.Key).ToList();
        if (placeholder.Key == IndexKey)
        {
            if (enclosing.Count == 0)
            {
                state.Error(placeholder.Line, GuiBlockErrorKind.OutsideRepeat, placeholder.Key);
            }

            return;
        }

        var known = state.Parameters.FirstOrDefault(p => p.Key == placeholder.Key);
        if (known is null)
        {
            state.Error(placeholder.Line, GuiBlockErrorKind.UnknownPlaceholder, placeholder.Key);
            return;
        }

        var parent = ParentOf(known.Key);
        if (parent.Length > 0 && !enclosing.Contains(parent))
        {
            state.Error(placeholder.Line, GuiBlockErrorKind.OutsideRepeat, placeholder.Key);
        }
    }

    /// <summary>Zerlegt eine Vorgabe (eine Zeile) in Text und Platzhalter.</summary>
    private static List<GuiBlockPart> ParseInline(string text, int line, ParseState state, Action<GuiBlockPlaceholder> check)
    {
        var parts = new List<GuiBlockPart>();
        var builder = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] != '$')
            {
                builder.Append(text[i++]);
                continue;
            }

            if (i + 1 < text.Length && text[i + 1] == '$')
            {
                builder.Append('$');
                i += 2;
                continue;
            }

            var close = text.IndexOf('$', i + 1);
            if (close < 0)
            {
                state.Error(line, GuiBlockErrorKind.LoneDollar, text);
                builder.Append(text, i, text.Length - i);
                break;
            }

            var inner = text[(i + 1)..close];
            if (KeyPattern.IsMatch(inner))
            {
                if (builder.Length > 0)
                {
                    parts.Add(new GuiBlockText(builder.ToString()));
                    builder.Clear();
                }

                var placeholder = new GuiBlockPlaceholder(inner, line);
                check(placeholder);
                parts.Add(placeholder);
            }
            else
            {
                state.Error(line, GuiBlockErrorKind.InvalidKey, inner);
            }

            i = close + 1;
        }

        if (builder.Length > 0)
        {
            parts.Add(new GuiBlockText(builder.ToString()));
        }

        return parts;
    }

    internal static string ParentOf(string key)
    {
        var dot = key.LastIndexOf('.');
        return dot < 0 ? string.Empty : key[..dot];
    }

    private static bool IsSameOrAncestor(string candidate, string key) =>
        candidate.Length == 0 || candidate == key || key.StartsWith(candidate + ".", StringComparison.Ordinal);

    private sealed class Frame
    {
        public Frame(string key, int line)
        {
            Key = key;
            Line = line;
        }

        public string Key { get; }

        public int Line { get; }

        public StringBuilder Text { get; } = new();

        public List<GuiBlockPart> Parts { get; } = new();

        public void Add(GuiBlockPart part)
        {
            Flush();
            Parts.Add(part);
        }

        public void Flush()
        {
            if (Text.Length > 0)
            {
                Parts.Add(new GuiBlockText(Text.ToString()));
                Text.Clear();
            }
        }

        public GuiBlockSection ToSection()
        {
            Flush();
            return new GuiBlockSection(Key, Parts, Line);
        }
    }

    private sealed class ParseState
    {
        public string? Name { get; set; }

        public string? Group { get; set; }

        public string? Description { get; set; }

        public List<string> Requires { get; } = new();

        public List<GuiBlockParameter> Parameters { get; } = new();

        public List<(string Key, string Label, int Count, int Line)> RepeatHeaders { get; } = new();

        public List<GuiBlockError> Errors { get; } = new();

        public void Error(int line, GuiBlockErrorKind kind, string detail) => Errors.Add(new GuiBlockError(line, kind, detail));

        /// <summary>Prüft Schlüssel, äußere Wiederholung und Eindeutigkeit.</summary>
        public bool TryDeclare(string key, int line)
        {
            if (!KeyPattern.IsMatch(key) || key.Split('.').Contains(IndexKey))
            {
                Error(line, GuiBlockErrorKind.InvalidKey, key);
                return false;
            }

            var parent = ParentOf(key);
            if (parent.Length > 0 && !RepeatHeaders.Any(r => r.Key == parent))
            {
                Error(line, GuiBlockErrorKind.UndeclaredParent, key);
                return false;
            }

            if (Parameters.Any(p => p.Key == key) || RepeatHeaders.Any(r => r.Key == key))
            {
                Error(line, GuiBlockErrorKind.DuplicateKey, key);
                return false;
            }

            return true;
        }

        public List<GuiBlockParameter> BuildParameters(string parent) =>
            Parameters.Where(p => ParentOf(p.Key) == parent).ToList();

        public List<GuiBlockRepeat> BuildRepeats(string parent) =>
            RepeatHeaders
                .Where(r => ParentOf(r.Key) == parent)
                .Select(r => new GuiBlockRepeat(r.Key, r.Label, r.Count, BuildParameters(r.Key), BuildRepeats(r.Key), r.Line))
                .ToList();
    }
}

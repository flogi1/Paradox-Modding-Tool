using System.Text;
using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class LocalizationProfileLearnerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-loclearn-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void Fixture()
    {
        var names = Enumerable.Range(1, 10).Select(i => $"building_{i:00}").ToList();
        Write("in_game/common/buildings/a.txt", string.Join("\n", names.Select(n => $"{n} = {{ cost = 1 }}")));
        Write("in_game/common/buildings/readme.txt", "ignored_thing = { }");

        var loc = new StringBuilder("l_english:\n");
        foreach (var n in names)
        {
            loc.Append($" {n}: \"B\"\n {n}_desc: \"D\"\n");
        }

        for (var i = 1; i <= 20; i++)
        {
            loc.Append($" guikey_{i}: \"G\"\n");
        }

        Write("main_menu/localization/english/b_l_english.yml", loc.ToString());
        Write("main_menu/localization/german/b_l_german.yml", "l_german:\n building_01: \"x\"\n");
        Write(
            "in_game/gui/w.gui",
            string.Join("\n", Enumerable.Range(1, 20).Select(i => $"text = \"guikey_{i}\"")) + "\n" + string.Join("\n", Enumerable.Range(1, 6).Select(i => $"text = \"free text {i}\"")));
    }

    [Fact]
    public void Keys_are_read_from_localization_lines()
    {
        const string text = "﻿l_english:\n # comment\n key_a: \"x\"\n key_b:0 \"y\"\n  key.c-d: \"z\"\n key_e: nothing\nnot_indented: \"q\"\n";

        Assert.Equal(new[] { "key_a", "key_b", "key.c-d" }, LocalizationKeyReader.ReadKeys(text));
    }

    [Fact]
    public void Script_files_are_named_by_their_folder_without_in_game()
    {
        Fixture();
        Write("common/laws/l.txt", "law_one = { }");
        Write("in_game/events/e.txt", "namespace = x");

        var folders = LocalizationProfileLearner.EnumerateScriptFiles(new[] { _dir }).Select(f => f.Folder).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();

        Assert.Equal(new[] { "common/buildings", "common/laws", "events", "gui" }, folders);
        Assert.DoesNotContain(LocalizationProfileLearner.EnumerateScriptFiles(new[] { _dir }), f => f.Path.EndsWith("readme.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Only_localization_files_of_the_language_are_listed()
    {
        Fixture();
        Write("in_game/localization/english/a_l_english.yml", "l_english:\n");
        Write("dlc/d001/main_menu/localization/english/d_l_english.yml", "l_english:\n");

        var files = LocalizationProfileLearner.EnumerateLocalizationFiles(new[] { _dir }, "english");

        Assert.Equal(3, files.Count);
        Assert.All(files, f => Assert.EndsWith("_l_english.yml", f.Path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Learning_finds_object_and_field_profiles()
    {
        Fixture();
        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

        var result = LocalizationProfileLearner.Learn(new[] { _dir }, "english", now);

        var profiles = result.Profiles;
        Assert.Equal(now, profiles.LearnedUtc);
        Assert.NotEmpty(profiles.Fingerprint);
        Assert.Equal(3, profiles.FilesScanned);
        Assert.Equal(new[] { "common/buildings", "gui" }, profiles.Profiles.Select(p => p.Path));

        var buildings = profiles.Profiles[0];
        Assert.Equal(10, buildings.ObjectCount);
        Assert.Equal(new[] { "", "_desc" }, buildings.Objects.Select(o => o.Suffix));
        Assert.Empty(buildings.Fields);

        var gui = profiles.Profiles[1];
        Assert.Equal(0, gui.ObjectCount);
        Assert.Empty(gui.Objects);
        var text = Assert.Single(gui.Fields);
        Assert.Equal("text", text.Name);
        Assert.Equal(20, text.Hits);
        Assert.False(text.Preselected);
    }

    [Fact]
    public void Learn_without_roots_gives_no_profiles()
    {
        var none = LocalizationProfileLearner.Learn(Array.Empty<string>(), "english", DateTime.UtcNow);
        var missing = LocalizationProfileLearner.Learn(new[] { Path.Combine(_dir, "gibt-es-nicht") }, "english", DateTime.UtcNow);

        Assert.Empty(none.Profiles.Profiles);
        Assert.Empty(missing.Profiles.Profiles);
    }

    [Fact]
    public void The_fingerprint_changes_when_a_file_changes_and_stays_when_nothing_does()
    {
        Fixture();
        var before = LocalizationProfileLearner.ComputeFingerprint(new[] { _dir }, "english");
        var again = LocalizationProfileLearner.ComputeFingerprint(new[] { _dir }, "english");

        Write("in_game/common/buildings/a.txt", "changed = { }");
        var after = LocalizationProfileLearner.ComputeFingerprint(new[] { _dir }, "english");

        Assert.Equal(before, again);
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Cancelled_learning_throws()
    {
        Fixture();
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => LocalizationProfileLearner.Learn(new[] { _dir }, "english", DateTime.UtcNow, source.Token));
    }

    [Fact]
    public void Learning_reports_progress_up_to_all_files()
    {
        Fixture();
        var progress = new Collect();

        var result = LocalizationProfileLearner.Learn(new[] { _dir }, "english", DateTime.UtcNow, CancellationToken.None, progress);

        Assert.NotEmpty(progress.Items);
        Assert.Equal((result.Profiles.FilesScanned, result.Profiles.FilesScanned), progress.Items[^1]);
        Assert.True(progress.Items.Zip(progress.Items.Skip(1)).All(pair => pair.First.Done <= pair.Second.Done));
    }

    private sealed class Collect : IProgress<(int Done, int Total)>
    {
        public List<(int Done, int Total)> Items { get; } = new();

        public void Report((int Done, int Total) value) => Items.Add(value);
    }
}

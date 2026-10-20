using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

public class SaveVariableReaderTests
{
    private static SaveReadResult Read(string body, SaveCaptureRequest? request = null) =>
        SaveVariableReader.ReadFile(SaveText.Write(body), request ?? SaveCaptureRequest.None, null, default);

    [Fact]
    public void VariablesOfObjectsAndGlobalsGetTheirOwnerPath()
    {
        var result = Read(
            "variables={ data={ { flag=g1 data={ type=value identity=100000 } } } }\n" +
            "countries={ database={\n0={\n\tvariables={ data={ { flag=iv2_x data={ type=value identity=60000000 } } } }\n}\n} }\n");

        Assert.Equal(SaveReadStatus.Complete, result.Status);
        Assert.Equal(
            new[]
            {
                new SaveVariableEntry("", "g1", SaveEntryKind.Variable, "value", "identity", "100000"),
                new SaveVariableEntry("countries/database/0", "iv2_x", SaveEntryKind.Variable, "value", "identity", "60000000"),
            },
            result.Entries);
    }

    [Fact]
    public void Hoi4_flat_variables_and_arrays_are_read()
    {
        // Hearts of Iron IV: Variablen stehen direkt als name=wert im Block, Listen als name^0, name^1 … und name^num (Länge).
        var result = SaveVariableReader.ReadFile(
            SaveText.Write(
                "countries={\n\tGER={\n\t\tvariables={\n\t\t\trandom={ 1 2 }\n\t\t\tfoo=1.5\n\t\t\tlist^0=10\n\t\t\tlist^1=11\n\t\t\tlist^num=2\n\t\t}\n\t}\n}\n" +
                "variables={\n\trandom={ 3 4 }\n\tglobal_x=3\n}\n",
                "HOI4txt"),
            SaveCaptureRequest.None,
            null,
            default);

        Assert.Equal(SaveReadStatus.Complete, result.Status);
        Assert.Equal(
            new[]
            {
                new SaveVariableEntry("countries/GER", "foo", SaveEntryKind.Variable, string.Empty, "value", "1.5"),
                new SaveVariableEntry("countries/GER", "list", SaveEntryKind.ListItem, string.Empty, "value", "10"),
                new SaveVariableEntry("countries/GER", "list", SaveEntryKind.ListItem, string.Empty, "value", "11"),
                new SaveVariableEntry(string.Empty, "global_x", SaveEntryKind.Variable, string.Empty, "value", "3"),
            },
            result.Entries);
    }

    [Fact]
    public void ListItemsFlagValuesAndLocationValuesKeepTheirValueField()
    {
        var result = Read(
            "variables={ data={ { flag=a data={ type=flag flag=\"iv2_army_12\" } } { flag=b data={ type=loc location=8966 } } }\n" +
            "\tlist={ { name=\"pope_countries\" item={ type=ctry identity=1504 } duration={ 1 } } { name=\"pope_countries\" item={ type=ctry identity=7 } } } }\n");

        Assert.Equal(
            new[]
            {
                new SaveVariableEntry("", "a", SaveEntryKind.Variable, "flag", "flag", "iv2_army_12"),
                new SaveVariableEntry("", "b", SaveEntryKind.Variable, "loc", "location", "8966"),
                new SaveVariableEntry("", "pope_countries", SaveEntryKind.ListItem, "ctry", "identity", "1504"),
                new SaveVariableEntry("", "pope_countries", SaveEntryKind.ListItem, "ctry", "identity", "7"),
            },
            result.Entries);
    }

    [Fact]
    public void BraceStructureWinsOverBrokenIndentation()
    {
        var result = Read(
            "countries={\n\tdatabase={\n0={\n\tcountry_name=\"A\"\n}\n1={\ncountry_name=\"B\"\n\t\t\t\tvariables={\n" +
            "data={ {\nflag=v\ndata={\ntype=boolean\nidentity=1\n}\n} }\n}\n}\n\t}\n}\n");

        var entry = Assert.Single(result.Entries);
        Assert.Equal("countries/database/1", entry.OwnerPath);
        Assert.Equal(SaveReadStatus.Complete, result.Status);
    }

    [Fact]
    public void QuotedStringsWithEscapedQuotesBracesAndNewlinesDoNotChangeNesting()
    {
        var result = Read(
            "metadata={ flag=\"CAS={\n\tpattern=\\\"p.dds\\\"\n\tcolored_emblem={\n}\n\" }\n" +
            "variables={ data={ { flag=after data={ type=boolean identity=1 } } } }\n");

        var entry = Assert.Single(result.Entries);
        Assert.Equal(("", "after"), (entry.OwnerPath, entry.Name));
        Assert.Equal(SaveReadStatus.Complete, result.Status);
    }

    [Fact]
    public void Utf8NamesAndValuesAreDecoded()
    {
        var entry = Assert.Single(Read("variables={ data={ { flag=größe_ä data={ type=raw text=\"Köln\" } } } }\n").Entries);

        Assert.Equal(("größe_ä", "text", "Köln"), (entry.Name, entry.ValueField, entry.RawValue));
    }

    [Fact]
    public void EmptyBlocksAndVariablesWithoutValueFieldYieldNoGhostEntries()
    {
        var result = Read(
            "variables={ }\ncountries={ database={ 1={ variables={ data={ } list={ } } } } }\n" +
            "provinces={ database={ 2={ variables={ data={ { flag=x data={ type=value } } { data={ type=value identity=1 } } } } } } }\n");

        Assert.Equal(new[] { new SaveVariableEntry("provinces/database/2", "x", SaveEntryKind.Variable, "value", "", "") }, result.Entries);
        Assert.Equal(SaveReadStatus.Complete, result.Status);
    }

    [Fact]
    public void TruncatedFileIsIncompleteAndKeepsWhatWasRead()
    {
        var result = Read(
            "variables={ data={ { flag=a data={ type=value identity=1 } } { flag=b data={ type=value identity=2 } }\n" +
            "{ flag=z data={ type=val");

        Assert.Equal(new[] { "a", "b" }, result.Entries.Select(e => e.Name));
        Assert.Equal(SaveReadStatus.Incomplete, result.Status);
    }

    [Fact]
    public void FileGrowingWhileReadingIsChanged()
    {
        var path = SaveText.Write("variables={ data={ { flag=a data={ type=value identity=1 } } } }\n");
        var appended = false;
        var progress = new InlineProgress(_ =>
        {
            if (!appended)
            {
                appended = true;
                File.AppendAllText(path, "x=1\n");
            }
        });

        var result = SaveVariableReader.ReadFile(path, SaveCaptureRequest.None, progress, default);

        Assert.Equal(SaveReadStatus.Changed, result.Status);
    }

    [Fact]
    public void CancellationStopsReading()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            SaveVariableReader.ReadFile(SaveText.Write("variables={ }\n"), SaveCaptureRequest.None, null, cancel.Token));
    }

    [Fact]
    public void RequestedTablesAndObjectFieldsAreCaptured()
    {
        var result = Read(
            "countries={ tags={ 0=DUMMY 3=SWE } }\ncharacter_db={ database={ 0={ first_name=\"name_birger\" adm=97 } 1={ adm=3 } } }\n",
            new SaveCaptureRequest(new[] { "countries/tags" }, new[] { new SaveFieldCapture("character_db/database/*", "first_name") }));

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["countries/tags/0"] = "DUMMY",
                ["countries/tags/3"] = "SWE",
                ["character_db/database/0/first_name"] = "name_birger",
            },
            result.Captured);
    }

    /// <summary>Meldet synchron (Progress&lt;T&gt; würde über den SynchronizationContext verzögern).</summary>
    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}

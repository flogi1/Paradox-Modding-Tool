using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

public class SaveValueFormatTests
{
    [Theory]
    [InlineData(100000, 100000, "1")]
    [InlineData(60000000, 100000, "600")]
    [InlineData(150000, 100000, "1.5")]
    [InlineData(-25000, 100000, "-0.25")]
    [InlineData(1, 100000, "0.00001")]
    [InlineData(0, 100000, "0")]
    public void FixedPointUsesTheProfileFactor(long raw, long factor, string expected) =>
        Assert.Equal(expected, SaveValueFormat.FixedPoint(raw, factor));

    [Theory]
    [InlineData(55523760, 24, "1338.5.1")]
    [InlineData(55515744, 24, "1337.6.1")]
    [InlineData(55746432, 24, "1363.10.1")]
    [InlineData(55523772, 24, "1338.5.1.12")]
    [InlineData(2313490, 1, "1338.5.1")]
    [InlineData(0, 1, "-5000.1.1")]
    public void TickDateCountsFromTheEpochYearWith365DayYears(long ticks, int ticksPerDay, string expected) =>
        Assert.Equal(expected, SaveValueFormat.TickDate(ticks, ticksPerDay, -5000));

    [Fact]
    public void EntriesAreFormattedByTheirProfileValueType()
    {
        var snapshot = SaveSnapshotTests.Load(
            "countries={ tags={ 3=SWE } database={ 3={ variables={ data={\n" +
            "{ flag=v data={ type=value identity=60000000 } }\n" +
            "{ flag=d data={ type=date identity=55523760 } }\n" +
            "{ flag=c data={ type=ctry identity=3 } }\n" +
            "{ flag=c2 data={ type=ctry identity=99 } }\n" +
            "{ flag=ch data={ type=char identity=0 } }\n" +
            "{ flag=b1 data={ type=boolean identity=1 } }\n" +
            "{ flag=b0 data={ type=boolean identity=0 } }\n" +
            "{ flag=f data={ type=flag flag=\"iv2_army_12\" } }\n" +
            "{ flag=l data={ type=loc location=8966 } }\n" +
            "{ flag=bad data={ type=value identity=abc } }\n" +
            "{ flag=empty data={ type=value } }\n" +
            "} } } } }\ncharacter_db={ database={ 0={ first_name=\"name_birger\" } } }\n");
        var format = new SaveValueFormat(snapshot);

        string Of(string name) => format.Format(snapshot.EntriesFor(name, SaveNameKind.Variable).Single());

        Assert.Equal("600", Of("v"));
        Assert.Equal("1338.5.1", Of("d"));
        Assert.Equal("SWE", Of("c"));
        Assert.Equal("99", Of("c2"));
        Assert.Equal("name_birger (0)", Of("ch"));
        Assert.Equal("ja", Of("b1"));
        Assert.Equal("nein", Of("b0"));
        Assert.Equal("iv2_army_12", Of("f"));
        Assert.Equal("8966", Of("l"));
        Assert.Equal("abc", Of("bad"));
        Assert.Equal(string.Empty, Of("empty"));
    }
}

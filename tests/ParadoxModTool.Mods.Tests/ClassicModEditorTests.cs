using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ClassicModEditorTests
{
    /// <summary>Wie die echte "Idea Variation BT Addon.mod": LF, kein Zeilenumbruch am Ende, keine <c>version</c>-Zeile.</summary>
    private const string Bt =
        "name=\"The Idea Variation Beyond Typus Addon\"\n" +
        "dependencies={\n\t\"Beyond Typus\"\n\t\"The Idea Variation\"\n}\n" +
        "tags={\n\t\"Addon\"\n\t\"Idea groups\"\n\t\"Beyond Typus\"\n}\n" +
        "picture=\"thumbnail.png\"\n" +
        "supported_version=\"v1.37.*.*\"\n" +
        "path=\"C:/Users/x/Documents/Paradox Interactive/Europa Universalis IV/mod/Idea Variation BT Addon\"\n" +
        "remote_file_id=\"1703319446\"";

    /// <summary>Wie die echte "Idea Variation Anbennar Addon.mod": <c>version</c> direkt nach dem tags-Block.</summary>
    private const string Anbennar =
        "name=\"The Idea Variation Anbennar Addon\"\n" +
        "dependencies={\n\t\"Anbennar: A Fantasy Total Conversion\"\n}\n" +
        "tags={\n\t\"Addon\"\n\t\"Anbennar\"\n}\n" +
        "version=\"1.0\"\n" +
        "picture=\"thumbnail.png\"\n" +
        "supported_version=\"v1.37.*.*\"\n" +
        "remote_file_id=\"3141790295\"";

    [Fact]
    public void ReadVersion_liest_die_version_Zeile_und_nicht_supported_version()
    {
        Assert.Equal("1.0", ClassicModEditor.ReadVersion(Anbennar));
        Assert.Null(ClassicModEditor.ReadVersion(Bt));
    }

    [Fact]
    public void ReadVersion_ignoriert_Kommentare_und_Bloecke_und_liest_ungequotete_Werte()
    {
        Assert.Null(ClassicModEditor.ReadVersion("# version=\"9.9\"\nname=\"X\""));
        Assert.Null(ClassicModEditor.ReadVersion("block={\n\tversion=\"9.9\"\n}"));
        Assert.Equal("2.1", ClassicModEditor.ReadVersion("name=\"X\"\nversion = 2.1 # Kommentar\n"));
        Assert.Equal("1.2.3", ClassicModEditor.ReadVersion("  Version = \" 1.2.3 \"\r\n"));
    }

    [Fact]
    public void SetVersion_aendert_vorhandene_Zeile_an_Ort_und_Stelle()
    {
        var result = ClassicModEditor.SetVersion(Anbennar, "1.0.1");

        Assert.Equal(Anbennar.Replace("version=\"1.0\"", "version=\"1.0.1\""), result);
    }

    [Fact]
    public void SetVersion_ersetzt_ungequoteten_Wert_durch_gequoteten()
    {
        Assert.Equal("name=\"X\"\nversion=\"2.2\" # Kommentar\n", ClassicModEditor.SetVersion("name=\"X\"\nversion=2.1 # Kommentar\n", "2.2"));
    }

    [Fact]
    public void SetVersion_fuegt_fehlende_Zeile_nach_dem_tags_Block_ein_wie_bei_Anbennar()
    {
        var result = ClassicModEditor.SetVersion(Bt, "1.0.0");

        Assert.Equal(Bt.Replace("}\npicture=", "}\nversion=\"1.0.0\"\npicture="), result);
        Assert.False(result.EndsWith('\n'));
        Assert.Equal("1.0.0", ClassicModEditor.ReadVersion(result));
    }

    [Fact]
    public void SetVersion_behaelt_CRLF()
    {
        var crlf = Bt.Replace("\n", "\r\n");

        var result = ClassicModEditor.SetVersion(crlf, "1.0.0");

        Assert.Equal(crlf.Replace("}\r\npicture=", "}\r\nversion=\"1.0.0\"\r\npicture="), result);
        Assert.DoesNotContain("\n", result.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void SetVersion_einzeiliger_tags_Block_als_letzte_Zeile_ohne_Zeilenumbruch()
    {
        Assert.Equal("name=\"X\"\ntags={ \"A\" \"B\" }\nversion=\"3\"", ClassicModEditor.SetVersion("name=\"X\"\ntags={ \"A\" \"B\" }", "3"));
    }

    [Fact]
    public void SetVersion_ohne_tags_vor_picture_sonst_am_Ende()
    {
        Assert.Equal("name=\"X\"\nversion=\"1\"\nsupported_version=\"1.*\"\n", ClassicModEditor.SetVersion("name=\"X\"\nsupported_version=\"1.*\"\n", "1"));
        Assert.Equal("name=\"X\"\npath=\"mod/x\"\nversion=\"1\"", ClassicModEditor.SetVersion("name=\"X\"\npath=\"mod/x\"", "1"));
        Assert.Equal("name=\"X\"\nversion=\"1\"\n", ClassicModEditor.SetVersion("name=\"X\"\n", "1"));
        Assert.Equal("version=\"1\"\n", ClassicModEditor.SetVersion(string.Empty, "1"));
    }

    [Fact]
    public void SetVersion_tags_Klammer_in_Anfuehrungszeichen_oder_Kommentar_zaehlt_nicht()
    {
        var text = "tags={\n\t\"A}\" # }\n}\npicture=\"p.png\"";

        Assert.Equal("tags={\n\t\"A}\" # }\n}\nversion=\"1\"\npicture=\"p.png\"", ClassicModEditor.SetVersion(text, "1"));
    }

    [Fact]
    public void SetVersion_einzeilige_Datei_mit_version_verweigert_statt_doppeltem_Schluessel()
    {
        const string text = "name=\"x\" version=\"1.0\"";

        Assert.Null(ClassicModEditor.ReadVersion(text));
        var ex = Assert.Throws<InvalidOperationException>(() => ClassicModEditor.SetVersion(text, "1.1"));
        Assert.Equal("Das Feld version steht nicht am Anfang einer eigenen Zeile. Ändere die Version direkt im Text.", ex.Message);
        Assert.Throws<InvalidOperationException>(() => ClassicModEditor.SetVersion("name=\"x\"\nversion={ 1 }\n", "1.1"));
    }

    [Fact]
    public void SetVersion_fuegt_ein_wenn_version_nur_in_anderem_Schluessel_Block_oder_Kommentar_steht()
    {
        Assert.Equal(
            "name=\"x\" supported_version=\"1.*\" myversion=\"2\"\nversion=\"1\"",
            ClassicModEditor.SetVersion("name=\"x\" supported_version=\"1.*\" myversion=\"2\"", "1"));
        Assert.Equal(
            "name=\"x\" # version=\"2\"\ndeps={ version=\"3\" }\nversion=\"1\"\n",
            ClassicModEditor.SetVersion("name=\"x\" # version=\"2\"\ndeps={ version=\"3\" }\n", "1"));
        Assert.Equal("name=\"version = 2\"\nversion=\"1\"", ClassicModEditor.SetVersion("name=\"version = 2\"", "1"));
    }

    [Fact]
    public void Backslash_am_Ende_eines_Werts_beendet_die_Anfuehrungszeichen_trotzdem()
    {
        const string text = "name=\"X\"\npath=\"C:\\mod\\\"\nversion=\"1.0\"\ntags={\n\t\"A\"\n}\n";

        Assert.Equal("1.0", ClassicModEditor.ReadVersion(text));
        Assert.Null(ClassicModEditor.Validate(text));
        Assert.Equal(text.Replace("1.0", "1.1"), ClassicModEditor.SetVersion(text, "1.1"));
        Assert.Equal(
            "path=\"C:\\mod\\\"\ntags={\n\t\"A\"\n}\nversion=\"2\"\npicture=\"p.png\"",
            ClassicModEditor.SetVersion("path=\"C:\\mod\\\"\ntags={\n\t\"A\"\n}\npicture=\"p.png\"", "2"));
    }

    [Fact]
    public void ReadRemoteFileId_liest_die_Zeile_auf_oberster_Ebene()
    {
        Assert.Equal("1703319446", ClassicModEditor.ReadRemoteFileId(Bt));
        Assert.Null(ClassicModEditor.ReadRemoteFileId("name=\"X\"\n# remote_file_id=\"1\"\n"));
    }

    [Fact]
    public void SetRemoteFileId_aendert_vorhandene_Zeile_an_Ort_und_Stelle()
    {
        Assert.Equal(Bt.Replace("1703319446", "42"), ClassicModEditor.SetRemoteFileId(Bt, "42"));
    }

    [Fact]
    public void SetRemoteFileId_haengt_fehlende_Zeile_ans_Ende_und_behaelt_Zeilenenden()
    {
        const string text = "name=\"X\"\r\nversion=\"1.0\"\r\n";

        Assert.Equal(text + "remote_file_id=\"42\"\r\n", ClassicModEditor.SetRemoteFileId(text, "42"));
        Assert.Equal("name=\"X\"\nremote_file_id=\"42\"", ClassicModEditor.SetRemoteFileId("name=\"X\"", "42"));
    }

    [Fact]
    public void SetRemoteFileId_verweigert_ungueltige_ID_und_Schluessel_ohne_eigene_Zeile()
    {
        Assert.Throws<ArgumentException>(() => ClassicModEditor.SetRemoteFileId(Bt, "12a"));
        Assert.Throws<ArgumentException>(() => ClassicModEditor.SetRemoteFileId(Bt, " "));
        Assert.Throws<InvalidOperationException>(() => ClassicModEditor.SetRemoteFileId("name=\"x\" remote_file_id=\"1\"", "2"));
    }

    [Fact]
    public void SetVersion_leere_Version_wirft()
    {
        Assert.Throws<ArgumentException>(() => ClassicModEditor.SetVersion(Bt, " "));
    }

    [Fact]
    public void Validate_akzeptiert_echte_Formen_und_meldet_Klammern_und_Anfuehrungszeichen()
    {
        Assert.Null(ClassicModEditor.Validate(Bt));
        Assert.Null(ClassicModEditor.Validate(Anbennar));
        Assert.Null(ClassicModEditor.Validate("tags={ \"}\" } # {"));
        Assert.Equal("Zeile 3: Diese schließende Klammer hat keine öffnende.", ClassicModEditor.Validate("a=\"1\"\nb={ }\n}"));
        Assert.Equal("Zeile 2: Diese Klammer wird nie geschlossen.", ClassicModEditor.Validate("a=\"1\"\ntags={\n\t\"A\"\n"));
        Assert.Equal("Zeile 1: Dieses Anführungszeichen wird nie geschlossen.", ClassicModEditor.Validate("name=\"X\nb=1"));
    }
}

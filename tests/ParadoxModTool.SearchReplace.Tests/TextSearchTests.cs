using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

public class TextSearchTests
{
    [Fact]
    public void FindAll_FindetAlleTreffer_MitZeileUndOffset()
    {
        var text = "modifier = {\n    tax = 5\n}\nmodifier = {\n    tax = 10\n}\n";

        var matches = TextSearch.FindAll(text, "modifier", new SearchOptions());

        Assert.Equal(2, matches.Count);
        Assert.Equal(1, matches[0].LineNumber);
        Assert.Equal(4, matches[1].LineNumber);
        Assert.Equal("modifier = {", matches[0].LineText);
    }

    [Fact]
    public void FindAll_OhneMatchCase_FindetGross_UndKleinschreibung()
    {
        var text = "Tax = 5\ntax = 10\nTAX = 15";

        var matches = TextSearch.FindAll(text, "tax", new SearchOptions { MatchCase = false });

        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void FindAll_MitMatchCase_FindetNurExakteSchreibweise()
    {
        var text = "Tax = 5\ntax = 10\nTAX = 15";

        var matches = TextSearch.FindAll(text, "tax", new SearchOptions { MatchCase = true });

        Assert.Single(matches);
        Assert.Equal(2, matches[0].LineNumber);
    }

    [Fact]
    public void FindAll_MitWholeWord_UebergehtTeilwortTreffer()
    {
        var text = "tax = 5\nincome_tax = 10\ntaxation = 3";

        var matches = TextSearch.FindAll(text, "tax", new SearchOptions { WholeWord = true });

        Assert.Single(matches);
        Assert.Equal(1, matches[0].LineNumber);
    }

    [Fact]
    public void FindAll_OhneWholeWord_FindetAuchTeilwortTreffer()
    {
        var text = "tax = 5\nincome_tax = 10\ntaxation = 3";

        var matches = TextSearch.FindAll(text, "tax", new SearchOptions { WholeWord = false });

        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void FindAll_ErlaubtUeberlappendeTreffer()
    {
        var matches = TextSearch.FindAll("aaaa", "aa", new SearchOptions());

        Assert.Equal(3, matches.Count);
        Assert.Equal(0, matches[0].Offset);
        Assert.Equal(1, matches[1].Offset);
        Assert.Equal(2, matches[2].Offset);
    }

    [Fact]
    public void FindAllNonOverlapping_UeberspringtUeberlappendeFolgetreffer()
    {
        var matches = TextSearch.FindAllNonOverlapping("aaaa", "aa", new SearchOptions());

        Assert.Equal(2, matches.Count);
        Assert.Equal(0, matches[0].Offset);
        Assert.Equal(2, matches[1].Offset);
    }

    [Fact]
    public void FindAll_FindetAuchMehrzeiligenBlock()
    {
        var block = "modifier = {\n    tax = 5\n}";
        var text = $"vorher\n{block}\nnachher";

        var matches = TextSearch.FindAll(text, block, new SearchOptions { MatchCase = true });

        Assert.Single(matches);
    }

    [Fact]
    public void ReplaceAll_ErsetztAlleTrefferUndLiefertAnzahl()
    {
        var text = "tax = 5\ntax = 10";

        var (newText, count) = TextSearch.ReplaceAll(text, "tax", "income", new SearchOptions());

        Assert.Equal(2, count);
        Assert.Equal("income = 5\nincome = 10", newText);
    }

    [Fact]
    public void ReplaceAll_OhneTreffer_LiefertUnveraendertenTextUndNull()
    {
        var (newText, count) = TextSearch.ReplaceAll("hallo welt", "xyz", "abc", new SearchOptions());

        Assert.Equal(0, count);
        Assert.Equal("hallo welt", newText);
    }

    [Fact]
    public void ReplaceAll_MitUeberlappendenTreffern_ErsetztNurNichtUeberlappende()
    {
        var (newText, count) = TextSearch.ReplaceAll("aaaa", "aa", "b", new SearchOptions());

        Assert.Equal(2, count);
        Assert.Equal("bb", newText);
    }

    // Die folgenden drei Tests decken den vom Nutzer gemeldeten Fehler ab ("Programm hat
    // Schwierigkeiten, ganze Codeblöcke zu finden"): Ein mehrzeiliger Suchbegriff aus einer WPF-
    // TextBox verwendet IMMER "\r\n", auch wenn die durchsuchte Datei nur "\n" (oder umgekehrt)
    // verwendet - ohne Angleichung der Zeilenenden schlägt der exakte string.IndexOf-Vergleich
    // dann fehl, obwohl der sichtbare Text identisch aussieht.

    [Fact]
    public void FindAll_MitCrlfSuchbegriff_FindetBlockInDateiMitReinemLf()
    {
        var text = "vorher\nmodifier = {\n    tax = 5\n}\nnachher";
        var query = "modifier = {\r\n    tax = 5\r\n}";

        var matches = TextSearch.FindAll(text, query, new SearchOptions { MatchCase = true });

        Assert.Single(matches);
    }

    [Fact]
    public void FindAll_MitLfSuchbegriff_FindetBlockInDateiMitCrlf()
    {
        var text = "vorher\r\nmodifier = {\r\n    tax = 5\r\n}\r\nnachher";
        var query = "modifier = {\n    tax = 5\n}";

        var matches = TextSearch.FindAll(text, query, new SearchOptions { MatchCase = true });

        Assert.Single(matches);
    }

    [Fact]
    public void ReplaceAll_MitCrlfSuchbegriffUndErsetzung_FunktioniertInDateiMitReinemLf()
    {
        var text = "vorher\nmodifier = {\n    tax = 5\n}\nnachher";
        var query = "modifier = {\r\n    tax = 5\r\n}";
        var replacement = "modifier = {\r\n    tax = 10\r\n}";

        var (newText, count) = TextSearch.ReplaceAll(text, query, replacement, new SearchOptions { MatchCase = true });

        Assert.Equal(1, count);
        Assert.Equal("vorher\nmodifier = {\n    tax = 10\n}\nnachher", newText);
    }

    [Fact]
    public void FindNext_FindetTrefferNachGegebenemOffset()
    {
        var text = "tax = 5\ntax = 10\ntax = 15";

        var match = TextSearch.FindNext(text, "tax", 5, new SearchOptions());

        Assert.NotNull(match);
        Assert.Equal(2, match!.LineNumber);
    }

    [Fact]
    public void FindNext_LaeuftAmEndeUmZumErstenTreffer()
    {
        var text = "tax = 5\ntax = 10";

        var match = TextSearch.FindNext(text, "tax", 100, new SearchOptions());

        Assert.NotNull(match);
        Assert.Equal(1, match!.LineNumber);
    }

    [Fact]
    public void FindNext_OhneTreffer_GibtNullZurueck()
    {
        var match = TextSearch.FindNext("hallo welt", "xyz", 0, new SearchOptions());

        Assert.Null(match);
    }

    // Die folgenden Tests decken das Nutzer-Feedback ab: "Manchmal hat man bei Suchen und
    // Ersetzen den Fall, dass einzelne Codeblöcke anders formatiert sind. [...] Wenn aber beide
    // ersetzt werden sollen, muss man nach beiden suchen." Mit SearchOptions.IgnoreFormatting
    // sollen unterschiedlich eingerückte/umgebrochene, aber inhaltlich gleiche Codeblöcke beide
    // gefunden werden - UND jeweils exakt der tatsächlich vorgefundene (unterschiedlich
    // formatierte) Textbereich ersetzt werden, ohne die umgebende Formatierung zu verändern.

    [Fact]
    public void FindAll_MitIgnoreFormatting_FindetTrotzAbweichenderEinrueckungUndZeilenumbruch()
    {
        var text = "modifier={\n  tax=5\n}";
        var query = "modifier = {\r\n    tax = 5\r\n}";

        var matches = TextSearch.FindAll(text, query, new SearchOptions { IgnoreFormatting = true, MatchCase = true });

        Assert.Single(matches);
        // Der Treffer zeigt auf die ECHTE, unveränderte Formatierung im Originaltext.
        Assert.Equal("modifier={\n  tax=5\n}", matches[0].MatchedText);
    }

    [Fact]
    public void FindAll_MitIgnoreFormatting_FindetBeideUnterschiedlichFormatiertenVarianten()
    {
        var text = "modifier = {\n    tax = 5\n}\n---\nmodifier={ tax=5 }";
        var query = "modifier = {\n    tax = 5\n}";

        var matches = TextSearch.FindAll(text, query, new SearchOptions { IgnoreFormatting = true, MatchCase = true });

        Assert.Equal(2, matches.Count);
        Assert.Equal("modifier = {\n    tax = 5\n}", matches[0].MatchedText);
        Assert.Equal("modifier={ tax=5 }", matches[1].MatchedText);
    }

    [Fact]
    public void FindAll_OhneIgnoreFormatting_FindetAbweichendFormatiertenBlockNicht()
    {
        var text = "modifier={\n  tax=5\n}";
        var query = "modifier = {\n    tax = 5\n}";

        var matches = TextSearch.FindAll(text, query, new SearchOptions { IgnoreFormatting = false, MatchCase = true });

        Assert.Empty(matches);
    }

    [Fact]
    public void ReplaceAll_MitIgnoreFormatting_ErsetztJedeVarianteInIhrerEigenenFormatierung()
    {
        var text = "modifier = {\n    tax = 5\n}\n---\nmodifier={ tax=5 }";
        var query = "modifier = {\n    tax = 5\n}";
        var replacement = "REPLACED";

        var (newText, count) = TextSearch.ReplaceAll(text, query, replacement, new SearchOptions { IgnoreFormatting = true, MatchCase = true });

        Assert.Equal(2, count);
        Assert.Equal("REPLACED\n---\nREPLACED", newText);
    }

    [Fact]
    public void FindAll_MitIgnoreFormattingUndWholeWord_BeruecksichtigtWholeWordAmOriginaltext()
    {
        var text = "tax_income = 5\n---\ntax = 5";
        var query = "tax";

        var matches = TextSearch.FindAll(text, query, new SearchOptions { IgnoreFormatting = true, WholeWord = true, MatchCase = true });

        Assert.Single(matches);
        Assert.Equal("tax", matches[0].MatchedText);
    }

    // Nutzer-Feedback (Folgerunde): "Wenn die neuen Dateien eingesetzt werden, ist die
    // Formatierung danach einen Tab weiter eingerückt als sie sein sollte." Ursache: derselbe
    // Suchbegriff kann mit IgnoreFormatting an Stellen mit UNTERSCHIEDLICHER tatsächlicher
    // Einrückungstiefe gefunden werden - ein absolut eingegebener, mehrzeiliger Ersetzungstext
    // passt dann bestenfalls zu EINER der Fundstellen. Die folgenden Tests decken die Korrektur
    // ab: die relative Einrückungsstruktur des eingegebenen Ersetzungstextes wird bei jeder
    // Fundstelle auf deren TATSÄCHLICHE Einrückung übertragen, statt die absolut eingegebene
    // Einrückung unverändert zu übernehmen.

    [Fact]
    public void ReplaceAll_MitIgnoreFormatting_PasstEinrueckungAnUnterschiedlicheFundstellenTiefeAn()
    {
        // "modifier"-Block einmal auf oberster Ebene (1 Tab Kontext-Einrückung) und einmal eine
        // Ebene tiefer verschachtelt (2 Tabs Kontext-Einrückung).
        var text = "top:\n\tmodifier = {\n\t\ttax=5\n\t}\nnested:\n\t\tmodifier = {\n\t\t\ttax=5\n\t\t}";
        var query = "modifier = {\r\n\ttax = 5\r\n}";
        var replacement = "modifier = {\r\n\tnewtax = 6\r\n}";

        var (newText, count) = TextSearch.ReplaceAll(text, query, replacement, new SearchOptions { IgnoreFormatting = true, MatchCase = true });

        Assert.Equal(2, count);
        // Fundstelle 1 (1 Tab Kontext): die relative Zusatz-Einrückung der Ersetzung (1 Tab
        // gegenüber ihrer eigenen ersten Zeile) wird auf 1+1 = 2 Tabs übertragen.
        // Fundstelle 2 (2 Tabs Kontext): entsprechend auf 2+1 = 3 Tabs.
        Assert.Equal(
            "top:\n\tmodifier = {\n\t\tnewtax = 6\n\t}\nnested:\n\t\tmodifier = {\n\t\t\tnewtax = 6\n\t\t}",
            newText);
    }

    [Fact]
    public void ReplaceAll_MitIgnoreFormatting_VerdoppeltEinrueckungDerErstenErsatzzeileNicht()
    {
        // Enthält der eingegebene Ersetzungstext (versehentlich) selbst eine Einrückung in seiner
        // ersten Zeile, darf diese NICHT zusätzlich zu der im Originaltext bereits vorhandenen
        // (unangetasteten) Einrückung der Fundstelle addiert werden.
        var text = "\tmodifier = {\n\t\ttax=5\n\t}";
        var query = "modifier = {\r\n\ttax = 5\r\n}";
        var replacement = "\tmodifier = {\r\n\tnewtax = 6\r\n}";

        var (newText, count) = TextSearch.ReplaceAll(text, query, replacement, new SearchOptions { IgnoreFormatting = true, MatchCase = true });

        Assert.Equal(1, count);
        Assert.Equal("\tmodifier = {\n\tnewtax = 6\n}", newText);
    }

    [Fact]
    public void ReplaceAll_OhneIgnoreFormatting_PasstEinrueckungNichtAn()
    {
        // Ohne IgnoreFormatting bleibt das bisherige Verhalten unverändert: der Ersetzungstext
        // wird exakt so eingefügt, wie eingegeben (Regressionsschutz für den bestehenden Modus).
        var text = "\tmodifier = {\n\ttax = 5\n}";
        var query = "modifier = {\n\ttax = 5\n}";
        var replacement = "modifier = {\n\tnewtax = 6\n}";

        var (newText, count) = TextSearch.ReplaceAll(text, query, replacement, new SearchOptions { IgnoreFormatting = false, MatchCase = true });

        Assert.Equal(1, count);
        Assert.Equal("\tmodifier = {\n\tnewtax = 6\n}", newText);
    }
}

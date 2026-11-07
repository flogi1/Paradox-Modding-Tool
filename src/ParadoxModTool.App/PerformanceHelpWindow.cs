using System.Windows;
using ParadoxModTool.Performance;

namespace ParadoxModTool.App;

/// <summary>
/// Anleitung zum Performance-Checker (F1 im Fenster) mit Inhaltsverzeichnis und Suche. Die Texte liefert
/// <see cref="PerformanceHelpTexts"/>; die Kapitel entsprechen den Reitern (F1 springt zum Kapitel des aktuellen Reiters).
/// Das Kapitel "Hinweise" gehört zur Auswertung; F1 dort bleibt bei "Herkunft und Rauschen", "Hinweise" folgt direkt danach im Inhaltsverzeichnis.
/// </summary>
internal sealed class PerformanceHelpWindow : GuideWindow
{
    /// <summary>Die Blöcke in der geladenen Oberflächensprache; bei jedem Öffnen neu gebaut, damit die Sprache stimmt.</summary>
    private static GuideBlock[] Blocks => new[]
    {
        new GuideBlock(GuideBlockKind.Title, PerformanceHelpTexts.Title),

        new GuideBlock(GuideBlockKind.Heading, PerformanceHelpTexts.WhatHeading, PerformanceHelpTexts.SectionWhat),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.WhatColumns),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.WhatUnit),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.WhatCumulative),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.WhatSelfTotal),

        new GuideBlock(GuideBlockKind.Heading, PerformanceHelpTexts.FlowHeading, PerformanceHelpTexts.SectionFlow),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.FlowIntro),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.FlowSteps),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.FlowPickup),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.FlowAutosave),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.FlowImport),

        new GuideBlock(GuideBlockKind.Heading, PerformanceHelpTexts.OriginHeading, PerformanceHelpTexts.SectionOrigin),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.OriginKinds),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.OriginJump),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.OriginNoise),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.OriginCalls),

        new GuideBlock(GuideBlockKind.Heading, PerformanceHelpTexts.HintsHeading, PerformanceHelpTexts.SectionHints),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.HintsIntro),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.HintsErrorSpam),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.HintsUnguarded),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.HintsFrequentTick),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.HintsVanillaCopy),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.HintsManyCalls),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.HintsErrorLog),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.HintsErrorLogWindow),

        new GuideBlock(GuideBlockKind.Heading, PerformanceHelpTexts.CompareHeading, PerformanceHelpTexts.SectionCompare),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.CompareChecks),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.CompareRows),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.CompareFiles),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.CompareNewRun),

        new GuideBlock(GuideBlockKind.Heading, PerformanceHelpTexts.AiHeading, PerformanceHelpTexts.SectionAi),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.AiWhat),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.AiContent),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.AiAnswer),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.AiStored),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.AiRecipe),

        new GuideBlock(GuideBlockKind.Heading, PerformanceHelpTexts.GamesHeading, PerformanceHelpTexts.SectionGames),
        new GuideBlock(GuideBlockKind.Paragraph, PerformanceHelpTexts.GamesList),
    };

    private static PerformanceHelpWindow? _open;

    private PerformanceHelpWindow()
        : base(PerformanceHelpTexts.Title, Blocks, withNavigation: true)
    {
    }

    /// <summary>Höchstens ein Anleitungsfenster; ein offenes wird nach vorn geholt. Mit <paramref name="section"/> springt es zum Kapitel.</summary>
    public static void Show(Window owner, string? section)
    {
        if (_open is null)
        {
            _open = new PerformanceHelpWindow { Owner = owner };
            _open.Closed += (_, _) => _open = null;
            _open.Show();
        }
        else
        {
            _open.Activate();
        }

        if (section is not null)
        {
            _open.ShowSection(section);
        }
    }
}

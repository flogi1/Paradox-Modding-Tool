using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Die Jomini-Vorschau zeigt die Endwerte des Zustands <c>_show</c> (size, position, alpha, scale) statt des Grundzustands.</summary>
public sealed class JominiShowStateTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-showstate-" + Guid.NewGuid().ToString("N"));
    private readonly GuiTextureLocator _textures;

    public JominiShowStateTests()
    {
        TestDds.WriteBgra(Path.Combine(_temp, "install", "game", "gfx", "tex.dds"), 100, 100);
        _textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(Path.Combine(_temp, "install"), Array.Empty<string>(), null, Array.Empty<string>()));
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private LayoutBox Layout(string gui, string library = "")
    {
        var (resolver, doc) = GuiResolverTests.Setup(gui, library);
        var element = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        return new GuiLayoutEngine(GuiResolverTests.Rules, _textures.SizeOf, null, _textures).Layout(element);
    }

    [Fact]
    public void Show_state_size_replaces_base_size()
    {
        var box = Layout("widget = { size = { 2650 1000 } state = { name = _show size = { 1650 910 } } }");

        Assert.Equal(1650, box.Width);
        Assert.Equal(910, box.Height);
    }

    [Fact]
    public void Market_window_lays_out_with_the_show_size()
    {
        // Flogi's Buildings and Technology Mod, show_flogi_market: Grundgröße 2650x1000, _show setzt 1650x910 (Dauer und Kurve zählen nicht).
        var box = Layout(
            "window = { name = \"market\" size = { 2650 1000 } position = { 0 0 } " +
            "state = { name = _show duration = 0.2 using = Animation_Curve_Default size = { 1650 910 } } " +
            "state = { name = _hide size = { 10 10 } } }",
            "template Animation_Curve_Default { bezier = { 0.25 0.1 0.25 1 } }");

        Assert.Equal(1650, box.Width);
        Assert.Equal(910, box.Height);
        Assert.DoesNotContain(box.Notes, n => n.Contains("nicht auswertbar", StringComparison.Ordinal));
    }

    [Fact]
    public void Later_show_state_wins()
    {
        var box = Layout("widget = { size = { 100 100 } state = { name = _show size = { 200 200 } position = { 5 6 } } state = { name = _show size = { 300 150 } } }");

        Assert.Equal(300, box.Width);
        Assert.Equal(150, box.Height);
        Assert.Equal(5, box.X);
        Assert.Equal(6, box.Y);
    }

    [Fact]
    public void Show_state_alpha_makes_a_faded_in_window_visible()
    {
        var box = Layout("widget = { size = { 100 50 } alpha = 0 state = { name = _show alpha = 1 } icon = { size = { 20 20 } texture = \"gfx/tex.dds\" } }");

        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, box.Notes);
        Assert.False(box.Uncertain);
        var icon = box.Children.Single();
        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, icon.Notes);
        Assert.Equal(1, icon.Sprite!.Opacity, 6);
    }

    [Fact]
    public void Show_state_alpha_on_a_sizeless_fade_in_widget_leaves_no_note()
    {
        var box = Layout(
            "widget = { alpha = 0 using = Fade icon = { size = { 20 20 } texture = \"gfx/tex.dds\" } }",
            "template Fade { state = { name = _show using = Animation_FadeIn_Quick } }\n" +
            "template Animation_FadeIn_Quick { using = Animation_Curve_Default duration = 0.15 alpha = 1 }\n" +
            "template Animation_Curve_Default { bezier = { 0.25 0.1 0.25 1 } }");

        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, box.Notes);
        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, box.Children.Single().Notes);
        Assert.Equal(1, box.Children.Single().Sprite!.Opacity, 6);
    }

    [Fact]
    public void Show_state_using_template_is_applied()
    {
        var box = Layout(
            "widget = { size = { 100 100 } alpha = 0 state = { name = _show using = Shown } icon = { size = { 20 20 } texture = \"gfx/tex.dds\" } }",
            "template Shown { alpha = 1 position = { 7 8 } using = Inner }\ntemplate Inner { size = { 40 30 } }");

        Assert.Equal(7, box.X);
        Assert.Equal(8, box.Y);
        Assert.Equal(40, box.Width);
        Assert.Equal(30, box.Height);
        Assert.Equal(1, box.Children.Single().Sprite!.Opacity, 6);
        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, box.Notes);
    }

    [Fact]
    public void Show_state_template_cycle_ends()
    {
        var box = Layout("widget = { size = { 100 100 } state = { name = _show using = A } }", "template A { using = B size = { 1 2 } }\ntemplate B { using = A alpha = 1 }");

        Assert.Equal(1, box.Width);
        Assert.Equal(2, box.Height);
    }

    [Fact]
    public void Other_states_are_ignored()
    {
        var box = Layout(
            "widget = { size = { 100 100 } alpha = 0 state = { name = _hide size = { 1 1 } alpha = 1 } " +
            "state = { name = _mouse_enter position = { 9 9 } alpha = 1 } state = { name = a size = { 3 3 } } }");

        Assert.Equal(100, box.Width);
        Assert.Equal(0, box.X);
        Assert.Contains(GuiTexts.LayoutAlphaInvisible, box.Notes);
    }

    [Fact]
    public void Quoted_show_name_is_recognised()
    {
        var box = Layout("widget = { size = { 100 100 } state = { name = \"_show\" size = { 12 34 } } }");

        Assert.Equal(12, box.Width);
        Assert.Equal(34, box.Height);
    }

    [Fact]
    public void Bound_value_in_show_state_is_ignored_with_note()
    {
        var box = Layout("widget = { size = { 100 80 } alpha = 0.5 position = { 1 2 } state = { name = _show size = { \"[GetW]\" 10 } alpha = \"[GetA]\" position = { 3 \"[GetY]\" } } }");

        Assert.Equal(100, box.Width);
        Assert.Equal(80, box.Height);
        Assert.Equal(1, box.X);
        Assert.Equal(2, box.Y);
        Assert.Contains(GuiTexts.LayoutSizeUnresolved("[GetW] 10"), box.Notes);
        Assert.Contains(GuiTexts.LayoutPositionUnresolved("3 [GetY]"), box.Notes);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("alpha"), box.Notes);
    }

    [Fact]
    public void Animation_keys_are_ignored()
    {
        var box = Layout(
            "widget = { size = { 100 100 } state = { name = _show duration = 5 delay = 1 animation = { size = { 1 1 } } bezier = { 0 0 1 1 } next = a " +
            "on_start = \"[Foo]\" trigger_on_create = yes } state = { name = a duration = 1 } }");

        Assert.Equal(100, box.Width);
        Assert.Equal(100, box.Height);
        Assert.False(box.Uncertain);
    }

    [Fact]
    public void Constants_in_show_state_are_substituted()
    {
        var box = Layout("@w = 77\nwidget = { size = { 100 100 } state = { name = _show size = { @w 20 } } }");

        Assert.Equal(77, box.Width);
        Assert.Equal(20, box.Height);
    }

    [Fact]
    public void Show_state_percent_size_in_sizeless_box_behaves_like_a_written_percent_size()
    {
        var written = Layout("widget = { size = { 400 100 } hbox = { hbox = { icon = { size = { 50% 20 } } } } }").Children[0].Children[0];
        var shown = Layout("widget = { size = { 400 100 } hbox = { hbox = { icon = { size = { 100 50 } state = { name = _show size = { 50% 20 } } } } } }").Children[0].Children[0];

        Assert.Contains(GuiTexts.LayoutPercentInSizelessBox, written.Notes);
        Assert.Contains(GuiTexts.LayoutPercentInSizelessBox, shown.Notes);
        Assert.Equal(written.Width, shown.Width, 3);
        Assert.Equal(200, shown.Children[0].Width, 3);
        Assert.Equal(20, shown.Children[0].Height, 3);
    }

    [Fact]
    public void Scale_is_exposed_through_the_same_overlay()
    {
        var (resolver, doc) = GuiResolverTests.Setup("widget = { scale = 1 state = { name = _show scale = 2 } }");
        var element = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));

        Assert.Equal("2", JominiShowState.Property(element, "scale")!.Value);
        Assert.Equal("1", element.Property("scale")!.Value);
    }

    [Fact]
    public void Next_chain_size_reaches_the_end_value()
    {
        // Wie EU V cooltip_types.gui TooltipIllustration: _show setzt eine Starthöhe, next = fade_in die eigentliche.
        var box = Layout(
            "widget = { size = { 300 0 } state = { name = _show size = { 300 10 } next = fade_in } " +
            "state = { name = fade_in size = { 300 120 } duration = 0.2 using = Animation_Curve_Default } }",
            "template Animation_Curve_Default { bezier = { 0.25 0.1 0.25 1 } }");

        Assert.Equal(300, box.Width);
        Assert.Equal(120, box.Height);
        Assert.DoesNotContain(GuiTexts.LayoutShowNextApproximated, box.Notes);
        Assert.False(box.Uncertain);
    }

    [Fact]
    public void Next_target_is_matched_quoted_or_unquoted()
    {
        // Wie Victoria 3 politics_panel_institutions.gui: _show 52×22, next = show_2 setzt 52×18; hier mit Anführungszeichen auf je einer Seite.
        var quotedName = Layout("widget = { size = { 52 0 } state = { name = _show next = show_2 size = { 52 22 } } state = { name = \"show_2\" size = { 52 18 } } }");
        var quotedNext = Layout("widget = { size = { 52 0 } state = { name = _show next = \"show_2\" size = { 52 22 } } state = { name = show_2 size = { 52 18 } } }");

        Assert.Equal(18, quotedName.Height);
        Assert.Equal(18, quotedNext.Height);
        Assert.DoesNotContain(GuiTexts.LayoutShowNextApproximated, quotedName.Notes);
        Assert.DoesNotContain(GuiTexts.LayoutShowNextApproximated, quotedNext.Notes);
    }

    [Fact]
    public void Next_chain_keeps_values_the_later_state_does_not_set()
    {
        var box = Layout("widget = { size = { 10 10 } state = { name = _show size = { 80 40 } position = { 3 4 } next = b } state = { name = b position = { 7 8 } } }");

        Assert.Equal(80, box.Width);
        Assert.Equal(40, box.Height);
        Assert.Equal(7, box.X);
        Assert.Equal(8, box.Y);
    }

    [Fact]
    public void Scale_pulse_loop_ends_at_the_state_before_the_first_repeat_with_a_note()
    {
        // _show → a → b → a (Wiederholung): gezeigt wird b, mit Hinweis.
        var box = Layout(
            "widget = { size = { 200 200 } icon = { size = { 40 40 } texture = \"gfx/tex.dds\" scale = 0.7 " +
            "state = { name = _show next = a scale = 0.7 duration = 0 } state = { name = a next = b scale = 1 duration = 0.05 } " +
            "state = { name = b next = a scale = 0.5 duration = 0.1 } } }");

        var icon = box.Children.Single();
        Assert.Equal(20, icon.Width, 6);
        Assert.Contains(GuiTexts.LayoutShowNextApproximated, icon.Notes);
    }

    [Fact]
    public void Scale_chain_without_loop_ends_at_the_last_state_without_note()
    {
        // Victoria 3 add_wargoal_panel.gui: _show 0,7 → a 1 → b 0,7; b hat kein next, die Kette endet.
        var box = Layout(
            "widget = { size = { 200 200 } icon = { size = { 40 40 } texture = \"gfx/tex.dds\" scale = 0.7 " +
            "state = { name = _show next = a scale = 0.7 duration = 0 } state = { name = a next = b scale = 1 duration = 0.05 } " +
            "state = { name = b scale = 0.5 duration = 0.1 } } }");

        var icon = box.Children.Single();
        Assert.Equal(20, icon.Width, 6);
        Assert.DoesNotContain(GuiTexts.LayoutShowNextApproximated, icon.Notes);
    }

    [Fact]
    public void Next_back_to_show_counts_as_loop()
    {
        var box = Layout("widget = { size = { 10 10 } state = { name = _show size = { 20 20 } next = a } state = { name = a size = { 30 30 } next = _show } }");

        Assert.Equal(30, box.Width);
        Assert.Contains(GuiTexts.LayoutShowNextApproximated, box.Notes);
    }

    [Fact]
    public void Alpha_chain_fading_in_after_a_wait_leaves_no_alpha_note()
    {
        // EU V multiplayer_lobby.gui mapmode_current_button: _show alpha 0 → show_wait alpha 0 → show_final alpha 1.
        var box = Layout(
            "widget = { size = { 100 50 } icon = { size = { 20 20 } texture = \"gfx/tex.dds\" position = { 0 -50 } " +
            "state = { name = _show alpha = 0 next = show_wait } state = { name = show_wait duration = 1 alpha = 0 next = show_final } " +
            "state = { name = show_final bezier = { 0.0 1.0 0.0 1.0 } alpha = 1 } state = { name = _hide alpha = 0 } } }");

        var icon = box.Children.Single();
        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, icon.Notes);
        Assert.DoesNotContain(GuiTexts.LayoutShowNextApproximated, icon.Notes);
        Assert.Equal(1, icon.Sprite!.Opacity, 6);
    }

    [Fact]
    public void Next_to_a_missing_state_ends_the_chain_without_note()
    {
        // Wie EU V cooltip_types.gui tooltip_delayed_showup_animation (next = fade_in ohne solchen Zustand): im Spiel endet die Kette dort.
        var box = Layout("widget = { size = { 10 10 } state = { name = _show size = { 20 25 } next = a } state = { name = a size = { 30 35 } next = nowhere } }");

        Assert.Equal(30, box.Width);
        Assert.Equal(35, box.Height);
        Assert.DoesNotContain(GuiTexts.LayoutShowNextApproximated, box.Notes);
        Assert.False(box.Uncertain);
    }

    [Fact]
    public void Show_state_without_next_has_no_chain_note()
    {
        var box = Layout("widget = { size = { 10 10 } state = { name = _show size = { 20 25 } } state = { name = a size = { 1 1 } next = a } }");

        Assert.Equal(20, box.Width);
        Assert.Equal(25, box.Height);
        Assert.DoesNotContain(GuiTexts.LayoutShowNextApproximated, box.Notes);
        Assert.False(box.Uncertain);
    }

    [Fact]
    public void Next_from_a_template_inside_the_state_is_followed()
    {
        var box = Layout(
            "widget = { size = { 10 10 } state = { name = _show using = Start } state = { name = grow size = { 90 45 } } }",
            "template Start { size = { 20 20 } next = grow }");

        Assert.Equal(90, box.Width);
        Assert.Equal(45, box.Height);
    }
}

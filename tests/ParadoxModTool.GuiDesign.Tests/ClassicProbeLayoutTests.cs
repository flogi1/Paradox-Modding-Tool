using ParadoxModTool.GuiDesign.Layout;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Sollbild: das Probe-Fenster <c>mainmenu_panel_upperleft</c> aus <c>make_gui_probes.py</c> (Spec-Ordner 2026-10-17-classic-gui-editor), im Spiel gezeigt in
/// <c>hoi4-probe-run2-2026-10-17.jpg</c> (HOI IV 1.19.3, 2048×1152, gui_scale 1.0, Bild auf 2000×1125 verkleinert, Faktor 0,977).
/// </summary>
public sealed class ClassicProbeLayoutTests : IDisposable
{
    // Text wie von make_gui_probes.py geschrieben: upperleft("S000") (im Lauf 2 gewann die Unterordner-Datei 000_pmt_sub_a, das Bild zeigt PMT_WIN_S000),
    // mit label() und RENDER eingesetzt; nur der umschließende guiTypes-Block ist ergänzt.
    private const string UpperLeft = """
        guiTypes = {
        	containerWindowType = {
        		name = "mainmenu_panel_upperleft"
        		position = { x = 0 y = -200 }
        		show_position = { x = 0 y = -1 }
        		animation_type = decelerated
        		animation_time = 1000
        		size = { width = 256 height = 768 }

        		iconType = {
        			name = "frontend_game_logo"
        			spriteType = "GFX_frontend_game_logo"
        			position = { x = 20 y = 20 }
        		}

        		instantTextBoxType = {
        			name = "pmt_win_label"
        			position = { x = 20 y = 260 }
        			font = "hoi_24header"
        			borderSize = { x = 0 y = 0 }
        			text = "PMT_WIN_S000"
        			maxWidth = 240
        			maxHeight = 30
        			format = left
        		}

        		containerWindowType = {
        			name = "pmt_render_probe"
        			position = { x = -236 y = -330 }
        			size = { width = 220 height = 160 }
        			Orientation = LOWER_RIGHT
        			clipping = no
        			background = {
        				name = "Background"
        				quadTextureSprite = "GFX_tiled_window"
        			}
        			# inheritance: no own orientation; at the container's top left unless it inherits LOWER_RIGHT
        			iconType = {
        				name = "pmt_inherit_icon"
        				spriteType = "GFX_global_alert_icons"
        				frame = 5
        				position = { x = 0 y = 0 }
        			}
        			# frames: 0, 1, (none), 2 side by side
        			iconType = {
        				name = "pmt_frame0"
        				spriteType = "GFX_global_alert_icons"
        				frame = 0
        				position = { x = 10 y = 10 }
        				Orientation = UPPER_LEFT
        			}
        			iconType = {
        				name = "pmt_frame1"
        				spriteType = "GFX_global_alert_icons"
        				frame = 1
        				position = { x = 60 y = 10 }
        				Orientation = UPPER_LEFT
        			}
        			iconType = {
        				name = "pmt_frame_none"
        				spriteType = "GFX_global_alert_icons"
        				position = { x = 110 y = 10 }
        				Orientation = UPPER_LEFT
        			}
        			iconType = {
        				name = "pmt_frame2"
        				spriteType = "GFX_global_alert_icons"
        				frame = 2
        				position = { x = 160 y = 10 }
        				Orientation = UPPER_LEFT
        			}
        			# percent sizes: 100%% (parent minus own position?) above 100%
        			containerWindowType = {
        				name = "pmt_pct_double"
        				position = { x = 60 y = 80 }
        				size = { width = 100%% height = 20 }
        				Orientation = UPPER_LEFT
        				clipping = no
        				background = {
        					name = "Background"
        					quadTextureSprite = "GFX_tiled_window"
        				}
        			}
        			containerWindowType = {
        				name = "pmt_pct_single"
        				position = { x = 60 y = 110 }
        				size = { width = 100% height = 20 }
        				Orientation = UPPER_LEFT
        				clipping = no
        				background = {
        					name = "Background"
        					quadTextureSprite = "GFX_tiled_window"
        				}
        			}
        		}

        	}
        }
        """;

    // Text wie von make_gui_probes.py geschrieben: upperright("S000") mit label() eingesetzt (das Bild zeigt PMT_PART_S000).
    private const string UpperRight = """
        guiTypes = {
        	containerWindowType = {
        		name = "mainmenu_panel_upperright"
        		position = { x = 0 y = -1 }
        		show_position = { x = -256 y = -1 }
        		animation_type = decelerated
        		animation_time = 1000
        		size = { width = 256 height = 768 }
        		Orientation = upper_right

        		ButtonType = {
        			name = "achievements_button"
        			position = { x = -50 y = 25 }
        			quadTextureSprite = "achievements"
        			pdx_tooltip = "MENU_ACHIEVEMENTS"
        		}

        		iconType = {
        			name = "frontend_dev_logo"
        			spriteType = "GFX_frontend_dev_logo"
        			position = { x = 15 y = 15 }
        		}

        		instantTextBoxType = {
        			name = "pmt_part_label"
        			position = { x = 15 y = 140 }
        			font = "hoi_24header"
        			borderSize = { x = 0 y = 0 }
        			text = "PMT_PART_S000"
        			maxWidth = 240
        			maxHeight = 30
        			format = left
        		}

        	}
        }
        """;

    // Text wie von make_gui_probes.py geschrieben: CHANGEBG_ONLY_AZZ mit label() eingesetzt (nur in zz_pmt_order_a.gui; das Bild zeigt PMT_ONLY_AZZ).
    private const string ChangeBackground = """
        guiTypes = {
        	containerWindowType = {
        		name = "change_background"
        		size = { width = 100 height = 40 }
        		position = { x = -20 y = -20 }
        		Orientation = lower_right
        		Origo = lower_right

        		buttonType = {
        			name = "change_background_button"
        			position = { x = -206 y = -24 }
        			quadTextureSprite = "GFX_button_238x38"
        			buttonText = "CHANGE_BACKGROUND"
        			buttonFont = "hoi_24header"
        			Orientation = center
        		}

        		instantTextBoxType = {
        			name = "pmt_wzz_label"
        			position = { x = -206 y = -60 }
        			font = "hoi_24header"
        			borderSize = { x = 0 y = 0 }
        			text = "PMT_ONLY_AZZ"
        			maxWidth = 240
        			maxHeight = 30
        			format = left
        		}

        		containerWindowType = {
        			name = "background_selection"
        			size = { width = 238 height = 700 }
        			position = { x = -156 y = -700 }
        			margin = { top = 18 bottom = 13 left = 8 }

        			background = {
        				name = "Background"
        				quadTextureSprite = "GFX_tiled_window2_1b_border"
        			}

        			checkboxType = {
        				name = "select_all_checkbox"
        				position = { x = 13 y = 0 }
        				quadTextureSprite = "GFX_generic_checkbox"
        				buttonText = ""
        				buttonFont = "Main_14_black"
        				clicksound = click_checkbox
        			}
        			instantTextBoxType = {
        				position = { x = 53 y = 6 }
        				name = "select_all_label"
        				font = "hoi_18mbs"
        				borderSize = { x = 0 y = 0 }
        				maxWidth = 150
        				maxHeight = 18
        				text = "SELECT_ALL"
        				orientation = "UPPER_LEFT"
        				format = left
        			}

        			containerWindowType = {
        				name = "background_selection_list"
        				size = { width = 230 height = 576 }
        				position = { x = 0 y = 90 }
        				verticalScrollbar = "right_vertical_slider"
        				scroll_wheel_factor = 63
        				smooth_scrolling = yes
        				background = {
        					name = "Background"
        					quadTextureSprite = "GFX_tiled_window_transparent"
        				}

        				gridBoxType = {
        					position = { x = 9 y = 0 }
        					name = "available_backgrounds"
        					size = { width = 200 height = 0 }
        					slotsize = { width = 200 height = 150 }
        					max_slots_horizontal = 1
        				}
        			}
        		}
        	}
        }
        """;

    private const double Tolerance = 10;

    private readonly ClassicLayoutKit _kit = new();

    public void Dispose() => _kit.Dispose();

    /// <summary>
    /// Lagen (linke obere Ecke) gegen das Bild, ±10 Spielpixel. Abgelesen (Bildpixel ÷ 0,977, Ausschnitt 3-fach vergrößert, 2026-10-17):
    /// Text PMT_WIN_S000 ab etwa (22, 266) (die Glyphen beginnen unter der Kastenoberkante, Spec: (22, 264)); pmt_render_probe etwa (22, 438)
    /// (Spec: (24, 440)); Frame-Icons Oberkante etwa 448 bei x ≈ 32, 82, 132, 182 (Spec: 450 bei 33, 82, 131, 182); der 100%%-Balken beginnt
    /// etwa bei x 85. Berechnet: (20, 259), (20, 437), 30/80/130/180 bei y 447, Balken bei x 80; alles in der Toleranz, kein Wert korrigiert.
    /// Größen dagegen exakt nach Definition und gemessenen Regeln, nicht nach dem Bild (die Ablesung, etwa 209 breit, ist dafür zu grob).
    /// Nebenbefund im Bild: die 20 hohen 9-Slice-Balken sind rund 85 bis 110 hoch und wachsen offenbar um ihre Mitte (Balken 1 Oberkante
    /// etwa 472 statt 517); PMT zeichnet 2 × borderSize um die Mitte der angegebenen Fläche (Spec "Sprites → 9-Slice", ClassicLayoutEngine.Drawn).
    /// </summary>
    [Fact]
    public void Probe_upperleft_matches_screenshot()
    {
        var root = _kit.Layout(UpperLeft, new LayoutOptions { ScreenWidth = 2048, ScreenHeight = 1152, UiScale = 1 });

        ClassicLayoutKit.AssertRect(root, 0, -1, 256, 768);
        Assert.False(root.OutsideScreen);

        var logo = ClassicLayoutKit.Find(root, "frontend_game_logo");
        ClassicLayoutKit.AssertRect(logo, 20, 19, 82, 52);

        AssertNear(ClassicLayoutKit.Find(root, "pmt_win_label"), 22, 266);
        AssertNear(ClassicLayoutKit.Find(root, "pmt_win_label"), 20, 259, exact: true);

        var probe = ClassicLayoutKit.Find(root, "pmt_render_probe");
        AssertNear(probe, 22, 438);
        ClassicLayoutKit.AssertRect(probe, 20, 437, 220, 160);
        AssertNear(ClassicLayoutKit.Find(root, "pmt_inherit_icon"), 20, 437, exact: true);

        var frames = new[] { ("pmt_frame0", 32.0, 1), ("pmt_frame1", 82.0, 1), ("pmt_frame_none", 132.0, 1), ("pmt_frame2", 182.0, 2) };
        foreach (var (name, x, frame) in frames)
        {
            var icon = ClassicLayoutKit.Find(root, name);
            AssertNear(icon, x, 448);
            Assert.Equal(447, icon.Y, 3);
            Assert.Equal((47, 42), (icon.Width, icon.Height));
            Assert.Equal(frame, Assert.IsType<GuiSpriteVisual>(icon.Visual).Frame);
        }

        var doubled = ClassicLayoutKit.Find(root, "pmt_pct_double");
        var single = ClassicLayoutKit.Find(root, "pmt_pct_single");
        // Nur der Anfang links gegen das Bild: die Oberkante der Balken verdeckt dort der vergrößert gezeichnete 9-Slice (siehe oben).
        Assert.InRange(doubled.X, 85 - Tolerance, 85 + Tolerance);
        Assert.InRange(single.X, 85 - Tolerance, 85 + Tolerance);
        ClassicLayoutKit.AssertRect(doubled, 80, 517, 160, 20);
        ClassicLayoutKit.AssertRect(single, 80, 547, 220, 20);
        Assert.Equal(probe.X + probe.Width, doubled.X + doubled.Width, 3);
        Assert.False(doubled.OutsideParent);
        Assert.True(single.OutsideParent);

        // Die 9-Slice-Hintergründe der 20 hohen Balken werden größer gezeichnet, gelten aber nicht als Überstand.
        Assert.False(Assert.Single(doubled.Children).OutsideParent);
        Assert.False(Assert.Single(single.Children).OutsideParent);
    }

    /// <summary>
    /// Anker UPPER_RIGHT gegen das Bild (Lauf 2, gleiche Umrechnung wie oben). Das Fenster steht bei show_position (−256, −1) von der rechten oberen
    /// Bildschirmecke, also bei (1792, −1). Abgelesen (2026-10-17): Text PMT_PART_S000 ab etwa (1809, 146); die Glyphen beginnen rund 7 Pixel unter der
    /// Kastenoberkante (wie bei PMT_WIN), der Kasten liegt also bei etwa (1809, 139). Berechnet: (1807, 139).
    /// </summary>
    [Fact]
    public void Probe_upperright_matches_screenshot()
    {
        var root = _kit.Layout(UpperRight, new LayoutOptions { ScreenWidth = 2048, ScreenHeight = 1152, UiScale = 1 });

        ClassicLayoutKit.AssertRect(root, 1792, -1, 256, 768);
        var label = ClassicLayoutKit.Find(root, "pmt_part_label");
        AssertNear(label, 1809, 146 - GlyphOffset);
        AssertNear(label, 1807, 139, exact: true);
    }

    /// <summary>
    /// Anker LOWER_RIGHT mit Origo LOWER_RIGHT und CENTER gegen das Bild (Lauf 2). change_background (100 × 40) sitzt mit seiner rechten unteren Ecke
    /// 20/20 vor der rechten unteren Bildschirmecke, also bei (1928, 1092). Abgelesen (2026-10-17): Text PMT_ONLY_AZZ ab etwa (1724, 1039), Kasten
    /// also etwa (1724, 1032); Knopf "Hintergrund ändern" linke obere Ecke etwa (1774, 1088). Berechnet: Text (1722, 1032) (ohne orientation, oben links
    /// im Container), Knopf (1772, 1088) (CENTER: 1928 + 50 − 206, 1092 + 20 − 24).
    /// </summary>
    [Fact]
    public void Probe_change_background_matches_screenshot()
    {
        var root = _kit.Layout(ChangeBackground, new LayoutOptions { ScreenWidth = 2048, ScreenHeight = 1152, UiScale = 1 });

        ClassicLayoutKit.AssertRect(root, 1928, 1092, 100, 40);
        var label = ClassicLayoutKit.Find(root, "pmt_wzz_label");
        AssertNear(label, 1724, 1039 - GlyphOffset);
        AssertNear(label, 1722, 1032, exact: true);
        var button = ClassicLayoutKit.Find(root, "change_background_button");
        AssertNear(button, 1774, 1088);
        AssertNear(button, 1772, 1088, exact: true);
    }

    // Im Bild beginnen die Glyphen der Probe-Texte (hoi_24header) rund 7 Spielpixel unter der Oberkante ihres Kastens.
    private const double GlyphOffset = 7;

    private static void AssertNear(LayoutBox box, double x, double y, bool exact = false)
    {
        var tolerance = exact ? 0.001 : Tolerance;
        Assert.InRange(box.X, x - tolerance, x + tolerance);
        Assert.InRange(box.Y, y - tolerance, y + tolerance);
    }
}

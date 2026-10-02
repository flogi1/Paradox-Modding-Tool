using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class EntryAlignerTests
{
    private const string AdvisorA = """
        artist = {
        	monarch_power = ADM

        	skill_scaled_modifier = {
        		trigger = {
        			owner = {
        				has_government_attribute = advisors_generate_innovativeness
        			}
        		}
        		modifier = {
        			yearly_innovativeness = 0.02
        		}
        	}

        	stability_cost_modifier = -0.10

        	skill_scaled_modifier = {
        		trigger = { owner = {  has_government_attribute = reform_progress_from_advisors  } }
        		modifier = { reform_progress_growth = 0.03 }
        	}
        }
        """;

    private const string AdvisorB = """
        artist = {
        	monarch_power = ADM
        	skill_scaled_modifier = {
        		trigger = {
        			owner = {
        				has_government_mechanic = russian_modernization_mechanic
        			}
        		}
        		modifier = {
        			monthly_russian_modernization = 0.02
        		}
        	}

        	skill_scaled_modifier = {
        		trigger = {
        			owner = {
        				has_country_flag = ven_splendor_flag
        			}
        		}
        		modifier = {
        			monthly_splendor = 1
        		}
        	}

        	skill_scaled_modifier = {
          trigger = { owner = {  has_country_modifier = mam_adm_advisors_generate_idea_cost_modifier  } }
        		modifier = { idea_cost = -0.01 }
        	}
        	skill_scaled_modifier = {
        		trigger = {
        			owner = {
        				has_government_attribute = advisors_generate_innovativeness
        			}
        		}
        		modifier = {
        			yearly_innovativeness = 0.02
        		}
        	}

        	skill_scaled_modifier = {
        		trigger = {
        			owner = {
        				has_government_mechanic = persian_influence_mechanic
        			}
        		}
        		modifier = {
        			monthly_persian_influence = 0.1
        		}
        	}

        	stability_cost_modifier = -0.10

        	skill_scaled_modifier = {
        		trigger = { owner = {  has_government_attribute = reform_progress_from_advisors  } }
        		modifier = { reform_progress_growth = 0.03 }
        	}
        }
        """;

    private static IReadOnlyList<ScriptEntry> Parse(string text) => ScriptDocument.Parse(text).Entries;

    /// <summary>Zeilen je Block der Ergebnisspalte: vom Kopf "skill_scaled_modifier = {" bis zur schließenden Klammer derselben Tiefe.</summary>
    private static List<List<AlignedRow>> ResultBlocks(IReadOnlyList<AlignedRow> rows, string header)
    {
        var blocks = new List<List<AlignedRow>>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Result?.Trim() != header)
            {
                continue;
            }

            var indent = rows[i].Result!.Length - rows[i].Result!.TrimStart().Length;
            var block = new List<AlignedRow> { rows[i] };
            for (var j = i + 1; j < rows.Count; j++)
            {
                block.Add(rows[j]);
                if (rows[j].Result is { } line && line.Trim() == "}" && line.Length - line.TrimStart().Length == indent)
                {
                    break;
                }
            }

            blocks.Add(block);
        }

        return blocks;
    }

    [Fact]
    public void Blocks_inserted_by_B_stand_alone_and_shared_blocks_stay_side_by_side()
    {
        var a = Parse(AdvisorA);
        var b = Parse(AdvisorB);

        var rows = EntryAligner.Align(a, b, b, null);

        var blocks = ResultBlocks(rows, "skill_scaled_modifier = {");
        Assert.Equal(6, blocks.Count);
        foreach (var inserted in new[] { blocks[0], blocks[1], blocks[2], blocks[4] })
        {
            Assert.All(inserted.Where(r => r.Result is not null), r =>
            {
                Assert.Null(r.A);
                Assert.Equal(RowOrigin.FromB, r.Origin);
                Assert.Equal(r.Result, r.B);
            });
        }

        foreach (var shared in new[] { blocks[3], blocks[5] })
        {
            Assert.All(shared, r =>
            {
                Assert.Equal(RowOrigin.Same, r.Origin);
                Assert.Equal(r.Result, r.A);
                Assert.Equal(r.Result, r.B);
            });
        }

        Assert.DoesNotContain(rows, r => r.Result is null);
        Assert.Contains(rows, r => r.Result?.Trim() == "stability_cost_modifier = -0.10" && r.Origin == RowOrigin.Same);
    }

    [Fact]
    public void All_columns_use_the_same_indentation()
    {
        var b = Parse(AdvisorB);

        var rows = EntryAligner.Align(Array.Empty<ScriptEntry>(), b, b, null).ToList();

        var triggerIndex = rows.FindIndex(r => r.Result?.Contains("mam_adm_advisors_generate_idea_cost_modifier") == true);
        var trigger = rows[triggerIndex];
        var header = rows.Take(triggerIndex).Last(r => r.Result?.Trim() == "skill_scaled_modifier = {");
        var indent = (string s) => s.Length - s.TrimStart().Length;
        Assert.True(indent(trigger.Result!) > indent(header.Result!));
        Assert.Equal(trigger.Result, trigger.B);
    }

    [Fact]
    public void Repeated_blocks_pair_by_content_not_by_the_first_equal_line()
    {
        var a = Parse("s = {\n\tt = 1\n}\ns = {\n\tt = 2\n}");
        var result = Parse("s = {\n\tt = 1\n}\ns = {\n\tt = 3\n}\ns = {\n\tt = 2\n}");

        var rows = EntryAligner.Align(a, result, result, null);

        Assert.Equal(9, rows.Count);
        Assert.All(rows.Skip(3).Take(3), r =>
        {
            Assert.Null(r.A);
            Assert.Equal(RowOrigin.FromB, r.Origin);
        });
        Assert.All(rows.Take(3).Concat(rows.Skip(6)), r => Assert.Equal(RowOrigin.Same, r.Origin));
        Assert.Equal("    t = 2", rows[7].A);
    }

    [Fact]
    public void Changed_value_inside_a_paired_block_stays_on_the_same_row()
    {
        var a = Parse("farm = {\n\tcost = 2\n\ttime = 1\n}");
        var b = Parse("farm = {\n\tcost = 1\n\ttime = 2\n\textra = yes\n}");
        var result = Parse("farm = {\n\tcost = 2\n\ttime = 2\n\textra = yes\n}");

        var rows = EntryAligner.Align(a, result, b, Parse("farm = {\n\tcost = 1\n\ttime = 1\n}"));

        Assert.Equal(RowOrigin.Same, rows[0].Origin);
        Assert.Contains(rows, r => r.Result == "    cost = 2" && r.A == "    cost = 2" && r.B == "    cost = 1" && r.Vanilla == "    cost = 1" && r.Origin == RowOrigin.FromA);
        Assert.Contains(rows, r => r.Result == "    time = 2" && r.A == "    time = 1" && r.Origin == RowOrigin.FromB);
        Assert.Contains(rows, r => r.Result == "    extra = yes" && r.A is null && r.Origin == RowOrigin.FromB);
        Assert.Equal("}", rows[^1].Result);
    }

    [Fact]
    public void Units_only_in_A_or_only_in_B_get_their_own_rows()
    {
        var a = Parse("x = 1\nonly_a = { v = 1 }\ny = 2");
        var b = Parse("x = 1\nonly_b = { v = 2 }\ny = 2");
        var result = Parse("x = 1\ny = 2");

        var rows = EntryAligner.Align(a, result, b, null);

        Assert.DoesNotContain(rows, r => r.A is not null && r.B is not null && r.Result is null);
        Assert.Contains(rows, r => r.A == "only_a = {" && r.B is null && r.Origin == RowOrigin.Gap);
        Assert.Contains(rows, r => r.B == "only_b = {" && r.A is null && r.Origin == RowOrigin.Gap);
    }

    [Fact]
    public void Comment_lines_are_kept_or_hidden()
    {
        var a = Parse("# note\nx = 1");

        Assert.Contains(EntryAligner.Align(a, a, a, null), r => r.Result == "# note");
        Assert.DoesNotContain(EntryAligner.Align(a, a, a, null, hideComments: true), r => r.Result?.StartsWith('#') == true);
    }
}

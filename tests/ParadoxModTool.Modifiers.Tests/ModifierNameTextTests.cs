using Xunit;

namespace ParadoxModTool.Modifiers.Tests;

/// <summary>Begriffe in Modifier-Namen: Victoria 3 schreibt sie ohne Format als <c>[concept_x]</c>.</summary>
public sealed class ModifierNameTextTests
{
    [Fact]
    public void Bare_concept_reference_is_resolved()
    {
        Assert.Equal("Casualties Taken", ModifierNameText.Clean("[concept_casualties] Taken", concept: id => id == "concept_casualties" ? "Casualties" : null));
    }

    [Fact]
    public void Bare_concept_reference_is_listed()
    {
        Assert.Equal(new[] { "concept_pop" }, ModifierNameText.ReferencedConcepts("[concept_pop] Growth"));
    }

    [Fact]
    public void Unresolved_bare_concept_drops_the_concept_prefix()
    {
        Assert.Equal("Pop Growth", ModifierNameText.Clean("[concept_pop] Growth"));
    }

    [Fact]
    public void Other_bare_brackets_still_give_no_name()
    {
        Assert.Null(ModifierNameText.Clean("[GetPlayer.GetName] x"));
        Assert.Null(ModifierNameText.Clean("[pop_growth] x"));
    }

    [Fact]
    public void Concept_call_with_key_reference_shows_the_referenced_text()
    {
        Assert.Equal("Pops", ModifierNameText.Clean("[Concept('concept_pop', '$concept_pops$')]", reference: k => k == "concept_pops" ? "Pops" : null));
    }
}

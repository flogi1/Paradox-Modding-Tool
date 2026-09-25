import unittest
from mediawiki_table import split_rows, split_cells, sections_with_tables
from parse_eu4 import (
    parse_effects,
    parse_modifiers,
    parse_on_actions,
    parse_scopes_logic,
    parse_scopes_table,
    parse_triggers,
)

# Fixtures below are trimmed but VERBATIM excerpts of the real pages, fetched via the
# built-in browser during this plan's Task 5 (the site blocks bare HTTP clients like
# urllib/curl with a JS bot-challenge - see build_eu4.py's docstring and the plan ledger).

EFFECTS_FIXTURE = """
== Dual scope ==
{{SVersion|1.24}}
{| class="wikitable"
! Name !! Parameters !! Examples !! Description !! Notes !! Version Added
|-
|set_global_flag
|<code><flag></code><br>A unique string to identify the global flag with.
|<code>set_global_flag = my_flag</code>
|Defines a global flag.
|Flags can be appended with scopes or event targets
|1.1
|-
|clr_global_flag
|<code><flag></code><br>The unique string of a global flag to clear.
|<code>clr_global_flag = my_flag</code>
|Clears a defined global flag.
|
|1.1
|}

== Country scope ==
=== General ===
{| class="wikitable"
! Name !! Parameters !! Examples !! Description !! Notes !! Version Added
|-
|add_country_modifier
|<code>name = <name></code>
|<code>add_country_modifier = { name = x }</code>
|Adds an event modifier to the current scope as a country modifier.
|
|1.1
|}
=== Economy ===
{| class="wikitable"
! Name !! Parameters !! Examples !! Description !! Notes !! Version Added
|-
|change_price
|<code>trade_goods = <tradegood></code>
|<code>change_price = { trade_goods = grain }</code>
|Changes the price of a trade good globally.
|
|1.8
|}

== Flow control ==
{| class="wikitable"
! Name !! Description
|-
|if
|Conditional block.
|}

== Removed effects ==
{| class="wikitable"
! Name !! Version removed
|-
|old_effect
|1.0
|}
"""

TRIGGERS_FIXTURE = """
== Operators ==
{| class="wikitable"
|-
! Operator !! Description !! Example
|-
| AND || Returns true if all enclosed conditions return true || <pre>AND = { }</pre>
|-
| OR || Returns true if at least one enclosed condition returns true || <pre>OR = { }</pre>
|}

== Simple triggers ==
=== Base scopes ===
{| class="wikitable"
|-
! Value !! Scopes !! Description
|-
| ROOT || anywhere || Refers to the receiver of an event, decision, mission, etc.
|}

=== List of triggers ===
{| class="wikitable sortable"
|-
! Value !! Type !! Description !! Scope !! Example
|-
| culture || Identifier, Scope || Returns true if the province culture is X. || Province ||<code>culture = basque</code>
|-
| adm_power || Integer || Returns true if the country has at least X administrative power. || Country ||<code>adm_power = 600</code>
|-
| messy_stray_pipe || Clause || Some description.<br>Bulleted continuation:<br>
*<code>a</code>
*<code>b</code>
|| Country ||<pre>messy_stray_pipe = { }</pre>
|-
| dual_scope_trigger || Boolean || Valid in both. || Country<br>Province ||<code>x</code>
|-
| placeholder_scope_trigger || Boolean || No scope documented yet. || ''Scope needed'' ||<code>x</code>
|-
| rowspan="2" | adm || Integer || Returns true if the country has a ruler with an administrative skill of at least X. || Country ||<code>adm = 3</code>
|}
"""

MODIFIERS_FIXTURE = """
== Overview ==
All modifiers are sorted into one of two internal scopes.

== Country modifiers ==
=== Military ===
{| class="wikitable sortable" width="100%"
! width="20%" | Modifier
! width="20%" | Example
! width="30%" | Description
! width="10%" | Effect type
! width="10%" | Version added
|-
|army_tradition
|<code>army_tradition = 1</code>
|Adds to the rate of army tradition gained each year.
|Additive
|1.1
|}

== Province modifiers ==
=== Military ===
{| class="wikitable sortable" width="100%"
! width="20%" | Modifier
! width="20%" | Example
! width="30%" | Description
! width="10%" | Effect type
! width="10%" | Version added
|-
|fort_level
|<code>fort_level = 1</code>
|Adds to the fort level in a province.
|Additive
|1.8
|}

==Unique modifiers==
{| class="wikitable"
! Modifier !! Example !! Description !! DLC
|-
|is_mercenary_modifier
|<code>is_mercenary_modifier = yes</code>
|Only affects mercenary companies.
|Mare Nostrum
|}

==Removed modifiers==
{| class="wikitable"
! Modifier !! Example !! Version removed
|-
|overseas_income
|<code>overseas_income = 0.1</code>
|1.4
|}
"""

SCOPES_FIXTURE = """
== Overview ==
Overview text.

=== Dynamic scopes ===
{| class="wikitable sortable" width="100%"
!Scope
!Description
!Example
!Notes
|-
|ROOT
|The base scope.
|
|
|-
|FROM
|The calling scope.
|
|
|}

=== Logic scopes ===
==== AND ====
The ''AND'' scope groups a set of triggers together, and returns true only if all triggers nested within return true as well.
<pre>
AND = {
    tag = FRA
}
</pre>

==== OR ====
The ''OR'' scope splits multiple sets of triggers.
<pre>
OR = { }
</pre>

== Dual scopes ==
=== Country and Province ===
{| class="wikitable sortable" width="100%"
! width="15%" | Name
! width="20%" | Example
! width="25%" | Description
! width="5%" | Multiple Subscopes
! width="5%" | Changes Scope To
! width="10%" | Version Added
|-
|<event_target>
|<code>event_target:x = { }</code>
|Refers to the saved event target scope.
|Yes
|Any
|1.8
|}

=== Country ===
{| class="wikitable sortable" width="100%"
! width="15%" | Name
! width="20%" | Example
! width="25%" | Description
! width="5%" | Multiple Subscopes
! width="5%" | Changes Scope To
! width="10%" | Version Added
|-
|overlord
|<code>overlord = { }</code>
|Refers to the country that is the overlord of the current country scope.
|No
|Country
|1.1
|}

== Effect scopes ==
=== Country ===
{| class="wikitable sortable" width="100%"
! width="15%" | Name
! width="20%" | Example
! width="25%" | Description
! width="5%" | Multiple Subscopes
! width="5%" | Changes Scope To
! width="10%" | Version Added
|-
|every_ally
|<code>every_ally = { }</code>
|Scopes to '''all''' valid countries that meet the triggers in the limit.
|Yes
|Country
|1.1
|}

== Trigger scopes ==
=== Country ===
{| class="wikitable sortable" width="100%"
! width="15%" | Name
! width="20%" | Example
! width="25%" | Description
! width="5%" | Multiple Subscopes
! width="5%" | Changes Scope To
! width="10%" | Version Added
|-
|any_ally
|<code>any_ally = { }</code>
|Scopes to '''one''' valid country that meet the triggers.
|No
|Country
|1.1
|}

== Removed scopes ==
{| class="wikitable sortable" width="100%"
! width="15%" | Name
! width="20%" | Example
! width="20%" | Description
! width="10%" | Version Removed
! width="15%" | Alternatives
|-
|every_trade_node
|<code>every_trade_node = { }</code>
|Scopes to '''all''' trade nodes.
|1.30
|Province group
|}
"""

ON_ACTIONS_FIXTURE = """
== Overview ==
On Actions are effects that are triggered by hardcoded actions. They are found in common/on_actions.

== List ==
{| class="wikitable sortable" width="100%"
! width="20%" | On Action
! width="30%" | Description
! width="10%" | ROOT
! width="10%" | THIS
! width="10%" | FROM
! width="20%" | Notes
|-
| on_abandon_colony
|Fired right before the colony is abandoned.
|location
|
|country
|country refers to who abandoned it
|-
| on_accept_tribute
|
|country
|
|overlord
|
|}

== Actions not in the file ==
{| class="wikitable sortable" width="100%"
! width="20%" | On Action
! width="30%" | Description
! width="10%" | ROOT
! width="10%" | THIS
! width="10%" | FROM
! width="20%" | Notes
|-
| on_harmonized_<religion>
|
|country
|
|
|Modded religions are supported
|}

== Removed on actions ==
{| class="wikitable sortable"
! On Action || Version removed
|-
| on_cossacks_receive_serfs
| 1.35
|}
"""


class MediaWikiTableTests(unittest.TestCase):
    def test_sections_with_tables_finds_all_tables_including_h3_nested(self):
        sections = sections_with_tables(EFFECTS_FIXTURE)
        # 4 tables total: Dual scope, Country scope>General, Country scope>Economy, Flow
        # control, Removed effects = 5 actually (one per heading here, since each has one
        # table). A first-table-per-H2 reader would only find 3 (Dual scope, Country scope,
        # Flow control) and silently drop Country scope>Economy and Removed effects.
        self.assertEqual(5, len(sections))
        paths = [s.heading_path for s in sections]
        self.assertIn(["Country scope", "General"], paths)
        self.assertIn(["Country scope", "Economy"], paths)

    def test_split_cells_handles_newline_led_cells(self):
        rows = split_rows(sections_with_tables(EFFECTS_FIXTURE)[0].table_wikitext)
        cells = split_cells(rows[0])
        self.assertEqual(6, len(cells))
        self.assertEqual("set_global_flag", cells[0].strip())


class ParseEffectsTests(unittest.TestCase):
    def test_dual_scope_heading_maps_to_anyscope_not_literal_dual(self):
        entries = parse_effects(EFFECTS_FIXTURE)
        first = next(e for e in entries if e["name"] == "set_global_flag")
        self.assertEqual("Effect", first["kind"])
        self.assertEqual([], first["scopes"])
        self.assertEqual("Defines a global flag.", first["description"])

    def test_h3_subsections_under_country_scope_both_captured_with_canonical_scope(self):
        # "Country scope" is the wiki's own heading text, but the engine's tracked scope
        # vocabulary (from Triggers' Scope column and Scopes' "Changes Scope To" targets)
        # is the bare "Country" - map to that, not the heading text verbatim.
        entries = parse_effects(EFFECTS_FIXTURE)
        names = {e["name"]: e for e in entries}
        self.assertEqual(["Country"], names["add_country_modifier"]["scopes"])
        self.assertEqual(["Country"], names["change_price"]["scopes"])
        self.assertEqual("1.1", names["add_country_modifier"]["extra"]["Version Added"])

    def test_flow_control_and_removed_effects_are_skipped(self):
        entries = parse_effects(EFFECTS_FIXTURE)
        names = {e["name"] for e in entries}
        self.assertNotIn("if", names)
        self.assertNotIn("old_effect", names)


class ParseTriggersTests(unittest.TestCase):
    def test_list_of_triggers_table_parsed_with_real_scope(self):
        entries = parse_triggers(TRIGGERS_FIXTURE)
        names = {e["name"]: e for e in entries}
        self.assertEqual(["Province"], names["culture"]["scopes"])
        self.assertEqual(["Country"], names["adm_power"]["scopes"])
        self.assertEqual("Identifier, Scope", names["culture"]["extra"]["Type"])

    def test_operators_table_parsed_as_anyscope(self):
        entries = parse_triggers(TRIGGERS_FIXTURE)
        names = {e["name"]: e for e in entries}
        self.assertEqual([], names["AND"]["scopes"])
        self.assertEqual("Trigger", names["AND"]["kind"])

    def test_base_scopes_table_is_skipped(self):
        entries = parse_triggers(TRIGGERS_FIXTURE)
        names = {e["name"] for e in entries}
        self.assertNotIn("ROOT", names)

    def test_stray_leading_pipe_from_wrapped_row_is_stripped(self):
        entries = parse_triggers(TRIGGERS_FIXTURE)
        names = {e["name"]: e for e in entries}
        self.assertEqual(["Country"], names["messy_stray_pipe"]["scopes"])

    def test_br_separated_multi_scope_cell_splits_into_two_scopes(self):
        entries = parse_triggers(TRIGGERS_FIXTURE)
        names = {e["name"]: e for e in entries}
        self.assertEqual(["Country", "Province"], names["dual_scope_trigger"]["scopes"])

    def test_scope_needed_placeholder_becomes_anyscope(self):
        entries = parse_triggers(TRIGGERS_FIXTURE)
        names = {e["name"]: e for e in entries}
        self.assertEqual([], names["placeholder_scope_trigger"]["scopes"])

    def test_rowspan_attribute_prefix_is_stripped_from_the_name(self):
        # Real wiki markup: `| rowspan="2" | adm || ...` - MediaWiki's cell-attribute
        # syntax (attr="v" | content). Without stripping it, the shipped entry name is
        # literally "rowspan=\"2\" | adm" - found on 116 real Triggers rows, including
        # heavily-used triggers like exists/adm/dip/base_tax.
        entries = parse_triggers(TRIGGERS_FIXTURE)
        names = {e["name"] for e in entries}
        self.assertIn("adm", names)
        self.assertNotIn('rowspan="2" | adm', names)


class ParseModifiersTests(unittest.TestCase):
    def test_country_and_province_modifiers_both_captured(self):
        # Same canonical-vocabulary mapping as Effects: "Country modifiers" (heading) ->
        # "Country" (the scope value the engine actually tracks).
        entries = parse_modifiers(MODIFIERS_FIXTURE)
        names = {e["name"]: e for e in entries}
        self.assertEqual(["Country"], names["army_tradition"]["scopes"])
        self.assertEqual(["Province"], names["fort_level"]["scopes"])
        self.assertEqual("Additive", names["army_tradition"]["extra"]["Effect type"])

    def test_overview_unique_and_removed_are_skipped(self):
        entries = parse_modifiers(MODIFIERS_FIXTURE)
        names = {e["name"] for e in entries}
        self.assertNotIn("is_mercenary_modifier", names)
        self.assertNotIn("overseas_income", names)


class ParseScopesTests(unittest.TestCase):
    def test_dynamic_scopes_table_is_anyscope_effect(self):
        entries = parse_scopes_table(SCOPES_FIXTURE)
        root = next(e for e in entries if e["name"] == "ROOT")
        self.assertEqual("Effect", root["kind"])
        self.assertEqual([], root["scopes"])

    def test_dual_scopes_country_and_province_h3_is_anyscope(self):
        entries = parse_scopes_table(SCOPES_FIXTURE)
        target = next(e for e in entries if e["name"] == "<event_target>")
        self.assertEqual([], target["scopes"])
        self.assertEqual(["Any"], target["targets"])

    def test_dual_scopes_country_h3_uses_h3_as_scope(self):
        entries = parse_scopes_table(SCOPES_FIXTURE)
        overlord = next(e for e in entries if e["name"] == "overlord")
        self.assertEqual(["Country"], overlord["scopes"])
        self.assertEqual(["Country"], overlord["targets"])

    def test_effect_scopes_and_trigger_scopes_get_correct_kind(self):
        entries = parse_scopes_table(SCOPES_FIXTURE)
        every_ally = next(e for e in entries if e["name"] == "every_ally")
        any_ally = next(e for e in entries if e["name"] == "any_ally")
        self.assertEqual("Effect", every_ally["kind"])
        self.assertEqual(["Country"], every_ally["scopes"])
        self.assertEqual("Trigger", any_ally["kind"])
        self.assertEqual(["Country"], any_ally["scopes"])

    def test_removed_scopes_is_skipped(self):
        entries = parse_scopes_table(SCOPES_FIXTURE)
        names = {e["name"] for e in entries}
        self.assertNotIn("every_trade_node", names)

    def test_parse_scopes_logic_yields_and_or_as_anyscope_triggers(self):
        entries = parse_scopes_logic(SCOPES_FIXTURE)
        names = {e["name"] for e in entries}
        self.assertEqual({"AND", "OR"}, names)
        for e in entries:
            self.assertEqual("Trigger", e["kind"])
            self.assertEqual([], e["scopes"])


class ParseOnActionsTests(unittest.TestCase):
    def test_list_table_parsed(self):
        entries = parse_on_actions(ON_ACTIONS_FIXTURE)
        names = {e["name"]: e for e in entries}
        abandon = names["on_abandon_colony"]
        self.assertEqual(["location"], abandon["scopes"])
        self.assertEqual("country", abandon["extra"]["FROM"])
        self.assertNotIn("THIS", abandon["extra"])

    def test_actions_not_in_the_file_also_parsed(self):
        entries = parse_on_actions(ON_ACTIONS_FIXTURE)
        names = {e["name"] for e in entries}
        self.assertIn("on_harmonized_<religion>", names)

    def test_removed_on_actions_is_skipped(self):
        entries = parse_on_actions(ON_ACTIONS_FIXTURE)
        names = {e["name"] for e in entries}
        self.assertNotIn("on_cossacks_receive_serfs", names)


if __name__ == "__main__":
    unittest.main()

# Task

Game: eu5

## Messlauf

Lauf: iv2_vorher
Spiel: eu5
Mod: Idea Variation 2
Gemessen am: 12.10.2026 18:40
Monate: geplant 12, gezählt 12
Spielstand: iv2_messung_1450
Playset: IV2 Test
Dateistand: wie gemessen
Alle Zeiten sind Rohwerte des Profiler-Dumps in s, ohne Umrechnung.
Rauschschwellen: Zeit relativ 10 %, absolut 0,050 s, Aufrufe 25 %

## Zeiten je Herkunft

Mod: 2,114 s
Mod und Vanilla: 3,408 s
Andere Mod: 0,000 s
Vanilla: 41,870 s
Unbekannt: 0,000 s
Mod-Seite (Mod sowie Mod und Vanilla): 5,522 s

## Hotspots (Mod und beide)

#  Stelle  Art  Calls  Self  Total
1  common/bureaucracies/iv2_research_system_bureaucracies.txt:101  script_value  189.619  0,083  1,670
2  common/script_values/iv2_research_script_values.txt:14  script_value  412.300  0,610  1,380
3  common/generic_action_ai_lists/iv2_generic_action_list_pick_ig.txt:102  trigger  656.635  0,223  1,063
4  common/generic_action_ai_lists/iv2_generic_action_list_adm_ideas.txt:6  trigger  656.635  0,129  0,808
5  common/generic_action_ai_lists/iv2_generic_action_list_religion.txt:6  trigger  21.884  0,011  0,386
6  common/generic_action_ai_lists/iv2_generic_action_list_researcher.txt:6  trigger  656.635  0,125  0,372
7  common/scripted_effects/iv2_research_monthly.txt:22  effect  12  0,050  0,294
8  common/generic_actions/iv2_ga_ai_chooser.txt:48  trigger  56  0,009  0,141

## Hinweise

[Warnung] common/script_values/iv2_research_script_values.txt:14 — Variablenzugriff ohne Guard
  Gelesen ohne vorherige Prüfung im selben Block: Iv2_researcher_adm. Fehlt die Variable, schreibt jeder Aufruf einen Fehler ins error.log. Prüfe vorher mit has_variable = NAME oder exists = var:NAME, oder lies sie mit ?=. In diesem Block stehen 18.402 Einträge im error.log. Gesamtzeit der Zeile im Profiler: 1,380 s.
[Warnung] common/script_values/iv2_research_script_values.txt:14 — Fehler-Spam an dieser Stelle
  18.402 Einträge im error.log (1.533,5 pro Minute). Jeder Eintrag kostet beim Schreiben Zeit. Häufigste Meldung: Variable Iv2_researcher_adm not found Gesamtzeit der Zeile im Profiler: 1,380 s.
[Hinweis] common/script_values/iv2_research_script_values.txt:14 — Script Value mit sehr vielen Aufrufen
  Etwa 412.300 Aufrufe pro Spieljahr. Ändert sich das Ergebnis selten, berechne es einmal (etwa monatlich) und speichere es als globale Variable; die Aufrufer lesen dann nur die Variable.
[Hinweis] common/bureaucracies/iv2_research_system_bureaucracies.txt:101 — Script Value mit sehr vielen Aufrufen
  Etwa 189.619 Aufrufe pro Spieljahr. Ändert sich das Ergebnis selten, berechne es einmal (etwa monatlich) und speichere es als globale Variable; die Aufrufer lesen dann nur die Variable.

## Betroffene Dateien

- common/bureaucracies/iv2_research_system_bureaucracies.txt
- common/script_values/iv2_research_script_values.txt
- common/generic_action_ai_lists/iv2_generic_action_list_pick_ig.txt
- common/generic_action_ai_lists/iv2_generic_action_list_adm_ideas.txt
- common/generic_action_ai_lists/iv2_generic_action_list_religion.txt
- common/generic_action_ai_lists/iv2_generic_action_list_researcher.txt
- common/scripted_effects/iv2_research_monthly.txt
- common/generic_actions/iv2_ga_ai_chooser.txt

## Was die KI liefern soll

- Eine Einschätzung je Hotspot: warum die Stelle teuer ist und ob sich eine Änderung lohnt.
- Konkrete Skriptvorschläge für die lohnenden Stellen, als Code mit Pfad und Block.
- Bevorzugte Mittel: Guards vor teuren Prüfungen, Zwischenspeichern in Variablen, engere Trigger, seltenere Pulse.
- Was nach der Änderung neu gemessen werden soll und woran man den Erfolg erkennt.

## What the modder wants

Das Spiel ruckelt im Monatswechsel. Was kostet am meisten und was kann ich ändern, ohne dass die KI-Länder passiver werden?

Each file follows below between `----- mod file: <path> -----` or `----- game file: <path> -----` and `----- end -----`. Paths are relative to the mod root or the game root.

The code blocks around the hotspots and the modder's own code blocks follow below.

----- hotspot block: common/script_values/iv2_research_script_values.txt:10-24 -----
Note: Hotspot Nr. 2, Total 1,380 s
iv2_res_skill_bonus_calc_adm = {
	# Research bonus of the administrative researcher, summed per country.
	value = 0
	add = {
		value = var:Iv2_researcher_adm.skill
		multiply = 0.5
	}
	if = {
		limit = { var:Iv2_researcher_adm = { is_alive = yes } }
		add = var:Iv2_researcher_adm.skill
	}
	min = 0
	max = global_var:iv2_research_cap_adm
}
----- end -----
----- hotspot block: common/generic_action_ai_lists/iv2_generic_action_list_pick_ig.txt:96-112 -----
Note: Hotspot Nr. 3, Total 1,063 s
iv2_gal_pick_ig = {
	ai_will_do = {
		scope:country = {
			is_ai = yes
			any_interest_group = {
				count >= 3
				is_in_list = yes
			}
		}
	}
	allow = {
		scope:country = { has_variable = Iv2_ig_pick_ready }
	}
}
----- end -----
----- hotspot block: common/bureaucracies/iv2_research_system_bureaucracies.txt:96-108 -----
Note: Hotspot Nr. 1, Total 1,670 s
iv2_research_bureaucracy_upkeep = {
	value = 0
	add = iv2_res_skill_bonus_calc_adm
	add = iv2_res_skill_bonus_calc_dip
	multiply = 0.1
	min = 1
}
----- end -----
----- hotspot block: common/scripted_effects/iv2_research_monthly.txt:18-30 -----
Note: Hotspot Nr. 7, Total 0,294 s
iv2_research_monthly_calc_effect = {
	every_country = {
		limit = { is_ai = no }
		set_variable = {
			name = Iv2_research_cap_adm
			value = iv2_res_cap_adm_value
		}
	}
}
----- end -----

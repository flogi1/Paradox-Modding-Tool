## Befund

Die Mod-Seite braucht 5,5 s von 47,4 s Eigenzeit; der Rest ist Vanilla und lässt sich nicht ändern. Der größte
Einzelposten ist die Forscher-Rechnung `iv2_res_skill_bonus_calc_adm` (Nr. 2, Gesamtzeit 1,38 s bei rund 412.000
Aufrufen pro Jahr). Sie liest `var:Iv2_researcher_adm` ohne Prüfung: Fast alle Länder haben keinen Forscher, darum
schreibt jeder Aufruf einen Fehler (18.402 Einträge, etwa 1.500 pro Minute). Beide Warnungen (Variablenzugriff ohne
Guard und Fehler-Spam) zeigen auf dieselbe Ursache, und ich habe sie im Code bestätigt: Der Lesezugriff in Zeile 14
steht vor jeder Prüfung, und der `limit` weiter unten prüft nur `is_alive`, nicht ob die Variable existiert. Der
Hotspot Nr. 1 (`iv2_research_bureaucracy_upkeep`, 1,67 s) ruft diese Rechnung auf; seine Gesamtzeit ist darum zum
großen Teil dieselbe Zeit und darf nicht zu der von Nr. 2 addiert werden. Die vier KI-Listen (Nr. 3, 4, 6, 8) haben
je Aufruf sehr geringe Eigenzeit; sie laufen für jedes Land und fallen nur durch die Zahl der Aufrufe auf. Dort
würde ich nichts ändern: Eine engere Bedingung spart weniger als das Rauschen (0,05 s), und ein seltenerer Tick würde
die KI passiver machen. Nr. 5 und Nr. 7 sind klein und laufen selten (Nr. 7 zwölfmal) und sind keine Änderung wert.

## Vorschläge

**1. Forscher-Rechnung absichern**

- Stelle: `common/script_values/iv2_research_script_values.txt:14` (Nr. 2, Hinweise Variablenzugriff ohne Guard, Fehler-Spam, viele Aufrufe)
- Änderung: Den Forscher nur lesen, wenn er existiert, mit dem sicheren Lesen `?=`. Ohne Forscher liefert der Wert dann
  ruhig 0, statt jedes Mal einen Fehler zu schreiben:

```
iv2_res_skill_bonus_calc_adm = {
	value = 0
	var:Iv2_researcher_adm ?= {
		if = {
			limit = { is_alive = yes }
			add = {
				value = skill
				multiply = 1.5
			}
		}
	}
	min = 0
	max = global_var:iv2_research_cap_adm
}
```

  Für Länder mit lebendem Forscher bleibt das Ergebnis gleich (0,5 mal Skill plus 1 mal Skill ergibt 1,5 mal Skill).
  Für Länder ohne Forscher ist es 0, auch das wie vorher. Eine Änderung gibt es nur bei einem toten Forscher: Vorher
  zählten dessen 0,5 mal Skill noch, jetzt gibt es 0. Das ist vermutlich gewollt; sag mir, falls nicht. Ich kenne
  deine übrigen Skill-Definitionen nicht und habe `skill` im Scope des Forschers vorausgesetzt.
- Erwarteter Effekt: Die 18.000 Fehlereinträge entfallen, und der Wert läuft für Länder ohne Forscher fast ohne Arbeit.
  Ich erwarte, dass Total von Nr. 2 deutlich sinkt (Größenordnung: auf unter ein Viertel), und weil Nr. 1 diese Rechnung
  aufruft, sinkt auch dessen Gesamtzeit. Schätzung, kein Versprechen.
- Risiko: Gering. Hängt ein anderes Skript davon ab, dass ein Fehler geschrieben wird, fällt das weg (unwahrscheinlich).

**2. Obergrenze nicht bei jedem Aufruf neu berechnen**

- Stelle: `common/bureaucracies/iv2_research_system_bureaucracies.txt:101` (Nr. 1, Hinweis viele Aufrufe)
- Änderung: Den Unterhalt der Forschungsbürokratie nicht pro Aufruf aus den beiden Forscher-Rechnungen zusammensetzen,
  sondern einmal im Monat im bestehenden Effekt `iv2_research_monthly_calc_effect` berechnen und als Variable am Land
  speichern; der Script Value liest nur noch die Variable, abgesichert mit `?=`:

```
iv2_research_bureaucracy_upkeep = {
	value = 0
	if = {
		limit = { has_variable = Iv2_research_upkeep }
		add = var:Iv2_research_upkeep
	}
	min = 1
}
```

  Im Effekt `iv2_research_monthly_calc_effect` (Datei `common/scripted_effects/iv2_research_monthly.txt`, Block ab
  Zeile 18) kommt dazu, je Land:

```
set_variable = {
	name = Iv2_research_upkeep
	value = {
		add = iv2_res_skill_bonus_calc_adm
		add = iv2_res_skill_bonus_calc_dip
		multiply = 0.1
	}
}
```

- Erwarteter Effekt: Der Script Value mit rund 190.000 Aufrufen pro Jahr wird zu einem Variablenzugriff. Der Preis ist
  eine Verzögerung bis zu einem Monat, bis sich ein neuer Forscher im Unterhalt zeigt.
- Risiko: Mittel. Der Unterhalt reagiert nicht mehr sofort auf einen Forscherwechsel; lässt sich das nicht akzeptieren,
  nur Vorschlag 1 umsetzen und danach neu messen, bevor du Vorschlag 2 angehst.

**3. Tote Forscher-Verweise aufräumen**

- Stelle: `common/scripted_effects/iv2_research_monthly.txt:22` (Nr. 7)
- Änderung: Im selben monatlichen Effekt Verweise auf Forscher löschen, die nicht mehr leben (`remove_variable`), und
  beim Wechsel eines Forschers in ein anderes Land (`on_character_moved_country`) den Verweis des alten Landes
  löschen. Der Code hängt von deinen Variablennamen ab; zeig mir `on_character_moved_country` und die
  Forscher-Effekte, dann schreibe ich ihn aus.
- Erwarteter Effekt: Kein eigener Zeitgewinn an dieser Stelle, aber die Rechnung aus Vorschlag 1 trifft keine toten
  Verweise mehr.
- Risiko: Gering.

Nicht ändern würde ich die KI-Listen (Nr. 3, 4, 6, 8) und Nr. 5: Ihre Eigenzeit je Aufruf ist winzig, und alles, was
sie seltener laufen lässt, macht die KI passiver.

## Nachmessen

Gleicher Spielstand (`iv2_messung_1450`), gleiche 12 Monate, gleiches Playset, mit `Script.Profiling.Restart`
beginnen, 12 Monate spielen, dann `Script.Profiling.Stop` und `Script.Profiling.DumpAll`. In PMT beide Läufe im Reiter
Vergleich gegenüberstellen und auf Folgendes achten:

- Total und Calls von Nr. 2 und Nr. 1 sollen sinken; ein Rückgang unter der Rauschschwelle (10 % und 0,05 s) zählt nicht.
- Im neuen Lauf soll es keine Hinweise Variablenzugriff ohne Guard und Fehler-Spam mehr für die Datei
  `iv2_research_script_values.txt` geben, und das error.log des Laufs soll fast leer sein.
- Die Calls der KI-Listen (Nr. 3, 4, 6, 8) müssen ungefähr gleich bleiben (Abweichung unter 25 %). Ändern sie sich
  stärker, ist die KI passiver oder aktiver geworden und der Vergleich mit "Verhalten geändert" zu lesen.
- Die Forscher selbst sollen in einem Testland weiterhin ihren Bonus liefern; schau dazu im Spiel nach, ob der
  Forschungsbonus eines Landes mit Forscher unverändert ist.

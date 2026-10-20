"""Schneidet einen grossen Profiler-Dump (Script.Profiling.Dump) auf eine kleine Testdatei zusammen.

Aufruf (ohne stdin): python -I tools/cut-profiling-dump.py <source.csv> <out.csv> [--keys <other-cut.csv>] [--top N]

Geschrieben werden die Kopfzeile und dann, in der Reihenfolge der Quelle und unveraendert im Text:
  - jede Zeile, deren "File Location" location_pulses.txt:38 oder d009_country_monthly.txt:1 enthaelt,
  - die N Zeilen (Standard 250) mit der hoechsten "Total Time",
  - 20 Zeilen mit "Total Time" 0 (die ersten der Quelle),
  - mit --keys jede Zeile, deren Schluessel (erste Spalte) in der anderen Schnittdatei vorkommt.
Gibt die Zeilenzahl der Ausgabe (ohne Kopfzeile) aus.
"""
import argparse
import sys

MARKERS = ("location_pulses.txt:38", "d009_country_monthly.txt:1")
TOTAL_TIME_COLUMN = 6
ZERO_ROWS = 20


def read_lines(path):
    # newline="" erhaelt die Zeilenenden der Quelle.
    with open(path, "r", encoding="utf-8", newline="") as handle:
        return handle.readlines()


def key_of(line):
    return line.split(";", 1)[0]


def total_time(line):
    columns = line.rstrip("\r\n").split(";")
    try:
        return float(columns[TOTAL_TIME_COLUMN])
    except (IndexError, ValueError):
        return 0.0


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("source")
    parser.add_argument("out")
    parser.add_argument("--keys", help="andere Schnittdatei; ihre Schluessel werden ebenfalls uebernommen")
    parser.add_argument("--top", type=int, default=250, help="Anzahl der Zeilen mit der hoechsten Total Time")
    args = parser.parse_args()

    lines = read_lines(args.source)
    if not lines:
        sys.exit("Quelle ist leer.")
    header, rows = lines[0], lines[1:]

    keep = set()
    for index, line in enumerate(rows):
        if any(marker in key_of(line) for marker in MARKERS):
            keep.add(index)

    by_total = sorted(range(len(rows)), key=lambda i: (-total_time(rows[i]), i))
    keep.update(by_total[: args.top])

    zeros = [i for i, line in enumerate(rows) if total_time(line) == 0.0]
    keep.update(zeros[:ZERO_ROWS])

    if args.keys:
        other_keys = {key_of(line) for line in read_lines(args.keys)[1:]}
        keep.update(i for i, line in enumerate(rows) if key_of(line) in other_keys)

    with open(args.out, "w", encoding="utf-8", newline="") as handle:
        handle.write(header)
        for index in sorted(keep):
            handle.write(rows[index])

    print(len(keep))


if __name__ == "__main__":
    main()

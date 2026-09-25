# EU IV Wiki Script-Docs scraper

Generates `profiles/scriptdocs/eu4.json`, the pre-baked Script-Docs data PMT ships for
Europa Universalis IV (which exports no `docs/*.log` files, unlike EU V). See
`docs/superpowers/specs/2026-09-22-wiki-scriptdocs-eu4-design.md` for why this exists and
how the output feeds into PMT.

## Refreshing the data

`eu4.paradoxwikis.com` sits behind a JS bot-challenge that blocks plain HTTP clients
(`urllib`, `curl`, and MediaWiki's own `api.php`/`rest.php`/`index.php?action=raw`
endpoints all return an identical, content-free "Client Challenge" HTML shell — verified
while building this tool). A real browser clears the challenge fine. Refreshing this data
therefore needs a browser-capable session (e.g. Claude Code with its built-in browser tool)
rather than a fully unattended script:

1. For each of the 5 pages — `Effects`, `Triggers`, `Modifier_list`, `Scopes`,
   `On_Actions` — open `https://eu4.paradoxwikis.com/index.php?title=<Page>&action=raw` in
   the browser and read its full text (the browser tool's `get_page_text` with a large
   `max_chars` works; long pages get saved to a tool-result JSON file instead of returned
   inline).
2. Save each page's raw wikitext into `wikitext/<Page>.wikitext`, overwriting the old
   snapshot. If the content came back as a browser tool-result JSON file, run
   `python extract_browser_snapshot.py <tool-result.json> wikitext/<Page>.wikitext` to
   strip the tool's own header/footer and get a clean file.
3. Run:
   ```
   cd tools/wiki-scraper
   python build_eu4.py
   ```
4. Review the printed distinct-scope-values list for anything suspicious — an
   organizational heading like "Military"/"Navy" leaking through as a scope value means a
   page's structure changed and a parser in `parse_eu4.py` needs a look (see the module
   docstring there for how each page's headings map to scopes).
5. Commit the updated `wikitext/*.wikitext` snapshots and the regenerated
   `profiles/scriptdocs/eu4.json`, and ship a new PMT release.

Requires Python 3, standard library only (no `pip install` needed). Tests:
`python -m unittest test_parse_eu4 -v` — these run against small fixed fixtures, not live
data, so they don't need network or browser access.

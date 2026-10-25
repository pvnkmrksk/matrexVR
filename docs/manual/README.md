# Maintain the static manual

The committed website under `docs/site/manual/` is built from the Markdown chapters selected in `navigation.json`, including the existing reference manuals in `docs/`. Readers need only a browser. GitHub Pages deploys the existing `docs/site` tree; no new release or hosting service is created.

With Python 3.9+ and Pandoc installed, run from the repository root:

```sh
python3 tools/build_docs_site.py
python3 tools/test_docs_site.py
node tools/test_manual_search.cjs
# Before publishing a complete gallery:
python3 tools/test_docs_site.py --require-gallery
```

Add a chapter to `navigation.json` with its group, title, unique slug, source path, icon ID and short description. Cross-links between registered source Markdown files become local HTML links. Referenced images/reports are copied into the static manual, while source code and unregistered source files link to the repository. Every generated page includes a source link, previous/next navigation and an on-page section index. Search is generated from actual rendered sections and runs locally in the browser.

The native gallery reads `docs/site/assets/gallery-manifest.json`. Add genuine Unity captures and provenance under `docs/site/assets/gallery/`, then rebuild. A missing capture remains explicitly pending; do not replace it with an invented image or imply hardware validation. Keep the compact-build/source-only distinction in captions.

Lucide SVG paths and the ISC license are bundled under `docs/site/manual/assets/lucide/`. The sprite preserves those paths. Main-site downloads retain their separate published-asset discovery script.

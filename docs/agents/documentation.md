# Documentation agent

Read [repository instructions](../../AGENTS.md), then the relevant implementation
and existing guide. Readers include operators using a downloaded player and
contributors building source; identify which workflow each instruction describes.

## Sources and ownership

- `README.md` is the front door. Put a working first-run path before detailed
  development history. Link to focused guides instead of duplicating them.
- `docs/getting-started.md`, `docs/ai-control.md`, and topic guides hold Markdown
  source. `docs/manual/navigation.json` defines the published manual's chapters.
- `tools/build_docs_site.py` generates `docs/site/manual/`, search data, schema
  reference pages, and copied reference assets. It requires installed Pandoc.
- `docs/site/index.html` and its site assets form the landing page. GitHub Pages
  deploys the committed `docs/site` tree on matching pushes to `main`; editing
  Markdown alone does not regenerate or publish the manual.
- Configuration schema source lives in `Assets/Editor/ConfigurationSchemaBuilder.cs`.
  Its generated JSON is `Assets/Resources/ConfigurationUI/schemas.json`.

## Completion checklist

1. Check controls, option spelling, file locations, dependency versions, and
   release claims against source. Separate released behavior from local preview
   behavior. Downloads must describe actual published assets.
2. Give copyable commands with explicit paths and success checkpoints. A quick
   start should reach a visible result using a no-hardware demo, then link to
   calibrated acquisition instructions.
3. If a manual source or navigation changes, regenerate and verify:

   ```sh
   python3 tools/build_docs_site.py
   python3 tools/test_docs_site.py
   node tools/test_manual_search.cjs
   node tools/test_release_downloads.cjs
   ```

   Use `python3 tools/test_docs_site.py --require-gallery` when publishing a gallery
   update; this additionally requires real capture provenance and images.
4. Inspect generated diffs and preview affected pages at desktop and narrow
   widths for layout work. Keep image credits/provenance and meaningful alt text.
   Check relative Markdown links for guides that are not manual chapters.
5. State which instructions or pages changed and the checks actually run. Keep
   historical validation reports historical; create new evidence for new claims.

For configuration examples, also run
`python3 tools/validate_config_templates.py`. For onboarding paths, consult
`RuntimePaths.cs`, `DesktopQuickStart.cs`, and `tools/build_local_desktop.py` rather
than assuming the release, Editor, and local preview use the same directories.

# Historical configuration archive

`Legacy/` preserves the old StreamingAssets configuration collection and generator scripts in their original relative layout. `Legacy/Templates/` contains snapshots of the templates before the 2026-10-06 cleanup. No historical JSON was reformatted or silently migrated. Checksums and original paths are recorded in [config-archive-manifest.json](../../../docs/config-archive-manifest.json).

`LegacyLocustInputs/` consolidates the earlier historical locust collection previously under `docs/legacy-locust-inputs`, also without content changes.

Use `Templates/` for complete current starting points and `Examples/` for maintained variations. Archived experiments can contain obsolete fields, disabled scenes, old machine addresses or missing historical assets; retaining a file is not a claim that its experimental behavior has been revalidated.

When an experiment reference is absent from its current StreamingAssets location, the loader also checks `Archive/Legacy/<original reference>`. An existing current file always wins. This preserves the root-level Choice config referenced by the untouched `Kannadi/sequenceConfig_JuliusTree.json`, and records the actual resolved config with the run. Absolute paths and missing files retain their normal behavior.

Active `system_config.json`, active `sequenceConfig.json`, the entire `Kannadi/` directory, and the Photosphere image assets were left in place. The system file remains local and Git-ignored. No filenames inside the archived originals were rewritten.

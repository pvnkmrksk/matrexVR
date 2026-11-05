# Hardware resource recovery

The [assembly worksheet](assembly.md), [generated part catalogue](../site/hardware/index.html) and [review bundle](../site/hardware/matrex-hardware-review.zip) cover five recovered source groups and nine mesh shapes. These are local review candidates, not a validated apparatus kit. No source redistribution license or matching parametric CAD history was established.

To regenerate from the original files in local Downloads:

```sh
# Python with numpy and Pillow available
python3 tools/prepare_hardware_resources.py --downloads "$HOME/Downloads"
python3 tools/build_hardware_site.py   # requires Pandoc
python3 tools/test_hardware_resources.py
node tools/test_release_downloads.cjs
```

`prepare_hardware_resources.py` inventories filename candidates and exports only the explicit `SELECTED` source groups. It resolves nested 3MF transforms and mirrors, preserves source rotation, removes plate translation, collapses identical mesh instances and emits geometry-only millimetre 3MF plus binary STL. Source slicer projects remain untouched. PNG illustrations are actual projected triangles. Neither export nor topology checks certify printability or fit.

`build_hardware_site.py` renders the Markdown assembly worksheet, generates the catalogue, and bundles geometry, illustrations, inventory and offline HTML/CSS. The archive excludes original slicer profiles and personal designer identifiers. It contains no G-code. Local source filenames and SHA-256 hashes identify provenance; matching CAD, hardware quantities, dimensions at mating surfaces, fastening and print settings require designer confirmation.

See [the weekend review report](review-20261101.md) for evidence and remaining inputs.

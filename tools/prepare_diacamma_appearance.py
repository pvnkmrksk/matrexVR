#!/usr/bin/env python3
"""Extract reference grass and install appearance only; requires Pillow.

No resizing, recolouring, protocol edits, or AI generation. Crop coordinates
are Pillow pixels (left, top, right, bottom), with the origin at top left.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from PIL import Image, __version__ as pillow_version

ROOT = Path(__file__).resolve().parents[1]
RELATIVE = Path('Experiments/PathAnglePilot')
SOURCE_SHA256 = '57fd50e7a77a07fc91d6c052c3db5ef4db5930c167c5809287f72e93ae773f94'
CROP = (0, 3072, 1024, 4096)  # Grass only, well below every painted path.


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path, help='Original 85deg_both.png')
    parser.add_argument('--install-config', type=Path, action='append', default=[],
                        help='Also install into this Config root (repeatable). No protocol/system edits.')
    args = parser.parse_args()
    if digest(args.reference) != SOURCE_SHA256:
        parser.error('Reference differs from the approved 85deg_both.png (SHA-256 mismatch).')
    target = ROOT / 'Assets/StreamingAssets' / RELATIVE
    tile = target / 'diacamma-grass.png'
    with Image.open(args.reference) as image:
        if image.size != (4096, 4096):
            parser.error('Expected a 4096 × 4096 reference.')
        image.convert('RGB').crop(CROP).save(tile, compress_level=9)
    appearance_file = target / 'appearance.json'
    appearance = json.loads(appearance_file.read_text())
    appearance.update(pathWidthCm=3, pathDarkness=0.5, pathEdgeSoftnessCm=1.0, groundBrightness=1,
                      textureScaleCm=50,
                      texturePath=(RELATIVE / tile.name).as_posix(),
                      preparedGroundImage=False)
    appearance_file.write_text(json.dumps(appearance, indent=2) + '\n')
    manifest = target / 'diacamma-grass.provenance.json'
    manifest.write_text(json.dumps(dict(
        sourceFile='85deg_both.png', sourceSha256=SOURCE_SHA256,
        sourceSizePixels=[4096, 4096], cropPixels=list(CROP),
        operation='RGB conversion and crop only; no resampling or colour adjustment',
        tileSha256=digest(tile), pillowVersion=pillow_version,
        appearance=appearance,
        scaleBasis='Approximate 200 cm reference square; 1024/4096 crop repeats every 50 cm. '
                   'Reference stem is about 60 pixels wide (about 3 cm), with about 50% darkening.'
    ), indent=2) + '\n')
    for config_root in args.install_config:
        destination = config_root.resolve() / RELATIVE
        if destination == target.resolve():
            continue
        current = destination / appearance_file.name
        if not current.is_file():
            parser.error(f'Existing pilot appearance is missing: {current}')
        # Preserve local extent/height and any unknown fields. Change visual style only.
        local = json.loads(current.read_text())
        for key in ('pathWidthCm', 'pathDarkness', 'pathEdgeSoftnessCm', 'groundBrightness', 'textureScaleCm',
                    'texturePath', 'preparedGroundImage'):
            local[key] = appearance[key]
        backup = destination / 'appearance.before-diacamma.json'
        if not backup.exists():
            shutil.copy2(current, backup)
        for asset in (tile, manifest):
            shutil.copy2(asset, destination / asset.name)
        current.write_text(json.dumps(local, indent=2) + '\n')
        print(f'Installed appearance: {current} (original: {backup.name})')
    print(f'Grass tile: {tile}; SHA-256: {digest(tile)}')


if __name__ == '__main__':
    main()

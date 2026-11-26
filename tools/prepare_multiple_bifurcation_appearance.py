#!/usr/bin/env python3
"""Create a separate pilot appearance from the packed MultipleBifurcation build.

Requires UnityPy 1.25.2 and Pillow. Reads the reference build without changing it.
The normal pilot profile is preserved. Only a new profile's appearance is changed.
"""
import argparse
import hashlib
from importlib.metadata import version
import json
from pathlib import Path
import shutil

import UnityPy

ROOT = Path(__file__).resolve().parents[1]
RELATIVE = Path('Experiments/PathAnglePilot')
CHECKSUMS = {
    'sharedassets5.assets': '7f6f3f85a6fe2e2e59c2d7f863956ab7e9db5323021bb00eefd2d10f84c64d49',
    'sharedassets5.assets.resS': 'bc02fe5968844804040be0262643320a872f8f58827aae73fb4288d17fba50fa',
}
CROP = (0, 3072, 1024, 4096)
VARIANT = 'multiple-bifurcation-v1'


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('data_directory', type=Path, help='MultipleBifurcation_Data directory')
    parser.add_argument('--base-config', type=Path, required=True, help='Existing pilot Config root to copy')
    parser.add_argument('--output-config', type=Path, required=True, help='Separate new Config root')
    args = parser.parse_args()
    base, output = args.base_config.resolve(), args.output_config.resolve()
    if base == output or base in output.parents or output in base.parents:
        parser.error('Base and new profile must be separate, non-nested directories.')
    if not (base / RELATIVE / 'appearance.json').is_file():
        parser.error('Base profile has no pilot appearance.json.')
    marker = output / 'appearance-variant.json'
    if output.exists() and (not marker.is_file() or json.loads(marker.read_text()).get('variant') != VARIANT):
        parser.error('Refusing to overwrite an existing unrelated profile.')
    for name, expected in CHECKSUMS.items():
        if sha256(args.data_directory / name) != expected:
            parser.error(f'Reference checksum mismatch: {name}')
    env = UnityPy.load(str(args.data_directory / 'sharedassets5.assets'))
    images = [o.read() for o in env.objects if o.type.name == 'Texture2D' and o.peek_name() == 'DataBifurcation']
    if len(images) != 1:
        parser.error('Expected one DataBifurcation texture in sharedassets5.assets.')
    image = images[0].image.convert('RGB')
    if image.size != (4096, 4096):
        parser.error('Unexpected reference texture dimensions.')
    target = ROOT / 'Assets/StreamingAssets' / RELATIVE
    tile = target / 'multiple-bifurcation-grass.png'
    image.crop(CROP).save(tile, compress_level=9)
    appearance = json.loads((base / RELATIVE / 'appearance.json').read_text())
    appearance.update(pathWidthCm=5.0, pathDarkness=0.65, pathEdgeSoftnessCm=3.0,
                      pathEdgeRoughnessCm=0.3, pathMottling=0.02,
                      groundBrightness=1, textureScaleCm=250,
                      texturePath=(RELATIVE / tile.name).as_posix(), preparedGroundImage=False)
    preset = target / 'appearance.multiple-bifurcation.json'
    write_json(preset, appearance)
    provenance = target / 'multiple-bifurcation.provenance.json'
    write_json(provenance, dict(
        variant=VARIANT, sourceBuild='DiacammaMultipleBifurcation/MultipleBifurcation_Data',
        sourceFilesSha256=CHECKSUMS, unityPyVersion=version('UnityPy'), pillowVersion=version('Pillow'),
        texture=dict(name='DataBifurcation', assetFile='sharedassets5.assets', pathId=4,
                     sizePixels=[4096, 4096], cropPixels=list(CROP)),
        tileSha256=sha256(tile), appearance=appearance,
        evidence=dict(terrainLayer='sharedassets5.assets:30', terrainScene='level5',
                      terrainTileSizeCm=1000, cropTileSizeCm=250,
                      material='Default-Terrain-Standard', shader='Nature/Terrain/Standard',
                      terrainHeightCm=-1, referenceStemWidthPixelsAbout=18,
                      referenceHalfDarknessWidthCmAbout=5,
                      reference10To90EdgeTransitionCmAbout=1.8,
                      referencePathToGrassRgbRatioAbout=[0.34, 0.35, 0.36]),
        adaptation='Keep pilot angles, goal positions, triggers, and apparatus settings. '
                   'Rounded procedural path approximately matches measured width/darkness; '
                   'use a 3 cm fade, 0.3 cm fixed edge irregularity and 2% darkness mottling. '
                   'Refined half-darkness width is about 5 cm; 10–90% transition about 1.8 cm. '
                   'Grass is an exact decoded RGB crop. '
                   'Pilot uses unlit rendering, not the old scene ambient lighting.'
    ))
    if not output.exists():
        shutil.copytree(base, output)
        write_json(marker, dict(variant=VARIANT, copiedFrom=str(base)))
    destination = output / RELATIVE
    for asset in (tile, preset, provenance):
        shutil.copy2(asset, destination / asset.name)
    # On regeneration, preserve local ground extent/height and any unknown settings.
    current = json.loads((destination / 'appearance.json').read_text())
    for key in ('pathWidthCm', 'pathDarkness', 'pathEdgeSoftnessCm', 'pathEdgeRoughnessCm', 'pathMottling', 'groundBrightness',
                'textureScaleCm', 'texturePath', 'preparedGroundImage'):
        current[key] = appearance[key]
    write_json(destination / 'appearance.json', current)
    print(f'New pilot profile: {output}')
    print(f'Grass tile SHA-256: {sha256(tile)}')


if __name__ == '__main__':
    main()

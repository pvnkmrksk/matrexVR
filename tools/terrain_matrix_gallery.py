#!/usr/bin/env python3
"""Package sixteen genuine Unity terrain captures without editing stimulus pixels."""
from pathlib import Path
import argparse
import hashlib
import html
import json
import os
import tempfile

import numpy as np
from PIL import Image, ImageDraw, ImageFont

os.environ.setdefault('MPLCONFIGDIR', str(Path(tempfile.gettempdir()) / 'matrex-terrain-gallery-matplotlib'))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

MODES = {
    'matched-terrain': 'Visible terrain + terrain navigation',
    'visual-terrain_navigation-flat': 'Visible terrain + flat navigation',
    'visual-flat_navigation-terrain': 'Flat visual + terrain navigation',
    'matched-flat': 'Flat visual + flat navigation',
}
PATTERNS = {
    'RoughSmooth': 'Rough left / smooth right',
    'SmoothRough': 'Smooth left / rough right',
    'SmoothSmooth': 'Both smooth',
    'RoughRough': 'Both rough',
    'Flat': 'Flat',
}
FONT = '/System/Library/Fonts/HelveticaNeue.ttc'


def font(size):
    return ImageFont.truetype(FONT, size)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    args = parser.parse_args()
    directory = args.directory.resolve()
    report = json.loads((directory / 'results.json').read_text())
    assert report['failure'] is None and len(report['cases']) == 16
    images = directory / 'screenshots'
    images.mkdir(exist_ok=True)
    hashes = {}
    comparisons = []
    tiles = []
    for case in report['cases']:
        raw = Path(case['directory'])
        item = Image.new('RGB', (1600, 1740), '#f3f6f8')
        draw = ImageDraw.Draw(item)
        draw.rectangle((0, 0, 1600, 118), fill='#162b38')
        draw.text((30, 18), MODES[case['mode']], font=font(32), fill='white')
        draw.text((30, 64), PATTERNS[case['pattern']], font=font(30), fill='#99d7cb')
        for i, side in enumerate(('left', 'right')):
            x = 24 + i * 784
            draw.text((x, 138), f'ACTUAL STIMULUS · {side.upper()} PROBE', font=font(23), fill='#183d4c')
            item.paste(Image.open(raw / f'animal-{side}.png').convert('RGB'), (x, 178))
            probe = case['probes'][i]
            draw.text((x, 958), f"Eye Y {probe['position']['y']:.3f} cm · tilt {probe['navigationTiltDegrees']:.2f}°", font=font(23), fill='#183d4c')
        for i, (key, label) in enumerate((('visual-clay-debug.png', 'DEBUG CLAY · VISIBLE GEOMETRY'), ('navigation-clay-debug.png', 'DEBUG CLAY · COLLISION GEOMETRY'))):
            x = 24 + i * 784
            draw.text((x, 1007), label, font=font(22), fill='#183d4c')
            overview = Image.open(raw / key).convert('RGB').resize((768, 432), Image.Resampling.LANCZOS)
            item.paste(overview, (x, 1040))
        section = case['crossSection']
        fig, axis = plt.subplots(figsize=(16, 2.4), dpi=100, layout='constrained')
        fig.set_facecolor('#f3f6f8'); axis.set_facecolor('#ffffff')
        axis.plot(section['x'], section['visual'], color='#147868', linewidth=2, label='Visible mesh')
        axis.plot(section['x'], section['navigation'], color='#c26119', linestyle='--', linewidth=1.8, label='Navigation mesh')
        axis.axvline(0, color='#90a6ad', linewidth=.8)
        axis.set(xlim=(-90, 90), ylim=(-.02, .85), xlabel='World X (cm): left ← 0 → right', ylabel='Ground height (cm)', title='DEBUG: true height cross-section at Z = 28.125 cm; same axes in every case')
        axis.grid(alpha=.2); axis.legend(loc='upper right', ncol=2)
        plot = raw / 'height-cross-section.png'; fig.savefig(plot, dpi=100); plt.close(fig)
        item.paste(Image.open(plot).convert('RGB'), (0, 1490))
        target = images / f"{case['name']}.jpg"
        item.save(target, quality=94, subsampling=0)
        tiles.append(item)
        hashes[case['name']] = {name: sha(raw / name) for name in ('animal-left.png', 'animal-right.png', 'animal-left-led128.png', 'animal-right-led128.png', 'visible-overview.png', 'navigation-overview.png', 'visual-clay-debug.png', 'navigation-clay-debug.png')}
    # A broad matrix uses the same fixed overview in every cell. It must show
    # expected duplicates when only navigation differs, rather than fabricating
    # sixteen distinct visible stimuli.
    # Keep all four rows uncropped; smaller contact previews do not replace the originals.
    matrix = Image.new('RGB', (3200, 3660), '#f3f6f8')
    draw = ImageDraw.Draw(matrix)
    draw.rectangle((0, 0, 3200, 180), fill='#162b38')
    draw.text((30, 20), 'Terrain verification: 4 treatments × 4 spatial scenes', font=font(48), fill='white')
    draw.text((30, 90), 'Actual animal view above; true-scale debug geometry below. Height plots use identical, labeled axes.', font=font(29), fill='#b8d6dd')
    for index, tile in enumerate(tiles):
        matrix.paste(tile.resize((800, 870), Image.Resampling.LANCZOS), ((index % 4) * 800, 180 + (index // 4) * 870))
    matrix.save(directory / 'terrain-16-contact-sheet.jpg', quality=95, subsampling=0)
    for row in range(4):
        strip = Image.new('RGB', (3200, 870), '#f3f6f8')
        for column in range(4):
            strip.paste(tiles[row * 4 + column].resize((800, 870), Image.Resampling.LANCZOS), (800 * column, 0))
        strip.save(directory / f'treatment-{row + 1}-four-scenes.jpg', quality=95, subsampling=0)
    led_sheet = Image.new('RGB', (1280, 960), '#f3f6f8')
    draw = ImageDraw.Draw(led_sheet)
    draw.rectangle((0, 0, 1280, 112), fill='#162b38')
    draw.text((20, 18), 'Actual standard front camera: 128 × 128 pixels', font=font(30), fill='white')
    draw.text((20, 66), 'Left/right steep probes. Every image below is at its original pixel size.', font=font(23), fill='#b8d6dd')
    short_modes = ('Matched relief', 'Visual terrain / nav flat', 'Visual flat / nav terrain', 'Both flat')
    for index, case in enumerate(report['cases']):
        x, y = (index % 4) * 320, 112 + (index // 4) * 212
        draw.text((x + 16, y + 12), short_modes[index // 4], font=font(18), fill='#162b38')
        draw.text((x + 16, y + 40), PATTERNS[case['pattern']], font=font(17), fill='#147868')
        for side, offset in (('left', 24), ('right', 168)):
            frame = Image.open(Path(case['directory']) / f'animal-{side}-led128.png').convert('RGB')
            assert frame.size == (128, 128)
            led_sheet.paste(frame, (x + offset, y + 74))
    led_sheet.save(directory / 'led128-16-contact-sheet.png')
    # Compare raw pixels, not composite annotations.
    for a_index, a in enumerate(report['cases']):
        for b in report['cases'][a_index + 1:]:
            if a['pattern'] != b['pattern']:
                continue
            per_view = {}
            for name in ('animal-left.png', 'animal-right.png', 'animal-left-led128.png', 'animal-right-led128.png', 'visible-overview.png', 'navigation-overview.png'):
                aa = np.asarray(Image.open(Path(a['directory']) / name).convert('RGB'), dtype=np.int16)
                bb = np.asarray(Image.open(Path(b['directory']) / name).convert('RGB'), dtype=np.int16)
                difference = np.abs(aa - bb)
                per_view[name] = {'identical': bool(np.array_equal(aa, bb)), 'meanAbsoluteChannelDifference': float(difference.mean()), 'changedPixelFraction': float((difference.max(axis=2) > 0).mean())}
            comparisons.append({'a': a['name'], 'b': b['name'], 'views': per_view})
    groups, eye_groups, led_groups = {}, {}, {}
    for case in report['cases']:
        digest = hashes[case['name']]['visible-overview.png']
        groups.setdefault(digest, []).append(case['name'])
        pair = (hashes[case['name']]['animal-left.png'], hashes[case['name']]['animal-right.png'])
        eye_groups.setdefault(pair, []).append(case['name'])
        led_pair = (hashes[case['name']]['animal-left-led128.png'], hashes[case['name']]['animal-right-led128.png'])
        led_groups.setdefault(led_pair, []).append(case['name'])
    summary = {
        'scope': report['scope'], 'checks': report['checks'], 'failure': report['failure'],
        'rawFrameCount': 128, 'reviewScreenshotCount': 16, 'uniqueFixedVisibleOverviewHashes': len(groups),
        'uniquePairedAnimalViewHashes': len(eye_groups), 'animalViewGroups': list(eye_groups.values()),
        'uniquePairedLed128ViewHashes': len(led_groups), 'led128ViewGroups': list(led_groups.values()),
        'visibleOverviewGroups': list(groups.values()), 'rawFrameHashes': hashes, 'withinSceneTreatmentPixelComparisons': comparisons,
        'expectation': 'Sixteen scene/treatment combinations do not imply sixteen unique visible geometries. The same visualSurface must yield the same fixed world image regardless of collision selection; eye views may change because the selected navigation surface changes actual eye height and tilt.',
        'imageProcessing': 'Only layout, labels, and documented thumbnail scaling. Original Unity PNG stimulus frames are retained without enhancement or manipulation. Clay-debug renders and same-axis height plots are explicitly diagnostic; neither changes the stimulus geometry or height scale.',
    }
    (directory / 'pixel-comparison.json').write_text(json.dumps(summary, indent=2) + '\n')
    cards = []
    for case in report['cases']:
        title = MODES[case['mode']] + ' — ' + PATTERNS[case['pattern']]
        image_path = 'screenshots/' + case['name'] + '.jpg'
        cards.append(f'<article><h2>{html.escape(title)}</h2><a href="{image_path}"><img src="{image_path}" loading="lazy" alt="{html.escape(title)}"></a><p>Visual: {html.escape(case["visual"])} · navigation: {html.escape(case["navigation"])}</p><p><a href="{case["name"]}/animal-left.png">Raw left eye 768px</a> · <a href="{case["name"]}/animal-right.png">Raw right eye 768px</a> · <a href="{case["name"]}/animal-left-led128.png">Actual front left 128px</a> · <a href="{case["name"]}/animal-right-led128.png">Actual front right 128px</a> · <a href="{case["name"]}/visible-overview.png">Raw textured overview</a> · <a href="{case["name"]}/visual-clay-debug.png">Visible clay geometry</a> · <a href="{case["name"]}/navigation-clay-debug.png">Collision clay geometry</a></p></article>')
    document = '<!doctype html><html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Terrain 16-case visual verification</title><style>body{font:16px system-ui;margin:24px;background:#f3f6f8;color:#162b38}header{max-width:1100px;margin-bottom:28px}h1{font-size:30px}main{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:16px}article{background:white;border:1px solid #cdd7dc;border-radius:8px;padding:12px}h2{font-size:16px;min-height:48px}img{width:100%;height:auto}p{line-height:1.5}a{color:#176d66}@media(max-width:1400px){main{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:700px){main{display:block}article{margin-bottom:16px}}</style><header><h1>Terrain: 4 treatments × 4 spatial scenes</h1><p>The upper panels contain actual animal-camera renders with the original geometry, forest texture, and lighting. Lower clay panels are explicitly debug-only: the ground material is temporarily neutral gray and a common low-angle light reveals the true mesh normals; geometry and scale are unchanged. Both are restored before continuing. Height plots use identical axes across every case. The upper images use the actual authored VR1/Main Camera F component (90° FOV, 0.01 near clip) at fixed left/right probe X/Z coordinates and fixed headings in every case. Large review frames are 768×768; additional raw 128×128 frames use that exact same camera and verified identical projection. Only the temporary render target and full viewport differ; both are restored. This is panel-resolution rendering proof, not native display timing or a physical LED test. Eye height and tilt are set by the real navigation follower, with the authored 1 cm ground clearance. The two lower debug views share one fixed world camera and show visible geometry versus collision geometry; these diagnostic views are not shown to the animal. The original textured fixed-camera frames remain available below each card.</p><p>Rows change the relation between visible and navigation surfaces; columns change the left/right spatial pattern. Sixteen combinations do not mean sixteen unique visible geometries. Identical fixed views are required when the visual surface stays the same. All four flat-visual/flat-navigation controls should be identical.</p><p><a href="terrain-16-contact-sheet.jpg">All sixteen screenshots</a> · <a href="led128-16-contact-sheet.png">Actual 128px front-camera matrix</a> · <a href="results.json">Geometry and pose metrics</a> · <a href="pixel-comparison.json">Raw-pixel comparisons</a></p></header><main>' + ''.join(cards) + '</main></html>'
    (directory / 'index.html').write_text(document)
    print(json.dumps({'gallery': str(directory / 'index.html'), 'contactSheet': str(directory / 'terrain-16-contact-sheet.jpg'), 'led128ContactSheet': str(directory / 'led128-16-contact-sheet.png'), 'screenshots': 16, 'rawFrames': 128, 'fixedVisibleImageHashes': len(groups), 'uniquePairedAnimalViewHashes': len(eye_groups), 'uniquePairedLed128ViewHashes': len(led_groups)}, indent=2))


if __name__ == '__main__':
    main()

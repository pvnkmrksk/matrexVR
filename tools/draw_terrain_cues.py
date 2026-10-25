#!/usr/bin/env python3
"""Draw reproducible cross sections from measured Unity geometry (Pillow + SVG)."""
import html
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "docs/terrain-cue-cross-sections.json"
OUT = ROOT / "docs/terrain-cue-four-conditions"
WIDTH, HEIGHT, SCALE = 1600, 1120, 2
INK, MUTED = "#192D3A", "#526774"
VIEW, NAV, EYE = "#247AB3", "#CF711D", "#7941A6"
image = Image.new("RGB", (WIDTH * SCALE, HEIGHT * SCALE), "#F7F8F9")
draw = ImageDraw.Draw(image)
svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH}" height="{HEIGHT}" viewBox="0 0 {WIDTH} {HEIGHT}">',
       '<title>Four visual and navigation terrain conditions</title>',
       '<desc>Measured cross sections from TerrainCueLab. Blue is visible ground, dashed orange is navigation ground, purple is the eye path. Arrows show left-to-right heading and terrain-induced pitch. Vertical scale is exaggerated.</desc>',
       '<rect width="100%" height="100%" fill="#F7F8F9"/>']
font_path = "/System/Library/Fonts/Supplemental/Arial.ttf"
bold_path = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"


def text(x, y, value, size=18, color=INK, bold=False):
    try:
        font = ImageFont.truetype(bold_path if bold else font_path, size * SCALE)
    except OSError:
        font = ImageFont.load_default(size=size * SCALE)
    draw.text((x * SCALE, y * SCALE), value, font=font, fill=color)
    svg.append(f'<text x="{x}" y="{y + size}" font-family="Arial,sans-serif" font-size="{size}" font-weight="{700 if bold else 400}" fill="{color}">{html.escape(value)}</text>')


def line(points, color, width=2, dashed=False):
    # Draw dashed segments explicitly so PNG and SVG have the same marks.
    if dashed:
        for a, b in zip(points, points[1:]):
            distance = math.dist(a, b)
            steps = max(1, math.ceil(distance))
            for i in range(steps):
                if int(a[0] + (b[0] - a[0]) * i / steps) % 15 < 9:
                    p = (a[0] + (b[0] - a[0]) * i / steps, a[1] + (b[1] - a[1]) * i / steps)
                    q = (a[0] + (b[0] - a[0]) * (i + 1) / steps, a[1] + (b[1] - a[1]) * (i + 1) / steps)
                    draw.line([(p[0] * SCALE, p[1] * SCALE), (q[0] * SCALE, q[1] * SCALE)], fill=color, width=width * SCALE)
    else:
        draw.line([(x * SCALE, y * SCALE) for x, y in points], fill=color, width=width * SCALE)
    coords = " ".join(f"{x:.2f},{y:.2f}" for x, y in points)
    svg.append(f'<polyline points="{coords}" fill="none" stroke="{color}" stroke-width="{width}"' + (' stroke-dasharray="9 6"' if dashed else '') + '/>')


def polygon(points, color):
    draw.polygon([(x * SCALE, y * SCALE) for x, y in points], fill=color)
    svg.append('<polygon points="' + " ".join(f"{x:.2f},{y:.2f}" for x, y in points) + f'" fill="{color}"/>')


def arrow(x, y, dx, dy):
    length = math.hypot(dx, dy)
    ux, uy = dx / length, dy / length
    a, b = (x - ux * 15, y - uy * 15), (x + ux * 15, y + uy * 15)
    line([a, b], EYE, 3)
    polygon([b, (b[0] - ux * 9 - uy * 5, b[1] - uy * 9 + ux * 5),
             (b[0] - ux * 9 + uy * 5, b[1] - uy * 9 - ux * 5)], EYE)


text(40, 26, "Visible terrain and navigation terrain", 36, bold=True)
text(40, 77, "Four primary conditions • identical 1 cm clearance above the navigation surface", 22, MUTED)
for x, label, color, dashed in [(40, "Visible surface", VIEW, False), (370, "Navigation / collision surface", NAV, True), (910, "Eye path + direction of travel", EYE, False)]:
    line([(x, 143), (x + 65, 143)], color, 4, dashed)
    text(x + 80, 128, label, 20)

panels = json.loads(DATA.read_text())
labels = [
    ("A   Matched flat", "Visual: Flat   |   Navigation: Flat", "Constant height and level orientation"),
    ("B   Matched terrain", "Visual: RoughSmooth   |   Navigation: RoughSmooth", "Hugs the terrain: height and tilt change"),
    ("C   Visual terrain / flat navigation", "Visual: RoughSmooth   |   Navigation: Flat", "Visible bumps; horizontal, level movement"),
    ("D   Flat visual / terrain navigation", "Visual: Flat   |   Navigation: RoughSmooth", "Flat visible plane; hidden terrain changes height and tilt"),
]
for index, (panel, (title, subtitle, caption)) in enumerate(zip(panels, labels)):
    ox, oy = 40 + (index % 2) * 780, 195 + (index // 2) * 430
    draw.rounded_rectangle((ox * SCALE, oy * SCALE, (ox + 740) * SCALE, (oy + 402) * SCALE), radius=12 * SCALE, fill="#FFFFFF", outline="#D6DEE3", width=SCALE)
    svg.append(f'<rect x="{ox}" y="{oy}" width="740" height="402" rx="12" fill="#FFFFFF" stroke="#D6DEE3"/>')
    text(ox + 22, oy + 17, title, 24, bold=True)
    text(ox + 22, oy + 53, subtitle, 18, MUTED)
    left, top, width, height = ox + 65, oy + 113, 645, 205

    def xy(x, y):
        return left + (x + 20) / 40 * width, top + (2 - y) / 2.15 * height

    for h in (0, 1, 2):
        py = xy(0, h)[1]
        line([(left, py), (left + width, py)], "#E8EDF0", 1)
        text(left - 29, py - 11, str(h), 16, MUTED)
    text(left - 28, top - 24, "cm", 15, MUTED)
    for x in (-20, 0, 20):
        px, py = xy(x, -.15)
        text(px - 12, py + 6, str(x), 16, MUTED)
    samples = panel["samples"]
    polygon([xy(-20, -.15)] + [xy(s["x"], s["visual"]) for s in samples] + [xy(20, -.15)], "#E7F2F9")
    line([xy(s["x"], s["visual"]) for s in samples], VIEW, 5)
    line([xy(s["x"], s["navigation"]) for s in samples], NAV, 3, True)
    line([xy(s["x"], s["eye"]) for s in samples], EYE, 2)
    for i in range(23, 395, 44):
        s = samples[i]
        px, py = xy(s["x"], s["eye"])
        arrow(px, py, s["forwardX"] * width / 40, -s["forwardY"] * height / 2.15)
    text(ox + 24, oy + 366, caption, 19)
    if index == 0:
        px, py = xy(15, 0)
        _, ey = xy(15, 1)
        line([(px, py + 1), (px, ey - 1)], MUTED, 1)
        line([(px - 4, py), (px + 4, py)], MUTED, 1)
        line([(px - 4, ey), (px + 4, ey)], MUTED, 1)
        text(px + 10, (py + ey) / 2 - 10, "1 cm", 16, MUTED)

text(40, 1069, "Cross-section at z = 0, x = −20 to +20 cm. Travel →. Vertical scale exaggerated; arrow angles share that scale.", 19, MUTED)
svg.append("</svg>")
OUT.with_suffix(".svg").write_text("\n".join(svg) + "\n")
image.resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS).save(OUT.with_suffix(".png"))
print(OUT.with_suffix(".png"))

#!/usr/bin/env python3
"""Check scene/catalog GUIDs and build registration without needing Unity."""
from collections import defaultdict
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
ids = defaultdict(list)
errors = []
paths = defaultdict(list)
for meta in (ROOT / "Assets").rglob("*.meta"):
    match = re.search(r"^guid: ([0-9a-f]{32})", meta.read_text(errors="ignore"), re.M)
    if match:
        ids[match[1]].append(Path(str(meta)[:-5]))
    paths[str(meta.relative_to(ROOT)).casefold()].append(meta)
for guid, assets in ids.items():
    if len(assets) > 1:
        errors.append(f"Duplicate GUID {guid}: {assets}")
for names in paths.values():
    if len(names) > 1:
        errors.append(f"Case collision: {names}")
settings = (ROOT / "ProjectSettings/EditorBuildSettings.asset").read_text()
pending = [ROOT / "Assets/Resources/KannadiPrefabCatalog.asset"]
for scene in ("Kannadi", "Swarm"):
    path = ROOT / f"Assets/Scenes/{scene}.unity"
    if f"enabled: 1\n    path: Assets/Scenes/{scene}.unity" not in settings:
        errors.append(f"{scene} is not enabled in build settings")
    pending.append(path)
for removed in ("Assets/Scenes/Matrix.unity", "Assets/Scripts/Swarm/Vishwaroopa.cs"):
    if (ROOT / removed).exists():
        errors.append(f"Removed legacy mode still present: {removed}")
if "Assets/Scenes/Matrix.unity" in settings:
    errors.append("Removed legacy mode remains in build settings")
seen = set()
while pending:
    asset = pending.pop()
    if asset in seen:
        continue
    seen.add(asset)
    for guid in set(re.findall(r"guid: ([0-9a-f]{32})", asset.read_text(errors="ignore"))):
        if guid.startswith("000000"):
            continue  # Unity built-in resources.
        matches = ids.get(guid, [])
        if not matches:
            errors.append(f"Missing GUID {guid} referenced from {asset.relative_to(ROOT)}")
            continue
        target = matches[0]
        if not target.exists():
            errors.append(f"Missing asset {target.relative_to(ROOT)}")
        elif target.suffix in (".prefab", ".mat", ".controller"):
            pending.append(target)
if errors:
    raise SystemExit("\n".join(errors))
print(f"PASS: unique GUIDs, enabled scenes, and {len(seen)} scene/catalog dependency files")

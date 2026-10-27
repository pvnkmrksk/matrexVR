#!/usr/bin/env python3
"""Validate shipped terrain treatments, shared defaults and original texture wiring."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SA = ROOT / "Assets/StreamingAssets"
FOLDER = SA / "Examples/Rugosity"
PATTERNS = ["RoughSmooth", "SmoothRough", "SmoothSmooth", "RoughRough"]
SCENES = ["TerrainChoice_RoughLeft_SmoothRight", "TerrainChoice_SmoothLeft_RoughRight", "TerrainChoice_BothSmooth", "TerrainChoice_BothRough"]
MODES = {
    "matched-terrain": (True, True),
    "visual-terrain_navigation-flat": (True, False),
    "visual-flat_navigation-terrain": (False, True),
    "matched-flat": (False, False),
}

def main():
    scene = (ROOT / "Assets/TerrainCueLab/TerrainChoiceWorld.prefab").read_text()
    registered = re.findall(r"^  surfaceId: (.+)$", scene, re.M)
    assert set(registered) == set(PATTERNS + ["Flat"])
    assert len(registered) == len(set(registered))
    prefab_guid = re.search(r"^guid: (.+)$", (ROOT / "Assets/TerrainCueLab/TerrainChoiceWorld.prefab.meta").read_text(), re.M)[1]
    rig_guid = re.search(r"^guid: (.+)$", (ROOT / "Assets/Prefabs/Camera Prefabs/VR1.prefab.meta").read_text(), re.M)[1]
    assert rig_guid in scene
    assert "rigTargets: []" in scene
    assert "value: Left choice target" in scene and "value: Right choice target" in scene
    for name in SCENES + ["TerrainCueLab"]:
        thin = (ROOT / "Assets/Scenes" / (name + ".unity")).read_text()
        assert prefab_guid in thin
        assert "MeshFilter:" not in thin and "Camera:" not in thin
    base = json.loads((FOLDER / "terrain-treatment.json").read_text())
    assert base["rugosity"]["enabled"]
    assert base["rugosity"]["heightSurface"] == base["rugosity"]["normalSurface"] == ""
    assert base["rugosity"]["heightMode"] == "AboveGround"
    assert base["rugosity"]["orientationMode"] == "SurfaceNormal"
    assert base["closedLoopPosition"] is True and base["closedLoopOrientation"] is True
    assert base["autopilotEnabled"] is False
    checks = 8
    for name, (visual, navigation) in MODES.items():
        sequence = json.loads((FOLDER / ("sequence-treatment-" + name + ".json")).read_text())
        assert sequence["autoStart"] is False and sequence["loop"] is False
        assert sequence["randomise"] is False
        assert len(sequence["sequences"]) == 4
        checks += 3
        for pattern, scene_name, row in zip(PATTERNS, SCENES, sequence["sequences"]):
            assert row["_comment"] == pattern
            assert row["sceneName"] == scene_name and row["reloadScene"] is False
            assert row["duration"] > 0
            assert row["parameters"]["configFile"] == "Examples/Rugosity/terrain-treatment.json"
            overrides = row["parameters"]["rugosity"]
            assert set(overrides) == {"visualSurface", "collisionSurface"}
            assert overrides["visualSurface"] == (pattern if visual else "Flat")
            assert overrides["collisionSurface"] == (pattern if navigation else "Flat")
            assert set(overrides.values()) <= set(registered)
            # Trial patches start from the shared base, never a preceding row.
            effective = {**base["rugosity"], **overrides}
            assert effective["heightSurface"] == effective["normalSurface"] == ""
            checks += 9
    provenance = json.loads((ROOT / "docs/terrain-texture-provenance.json").read_text())
    texture = ROOT / provenance["asset"]
    assert hashlib.sha256(texture.read_bytes()).hexdigest() == provenance["assetSha256"]
    guid = re.search(r"^guid: (.+)$", Path(str(texture) + ".meta").read_text(), re.M)[1]
    material = (ROOT / "Assets/TerrainCueLab/Ground.mat").read_text()
    assert "guid: " + guid + ", type: 3}" in material  # External texture importer, not serialized .asset.
    assert "m_Scale: {x: 40, y: 40}" not in material
    assert "_Color: {r: 1, g: 1, b: 1, a: 1}" in material
    checks += 4
    print(f"Terrain treatment configuration and texture checks passed: {checks}")

if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Check the shipped world/sequence chooser against actual packaged JSON and scenes.

This is a static contract. It does not claim Unity rendering or apparatus acceptance.
"""
from pathlib import Path
import json
import math
import re

ROOT = Path(__file__).resolve().parents[1]
STREAMING = ROOT / "Assets/StreamingAssets"
EXPECTED = {
    "flat-base": "Choice",
    "forest-floor": "Rugosity_RS",
    "dense-forest": "Choice_Selwyn",
    "real-world-terrain": "Choice_Selwyn",
    "optomotor": "Optomotor",
}


def main():
    checks = 0

    def check(condition, message):
        nonlocal checks
        checks += 1
        if not condition:
            raise AssertionError(message)

    build = (ROOT / "ProjectSettings/EditorBuildSettings.asset").read_text()
    enabled = {name for flag, name in re.findall(
        r"- enabled:\s*(\d+)\s+path:\s*Assets/Scenes/(.+?)\.unity", build
    ) if flag == "1"}
    chooser = (ROOT / "Assets/Scripts/ConfigurationUI/ConfigWorkbench.SequenceLibrary.cs").read_text()
    entries = re.findall(r'new SequenceStarter\("([^"\n]+)","([^"\n]+)"', chooser)
    check(len(entries) >= 5, "Sequence chooser lost its prepared starters")
    world_entries = [(title, ref) for title, ref in entries if title.startswith("World examples · ")]
    check(len(world_entries) == 5, "Expose exactly five clearly labelled basic world examples")
    check({Path(ref).name.removesuffix(".sequence.json") for _, ref in world_entries} == set(EXPECTED),
          "World chooser and required five-world catalog disagree")
    catalog = json.loads((ROOT / "Assets/Resources/ConfigurationUI/choice-assets.json").read_text())["scenes"]
    worlds = {}

    for title, reference in entries:
        sequence_path = STREAMING / reference
        check(sequence_path.is_file(), f"Missing sequence starter: {title}: {reference}")
        sequence = json.loads(sequence_path.read_text())
        rows = sequence.get("sequences", [])
        check(bool(rows), f"Starter is empty: {reference}")
        for row in rows:
            scene = row.get("sceneName")
            # Historical prepared sequences retain an explicitly supported alias.
            resolved_scene = "Kannadi" if scene == "Swarm" else scene
            check(resolved_scene in enabled, f"Starter uses scene excluded from desktop build: {scene}")
            file = row.get("parameters", {}).get("configFile") or row.get("parameters", {}).get("design")
            check(bool(file) and (STREAMING / file).is_file(), f"Missing experiment for {reference}: {file}")
            experiment = json.loads((STREAMING / file).read_text())
            check(not experiment.get("sourceBuildOnly", False), f"Starter requires a source-only build: {file}")
        if not title.startswith("World examples · "):
            continue
        key = sequence_path.name.removesuffix(".sequence.json")
        check(len(rows) == 1, f"World example should be a simple single block: {key}")
        row = rows[0]
        check(row["sceneName"] == EXPECTED[key], f"Unexpected world mapping: {key}")
        check(sequence.get("autoStart") is False and sequence.get("loop") is False,
              f"Starter must require explicit start and stop after one pass: {key}")
        check(sequence.get("randomSeedMode") == "Fixed" and sequence.get("randomSeed") == 1,
              f"Starter reproducibility defaults missing: {key}")
        check(all(isinstance(document.get("title"), str) and document["title"].strip()
                  for document in (sequence, row, experiment)), f"Human titles missing: {key}")
        check(bool(sequence.get("comments")) and bool(experiment.get("comments")), f"Context/units missing: {key}")
        check(isinstance(row["duration"], (float, int)) and 0 < row["duration"] <= 120,
              f"Use a bounded inspection duration: {key}")
        sun = experiment.get("sun", {})
        check(sun.get("enabled") is True and sun.get("intensity", -1) >= 0, f"Sun controls missing: {key}")
        check(set(sun.get("rotation", {})) == {"x", "y", "z"}
              and all(0 <= value <= 360 for value in sun["rotation"].values()), f"Sun angles invalid: {key}")
        check(all(0 <= sun["color"][axis] <= 1 for axis in ("r", "g", "b")), f"Sun color invalid: {key}")
        if key != "optomotor":
            check(experiment.get("autopilotEnabled") is False, f"World must not start scripted movement: {key}")
            check(experiment.get("resetPositionOnStart") is True
                  and experiment.get("resetRotationOnStart") is True, f"Initial pose ownership missing: {key}")
        object_names = {entry["name"] for entry in catalog.get(row["sceneName"], {}).get("objects", [])}
        for obj in experiment.get("objects", []):
            check(obj["type"] in object_names, f"Object not registered in packaged scene: {obj['type']}")
            check(all(math.isfinite(obj["scale"][axis]) and 0 < obj["scale"][axis] <= 2 for axis in ("x", "y", "z")),
                  f"Unsafe starter object scale: {key}")
            check(all(math.isfinite(obj["position"][axis]) for axis in ("x", "y", "z")), f"Object position invalid: {key}")
        worlds[key] = experiment

    check(worlds["flat-base"]["objects"] == [], "Flat base must be ready for experimenter-added objects")
    check("m_Name: Plane" in (ROOT / "Assets/Scenes/Choice.unity").read_text(), "Flat example lost its authored ground plane")
    floor = worlds["forest-floor"]["rugosity"]
    check(floor["enabled"] and floor["visualSurface"] == floor["collisionSurface"] == "RS",
          "Forest floor must use the real rugged RS surface in its own scene")
    check(floor["centimetersPerUnit"] == 1 and floor["clearanceCm"] == 1, "Forest-floor units/clearance changed")
    trees = worlds["dense-forest"]["objects"]
    check(len(trees) == 90 and all(tree.get("groundOnTerrain") for tree in trees), "Dense forest requires90 grounded trees")
    check(all(abs(tree["position"]["x"]) >= 8 for tree in trees), "Keep the central forest corridor open")
    for name in ("dense-forest", "real-world-terrain"):
        check(worlds[name]["walkingPositionGain"] == .01 and worlds[name]["aglHeight"] == 1.6,
              f"Metre-scale walking conversion or ground-following height changed: {name}")
    check(worlds["real-world-terrain"]["objects"] == [], "Real terrain example should not add stimuli")
    drum = worlds["optomotor"]
    check(sum(stimulus["duration"] for stimulus in drum["stimuli"]) == 30, "Drum must match its30second inspection block")
    check(all(stimulus["frequency"] > 0 and stimulus["speed"] >= 0 and 0 <= stimulus["contrast"] <= 1
              and stimulus["closedLoopPosition"] is False and stimulus["closedLoopOrientation"] is False
              for stimulus in drum["stimuli"]), "Optomotor example must remain centered and open-loop")
    canonical = "Examples/Sequences/kannadi-entrainment-social.sequence.json"
    check(any(title == "Kannadi · entrainment then social coupling" and reference == canonical
              for title, reference in entries), "Kannadi library must offer the canonical continuous phase starter")
    sequence = json.loads((STREAMING / canonical).read_text())
    check(len(sequence["sequences"]) == 1, "Entrainment/social phases must be one atomic outer block")
    row = sequence["sequences"][0]
    check(row["sceneName"] == "Kannadi" and row["reloadScene"] is False and row["duration"] == 420,
          "Canonical population example must retain one Kannadi scene for120+300 seconds")
    experiment = json.loads((STREAMING / row["parameters"]["configFile"]).read_text())
    pre, stimulus, post = (experiment[key] for key in ("preStimulus", "stimulus", "postStimulus"))
    check(pre["enabled"] and pre["durationSeconds"] == 120 and pre["populationMode"] == "Swarm",
          "Pre-stimulus must provide120 seconds of ordered swarm entrainment")
    check(stimulus["enabled"] and stimulus["durationSeconds"] == 300 and stimulus["populationMode"] == "Mirror",
          "Stimulus must provide300 seconds of mirror/social coupling")
    check(not stimulus["resetPositionOnStart"] and not stimulus["resetRotationOnStart"],
          "Social stimulus must preserve the tracked pose from entrainment")
    check(post["enabled"] is False and not post["resetPositionOnStart"] and not post["resetRotationOnStart"],
          "Optional post-stimulus must start disabled with pose continuity")
    ordered = json.loads((STREAMING / "Examples/Swarm/ordered-entrainment.example.json").read_text())
    social = json.loads((STREAMING / "Examples/Kannadi/social-coupling.example.json").read_text())
    check(all(pre[key] == value for key, value in ordered.items()), "Canonical pre-phase must preserve the original ordered stimulus payload")
    check(all(stimulus[key] == value for key, value in social.items()
              if key not in ("resetPositionOnStart", "resetRotationOnStart")),
          "Canonical social phase must preserve the original stimulus settings apart from pose resets")
    print(f"PASS: {checks} static world-starter/catalog checks; no native rendering or hardware claim.")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Read-only audit of canonical ID,X,Y,Z,time swarm data and an experiment recipe.

Reports measured speeds, births, spacing when multiple IDs are co-observed,
and closest approaches for a constant-velocity observer. No third-party modules.
Never edits observations or invents extra arrivals.
"""
import argparse
import csv
import hashlib
import json
import math
from collections import defaultdict
from pathlib import Path


def length(v):
    return math.sqrt(sum(x * x for x in v))


def subtract(a, b):
    return tuple(x - y for x, y in zip(a, b))


def quantiles(values):
    values = sorted(values)
    if not values:
        return None
    result = {}
    for name, q in zip(("min", "p10", "p25", "median", "p75", "p90", "p95", "max"), (0, .1, .25, .5, .75, .9, .95, 1)):
        i = (len(values) - 1) * q
        lo = math.floor(i)
        hi = math.ceil(i)
        result[name] = values[lo] + (values[hi] - values[lo]) * (i - lo)
    return result


def closest_on_line(position, delta, finite):
    squared = sum(x * x for x in delta)
    t = max(0, -sum(p * d for p, d in zip(position, delta)) / squared) if squared > 1e-12 else 0
    if finite:
        t = min(1, t)
    return length(tuple(p + d * t for p, d in zip(position, delta)))


def audit(path, recipe, observer_speed=5):
    playback = recipe.get("swarm", recipe).get("playback", {})
    units = playback.get("dataUnitsPerMetre", 1)
    gap = playback.get("maxGapSeconds", .1)
    window = playback.get("terminalVelocityWindowSeconds", .1)
    angle = math.radians(playback.get("yawOffsetDegrees", 0))
    c, s = math.cos(angle), math.sin(angle)
    rotate = lambda p: (c * p[0] + s * p[2], p[1], -s * p[0] + c * p[2])
    groups, observations = defaultdict(list), defaultdict(list)
    count = 0
    centroid = [0, 0, 0]
    with Path(path).open(newline="", encoding="utf-8-sig") as file:
        for row in csv.DictReader(file):
            t = float(row["time"])
            p = tuple(float(row[k]) / units for k in "XYZ")
            assert all(math.isfinite(v) for v in (t, *p)), "Nonfinite observation"
            groups[row["ID"]].append((t, p))
            observations[round(t, 9)].append(p)
            centroid = [a + b for a, b in zip(centroid, p)]
            count += 1
    assert count, "Empty dataset"
    centroid = tuple(v / count for v in centroid)
    groups = {key: sorted(values) for key, values in groups.items()}
    duration = max(v[-1][0] for v in groups.values())
    births = sorted(v[0][0] for v in groups.values())
    intervals = [b - a for a, b in zip(births, births[1:])]
    speeds, segments = [], []
    for key, values in groups.items():
        first = 0
        for i in range(1, len(values) + 1):
            if i < len(values):
                dt = values[i][0] - values[i-1][0]
                assert dt > 0, "Repeated or backward ID/time"
                if dt <= gap + 1e-8:
                    speeds.append(length(subtract(values[i][1], values[i-1][1])) / dt)
                    continue
            segment = values[first:i]
            last = len(segment) - 1
            terminal_start = max(0, last - 1)
            while terminal_start > 0 and segment[terminal_start][0] > segment[-1][0] - window:
                terminal_start -= 1
            dt = segment[-1][0] - segment[terminal_start][0]
            velocity = tuple(v / dt for v in subtract(segment[-1][1], segment[terminal_start][1])) if dt > 0 else (0, 0, 0)
            segments.append((key, segment, velocity))
            first = i
    neighbour = []
    for points in observations.values():
        if len(points) > 1:
            for i, p in enumerate(points):
                neighbour.append(min(length(subtract(p, q)) for j, q in enumerate(points) if i != j))
    minute_bins = [sum(m * 60 <= t < (m + 1) * 60 for t in births) for m in range(math.ceil(duration / 60))]
    experiment = recipe.get("swarm", recipe)
    centered = playback.get("centerOnAnimal", True)
    offset = experiment.get("spawnCenter") or {}
    model_supported = (centered and playback.get("speed", 1) == 1 and playback.get("startTimeSeconds", 0) == 0
                       and not experiment.get("useHeadingReference", False) and all(offset.get(k, 0) == 0 for k in "xyz"))

    def contacts(tail_speed):
        passes = []
        for _, points, terminal in segments:
            relative = []
            for t, p in points:
                p = rotate(subtract(p, centroid))
                relative.append((p[0], p[1], p[2] - observer_speed * (t - points[0][0])))
            minimum = min(length(p) for p in relative)
            for p, q in zip(relative, relative[1:]):
                minimum = min(minimum, closest_on_line(p, subtract(q, p), True))
            speed = length(terminal)
            if tail_speed is not None and speed > 1e-8:
                terminal = tuple(v * tail_speed / speed for v in terminal)
            v = rotate(terminal)
            minimum = min(minimum, closest_on_line(relative[-1], (v[0], v[1], v[2] - observer_speed), False))
            passes.append(minimum)
        return dict(continuationSpeedMetresPerSecond=tail_speed, nearestPassMetres=quantiles(passes),
                    passesWithinMetres={str(r): dict(count=sum(d <= r for d in passes),
                                                      perMinute=sum(d <= r for d in passes) / duration * 60) for r in (1, 2, 3)})

    return dict(sourceSha256=hashlib.sha256(Path(path).read_bytes()).hexdigest(), rows=count, ids=len(groups),
                uninterruptedSegments=len(segments), durationSeconds=duration, birthsPerMinute=len(groups) / duration * 60,
                meanBirthIntervalSeconds=duration / len(groups), birthGapSeconds=quantiles(intervals), birthsByMinute=minute_bins,
                recordedSpeedMetresPerSecond=quantiles(speeds), measuredTerminalSpeedMetresPerSecond=quantiles(length(v) for _, _, v in segments),
                coobservedNearestNeighbourMetres=quantiles(neighbour), maximumSimultaneouslyObserved=max(map(len, observations.values())),
                meanSimultaneouslyObserved=sum(p[-1][0] - p[0][0] for _, p, _ in segments) / duration,
                sampleCentroidMetres=dict(zip("XYZ", centroid)),
                observerSpeedMetresPerSecond=observer_speed, closestPassModelSupported=model_supported,
                measuredContinuation=contacts(None) if model_supported else None,
                configuredContinuation=contacts(playback.get("continuationSpeedMetresPerSecond")) if model_supported else None,
                note="IDs are unique recorded arrivals, not simultaneous population. Co-observed spacing is conditional on multiple recorded IDs at exactly the same timestamp; it is not a complete density census. Closest-pass estimates assume exact-time birth anchors, constant northward observer motion, unit playback clock, centered data, and continuing tails. No spatial rescaling.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("csv", type=Path)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--observer-speed", type=float, default=5)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    report = audit(args.csv, json.loads(args.config.read_text()), args.observer_speed)
    text = json.dumps(report, indent=2) + "\n"
    if args.report:
        args.report.write_text(text)
    print(text)


if __name__ == "__main__":
    main()

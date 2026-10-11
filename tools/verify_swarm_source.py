#!/usr/bin/env python3
"""Independently compare every converted position/ID/time with the source CSV.

No Unity or third-party modules needed. Source calibration is mm, +X north,
+Y west, +Z up, Z=0 at 5m AGL. Checks canonical data before presentation yaw.
"""
import argparse
import csv
import datetime as dt
import hashlib
import json
import statistics
from collections import defaultdict
from pathlib import Path


def verify(source, converted):
    with Path(source).open(newline="", encoding="utf-8-sig") as file:
        raw = list(csv.DictReader(file))
    with Path(converted).open(newline="", encoding="utf-8-sig") as file:
        unity = list(csv.DictReader(file))
    start = min(dt.datetime.fromisoformat(row["TimeExact"]) for row in raw)
    original = {}
    for row in raw:
        time = (dt.datetime.fromisoformat(row["TimeExact"]) - start).total_seconds()
        key = (row["ID"], round(time, 9))
        assert key not in original, f"Duplicate raw ID/time: {key}"
        original[key] = (-float(row["Y"]) / 1000, 5 + float(row["Z"]) / 1000, float(row["X"]) / 1000)
    errors, seen, groups = [], set(), defaultdict(list)
    for row in unity:
        key = (row["ID"], round(float(row["time"]), 9))
        assert key in original and key not in seen, f"Unexpected/repeated converted ID/time: {key}"
        seen.add(key)
        point = tuple(float(row[k]) for k in ("X", "Y", "Z"))
        errors.extend(abs(a - b) for a, b in zip(point, original[key]))
        groups[row["ID"]].append((float(row["time"]), point))
    assert seen == set(original), "Missing converted observations"
    assert max(errors) < 5e-8, f"Coordinate error: {max(errors)}m"
    centroid = [sum(float(row[k]) for row in unity) / len(unity) for k in ("X", "Y", "Z")]
    lengths = [max(t for t, _ in rows) - min(t for t, _ in rows) for rows in groups.values()]
    gaps = sum(b[0] - a[0] > .10000001 for rows in groups.values() for a, b in zip(sorted(rows), sorted(rows)[1:]))
    return dict(passed=True, rows=len(unity), ids=len(groups), durationSeconds=max(t for _, t in seen),
                sourceSha256=hashlib.sha256(Path(source).read_bytes()).hexdigest(),
                convertedSha256=hashlib.sha256(Path(converted).read_bytes()).hexdigest(),
                maxCoordinateErrorMetres=max(errors), sampleCentroidMetres=dict(zip("XYZ", centroid)),
                medianTrackSeconds=statistics.median(lengths), minTrackSeconds=min(lengths), maxTrackSeconds=max(lengths),
                gapsOverPointOneSecond=gaps,
                note="Canonical data matches every source ID/TimeExact/position. Presentation yaw is configured separately from the canonical data; continuation after recorded samples is extrapolated.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("converted", type=Path)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    report = verify(args.source, args.converted)
    text = json.dumps(report, indent=2) + "\n"
    if args.report:
        args.report.write_text(text)
    print(text)


if __name__ == "__main__":
    main()

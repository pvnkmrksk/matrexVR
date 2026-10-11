#!/usr/bin/env python3
"""Convert Selwyn long-format tracking CSV to ID,X,Y,Z,time (Unity metres/seconds).

Only the Python standard library is needed. Source +X=north, +Y=west,
+Z=up; Z=0 is 5 m AGL. No compass correction is inferred from flight direction.
"""
import argparse
import csv
import datetime as dt
import hashlib
import json
import math
from pathlib import Path
from zoneinfo import ZoneInfo


def convert(source, output, source_units="mm", yaw_degrees=0, height_origin_m=5,
            timezone="Australia/Sydney", duplicate_policy="error", output_units="m"):
    units_per_metre = {"m": 1.0, "cm": 100.0}[output_units]
    scale = {"mm": .001, "cm": .01, "m": 1.0}[source_units] * units_per_metre
    records, rejected, duplicates = {}, [], 0
    frames = []
    with Path(source).open(newline="", encoding="utf-8-sig") as stream:
        reader = csv.DictReader(stream)
        required = {"Frame", "ID", "X", "Y", "Z", "TimeExact"}
        if not required.issubset(reader.fieldnames or []):
            raise ValueError(f"Missing required columns: {sorted(required - set(reader.fieldnames or []))}")
        for line, row in enumerate(reader, 2):
            try:
                identifier = row["ID"].strip()
                if not identifier:
                    raise ValueError("empty ID")
                timestamp = dt.datetime.fromisoformat(row["TimeExact"].strip())
                # Naive wall times in this format are local time, not UTC.
                timestamp = timestamp.replace(tzinfo=ZoneInfo(timezone)) if timestamp.tzinfo is None else timestamp
                frame = int(row["Frame"])
                north, west, up = (float(row[k]) for k in ("X", "Y", "Z"))
                if not all(math.isfinite(v) for v in (north, west, up)):
                    raise ValueError("nonfinite position")
            except (ValueError, TypeError) as error:
                rejected.append({"line": line, "reason": str(error)})
                continue
            key = (identifier, timestamp.astimezone(dt.timezone.utc))
            if key in records:
                duplicates += 1
                if duplicate_policy == "error":
                    raise ValueError(f"Duplicate ID/TimeExact at line {line}: {identifier}, {timestamp}")
                if duplicate_policy == "first":
                    continue
            x, y, z = -west * scale, up * scale + height_origin_m * units_per_metre, north * scale
            angle = math.radians(yaw_degrees)
            records[key] = (x * math.cos(angle) + z * math.sin(angle), y,
                            -x * math.sin(angle) + z * math.cos(angle))
            frames.append(frame)
    if not records:
        raise ValueError("No valid trajectory rows")
    start = min(t for _, t in records)
    ordered = sorted(records.items(), key=lambda r: (r[0][1], r[0][0]))
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.writer(stream, lineterminator="\n")
        writer.writerow(["ID", "X", "Y", "Z", "time"])
        for (identifier, timestamp), xyz in ordered:
            writer.writerow([identifier, *(format(v, ".9g") for v in xyz),
                             format((timestamp - start).total_seconds(), ".9g")])
    groups = {}
    for (identifier, timestamp), xyz in ordered:
        groups.setdefault(identifier, []).append((timestamp, xyz))
    displacement = [0.0, 0.0, 0.0]
    for track in groups.values():
        for i in range(3):
            displacement[i] += track[-1][1][i] - track[0][1][i]
    report = {
        "source": Path(source).name, "sourceSha256": hashlib.sha256(Path(source).read_bytes()).hexdigest(),
        "sourceUnits": source_units, "outputUnits": output_units, "unitsPerMetre": units_per_metre,
        "axes": "X east, Y up, Z north",
        "mapping": "(-sourceY, sourceZ, sourceX) * scale + (0, heightOriginMetres * unitsPerMetre, 0)",
        "heightOriginMetres": height_origin_m, "additionalYawDegrees": yaw_degrees,
        "startLocal": start.astimezone(ZoneInfo(timezone)).isoformat(), "timeZone": timezone,
        "durationSeconds": (max(t for _, t in records) - start).total_seconds(),
        "rows": len(records), "tracks": len(groups), "frameRange": [min(frames), max(frames)],
        "duplicatePolicy": duplicate_policy, "duplicates": duplicates, "rejectedRows": rejected,
        "meanDisplacementHeadingDegrees": math.degrees(math.atan2(displacement[0], displacement[2])) % 360,
        "bounds": {k: [min(p[i] for p in records.values()), max(p[i] for p in records.values())]
                     for i, k in enumerate(("X", "Y", "Z"))},
        "note": "Geometry alone cannot establish geographical axes. Mapping uses the user's calibration; no outlier clipping or direction reversal.",
    }
    output.with_suffix(".metadata.json").write_text(json.dumps(report, indent=2) + "\n")
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--source-units", choices=("mm", "cm", "m"), default="mm")
    parser.add_argument("--output-units", choices=("m", "cm"), default="m")
    parser.add_argument("--yaw-degrees", type=float, default=0, help="Optional clockwise rotation around Unity up; -90 maps southwest to southeast.")
    parser.add_argument("--height-origin-m", type=float, default=5)
    parser.add_argument("--timezone", default="Australia/Sydney")
    parser.add_argument("--duplicates", choices=("error", "first", "last"), default="error")
    args = parser.parse_args()
    result = convert(args.source, args.output, args.source_units, args.yaw_degrees,
                     args.height_origin_m, args.timezone, args.duplicates, args.output_units)
    print(f"Converted {result['rows']} rows / {result['tracks']} tracks, {result['durationSeconds']:.3f}s; heading {result['meanDisplacementHeadingDegrees']:.2f} degrees")
    print(f"Rejected {len(result['rejectedRows'])} invalid rows; metadata: {args.output.with_suffix('.metadata.json')}")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Read a bounded sample of Kinefly CSVs without modifying the recordings."""
import argparse
import json
from pathlib import Path

import numpy as np
import pandas as pd


def quantiles(values):
    values = np.asarray(values)
    values = values[np.isfinite(values)]
    return dict(zip(["p01", "p05", "p25", "median", "p75", "p95", "p99"],
                    map(float, np.quantile(values, [.01, .05, .25, .5, .75, .95, .99]))))


def longest_run(times, active):
    start = None
    longest = 0.0
    for t, on in zip(times, active):
        if on:
            if start is None:
                start = t
            longest = max(longest, t - start)
        else:
            start = None
    return float(longest)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--replay-dir", type=Path)
    parser.add_argument("--rows", type=int, default=45000)
    args = parser.parse_args()
    selections = [("20261010_023123", rig) for rig in (1, 2, 3, 4)]
    selections += [("20261007_225146", rig) for rig in (1, 4)]
    results = []
    for session, rig in selections:
        path = args.root / session / f"{session}_Choice_Selwyn_VR{rig}_.csv"
        cols = ["Current Time", "wireYaw", "yawGain", "yawDCOffsetRadians",
                "SensPosX", "SensPosY", "experimentPhase", "trackingImplementation"]
        frame = pd.read_csv(path, usecols=cols, nrows=args.rows)
        frame["time"] = pd.to_datetime(frame["Current Time"])
        frame["seconds"] = (frame.time - frame.time.iloc[0]).dt.total_seconds()
        intervals = frame.seconds.diff()
        # Match the estimator's maximum 50 Hz sampling rate. Preserve held values.
        selected = []
        previous = -np.inf
        for index, t in enumerate(frame.seconds):
            if t - previous >= .02:
                selected.append(index)
                previous = t
        signal = frame.iloc[selected].copy()
        signal.index = pd.to_timedelta(signal.seconds, unit="s")
        signal["variance"] = signal.wireYaw.rolling("2s", min_periods=10).var(ddof=0)
        signal.loc[signal.seconds < 2, "variance"] = np.nan
        assert np.nanmax(np.abs(signal.wireYaw - (signal.SensPosX - signal.SensPosY))) < 1e-5
        summary = {
            "source": str(path), "rowsRead": len(frame), "durationSeconds": float(frame.seconds.iloc[-1]),
            "estimatorSamples": len(signal), "rawYawRadians": quantiles(signal.wireYaw),
            "leftRadians": quantiles(signal.SensPosX), "rightRadians": quantiles(signal.SensPosY),
            "variance2SecondsRadiansSquared": quantiles(signal.variance),
            "gain": sorted(map(float, frame.yawGain.unique())),
            "recordedOffsetRadians": quantiles(frame.yawDCOffsetRadians),
            "trackingImplementationCounts": {str(k): int(v) for k, v in frame.trackingImplementation.value_counts(dropna=False).items()},
            "nonfiniteRawRows": int((~np.isfinite(frame.wireYaw)).sum()),
            "gapsOver1Second": int((intervals > 1).sum()),
            "varianceGates": {str(threshold): {
                "fractionAbove": float((signal.variance.dropna() > threshold).mean()),
                "longestContinuousSeconds": longest_run(signal.seconds, signal.variance > threshold)
            } for threshold in (.001, .003, .01, .03, .1, .3)},
            "phases": {}
        }
        for phase, part in frame.groupby("experimentPhase"):
            delta = intervals.loc[part.index].dropna()
            raw = signal[signal.experimentPhase == phase]
            summary["phases"][phase] = {
                "rows": len(part), "medianFrameIntervalMs": float(delta.median() * 1000),
                "p95FrameIntervalMs": float(delta.quantile(.95) * 1000),
                "rawYawRadians": quantiles(raw.wireYaw),
                "variance2SecondsRadiansSquared": quantiles(raw.variance),
            }
        results.append(summary)
        if args.replay_dir:
            args.replay_dir.mkdir(parents=True, exist_ok=True)
            signal[["seconds", "wireYaw", "experimentPhase"]].to_csv(
                args.replay_dir / f"{session}_VR{rig}.csv", index=False)
        print(json.dumps({"session": session, "rig": rig, "raw": summary["rawYawRadians"],
                          "variance": summary["variance2SecondsRadiansSquared"],
                          "gates": summary["varianceGates"], "phases": summary["phases"]}))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps({
        "method": f"First {args.rows:,} rows of six CSVs from two sessions; raw wireYaw=L-R in radians; "
                  "population variance over trailing 2 seconds, sampled at at most 50 Hz. "
                  "Render timestamps approximate input timing; recordings lack per-input sequence IDs. "
                  "No flight labels exist, so variance gates cannot establish biological flight accuracy.",
        "files": results}, indent=2) + "\n")


if __name__ == "__main__":
    main()

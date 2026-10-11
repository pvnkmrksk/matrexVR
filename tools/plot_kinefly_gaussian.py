#!/usr/bin/env python3
"""Plot traces written by KineflyGaussianValidation.Run (requires matplotlib)."""
import argparse
import csv
import json
from pathlib import Path

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('validation_directory', type=Path)
    parser.add_argument('--output', type=Path, default=Path('docs/figures/kinefly-gaussian-heading.svg'))
    args = parser.parse_args()
    report = json.loads((args.validation_directory / 'gaussian-auto-trim-validation.json').read_text())
    cases = [case for case in report['cases'] if case['actualMotionIntegration']]
    fig, axes = plt.subplots(3, len(cases), figsize=(12, 10), layout='constrained', squeeze=False)
    for column, case in enumerate(cases):
        with (args.validation_directory / f"gaussian-heading-{case['seed']}.csv").open() as source:
            rows = list(csv.DictReader(source))
        t = [float(row['seconds']) for row in rows]
        heading = [float(row['headingDegrees']) for row in rows]
        untrimmed = [float(row['untrimmedHeadingDegrees']) for row in rows]
        dc = [float(row['dcDegrees']) for row in rows]
        top, tail, offset = axes[:, column]
        top.set_title(f"Hidden mean {case['hiddenMeanDegrees']:+.2f}°; gain {case['gain']:+g}")
        top.plot(t, untrimmed, color='#999999', label='Same noise, no trimming')
        top.plot(t, heading, color='#187c54', label='Auto trim: real Unity rotation')
        top.set_ylabel('Unwrapped heading (°)')
        top.legend(fontsize=8)
        start = next(i for i, seconds in enumerate(t) if seconds >= 180)
        tail.plot(t[start:], [value - heading[start] for value in heading[start:]], color='#187c54')
        tail.set_title(f"Final 60 s: slope {case['final60SecondHeadingSlopeDegPerSecond']:+.2f}°/s; "
                       f"true bias {case['trueResidualDriftDegPerSecond']:+.2f}°/s", fontsize=10)
        tail.set_ylabel('Heading change from 180 s (°)')
        offset.step(t, dc, where='post', color='#187c54', label=f"DC: {case['passes']} corrections")
        offset.axhline(case['hiddenMeanDegrees'], color='#c37612', linestyle='--', label='Hidden mean (test truth)')
        offset.set_ylabel('DC offset (°)')
        offset.legend(fontsize=8)
        for correction in case['corrections']:
            top.axvline(correction['seconds'], color='#c37612', alpha=.4, linewidth=.8)
        for axis in (top, tail, offset):
            axis.set_xlabel('Time (s)')
            axis.grid(alpha=.2)
        for axis in (top, offset):
            axis.set_xlim(0, 240)
        tail.set_xlim(180, 240)
    fig.suptitle('Gaussian yaw input: bias removed; random heading diffusion remains\n'
                 '25 Hz, 240 s, noise SD 8.59°, default controller; hidden mean never supplied to auto trim', fontsize=13)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(args.output)
    if args.output.suffix.lower() == '.svg':
        args.output.write_text('\n'.join(line.rstrip() for line in args.output.read_text().splitlines()) + '\n')
    fig.savefig(args.output.with_suffix('.png'), dpi=140)
    print(args.output.resolve())


if __name__ == '__main__':
    main()

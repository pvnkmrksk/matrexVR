#!/usr/bin/env python3
"""Summarize observed reload resources; does not turn a non-crash into a cause/fix claim."""
import argparse
from datetime import datetime
import json
from pathlib import Path
import re
import statistics


def lines(path):
    return [json.loads(s) for s in path.read_text().splitlines() if s] if path.exists() else []


def value(row, key):
    for part in key.split('.'):
        if not isinstance(row, dict):
            return None
        row = row.get(part)
    return row


def packet_is_fresh(packet):
    # Json.NET serializes PositiveInfinity as a string when no pose arrived.
    try:
        age = float(packet.get('ageSeconds', 'Infinity'))
    except (TypeError, ValueError):
        age = float('inf')
    return bool(packet.get('hasPose')) and age < .25 and bool(packet.get('progressing'))


def timestamp(text):
    # Unity/.NET emits variable fractional precision; Python3.9 accepts six digits.
    text = re.sub(r'\.(\d+)', lambda match: '.'+match.group(1).ljust(6, '0')[:6], text)
    return datetime.fromisoformat(text).timestamp()


def trend(rows, key):
    points = [(row['load'], value(row, key)) for row in rows if isinstance(value(row, key), (int, float))]
    if not points:
        return None
    xs, ys = zip(*points)
    mean_x, mean_y = statistics.mean(xs), statistics.mean(ys)
    denominator = sum((x-mean_x)**2 for x in xs)
    slope = sum((x-mean_x)*(y-mean_y) for x, y in points)/denominator if denominator else 0
    return dict(first=ys[0], last=ys[-1], minimum=min(ys), maximum=max(ys), median=statistics.median(ys), first5Median=statistics.median(ys[:5]), last5Median=statistics.median(ys[-5:]), slopePer100Reloads=slope*100)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--warmup-loads', type=int, default=20)
    args = parser.parse_args()
    root = args.directory.resolve()
    metrics = lines(root/'metrics.jsonl')
    process = lines(root/'process.jsonl')
    errors = lines(root/'errors.jsonl')
    summary = json.loads((root/'summary.json').read_text()) if (root/'summary.json').exists() else None
    late = [row for row in metrics if row.get('kind') == 'active-after-Start' and row.get('elapsedInSceneSeconds', 0) > 1]
    process_times = [(timestamp(row['utc']), row) for row in process if row.get('osThreadCount')]
    for row in late:
        if process_times:
            moment = timestamp(row['utc'])
            nearest_time, nearest = min(process_times, key=lambda item: abs(item[0]-moment))
            if abs(nearest_time-moment) <= 3:
                row['externalOsThreads'] = nearest['osThreadCount']
    keys = ['rssBytes', 'managedBytes', 'monoUsedBytes', 'monoHeapBytes', 'unityAllocatedBytes', 'unityReservedBytes', 'unityUnusedReservedBytes', 'graphicsDriverBytes', 'fileDescriptors', 'externalOsThreads', 'activeTrackingWorkers', 'activeAsyncWriters', 'ownedTrialMaterials', 'gameObjects', 'materials.nonPersistentCount', 'materials.nonPersistentBytes', 'meshes.nonPersistentCount', 'meshes.nonPersistentBytes', 'textures.nonPersistentCount', 'textures.nonPersistentBytes', 'renderTextures.nonPersistentCount', 'renderTextures.nonPersistentBytes', 'diagnosticRenderTargets']
    groups = {}
    for name in sorted({row['scene'] for row in late}):
        all_rows = [row for row in late if row['scene'] == name]
        warm = [row for row in all_rows if row['load'] > args.warmup_loads]
        groups[name] = dict(activeLateSamples=len(all_rows), afterWarmupSamples=len(warm), allObserved={key: trend(all_rows, key) for key in keys}, afterWarmup={key: trend(warm, key) for key in keys})
    active = [row for row in metrics if row.get('kind') == 'active-after-Start']
    minimum_inputs = min((sum(packet_is_fresh(packet) for packet in row.get('received', [])) for row in active), default=0)
    minimum_components = min((len(row.get('received', [])) for row in active), default=0)
    stale = sum(1 for row in active for packet in row.get('received', []) if not packet_is_fresh(packet))
    durations = [row['actualSceneSeconds'] for row in (summary or {}).get('steps', [])]
    teardown = next((row['utc'] for row in metrics if row.get('kind') == 'last-scene-before-teardown'), None)
    runtime_errors = [row for row in errors if not row.get('editorIndex')]
    active_errors = [row for row in runtime_errors if row.get('duringExperiment') is True or ('duringExperiment' not in row and teardown and row['utc'] < teardown)]
    report = dict(summary=summary, warmupLoads=args.warmup_loads, groups=groups,
                  coverage=dict(activeSamples=len(active), minimumLiveInputComponents=minimum_inputs, minimumInputComponents=minimum_components, staleOrNonprogressingInputObservations=stale,
                                renderedFaces=sorted(set(row.get('renderedFaceCameraIds', 0) for row in active)), naturalCameraRenders=sum(row.get('naturalCameraRenders', 0) for row in late), forcedCameraRenders=sum(row.get('forcedCameraRenders', 0) for row in late)),
                  stimulusCoverage=dict(terrainComponentCounts=sorted({row['terrainComponents'] for row in active if 'terrainComponents' in row}), skyImageSizes=sorted({(row['skyImageWidth'], row['skyImageHeight']) for row in active if row.get('skyImageWidth')}), samplesWithRecordedSky=sum(bool(row.get('nightSkyId')) for row in active)),
                  durations=dict(minimum=min(durations), median=statistics.median(durations), maximum=max(durations), totalSceneSeconds=sum(durations)) if durations else None,
                  cleanup=[row for row in metrics if row.get('kind', '').startswith('after-')], runtimeErrors=runtime_errors, runtimeErrorsDuringActiveProtocol=active_errors, runtimeErrorsOutsideActiveProtocol=[row for row in runtime_errors if row not in active_errors], excludedEditorIndexErrors=[row for row in errors if row.get('editorIndex')],
                  externalProcess=dict(samples=len(process), peakRssBytes=max((row.get('rssBytes', 0) for row in process), default=0), minimumThreads=min((row.get('osThreadCount', 9999) for row in process), default=None), maximumThreads=max((row.get('osThreadCount', 0) for row in process), default=None), maxFileDescriptors=max((row.get('fileDescriptorCount', 0) for row in process), default=None)),
                  interpretation='Compare the same scene at the same active phase after warm-up. RSS/heap reservation can reflect allocator or Editor caches; rising native live counts/threads/FDs need independent attribution. A non-crashing200-load run does not identify the historical crash cause. Explicit diagnostic render targets are separately counted.')
    (root/'analysis.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps({key: report[key] for key in ['coverage', 'durations', 'externalProcess']}, indent=2))
    for name, group in groups.items():
        print(name, 'late samples', group['activeLateSamples'])
        for key in ['rssBytes', 'managedBytes', 'unityAllocatedBytes', 'fileDescriptors', 'materials.nonPersistentCount', 'meshes.nonPersistentCount', 'textures.nonPersistentCount', 'renderTextures.nonPersistentCount']:
            print(' ', key, group['afterWarmup'][key])


if __name__ == '__main__':
    main()

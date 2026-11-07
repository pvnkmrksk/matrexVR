#!/usr/bin/env python3
"""Export finalized MATREX population data to standard Parquet, and Parquet back to CSV."""

import argparse
import csv
import hashlib
import json
import os
import shutil
import tempfile
from pathlib import Path

from unpack_population import csv_sample, population_frames


FRAME_FIELDS = (
    'UnityFrame', 'TimestampTicks', 'AgentCount', 'SkyboxId', 'SkyboxSampleUtc',
    'ExperimentPhase', 'VRIndex', 'NumberOfRings', 'HexRadius', 'ScaleX', 'ScaleY', 'ScaleZ',
)
TEXT_COLUMNS = {
    'Name', 'CloneName', 'Layer', 'skyboxId', 'skyboxSampleUtc', 'experimentPhase',
    'AnimationState',
}
INTEGER_COLUMNS = {'VRIndex', 'CloneIndex', 'UnityFrame', 'NumberOfRings'}
FLOAT_COLUMNS = {
    'X', 'Y', 'Z', 'PositionX', 'PositionY', 'PositionZ', 'RotationX', 'RotationY',
    'RotationZ', 'ScaleX', 'ScaleY', 'ScaleZ', 'hexRadius', 'Speed', 'VisibilityPhase',
}


def polars():
    os.environ.setdefault('POLARS_MAX_THREADS', '1')
    try:
        import polars as pl
    except ImportError as error:
        raise ValueError('Install Polars in the Python environment used for offline export.') from error
    return pl


def sample_schema(pl, columns):
    types = {name: pl.String for name in TEXT_COLUMNS}
    types.update({name: pl.Int32 for name in INTEGER_COLUMNS})
    types.update({name: pl.Float32 for name in FLOAT_COLUMNS})
    types.update(Timestamp=pl.Int64, Visible=pl.Boolean)
    if not columns or columns[0] != 'Timestamp' or len(columns) != len(set(columns)):
        raise ValueError('Invalid population columns.')
    try:
        return {**{name: types[name] for name in columns}, 'IdentityId': pl.Int32, 'SourceIndex': pl.Int32}
    except KeyError as error:
        raise ValueError(f'Unknown population column {error.args[0]}') from error


def write_parquet(source, output, frames_per_chunk):
    if output.exists() or output.is_symlink():
        raise ValueError('The output dataset already exists.')
    pl = polars()
    frames = []
    samples = []
    chunks = []
    frame_types = (pl.Int32, pl.Int64, pl.Int32, pl.String, pl.String, pl.String,
                   pl.Int32, pl.Int32, pl.Float32, pl.Float32, pl.Float32, pl.Float32)
    frame_schema = dict(zip(FRAME_FIELDS, frame_types))
    temporary = Path(tempfile.mkdtemp(prefix=f'.{output.name}-', dir=output.parent))
    try:
        with source.open('rb') as stream:
            records = population_frames(stream)
            header, columns, layout = next(records)
            rows_schema = sample_schema(pl, columns)

            def flush():
                index = len(chunks)
                frame_file = f'frames-{index:06}.parquet'
                sample_file = f'samples-{index:06}.parquet'
                pl.DataFrame(frames, schema=frame_schema, orient='row').write_parquet(
                    temporary / frame_file, compression='zstd', compression_level=3, statistics=True)
                pl.DataFrame(samples, schema=rows_schema, orient='row').write_parquet(
                    temporary / sample_file, compression='zstd', compression_level=3, statistics=True)
                chunks.append({'framesFile': frame_file, 'samplesFile': sample_file,
                               'frameCount': len(frames), 'sampleCount': len(samples),
                               'minFrame': min(row[0] for row in frames),
                               'maxFrame': max(row[0] for row in frames),
                               'minWallTicks': min(row[1] for row in frames),
                               'maxWallTicks': max(row[1] for row in frames)})
                frames.clear()
                samples.clear()

            for frame, rows in records:
                if frames and (len(frames) == frames_per_chunk or len(samples) + len(rows) > 32768):
                    flush()
                frames.append(frame)
                samples.extend(rows)
            if frames:
                flush()
        manifest = {'version': 1, 'format': 'MATREX population Parquet dataset', 'layout': layout,
                    'header': header, 'csvColumns': columns, 'timestampUnits': 'local .NET ticks (100 ns)',
                    'frames': sum(chunk['frameCount'] for chunk in chunks),
                    'samples': sum(chunk['sampleCount'] for chunk in chunks), 'chunks': chunks}
        (temporary / 'population.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
        if output.exists() or output.is_symlink():
            raise ValueError('The output dataset already exists.')
        temporary.rename(output)
    finally:
        if temporary.exists():
            shutil.rmtree(temporary)


def write_run(source, output, frames_per_chunk):
    if output.exists() or output.is_symlink() or output.resolve().is_relative_to(source.resolve()):
        raise ValueError('Choose a new output directory outside the recorded run.')
    manifest_path = source / 'recording_manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    lifecycle = manifest['lifecycle']
    if lifecycle['status'] != 'finalized' or not lifecycle.get('writersDrained'):
        raise ValueError('Wait for the recording to finish before exporting the run.')
    streams = [entry for entry in manifest['files']
               if entry.get('role') == 'agent-population' and entry['relativePath'].endswith('.matpop')]
    if not streams:
        raise ValueError('This finalized run contains no .matpop population streams.')
    temporary = Path(tempfile.mkdtemp(prefix=f'.{output.name}-', dir=output.parent))
    try:
        inventory = []
        for index, entry in enumerate(streams, 1):
            filename = entry['relativePath']
            path = source / filename
            if Path(filename).name != filename or path.is_symlink() or not path.is_file():
                raise ValueError(f'Invalid population inventory path: {filename}')
            before = path.stat()
            if before.st_size != entry['bytes']:
                raise ValueError(f'Population inventory differs from the recording manifest: {filename}')
            digest = hashlib.sha256()
            with path.open('rb') as recording:
                for block in iter(lambda: recording.read(1024 * 1024), b''):
                    digest.update(block)
            if digest.hexdigest() != entry.get('sha256'):
                raise ValueError(f'Population checksum differs from the recording manifest: {filename}')
            dataset = f'stream-{index:03}'
            write_parquet(path, temporary / dataset, frames_per_chunk)
            after = path.stat()
            if (before.st_ino, before.st_size, before.st_mtime_ns, before.st_ctime_ns) != (
                    after.st_ino, after.st_size, after.st_mtime_ns, after.st_ctime_ns):
                raise ValueError(f'Population file changed during export: {filename}')
            inventory.append({'source': filename, 'dataset': dataset, 'sha256': digest.hexdigest()})
        (temporary / 'run.json').write_text(
            json.dumps({'version': 1, 'sourceRun': source.name, 'streams': inventory}, indent=2) + '\n',
            encoding='utf-8')
        if output.exists() or output.is_symlink():
            raise ValueError('The output dataset already exists.')
        temporary.rename(output)
    finally:
        if temporary.exists():
            shutil.rmtree(temporary)


def to_csv(dataset, output, first, last, legacy):
    pl = polars()
    manifest = json.loads((dataset / 'population.json').read_text(encoding='utf-8'))
    if manifest['version'] != 1 or manifest['format'] != 'MATREX population Parquet dataset':
        raise ValueError('Unsupported population dataset.')
    columns = manifest['csvColumns']
    sample_schema(pl, columns)
    created = False
    try:
        with output.open('x', encoding='utf-8', newline='') as destination:
            created = True
            writer = csv.writer(destination, lineterminator='\r\n')
            writer.writerow(manifest['header'] if legacy else columns)
            for index, chunk in enumerate(manifest['chunks']):
                filename = f'samples-{index:06}.parquet'
                if chunk['samplesFile'] != filename:
                    raise ValueError('Unexpected population chunk path.')
                if ((first is not None and chunk['maxFrame'] < first) or
                    (last is not None and chunk['minFrame'] > last)):
                    continue
                samples = pl.scan_parquet(dataset / filename).select(columns)
                if first is not None:
                    samples = samples.filter(pl.col('UnityFrame') >= first)
                if last is not None:
                    samples = samples.filter(pl.col('UnityFrame') <= last)
                writer.writerows(csv_sample(row) for row in samples.collect().iter_rows())
    except BaseException:
        if created:
            output.unlink(missing_ok=True)
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    pack = commands.add_parser('pack', help='Write a new Parquet dataset from a finalized run or .matpop stream.')
    pack.add_argument('source', type=Path)
    pack.add_argument('output', type=Path)
    pack.add_argument('--frames-per-chunk', type=int, default=120, choices=range(1, 601), metavar='1–600')
    csv_parser = commands.add_parser('csv', help='Write CSV from a complete dataset or frame range.')
    csv_parser.add_argument('source', type=Path)
    csv_parser.add_argument('output', type=Path)
    csv_parser.add_argument('--first-frame', type=int)
    csv_parser.add_argument('--last-frame', type=int)
    csv_parser.add_argument('--legacy-header', action='store_true')
    args = parser.parse_args()
    try:
        if args.command == 'pack':
            export = write_run if args.source.is_dir() else write_parquet
            export(args.source, args.output, args.frames_per_chunk)
        else:
            if args.first_frame is not None and args.last_frame is not None and args.first_frame > args.last_frame:
                raise ValueError('The first frame must not be later than the last frame.')
            to_csv(args.source, args.output, args.first_frame, args.last_frame, args.legacy_header)
    except (OSError, ValueError, OverflowError, KeyError, TypeError) as error:
        parser.exit(1, f'{parser.prog}: {error}\n')


if __name__ == '__main__':
    main()

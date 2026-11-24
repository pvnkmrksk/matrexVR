#!/usr/bin/env python3
"""Convert one finalized MATREX .matpop population stream to ordinary CSV."""

import argparse
import csv
import datetime as dt
import io
import json
import struct
import sys
import zlib
from functools import lru_cache
from pathlib import Path

MAX_BLOCK_BYTES = 64 * 1024**2
MAX_STORED_BLOCK_BYTES = MAX_BLOCK_BYTES + 1024**2
INDEX_ENTRY = "qiiqi"


def read_exact(source, size):
    value = source.read(size)
    if len(value) != size:
        raise ValueError("Truncated population data")
    return value


@lru_cache(maxsize=16)
def schema(fmt):
    return struct.Struct("<" + fmt)


def unpack(source, fmt):
    fields = schema(fmt)
    return fields.unpack(read_exact(source, fields.size))


def text(source, end):
    length = 0
    for shift in range(0, 35, 7):
        byte = read_exact(source, 1)[0]
        length |= (byte & 127) << shift
        if byte < 128:
            if length > end - source.tell():
                raise ValueError("Population string exceeds its record")
            return read_exact(source, length).decode("utf-8")
    raise ValueError("Invalid population string length")


def animation(source, end):
    kind, = unpack(source, "B")
    if kind == 0:
        return ""
    if kind == 1:
        speed, state, time, weight, transitioning = unpack(source, "fiff?")
        payload = {
            "version": 1,
            "animators": [{"path": "", "speed": speed,
                           "layers": [{"state": state, "time": time, "weight": weight,
                                       "transitioning": transitioning}],
                           "parameters": []}],
            "legacy": [],
        }
    elif kind == 2:
        time, phase = unpack(source, "ff")
        payload = {"version": 1, "animators": [], "legacy": [],
                   "procedural": {"time": time, "phase": phase}}
    elif kind == 3:
        return text(source, end)
    else:
        raise ValueError("Unknown population animation format")
    return json.dumps(payload, separators=(",", ":"), allow_nan=False)


def timestamp(ticks):
    seconds, fraction = divmod(ticks, 10_000_000)
    moment = dt.datetime(1, 1, 1) + dt.timedelta(seconds=seconds)
    return f"{moment.isoformat(sep=' ', timespec='seconds')}.{fraction:07d}"


def frame_record(source, extent):
    size, unity_frame, ticks, count = unpack(source, "iiqi")
    end = source.tell() + size - 16
    if end > extent or not 31 <= size <= MAX_BLOCK_BYTES or not 0 <= count <= (size - 31) // 30:
        raise ValueError("Invalid population frame size or row count")
    return source, end, unity_frame, ticks, count


def frame_records(source, start, extent, packed, index_start, index_end):
    source.seek(start)
    if not packed:
        while source.tell() < extent:
            yield frame_record(source, extent)
        return

    index = index_start
    while source.tell() < extent:
        block_start = source.tell()
        raw_size, stored_size = unpack(source, "ii")
        if not (35 <= raw_size <= MAX_BLOCK_BYTES and
                0 < stored_size <= min(MAX_STORED_BLOCK_BYTES, extent - source.tell())):
            raise ValueError("Invalid population block lengths")
        encoded = read_exact(source, stored_size)
        next_block = source.tell()
        decompressor = zlib.decompressobj(wbits=31)
        try:
            raw = decompressor.decompress(encoded, raw_size + 1)
        except zlib.error as error:
            raise ValueError("Corrupt population block") from error
        if len(raw) != raw_size or not decompressor.eof or decompressor.unused_data:
            raise ValueError("Population block differs from its stored length")
        block = io.BytesIO(raw)
        source.seek(index)
        while block.tell() < raw_size:
            frame_offset = block.tell()
            record = frame_record(block, raw_size)
            if source.tell() >= index_end or unpack(source, INDEX_ENTRY) != (block_start, frame_offset, *record[2:]):
                raise ValueError("Population frame payload differs from its directory")
            yield record
        index = source.tell()
        source.seek(next_block)
    if index != index_end:
        raise ValueError("Population frame count differs from its directory")


def population_frames(source):
    source.seek(0, 2)
    footer_offset = source.tell() - 48
    if footer_offset < 12:
        raise ValueError("Missing population header or footer")
    source.seek(0)
    magic = read_exact(source, 8)
    if magic not in (b"MATPOP01", b"MATPOP02"):
        raise ValueError("Unknown population stream version")
    packed = magic == b"MATPOP02"
    layout, = unpack(source, "i")
    if layout not in (0, 1, 2):
        raise ValueError("Unknown population layout")
    header = text(source, footer_offset).split(",")
    source_offset = source.tell()
    source.seek(footer_offset)
    if read_exact(source, 8) != (b"MATEND02" if packed else b"MATEND01"):
        raise ValueError("Missing population footer. The recording may not have finished")
    identity_offset, frames, rows, first_ticks, last_ticks = unpack(source, "qqqqq")
    if not source_offset <= identity_offset <= footer_offset - 4 or min(frames, rows, first_ticks) < 0 or last_ticks < first_ticks:
        raise ValueError("Invalid population stream totals")
    index_offset = footer_offset - frames * schema(INDEX_ENTRY).size if packed else footer_offset
    if index_offset < identity_offset + 4:
        raise ValueError("Invalid population frame directory size")

    source.seek(identity_offset)
    identity_count, = unpack(source, "i")
    if not 0 <= identity_count <= (index_offset - source.tell()) // 6:
        raise ValueError("Invalid population identity count")
    identities = [(unpack(source, "i")[0], text(source, index_offset), text(source, index_offset))
                  for _ in range(identity_count)]
    if source.tell() != index_offset:
        raise ValueError("Invalid population identity table length")

    data_columns = [column for column in header if ":" not in column]
    yield header, data_columns, layout
    read_frames = read_rows = 0
    observed_first = observed_last = None
    for frame_source, end, unity_frame, ticks, count in frame_records(
            source, source_offset, identity_offset, packed, index_offset, footer_offset):
        sky, sky_utc, phase = (text(frame_source, end) for _ in range(3))
        if layout == 1:
            vr, rings, radius = unpack(frame_source, "iif")
        default_scale = unpack(frame_source, "fff")
        frame = (unity_frame, ticks, count, sky, sky_utc, phase,
                 vr if layout == 1 else None, rings if layout == 1 else None,
                 radius if layout == 1 else None, *default_scale)
        samples = []
        for _ in range(count):
            identity, *pose = unpack(frame_source, "iffffff")
            if not 0 <= identity < identity_count:
                raise ValueError("Unknown population identity")
            clone_index, name, layer = identities[identity]
            position, rotation = pose[:3], pose[3:]
            evidence = animation(frame_source, end)
            custom_scale, = unpack(frame_source, "?")
            scale = unpack(frame_source, "fff") if custom_scale else default_scale
            if layout == 1:
                row = [ticks, vr, clone_index, name, *position, *rotation, rings, radius,
                       sky, sky_utc, phase, evidence, unity_frame, *scale]
            elif layout == 2:
                speed, visibility_phase, visible = unpack(frame_source, "ff?")
                row = [ticks, name, layer, *position, *rotation, speed, visibility_phase,
                       phase, visible, unity_frame, evidence, *scale]
            else:
                row = [ticks, name, layer, *position, sky, sky_utc, phase, *rotation, evidence, unity_frame, *scale]
            if len(row) != len(data_columns):
                raise ValueError("Population layout differs from its CSV header")
            samples.append((*row, identity, clone_index))
        if frame_source.tell() != end:
            raise ValueError("Population samples differ from their recorded frame length")
        read_frames += 1
        read_rows += count
        observed_first = ticks if observed_first is None else min(ticks, observed_first)
        observed_last = ticks if observed_last is None else max(ticks, observed_last)
        yield frame, samples
    if (read_frames, read_rows, observed_first or 0, observed_last or 0) != (frames, rows, first_ticks, last_ticks):
        raise ValueError("Population totals differ from the recorded footer")


def csv_sample(row):
    return [timestamp(row[0]), *(format(value, ".9g") if isinstance(value, float) else value for value in row[1:])]


def export(source, destination, legacy_header=False):
    records = population_frames(source)
    header, data_columns, _ = next(records)
    output = csv.writer(destination, lineterminator="\r\n")
    output.writerow(header if legacy_header else data_columns)
    for _, samples in records:
        output.writerows(csv_sample(row[:-2]) for row in samples)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="A finalized .matpop file")
    parser.add_argument("output", nargs="?", default="-", help="New CSV file, or - for standard output (default)")
    parser.add_argument("--legacy-header", action="store_true",
                        help="Append the historical configuration metadata fields to the header")
    args = parser.parse_args()
    output_path = None if args.output == "-" else Path(args.output)
    created = False
    try:
        with args.source.open("rb") as source:
            if output_path is None:
                export(source, sys.stdout, args.legacy_header)
            else:
                with output_path.open("x", encoding="utf-8", newline="") as output:
                    created = True
                    export(source, output, args.legacy_header)
    except (OSError, ValueError, OverflowError) as error:
        if created:
            output_path.unlink(missing_ok=True)
        parser.exit(1, f"{parser.prog}: {error}\n")


if __name__ == "__main__":
    main()

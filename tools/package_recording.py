#!/usr/bin/env python3
"""Package a finalized MATREX run as a portable ZIP while acquisition is idle."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import stat
import tempfile
import zipfile


MANIFEST = "recording_manifest.json"
COMPRESSED_SUFFIXES = {".gz", ".zip", ".mp4", ".png", ".jpg", ".jpeg", ".webp"}


def signature(info):
    return info.st_dev, info.st_ino, info.st_size, info.st_mtime_ns


def snapshot(run):
    files = {}
    for path in run.rglob("*"):
        relative = path.relative_to(run).as_posix()
        info = path.lstat()
        if stat.S_ISLNK(info.st_mode) or "\\" in relative:
            raise ValueError(f"Unsupported path in the recording: {relative}")
        if stat.S_ISDIR(info.st_mode):
            continue
        if not stat.S_ISREG(info.st_mode):
            raise ValueError(f"Unsupported file in the recording: {relative}")
        files[relative] = signature(info)
    return files


def sealed_inventory(run, files):
    with (run / MANIFEST).open(encoding="utf-8") as source:
        if signature(os.fstat(source.fileno())) != files.get(MANIFEST):
            raise ValueError("The recording manifest changed during packaging.")
        evidence = json.load(source)
    if not isinstance(evidence, dict) or not isinstance(evidence.get("files"), list):
        raise ValueError("The recording manifest has no acquisition inventory.")
    lifecycle = evidence.get("lifecycle")
    if (not isinstance(lifecycle, dict) or lifecycle.get("status") != "finalized"
            or lifecycle.get("writersDrained") is not True):
        raise ValueError("Only a finalized run with drained writers can be packaged.")
    for writer in evidence.get("writerEvidence", []):
        if not isinstance(writer, dict) or writer.get("failure") or writer.get("droppedFrames", 0):
            raise ValueError("The recording reports incomplete population evidence.")

    expected = {}
    for record in evidence["files"]:
        if not isinstance(record, dict):
            raise ValueError("Invalid acquisition inventory entry.")
        name = record["relativePath"]
        digest = record.get("sha256")
        if (Path(name).name != name or name in expected or name not in files
                or record["bytes"] != files[name][2]
                or not isinstance(digest, str) or len(digest) != 64
                or any(character not in "0123456789abcdef" for character in digest)):
            raise ValueError(f"Missing, changed or invalid acquisition inventory entry: {name}")
        expected[name] = digest
    actual = {name for name in files if "/" not in name and name.lower().endswith((".csv", ".csv.gz", ".matpop"))}
    if actual != expected.keys():
        raise ValueError("The run's acquisition files differ from its finalized inventory.")
    return expected


def package(run, destination, store):
    run = run.resolve(strict=True)
    if not run.is_dir():
        raise ValueError("Select a finalized run directory.")
    destination = destination.parent.resolve(strict=True) / destination.name
    if run == destination or run in destination.parents:
        raise ValueError("Write the ZIP outside the recording directory.")
    if os.path.lexists(destination):
        raise FileExistsError(destination)

    files = snapshot(run)
    expected = sealed_inventory(run, files)
    descriptor, temporary = tempfile.mkstemp(prefix=destination.name + ".", suffix=".writing", dir=destination.parent)
    os.close(descriptor)
    try:
        with zipfile.ZipFile(temporary, "w", allowZip64=True) as archive:
            for relative, before in sorted(files.items()):
                path = run / relative
                entry = zipfile.ZipInfo.from_file(path, relative)
                entry.compress_type = (zipfile.ZIP_STORED if store or path.suffix.lower() in COMPRESSED_SUFFIXES
                                       else zipfile.ZIP_DEFLATED)
                digest = hashlib.sha256() if relative in expected else None
                with path.open("rb") as source:
                    if signature(os.fstat(source.fileno())) != before:
                        raise ValueError(f"Recording file changed during packaging: {relative}")
                    with archive.open(entry, "w") as output:
                        while True:
                            block = source.read(256 * 1024)
                            if not block:
                                break
                            if digest is not None:
                                digest.update(block)
                            output.write(block)
                    if signature(os.fstat(source.fileno())) != before:
                        raise ValueError(f"Recording file changed during packaging: {relative}")
                if digest is not None and digest.hexdigest() != expected[relative]:
                    raise ValueError(f"Acquisition checksum differs from the finalized run: {relative}")
        if snapshot(run) != files:
            raise ValueError("The recording contents changed during packaging.")
        # Publish a complete ZIP without overwriting another export, including if two workers race.
        os.link(temporary, destination)
    finally:
        os.unlink(temporary)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path, help="Finalized recording directory")
    parser.add_argument("output", type=Path, nargs="?", help="Destination ZIP, defaulting to a sibling of the run")
    parser.add_argument("--store", action="store_true", help="Package without ZIP compression")
    args = parser.parse_args()
    try:
        run = args.run.resolve(strict=True)
        output = args.output or run.with_name(run.name + ".zip")
        package(run, output, args.store)
    except (OSError, ValueError, KeyError, TypeError) as error:
        parser.exit(1, f"{parser.prog}: {error}\n")
    print(output)


if __name__ == "__main__":
    main()

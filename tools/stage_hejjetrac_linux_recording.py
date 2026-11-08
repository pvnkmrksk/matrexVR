#!/usr/bin/env python3
"""Install the verified recording adapter into a MATREX VR 0.4.3 Linux companion catalog."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import tempfile

ROOT = Path(__file__).resolve().parent
LOCK = ROOT / "native_patches/hejjetrac-recording-linux.lock.json"

def digest(file):
    result = hashlib.sha256()
    with file.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(chunk)
    return result.hexdigest()

def inside(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root):
        raise ValueError("Companion path escapes its package")
    return path

def stage(companions, binary, lock_path=LOCK):
    companions, binary = Path(companions).resolve(), Path(binary).resolve()
    lock = json.loads(Path(lock_path).read_text())
    expected = lock["executableSha256"]
    if digest(binary) != expected:
        raise ValueError("HejjéTrac adapter executable failed its SHA-256 check")
    catalog_path = companions / "catalog.json"
    before = catalog_path.read_bytes()
    catalog = json.loads(before)
    entry, = [item for item in catalog["entries"] if item["backend"] == "hejjetrac" and item["platform"] == "linux-x64"]
    source = inside(companions, entry["packageRoot"])
    if entry.get("patchSha256") == lock["patchSha256"] and entry.get("executableSha256") == expected:
        if digest(inside(source, entry["executable"])) != expected:
            raise ValueError("Installed HejjéTrac adapter does not match the catalog")
        return source
    if entry["releaseTag"] != lock["upstreamTag"] or entry.get("archiveSha256") != lock["upstreamArchiveSha256"]:
        raise ValueError("Use the checksum-pinned HejjéTrac 0.5.0 payload shipped with MATREX VR 0.4.3")
    if digest(inside(source, entry["executable"])) != entry["executableSha256"]:
        raise ValueError("The original HejjéTrac companion executable does not match the catalog")

    tag = lock["upstreamTag"] + "-matrex-recording-" + lock["patchSha256"][:12]
    target = companions / "hejjetrac" / tag / "linux-x64"
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists():
        with tempfile.TemporaryDirectory(prefix=".staging-", dir=target.parent) as folder:
            staging = Path(folder) / "payload"
            shutil.copytree(source, staging, symlinks=True)
            executable = inside(staging, entry["executable"])
            shutil.copy2(binary, executable)
            executable.chmod(0o755)
            alias = executable.with_name("hejjetrak")
            if alias.is_file() and not alias.is_symlink():
                shutil.copy2(binary, alias)
            shutil.copy2(lock_path, executable.parent / "MATREX-NATIVE-PROVENANCE.json")
            staging.rename(target)
    target_binary = inside(target, entry["executable"])
    if digest(target_binary) != expected:
        raise ValueError("Versioned adapter exists with unexpected contents")
    entry["replacedReleaseTag"] = entry["releaseTag"]
    entry["replacedArchiveSha256"] = entry.pop("archiveSha256")
    entry["upstreamExecutableSha256"] = entry["executableSha256"]
    entry.update(releaseTag=tag, releaseUrl=lock["sourceUrl"], executableSha256=expected,
                 packageRoot=target.relative_to(companions).as_posix(),
                 supportsRecordingControl=True, sourceRevision=lock["sourceRevision"],
                 patchSha256=lock["patchSha256"], localSourceBuild=True)
    catalog.setdefault("upstreamCatalogId", catalog["catalogId"])
    catalog["catalogId"] = hashlib.sha256((catalog["catalogId"] + ":" + expected).encode()).hexdigest()[:16]
    with tempfile.NamedTemporaryFile(prefix=".catalog-", dir=companions, delete=False) as stream:
        pending = Path(stream.name)
        stream.write((json.dumps(catalog, indent=2) + "\n").encode())
    try:
        if catalog_path.read_bytes() != before:
            raise RuntimeError("The companion catalog changed while staging the adapter")
        pending.replace(catalog_path)
    finally:
        pending.unlink(missing_ok=True)
    return target

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("companions", type=Path, help="The MATREX VR 0.4.3 Linux Companions directory")
    parser.add_argument("--binary", type=Path, default=ROOT / "hejjetrac")
    parser.add_argument("--lock", type=Path, default=LOCK)
    args = parser.parse_args()
    print(stage(args.companions, args.binary, args.lock))

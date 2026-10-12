#!/usr/bin/env python3
"""Read-only audit of saved run configs. Never backfills runs with current files."""
import argparse
import collections
import hashlib
import json
from pathlib import Path


def read_json(path):
    # Json.NET accepts comments/trailing commas. Preserve quoted paths and URLs.
    text = Path(path).read_text(encoding="utf-8-sig")
    out, i, quoted, escaped = [], 0, False, False
    while i < len(text):
        c = text[i]
        if quoted:
            out.append(c)
            if escaped: escaped = False
            elif c == "\\": escaped = True
            elif c == '"': quoted = False
            i += 1
            continue
        if c == '"': quoted = True
        elif text[i:i+2] == "//":
            end = text.find("\n", i)
            i = len(text) if end < 0 else end
            continue
        elif text[i:i+2] == "/*":
            end = text.find("*/", i+2)
            if end < 0: raise ValueError("Unclosed JSON comment")
            i = end+2
            continue
        out.append(c)
        i += 1
    text = "".join(out)
    out, quoted, escaped = [], False, False
    for i, c in enumerate(text):
        if quoted:
            if escaped: escaped = False
            elif c == "\\": escaped = True
            elif c == '"': quoted = False
        elif c == '"': quoted = True
        elif c == ",":
            j = i+1
            while j < len(text) and text[j].isspace(): j += 1
            if j < len(text) and text[j] in "}]": continue
        out.append(c)
    return json.loads("".join(out))


def references(obj):
    if isinstance(obj, dict):
        for key, value in obj.items():
            if key in ("configFile", "design") and isinstance(value, str) and value.strip():
                yield value.replace("\\", "/")
            elif isinstance(value, (dict, list)):
                yield from references(value)
    elif isinstance(obj, list):
        for child in obj: yield from references(child)


def audit_session(folder):
    folder = Path(folder)
    files = [p for p in folder.rglob("*") if p.is_file()]
    result = dict(session=folder.name, missingSequence=False, missingSystem=False,
                  unreadableSequences=[], missingExperimentReferences=[], nestedExperimentCopies=[],
                  manifestPresent=False, manifestComplete=None, invalidSnapshots=[])
    manifest = folder/"config_manifest.json"
    if manifest.is_file():
        result["manifestPresent"] = True
        try:
            doc = read_json(manifest)
            result["manifestComplete"] = doc["complete"]
            roles = {role for f in doc["files"] for role in f["roles"]}
            result["missingSequence"] = "sequence" not in roles
            result["missingSystem"] = "system" not in roles
            for record in doc["files"]:
                path = folder/record["snapshotPath"]
                if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != record["sha256"]:
                    result["invalidSnapshots"].append(record["snapshotPath"])
            result["archiveErrors"] = doc.get("errors", [])
            return result
        except (ValueError, KeyError, TypeError) as error:
            result["invalidSnapshots"].append("Manifest: "+str(error))
    sequence = [p for p in files if p.name == "sequenceConfig.json" or p.name.endswith("_sequenceConfig.json")]
    system = [p for p in files if p.name == "system_config.json" or p.name.endswith("_system_config.json")]
    result["missingSequence"], result["missingSystem"] = not sequence, not system
    refs = set()
    for path in sequence:
        try: refs.update(references(read_json(path)))
        except (ValueError, TypeError): result["unreadableSequences"].append(str(path.relative_to(folder)))
    relative = {p: str(p.relative_to(folder)).replace("\\", "/") for p in files}
    for ref in sorted(refs):
        matches = [p for p, name in relative.items() if name == ref or name.endswith("_"+ref)]
        if not matches and "/" not in ref:
            matches = [p for p in files if p.name == ref or p.name.endswith("_"+ref)]
        if not matches: result["missingExperimentReferences"].append(ref)
        elif any(p.parent != folder for p in matches): result["nestedExperimentCopies"].append(ref)
    return result


def audit(root, recent_since=None):
    sessions = [audit_session(p) for p in sorted(Path(root).iterdir()) if p.is_dir() and p.name != "Backup"]
    def counts(items):
        return dict(sessions=len(items), missingSequence=sum(s["missingSequence"] for s in items),
                    missingSystem=sum(s["missingSystem"] for s in items),
                    sessionsMissingExperimentConfigs=sum(bool(s["missingExperimentReferences"]) for s in items),
                    missingUniqueExperimentReferences=sum(len(s["missingExperimentReferences"]) for s in items),
                    unreadableSequences=sum(len(s["unreadableSequences"]) for s in items),
                    sessionsWithNestedExperimentCopies=sum(bool(s["nestedExperimentCopies"]) for s in items),
                    sessionsWithManifest=sum(s["manifestPresent"] for s in items),
                    invalidSnapshotSessions=sum(bool(s["invalidSnapshots"]) for s in items))
    report = dict(counts=counts(sessions), sessions=sessions,
                  note="Read-only: no historical configs are created, copied or replaced. Old sessions may predate system_config. Missing means no saved version was located, not proof a current file reproduces it. Nested timestamp-prefixed folders are searched. New manifest snapshots are SHA-256 verified.")
    if recent_since:
        cutoff = recent_since.replace("-", "")
        report["recentSince"] = recent_since
        report["recentCounts"] = counts([s for s in sessions if s["session"][:8] >= cutoff])
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run_data", type=Path)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--recent-since")
    args = parser.parse_args()
    result = audit(args.run_data, args.recent_since)
    args.report.write_text(json.dumps(result, indent=2)+"\n")
    print(json.dumps({k: v for k, v in result.items() if k != "sessions"}, indent=2))


if __name__ == "__main__": main()

"""Read-only source and archived-run audit for restarting P2 development."""
import collections
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
CBATH = Path(r"E:\MBmod\CBATH-P2")
OUT = Path(__file__).parent


def git(root, *args):
    return subprocess.check_output(["git", *args], cwd=root, text=True, encoding="utf-8").strip()


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def payload(path):
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    value = value.get("result", value)
    if "structuredContent" in value:
        return value["structuredContent"]
    if "content" in value:
        return json.loads(value["content"][0]["text"])
    return value


result = {"repositories": {}, "sessions": {}}
shared_log = Path.home() / "Documents/CombatBrain2/CombatBrain2.log"
if shared_log.exists():
    raw = shared_log.read_bytes()
    result["shared_log_at_review"] = {"path": str(shared_log), "sha256": digest(shared_log),
        "bytes": len(raw), "nul_bytes": raw.count(b"\0"),
        "first_line": raw.split(b"\n", 1)[0].decode("utf-8-sig", errors="replace")}
    if b"b20261013-211336-11880d6a" in raw.split(b"\n", 1)[0]:
        recovered = OUT / "recovered-B-log.bin"
        recovered.write_bytes(raw)
        result["shared_log_at_review"]["preserved_copy"] = recovered.name
for root in (ROOT, CBATH):
    changed = [p for p in git(root, "diff", "--name-only").splitlines()
               if p != "prompts/registry.json"]
    result["repositories"][str(root)] = {
        "head": git(root, "rev-parse", "HEAD"),
        "branch": git(root, "branch", "--show-current"),
        "status": "\n".join(line for line in git(root, "status", "--porcelain").splitlines()
            if not line.endswith("prompts/registry.json")
            and not line.endswith("Program/_verify/P2-RESTART-REMEDIATION-DISPATCH.md")),
        "tracked_dirty_sha256": {p: digest(root / p) for p in changed},
    }

for name in ("p2-A-20261013-210823", "p2-B-20261013-211339"):
    directory = ROOT / "Program/_verify/sessions" / name
    data = {"file_sha256": {p.name: digest(p) for p in directory.iterdir() if p.is_file()},
            "events": [], "selected_samples": [], "sample_errors": []}
    log = directory / "CombatBrain2.log"
    if log.exists():
        for n, line in enumerate(log.read_text(encoding="utf-8-sig").splitlines(), 1):
            event = re.match(r"\[CB2\]\[([^]]+)\]", line)
            if not event:
                continue
            fields = dict(re.findall(r"(?:^|\s)([\w*]+)=([^\s]+)", line))
            data["events"].append({"line": n, "event": event[1], "fields": fields})
        data["counts"] = dict(collections.Counter(e["event"] for e in data["events"]))
        ordinary = [e for e in data["events"] if e["event"] == "WRITE_EXECUTED"
                    and "-exit" not in e["fields"]["task"]]
        data["ordinary_stagger_violations"] = [
            {"previous_line": a["line"], "line": b["line"],
             "previous_task": a["fields"]["task"], "task": b["fields"]["task"],
             "gap": round(float(b["fields"]["at"]) - float(a["fields"]["at"]), 3)}
            for a, b in zip(ordinary, ordinary[1:])
            if float(b["fields"]["at"]) - float(a["fields"]["at"]) < 1.5]
        identity = next(e["fields"] for e in data["events"] if e["event"] == "RUNTIME_IDENTITY")
        data["deployment_matches_runtime_disk_hash"] = (
            payload(directory / "deployed-module.json")["sha256"].lower()
            == identity["assembly_file_sha256"].lower())
        data["events"] = [e for e in data["events"] if e["event"] in {
            "RUNTIME_IDENTITY", "WRITE_EXECUTED", "TASK_FEEDBACK", "ASSIGNMENT_PUBLISHED",
            "COORDINATION_ADVANCE", "COORDINATION_REASSIGNED", "COORDINATION_REBOUND_TASK",
            "BEHAVIOR_REWEIGHTED", "P2_PROBE_TRIGGER", "PLAN_INVALIDATED", "MISSION_CLEANUP",
            "EXIT_STOP_SUBMITTED", "EXIT_STOP_NOT_APPLIED", "ARBITRATION_CANDIDATE"}]
    for path in sorted(directory.glob("p2-evidence-*.json")):
        try:
            sample = payload(path)
        except (ValueError, KeyError) as error:
            data["sample_errors"].append({"file": path.name, "reason": type(error).__name__})
            continue
        if "mission_time" not in sample:
            continue
        now = float(sample["mission_time"])
        if name.startswith("p2-A") and 33 <= now <= 58:
            own = next(t for t in sample["teams"] if not t["is_player_team"])
            data["selected_samples"].append({
                "file": path.name, "time": now, "mission_identity": sample.get("mission_identity"),
                "formations": [{k: f.get(k) for k in
                    ("formation_index", "formation_identity", "formation_class", "movement_order",
                     "active_center", "active_member_count", "member_digest", "is_ai_controlled", "move_target")}
                    for f in own["formations"]]})
    result["sessions"][name] = data
    print(name, "archived_log=", log.exists(), "bad_samples=", data["sample_errors"],
          "stagger_violations=", data.get("ordinary_stagger_violations"),
          "reweights=", data.get("counts", {}).get("BEHAVIOR_REWEIGHTED", 0))

path = OUT / "current-snapshot.json"
path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(path)

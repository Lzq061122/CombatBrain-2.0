"""Read archived P2 evidence without invoking a game, build, or deployment."""
import collections
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
SESSIONS = ROOT / "Program/_verify/sessions"
NAMES = ("p2-A-20261013-163716", "p2-B-20261013-164318",
         "20261013-170546", "20261013-170928")


def read_json(path):
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if "result" in value:
        value = value["result"]
    if "structuredContent" in value:
        return value["structuredContent"]
    if "content" in value:
        return json.loads(next(c["text"] for c in value["content"] if c["type"] == "text"))
    return value


def fields(line):
    return dict(re.findall(r"(?:^|\s)([\w*]+)=([^\s]+)", line))


def point(text):
    return tuple(float(v) for v in text.strip("()").split(","))


def distance(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def check_geometry(sample, geometry, slot, role):
    team = next(t for t in sample["teams"] if t["team_index"] == 1)
    f = next(f for f in team["formations"] if f["formation_index"] == slot)
    p = f.get("active_center")
    if not f["active_center_known"] or not p:
        return {"known": False}
    center = float(p["x"]), float(p["y"])
    m, target = point(geometry["M*"]), point(geometry["G*" if role == "Guard" else "S*"])
    u, v = point(geometry["u"]), point(geometry["v"])
    delta = center[0] - m[0], center[1] - m[1]
    forward = sum(a * b for a, b in zip(delta, u))
    lateral = sum(a * b for a, b in zip(delta, v))
    error = distance(center, target)
    inside = error <= 5 and (5 <= forward <= 15 and abs(lateral) <= 5
                            if role == "Guard" else 7 <= abs(lateral) <= 17)
    return {"known": True, "center": center, "target_error": round(error, 3),
            "forward": round(forward, 3), "lateral": round(lateral, 3),
            "inside": inside, "ai": f["is_ai_controlled"],
            "members": f["active_member_count"], "member_digest": f["member_digest"],
            "order": f["movement_order"], "move_target": f["move_target"]}


output = {"sessions": {}, "input_sha256": {}}
output["reviewed_head"] = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
output["implementation_commit"] = "8fb1efa"
output["implementation_file_sha256"] = {}
for name in subprocess.check_output(["git", "diff-tree", "--no-commit-id", "--name-only", "-r", "8fb1efa"],
                                    cwd=ROOT, text=True).splitlines():
    output["implementation_file_sha256"][name] = hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
for name in NAMES:
    directory = SESSIONS / name
    lines = (directory / "CombatBrain2.log").read_text(encoding="utf-8-sig").splitlines()
    events = []
    for n, line in enumerate(lines, 1):
        match = re.match(r"\[CB2\]\[([^]]+)\]", line)
        if match:
            events.append({"line": n, "event": match[1], "fields": fields(line), "raw": line})
    counts = collections.Counter(e["event"] for e in events)
    identity = next(e["fields"] for e in events if e["event"] == "RUNTIME_IDENTITY")
    deployment = read_json(directory / "deployed-module.json")
    cbath = read_json(directory / "build-identity.json")
    writes = [e for e in events if e["event"] == "WRITE_EXECUTED"]
    data = {"event_counts": dict(counts), "cb2_identity": identity,
            "cb2_deployed_sha": deployment["sha256"],
            "cb2_runtime_disk_sha_matches": deployment["sha256"].lower() == identity["assembly_file_sha256"].lower(),
            "cbath_identity": cbath, "writes": writes,
            "feedback": [e for e in events if e["event"] == "TASK_FEEDBACK"],
            "assignments": [e for e in events if e["event"] == "ASSIGNMENT_PUBLISHED"],
            "transitions": [e for e in events if e["event"] in
                {"COORDINATION_ADVANCE", "COORDINATION_REASSIGNED", "COORDINATION_DEPENDENCY_LOST",
                 "P2_PROBE_TRIGGER", "P2_PROBE_ELIGIBILITY_LOST", "PLAN_INVALIDATED", "MISSION_CLEANUP"}],
            "arbitration": [e for e in events if e["event"] == "ARBITRATION_CANDIDATE"]}
    samples = [(p, read_json(p)) for p in sorted(directory.glob("p2-evidence-*.json"))]
    data["independent_sample_count"] = len(samples)
    data["samples_without_mission_time"] = [{"file": p.name, "payload": s}
        for p, s in samples if "mission_time" not in s]
    data["independent_has_object_identity"] = bool(samples) and all(
        "object_identity" in f for _, s in samples for t in s.get("teams", []) for f in t["formations"])
    if name == NAMES[0]:
        g = next(e["fields"] for e in events if e["event"] == "PLAN_CREATED" and e["fields"]["plan"] == "plan-3")
        selected = []
        for path, sample in samples:
            if "mission_time" not in sample:
                continue
            now = float(sample["mission_time"])
            if 55 <= now <= 66 or 34 <= now <= 43:
                team = next(t for t in sample["teams"] if t["team_index"] == 1)
                selected.append({"file": path.name, "time": now,
                    "main": next(f for f in team["formations"] if f["formation_index"] == 0),
                    "guard": check_geometry(sample, g, 1, "Guard"),
                    "support": check_geometry(sample, g, 3, "Support")})
        data["selected_independent_samples"] = selected
    elif name == NAMES[1]:
        data["loss_window_samples"] = [{"file": p.name, "sample": s} for p, s in samples
            if "mission_time" in s and 9.5 <= float(s["mission_time"]) <= 15.5]
    for path in [directory / "CombatBrain2.log", *directory.glob("*.json")]:
        output["input_sha256"][str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
    output["sessions"][name] = data

destination = Path(__file__).with_name("evidence-summary.json")
destination.write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding="utf-8")
for name, data in output["sessions"].items():
    print(name, "identity_match=", data["cb2_runtime_disk_sha_matches"],
          "writes=", len(data["writes"]), "samples=", data["independent_sample_count"],
          "object_identity=", data["independent_has_object_identity"])
    print("counts", {k: data["event_counts"].get(k, 0) for k in
          ("COORDINATION_ADVANCE", "COORDINATION_REASSIGNED", "ARBITRATION_CANDIDATE",
           "ARBITRATION_INJECT", "WRITE_BLOCKED", "TASK_SELECTION_TIMEOUT", "MISSION_CLEANUP")})
    if name == NAMES[0]:
        print("revision2_assignments", [e for e in data["assignments"] if e["fields"].get("revision") == "2"])
        for s in data["selected_independent_samples"]:
            if s["time"] >= 55:
                print(s["file"], s["time"], "G", s["guard"]["target_error"], s["guard"]["inside"],
                      "S", s["support"]["target_error"], s["support"]["inside"],
                      "main_order", s["main"]["movement_order"], "main_center", s["main"].get("active_center"))
print("summary:", destination)

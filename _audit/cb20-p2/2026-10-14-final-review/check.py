"""Recompute final P2 run evidence without starting or deploying a game."""
import collections
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).parent
NAMES = ("p2-A-20261014-114127", "p2-B-20261014-114553",
         "20261014-115634", "20261014-120020")


def payload(p):
    x = json.loads(p.read_text(encoding="utf-8-sig"))
    x = x.get("result", x)
    return x["structuredContent"] if "structuredContent" in x else x


def point(s):
    return tuple(float(v) for v in s.strip("()").split(","))


def geometry(f, g, role):
    p = f["active_center"]
    p = float(p["x"]), float(p["y"])
    target = point(g["G*" if role == "Guard" else "S*"])
    main, u, v = point(g["M*"]), point(g["u"]), point(g["v"])
    delta = p[0] - main[0], p[1] - main[1]
    forward = sum(a * b for a, b in zip(delta, u))
    lateral = sum(a * b for a, b in zip(delta, v))
    error = math.dist(p, target)
    return {"error": round(error, 3), "forward": round(forward, 3), "lateral": round(lateral, 3),
            "inside": error <= 5 and (5 <= forward <= 15 and abs(lateral) <= 5
                if role == "Guard" else 7 <= abs(lateral) <= 17)}


result = {"head": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
          "sessions": {}}
for name in NAMES:
    d = ROOT / "Program/_verify/sessions" / name
    events = []
    for n, line in enumerate((d / "CombatBrain2.log").read_text(encoding="utf-8-sig").splitlines(), 1):
        m = re.match(r"\[CB2\]\[([^]]+)\]", line)
        if m:
            events.append({"line": n, "event": m[1],
                "fields": dict(re.findall(r"(?:^|\s)([\w*]+)=([^\s]+)", line))})
    counts = collections.Counter(e["event"] for e in events)
    identity = next(e["fields"] for e in events if e["event"] == "RUNTIME_IDENTITY")
    writes = [e for e in events if e["event"] == "WRITE_EXECUTED"]
    ordinary = [e for e in writes if not e["fields"]["task"].endswith("-exit")]
    gaps = [round(float(b["fields"]["at"]) - float(a["fields"]["at"]), 3)
            for a, b in zip(ordinary, ordinary[1:])]
    cbath_identity = payload(d / "build-identity.json")
    data = {"identity": identity, "cbath_identity": cbath_identity,
        "same_process_for_cb2_and_cbath": identity["pid"] == str(cbath_identity["pid"]),
        "deployed_hash_matches_runtime_disk_hash": payload(d / "deployed-module.json")["sha256"].lower()
            == identity["assembly_file_sha256"].lower(),
        "counts": dict(counts), "ordinary_write_gaps": gaps,
        "min_ordinary_write_gap": min(gaps) if gaps else None,
        "all_ordinary_gaps_at_least_1_5": all(v >= 1.5 for v in gaps),
        "all_write_receipts_valid": all(e["fields"].get("is_main_thread") == "1" and
            e["fields"].get("original_body_entered") == "1" and e["fields"].get("error") == "<none>"
            for e in writes),
        "events": [e for e in events if e["event"] in {"WRITE_EXECUTED", "TASK_FEEDBACK",
            "PLAN_CREATED", "ASSIGNMENT_PUBLISHED", "COORDINATION_ADVANCE", "COORDINATION_REASSIGNED",
            "COORDINATION_DEPENDENCY_LOST", "PLAN_INVALIDATED", "P2_PROBE_TRIGGER",
            "EXIT_STOP_QUEUED", "EXIT_STOP_SUBMITTED", "EXIT_STOP_DONE", "EXIT_STOP_NOT_APPLIED",
            "ARBITRATION_CANDIDATE", "PROPOSAL_PUBLISHED", "P2_PROBE_ELIGIBILITY_LOST",
            "PROBE_ELIGIBILITY_RESTORED", "PROBE_ELIGIBILITY_LOST", "BEHAVIOR_DISARMED",
            "HANDOFF_CLEARED", "COMPETITION_INJECT_WINDOW_SATISFIED"}],
        "sample_errors": [], "selected_samples": [],
        "sha256": {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in d.iterdir() if p.is_file()}}
    g = next((e["fields"] for e in events if e["event"] == "PLAN_CREATED" and "G*" in e["fields"]), None)
    for p in sorted(d.glob("p2-evidence-*.json")):
        try:
            s = payload(p)
        except (ValueError, KeyError) as error:
            data["sample_errors"].append({"file": p.name, "reason": type(error).__name__})
            continue
        if "mission_time" not in s:
            continue
        now = float(s["mission_time"])
        if (name == NAMES[0] and (16 <= now <= 25 or 27 <= now <= 53 or 97 <= now <= 101)) or (name == NAMES[1] and 9 <= now <= 15):
            own = next(t for t in s["teams"] if not t["is_player_team"])
            formations = [{k: f.get(k) for k in ("formation_index", "formation_identity", "formation_class",
                "active_center", "member_digest", "is_ai_controlled", "active_member_count",
                "movement_order", "move_target")} for f in own["formations"]]
            if g and name == NAMES[0] and (16 <= now <= 25 or 40 <= now <= 51):
                for f in formations:
                    roles = {1: "Guard", 2: "Support"} if now < 25 else {2: "Guard", 3: "Support"}
                    if f["formation_index"] in roles:
                        f["geometry"] = geometry(f, g, roles[f["formation_index"]])
            data["selected_samples"].append({"file": p.name, "time": now,
                "mission_identity": s.get("mission_identity"), "formations": formations})
    if name in NAMES[2:]:
        timeline = payload(d / "cbath-timeline.json")
        data["p1_early_independent_orders"] = [
            {"time": s["t"], "orders": [{"slot": f["formation_index"], "order": f["movement_order"]}
                for t in s["teams"] if not t["is_player_team"] for f in t["formations"]]}
            for s in timeline["samples"] if 4 < float(s["t"]) < 8]
    result["sessions"][name] = data
    print(name, "hash_match", data["deployed_hash_matches_runtime_disk_hash"], "writes", len(writes),
          "ordinary", len(ordinary), "min_gap", data["min_ordinary_write_gap"],
          "receipts", data["all_write_receipts_valid"], "bad_samples", data["sample_errors"],
          "reweighted", counts.get("BEHAVIOR_REWEIGHTED", 0))
    if name == NAMES[0]:
        for s in data["selected_samples"]:
            if 43 <= s["time"] <= 51:
                print(s["file"], s["time"], [(f["formation_index"], f["movement_order"],
                    f.get("geometry", {}).get("error"), f.get("geometry", {}).get("inside"))
                    for f in s["formations"]])
comparisons = []
for module, rebuilt, deployed in (
    ("CB2", ROOT / "Program/bin/Debug/CombatBrain2.dll",
     Path(r"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CombatBrain2\bin\Win64_Shipping_Client\CombatBrain2.dll")),
    ("CBATH", OUT / "cbath-build/CombatBrainAutonomousTestHarness.dll",
     Path(r"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CombatBrainAutonomousTestHarness\bin\Win64_Shipping_Client\CombatBrainAutonomousTestHarness.dll"))):
    il = [subprocess.check_output(["ilspycmd", "-il", str(p)], text=True, encoding="utf-8")
          for p in (rebuilt, deployed)]
    comparisons.append({"module": module, "rebuilt": str(rebuilt), "deployed": str(deployed),
        "rebuilt_sha256": hashlib.sha256(rebuilt.read_bytes()).hexdigest(),
        "deployed_sha256": hashlib.sha256(deployed.read_bytes()).hexdigest(),
        "full_il_output_identical": il[0] == il[1],
        "il_sha256": [hashlib.sha256(x.encode("utf-8")).hexdigest() for x in il]})
result["current_rebuilt_vs_last_deployed"] = comparisons
(OUT / "evidence-summary.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")

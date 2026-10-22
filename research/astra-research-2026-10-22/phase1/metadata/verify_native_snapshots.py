"""Recheck downloaded native-evidence snapshots; no network or game execution."""
from pathlib import Path
from collections import defaultdict
import csv
import hashlib
import json
import statistics

ROOT = Path(__file__).resolve().parents[1]
TCL = ROOT / 'sources/tcl/phase10'
CBATH = ROOT / 'sources/cbath/default'
index = json.loads((TCL / 'docs/evidence/phase10-closure-20261007/closure-index.json').read_text())
checks = []
for item in index['derived_outputs']:
    target = TCL / item['path']
    actual = hashlib.sha256(target.read_bytes()).hexdigest()
    checks.append({'path': item['path'], 'present': target.exists(),
                   'sha_match': actual == item['sha256'], 'sha256': actual})
base = TCL / 'docs/evidence/phase10-closure-20261007'
consistency = []
for run in ('run05', 'run06'):
    with (base / (run + '-changes.csv')).open() as f:
        rows = list(csv.DictReader(f))
    groups = defaultdict(list)
    for row in rows:
        groups[(row['entity'], row['domain'])].append(row)
    triples = []
    for key, group in groups.items():
        group.sort(key=lambda x: int(x['source_row']))
        for a, b, c in zip(group, group[1:], group[2:]):
            if a['new_value'] == c['new_value'] and a['new_value'] != b['new_value']:
                triples.append((key, int(a['source_row']), int(b['source_row']), int(c['source_row'])))
    with (base / (run + '-triples.csv')).open() as f:
        archived = list(csv.DictReader(f))
    archive_set = {((x['entity'], x['domain']), int(x['row_a']), int(x['row_b']), int(x['row_c'])) for x in archived}
    consistency.append({'run': run, 'accepted_change_rows': len(rows),
                        'unique_entity_domain': len(groups),
                        'ABA_recomputed_from_derived_changes': len(triples),
                        'ABA_identity_matches_archived_derived': set(triples) == archive_set,
                        'target_T1F0_ABA': sum(x[0][0] == 'T1F0' for x in triples),
                        'scope': 'derived data consistency only; raw telemetry absent'})
timeline = CBATH / 'RD/CombatBrain-Autonomous-Test-Harness/evidence/runs/p2-006-cbath-timeline-live.json'
data = json.loads(timeline.read_text(encoding='utf-8-sig'))
times = [float(s['t']) for s in data['samples']]
deltas = [b - a for a, b in zip(times, times[1:])]
summary = {
    'read_date': '2026-10-22', 'tcl_derived_files': checks,
    'tcl_raw_run04_11_present': (TCL / '_audit/_local-phase10-20261006').exists(),
    'cbath_final_zt_raw_present': bool(list(CBATH.rglob('zt-*.json'))),
    'cbath_older_timeline': {'path': str(timeline.relative_to(ROOT)),
                           'sha256': hashlib.sha256(timeline.read_bytes()).hexdigest(),
                           'samples': len(times), 'declared_samples': data['sample_count'],
                           'first_time': times[0], 'last_time': times[-1],
                           'interval_min': min(deltas), 'interval_max': max(deltas),
                           'interval_median': statistics.median(deltas),
                           'mission_start': data['mission_start_time'], 'mission_end': data['mission_end_time']},
    'applicable_AGENTS_files': [str(p.relative_to(ROOT)) for b in ('sources/tcl', 'sources/cbath') for p in (ROOT / b).rglob('AGENTS.md')],
    'license_files': [str(p.relative_to(ROOT)) for b in ('sources/tcl', 'sources/cbath') for p in (ROOT / b).rglob('*') if p.is_file() and p.name.lower().startswith(('license', 'licence'))],
    'derived_consistency_recheck': consistency,
}
(ROOT / 'metadata/native_snapshot_audit.json').write_text(json.dumps(summary, indent=2, ensure_ascii=False) + '\n')
print(json.dumps({'derived_hash_checks': sum(x['sha_match'] for x in checks), 'derived_file_count': len(checks),
                  'derived_consistency': consistency, 'cbath_older_timeline_samples': len(times),
                  'runtime': 'NOT_RUN'}, indent=2))
assert all(x['sha_match'] for x in checks)
assert all(x['ABA_identity_matches_archived_derived'] for x in consistency)
assert len(times) == data['sample_count']

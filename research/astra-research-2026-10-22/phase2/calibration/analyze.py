#!/usr/bin/env python3
"""Descriptive, paired calibration analysis. Standard library only; no game/DLL access.

The CSV is observations, never planner success labels. SYNTHETIC data cannot
be silently presented as GAME. See schema.md for estimands and censoring rules.
"""
import argparse
import csv
import json
import math
from collections import defaultdict
from pathlib import Path
from statistics import mean, median

META = ('schema_version', 'evidence_kind', 'experiment', 'protocol_id', 'block_id',
        'run_id', 'arm', 'game_build', 'cb_commit', 'mod_profile', 'scene',
        'roster_hash', 'equipment_hash', 'opponent_policy', 'morale', 'side',
        'seed', 'horizon_s', 'measurement_source', 'evidence_ref', 'physical_prefix_id')
FIELDS = META + ('metric', 'entity_id', 'value', 'unit', 'status', 'pred_lo_s',
                 'pred_hi_s', 'pred_point_s', 'deadline_s')
COHORT = ('evidence_kind', 'experiment', 'protocol_id', 'game_build', 'cb_commit',
          'mod_profile', 'scene', 'roster_hash', 'equipment_hash', 'opponent_policy',
          'morale', 'side', 'horizon_s')
TEXT = {'observation', 'action_before', 'action_after'}
ENDPOINT = ('enemy_killed', 'own_killed', 'enemy_routed', 'own_routed')
ARMS = {'ETA': {'forecast'},
        'FIX': {'none', 'single_a', 'single_b', 'joint', 'same_resource_alt'},
        'PRESSURE': {'observe', 'blind', 'no_pressure'},
        'STAGES': {'staged', 'single_window', 'local_repair', 'full_replan'}}
METRICS = {
    'ETA': {'travel_s', 'setup_s', 'exit_s', 'ready_at_s'},
    'FIX': set(ENDPOINT) | {'support_arrival_s', 'duty_gap_s'},
    'PRESSURE': set(ENDPOINT) | TEXT | {'pressure_own_loss', 'pressure_time_s', 'exit_delay_s'},
    'STAGES': set(ENDPOINT) | {'planned_stages', 'completed_stages', 'cancelled_stages',
                'duty_gap_s', 'resource_overlap_s', 'premature_starts', 'cancel_latency_s',
                'stale_writes', 'replan_time_ms', 'aba_reversals'},
}
COUNTS = set(ENDPOINT) | {'planned_stages', 'completed_stages', 'cancelled_stages',
                         'premature_starts', 'stale_writes', 'aba_reversals', 'pressure_own_loss'}


def number(value, name):
    try:
        out = float(value)
    except (TypeError, ValueError):
        raise ValueError(f'{name}: expected a number, got {value!r}')
    if not math.isfinite(out):
        raise ValueError(f'{name}: non-finite value')
    return out


def percentile(values, q):
    values = sorted(values)
    p = (len(values) - 1) * q
    lo = math.floor(p)
    return values[lo] + (values[math.ceil(p)] - values[lo]) * (p - lo)


def describe(values):
    values = list(values)
    if not values:
        return {'n': 0}
    return {'n': len(values), 'mean': mean(values), 'median': median(values),
            'p10': percentile(values, .1), 'p90': percentile(values, .9),
            'min': min(values), 'max': max(values)}


def load(path):
    with path.open(encoding='utf-8-sig', newline='') as f:
        reader = csv.DictReader(f)
        absent = set(FIELDS) - set(reader.fieldnames or ())
        if absent:
            raise ValueError('missing columns: ' + ', '.join(sorted(absent)))
        rows = list(reader)
    if not rows:
        raise ValueError('empty dataset')
    seen, run_meta, pair_runs = set(), {}, {}
    for line, r in enumerate(rows, 2):
        prefix = f'line {line}'
        for key in META + ('metric', 'entity_id', 'value', 'unit', 'status'):
            if key != 'physical_prefix_id' and not r[key]:
                raise ValueError(f'{prefix}: missing {key}')
        if r['schema_version'] != '1':
            raise ValueError(f'{prefix}: unknown schema')
        if r['evidence_kind'] not in {'GAME', 'SYNTHETIC'}:
            raise ValueError(f'{prefix}: evidence_kind must be GAME or SYNTHETIC')
        exp, metric = r['experiment'], r['metric']
        if exp not in ARMS or r['arm'] not in ARMS[exp] or metric not in METRICS[exp]:
            raise ValueError(f'{prefix}: unsupported experiment, arm, or metric')
        if r['morale'] not in {'normal', 'deathmatch'}:
            raise ValueError(f'{prefix}: unknown morale mode')
        horizon = number(r['horizon_s'], prefix)
        if horizon <= 0:
            raise ValueError(f'{prefix}: horizon must be positive')
        if r['status'] not in {'OBSERVED', 'RIGHT_CENSORED'}:
            raise ValueError(f'{prefix}: unknown status; missing observations must not be zero')
        if r['status'] == 'RIGHT_CENSORED' and not (exp == 'ETA' or metric == 'support_arrival_s'):
            raise ValueError(f'{prefix}: censoring not supported for this metric')
        if metric not in TEXT:
            value = number(r['value'], prefix)
            if value < 0:
                raise ValueError(f'{prefix}: observations cannot be negative')
            if metric in COUNTS and value != int(value):
                raise ValueError(f'{prefix}: count must be integer')
            r['_value'] = value
        if metric in TEXT and r['unit'] != 'text':
            raise ValueError(f'{prefix}: text unit expected')
        if metric not in TEXT:
            expected_unit = 'count' if metric in COUNTS else ('ms' if metric.endswith('_ms') else 's')
            if r['unit'] != expected_unit:
                raise ValueError(f'{prefix}: expected unit {expected_unit}')
        if exp == 'ETA':
            for key in ('pred_lo_s', 'pred_hi_s', 'pred_point_s'):
                r['_' + key] = number(r[key], f'{prefix} {key}')
            if not 0 <= r['_pred_lo_s'] <= r['_pred_point_s'] <= r['_pred_hi_s']:
                raise ValueError(f'{prefix}: inconsistent forecast interval')
            if r['deadline_s']:
                r['_deadline_s'] = number(r['deadline_s'], prefix)
                if r['_deadline_s'] < 0:
                    raise ValueError(f'{prefix}: negative deadline')
        elif r['entity_id'] != 'run':
            raise ValueError(f'{prefix}: outcome rows require entity_id=run')
        if metric == 'support_arrival_s':
            if r['_value'] > horizon or (r['status'] == 'RIGHT_CENSORED' and r['_value'] != horizon):
                raise ValueError(f'{prefix}: support arrival must be observed within horizon or censored exactly at horizon')
        if r['evidence_kind'] == 'GAME':
            if r['measurement_source'] != 'independent_capture':
                raise ValueError(f'{prefix}: GAME observations require independent_capture')
            if len(r['cb_commit']) != 40 or any(c not in '0123456789abcdef' for c in r['cb_commit']):
                raise ValueError(f'{prefix}: GAME commit must be full hexadecimal SHA')
            if any('synthetic' in r[k].lower() or r[k].lower() in {'unknown', 'todo', 'na'}
                   for k in ('game_build', 'mod_profile', 'scene', 'roster_hash', 'equipment_hash', 'evidence_ref')):
                raise ValueError(f'{prefix}: GAME provenance contains placeholder')
        identity = (r['evidence_kind'], r['run_id'])
        metadata = tuple(r[k] for k in META)
        if identity in run_meta and run_meta[identity] != metadata:
            raise ValueError(f'{prefix}: run metadata changed within run_id')
        run_meta[identity] = metadata
        unique = identity + (metric, r['entity_id'])
        if unique in seen:
            raise ValueError(f'{prefix}: duplicate metric/entity in run')
        seen.add(unique)
        pair = tuple(r[k] for k in COHORT) + (r['block_id'], r['arm'])
        if pair in pair_runs and pair_runs[pair] != r['run_id']:
            raise ValueError(f'{prefix}: multiple runs in paired block/arm; use unique block per replication')
        pair_runs[pair] = r['run_id']
    return rows


def paired(blocks, left, right, metric):
    differences, excluded = [], []
    for block, arms in blocks.items():
        a, b = arms.get(left, {}), arms.get(right, {})
        if metric not in a or metric not in b:
            excluded.append(block)
            continue
        if a[metric]['seed'] != b[metric]['seed']:
            raise ValueError(f'{block}: paired arms have different seed')
        if left == 'observe' and right == 'blind':
            x, y = a[metric]['physical_prefix_id'], b[metric]['physical_prefix_id']
            if not x or x != y:
                excluded.append(block + ':unverified_physical_prefix')
                continue
        differences.append(a[metric]['_value'] - b[metric]['_value'])
    return {'left_minus_right': describe(differences), 'excluded_blocks': excluded,
            'interpretation': 'descriptive paired differences; no causal or statistical guarantee'}


def eta_summary(rows):
    result = {}
    for metric in sorted({r['metric'] for r in rows}):
        group = [r for r in rows if r['metric'] == metric]
        observed = [r for r in group if r['status'] == 'OBSERVED']
        censored = [r for r in group if r['status'] == 'RIGHT_CENSORED']
        by_block = defaultdict(list)
        for r in observed:
            by_block[r['block_id']].append(r['_value'] - r['_pred_point_s'])
        deadlines = [r for r in observed if '_deadline_s' in r]
        result[metric] = {
            'observed_rows': len(observed), 'censored_rows': len(censored),
            'independent_blocks_declared': len({r['block_id'] for r in group}),
            'signed_error_s': describe(r['_value'] - r['_pred_point_s'] for r in observed),
            'absolute_error_s': describe(abs(r['_value'] - r['_pred_point_s']) for r in observed),
            'interval_hits_observed': sum(r['_pred_lo_s'] <= r['_value'] <= r['_pred_hi_s'] for r in observed),
            'interval_denominator_observed': len(observed),
            'interval_misses_proven_by_censor': sum(r['_value'] > r['_pred_hi_s'] for r in censored),
            'block_max_absolute_error_s': describe(max(abs(x) for x in xs) for xs in by_block.values()),
            'blocks_with_multiple_observations': sum(len(xs) > 1 for xs in by_block.values()),
            'multi_observation_blocks_all_late': sum(len(xs) > 1 and all(x > 0 for x in xs) for xs in by_block.values()),
            'window_slack_s_observed': describe(r['_deadline_s'] - r['_value'] for r in deadlines),
            'window_misses_observed': sum(r['_value'] > r['_deadline_s'] for r in deadlines),
            'window_denominator_observed': len(deadlines),
            'window_misses_proven_by_censor': sum('_deadline_s' in r and r['_value'] > r['_deadline_s'] for r in censored),
        }
    return result


def outcomes(rows):
    blocks = defaultdict(lambda: defaultdict(dict))
    for r in rows:
        blocks[r['block_id']][r['arm']][r['metric']] = r
    exp = rows[0]['experiment']
    metrics = sorted(set(r['metric'] for r in rows) - TEXT)
    by_arm = {}
    for arm in sorted({r['arm'] for r in rows}):
        by_arm[arm] = {m: describe(r['_value'] for r in rows if r['arm'] == arm and r['metric'] == m)
                       for m in metrics}
    result = {'by_arm': by_arm, 'paired': {}}
    contrasts = {'FIX': [('joint', 'none'), ('joint', 'single_a'), ('joint', 'single_b'), ('joint', 'same_resource_alt')],
                 'PRESSURE': [('observe', 'blind'), ('observe', 'no_pressure')],
                 'STAGES': [('staged', 'single_window'), ('local_repair', 'full_replan')]}[exp]
    for a, b in contrasts:
        result['paired'][a + '_minus_' + b] = {m: paired(blocks, a, b, m) for m in metrics}
    if exp == 'FIX':
        interaction = []
        for arms in blocks.values():
            if all('support_arrival_s' in arms.get(a, {}) for a in ('joint', 'single_a', 'single_b', 'none')):
                vals = {a: arms[a]['support_arrival_s']['_value'] for a in ('joint', 'single_a', 'single_b', 'none')}
                seeds = {arms[a]['support_arrival_s']['seed'] for a in vals}
                if len(seeds) != 1:
                    raise ValueError('FIX interaction has mismatched seeds')
                interaction.append(vals['joint'] - vals['single_a'] - vals['single_b'] + vals['none'])
        result['restricted_arrival_interaction_s'] = describe(interaction)
        result['support_censored_rows'] = sum(r['metric'] == 'support_arrival_s' and r['status'] == 'RIGHT_CENSORED' for r in rows)
        result['support_arrival_interpretation'] = 'min(arrival time, horizon); higher means delayed within this horizon, not permanent fixing'
    if exp == 'PRESSURE':
        decision = {}
        for arm in ('observe', 'blind', 'no_pressure'):
            available = [a[arm] for a in blocks.values() if arm in a and all(m in a[arm] for m in ('action_before', 'action_after'))]
            decision[arm] = {'available': len(available), 'changed': sum(x['action_before']['value'] != x['action_after']['value'] for x in available)}
        result['decision_change_descriptive_only'] = decision
        result['warning'] = 'Changing an action does not establish positive information value; observe vs blind requires matched physical prefix and outcomes.'
    return result


def analyze(rows):
    groups = defaultdict(list)
    for r in rows:
        groups[tuple(r[k] for k in COHORT)].append(r)
    kinds = {r['evidence_kind'] for r in rows}
    result = {'schema_version': 1, 'evidence_kinds': sorted(kinds),
              'game_evidence_present': 'GAME' in kinds,
              'warning': 'SYNTHETIC validates the analyzer only. Results are descriptive; no win rate, confidence bound, or game effectiveness is inferred.',
              'cohorts': []}
    for key, group in sorted(groups.items()):
        item = {'cohort': dict(zip(COHORT, key)), 'rows': len(group),
                'runs': len({r['run_id'] for r in group}),
                'declared_blocks': len({r['block_id'] for r in group})}
        item['analysis'] = eta_summary(group) if group[0]['experiment'] == 'ETA' else outcomes(group)
        result['cohorts'].append(item)
    return result


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('csv', type=Path)
    p.add_argument('--output', type=Path)
    p.add_argument('--require-game', action='store_true', help='reject any synthetic row')
    args = p.parse_args()
    try:
        rows = load(args.csv)
        if args.require_game and any(r['evidence_kind'] != 'GAME' for r in rows):
            raise ValueError('--require-game rejects SYNTHETIC data')
        result = analyze(rows)
    except (ValueError, OSError) as exc:
        p.error(str(exc))
    text = json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + '\n'
    if args.output:
        args.output.write_text(text, encoding='utf-8')
        print(f'{args.output}: {len(result["cohorts"])} cohorts; evidence={result["evidence_kinds"]}')
    else:
        print(text, end='')


if __name__ == '__main__':
    main()

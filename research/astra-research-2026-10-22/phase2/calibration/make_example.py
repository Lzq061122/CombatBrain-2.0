#!/usr/bin/env python3
"""Create explicitly SYNTHETIC fixtures, not combat simulations or game evidence."""
import csv
from pathlib import Path
from analyze import FIELDS


def create(path):
    rows = []
    def put(exp, block, arm, metric, value, entity='run', status='OBSERVED', **extra):
        r = dict.fromkeys(FIELDS, '')
        r.update(schema_version='1', evidence_kind='SYNTHETIC', experiment=exp,
                 protocol_id='SYNTHETIC-PILOT-v1', block_id=f'{exp}-{block}',
                 run_id=f'SYNTHETIC-{exp}-{block}-{arm}', arm=arm,
                 game_build='SYNTHETIC-NO-GAME', cb_commit='dd28965238a83c29ae824f7225d90e3b2c8bfce4',
                 mod_profile='SYNTHETIC-Vanilla', scene='SYNTHETIC-flat',
                 roster_hash='SYNTHETIC-roster', equipment_hash='SYNTHETIC-equipment',
                 opponent_policy='SYNTHETIC-fixture', morale='normal', side='A',
                 seed=str(block), horizon_s='30', measurement_source='fixture_generator',
                 evidence_ref='make_example.py', physical_prefix_id=f'SYNTHETIC-prefix-{block}',
                 metric=metric, entity_id=entity, value=str(value), status=status,
                 unit='s' if metric.endswith('_s') else 'ms' if metric.endswith('_ms') else 'count')
        r.update({k: str(v) for k, v in extra.items()})
        rows.append(r)
    for block in range(1, 5):
        for actor in ('a', 'b'):
            for metric, point in [('travel_s', 8), ('setup_s', 3), ('exit_s', 2), ('ready_at_s', 11)]:
                value = point + block - 2 + (0.5 if actor == 'b' else 0)
                put('ETA', block, 'forecast', metric, value, actor,
                    pred_lo_s=point - 1, pred_hi_s=point + 1, pred_point_s=point,
                    deadline_s=14 if metric == 'ready_at_s' else '')
        # Failure is right censored, never silently changed to 0 or dropped.
        if block == 4:
            put('ETA', block, 'forecast', 'ready_at_s', 20, 'blocked', 'RIGHT_CENSORED',
                pred_lo_s=10, pred_hi_s=12, pred_point_s=11, deadline_s=14)
        for arm, delay, ek, ok in [('none', 10, 8, 4), ('single_a', 13, 9, 6),
                                  ('single_b', 12, 9, 5), ('joint', 20, 12, 11),
                                  ('same_resource_alt', 11, 16, 7)]:
            arrival = min(30, delay + block)
            if arm == 'joint' and block == 4:
                arrival = 30
            put('FIX', block, arm, 'support_arrival_s', arrival,
                status='RIGHT_CENSORED' if arrival == 30 else 'OBSERVED')
            for metric, value in [('enemy_killed', ek + block), ('own_killed', ok),
                                  ('enemy_routed', 2), ('own_routed', 1), ('duty_gap_s', 0)]:
                put('FIX', block, arm, metric, value)
        for arm in ('observe', 'blind', 'no_pressure'):
            # Better/worse alternating outcomes; decision-change count is not a reward.
            ek = 12 + (2 if block % 2 else -2) if arm == 'observe' else 12
            for metric, value in [('enemy_killed', ek), ('own_killed', 7 if arm != 'no_pressure' else 4),
                                  ('enemy_routed', 1), ('own_routed', 1),
                                  ('pressure_own_loss', 3 if arm != 'no_pressure' else 0),
                                  ('pressure_time_s', 5 if arm != 'no_pressure' else 0),
                                  ('exit_delay_s', block if arm != 'no_pressure' else 0)]:
                put('PRESSURE', block, arm, metric, value)
            for metric, value in [('observation', 'counter' if block % 2 else 'no_response'),
                                  ('action_before', 'strike'),
                                  ('action_after', 'retreat' if arm == 'observe' and block % 2 else 'strike')]:
                put('PRESSURE', block, arm, metric, value, unit='text')
        for arm in ('staged', 'single_window', 'local_repair', 'full_replan'):
            for metric, value in [('enemy_killed', 12), ('own_killed', 6), ('enemy_routed', 1), ('own_routed', 1),
                                  ('planned_stages', 2), ('completed_stages', 1), ('cancelled_stages', 1),
                                  ('duty_gap_s', block if arm == 'full_replan' else 0),
                                  ('resource_overlap_s', 0), ('premature_starts', 0),
                                  ('cancel_latency_s', .5), ('stale_writes', 0),
                                  ('replan_time_ms', 5 if arm == 'local_repair' else 12), ('aba_reversals', 0)]:
                put('STAGES', block, arm, metric, value)
    with path.open('w', encoding='utf-8', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=FIELDS)
        writer.writeheader()
        writer.writerows(rows)
    return len(rows)


if __name__ == '__main__':
    path = Path(__file__).with_name('example.csv')
    print(f'{path}: {create(path)} SYNTHETIC rows; no game was run')

"""Independent bounded contract probes, not tactical or engine benchmarks.

Usage: python3 probe_contract_gaps.py /absolute/path/to/phase2/package
The input package is only imported/read; results are printed to stdout.
"""
import json
from pathlib import Path
import sys
from dataclasses import replace

root = Path(sys.argv[1]).resolve()
sys.path.insert(0, str(root / 'prototype'))
from baseline import planner as p
from controller import Controller
from temporal import Task, Plan, CommanderLedger


def probe_failure_memory():
    actor = p.Formation('a', 0, 0, 10, (p.Group(100, 1, 1, .5),))
    enemy = p.Formation('e', 50, 0, 10, (p.Group(100, 1, 0, .5),))
    st = p.Snapshot((actor,), (enemy,), now=10)
    probe = p.action_for(actor, 'probe', 'e')
    ev = p.assessment(st, (probe,), p.Config())
    rt = p.Runtime(st.mission, st.domain)
    assert rt.adopt(p.Selection((probe,), ev, 'fixture', {}, {}), st)
    assert rt.report('a', 1, 1, rt.revision, 1, {'no_response': True}, st)
    informed = rt.feedback_snapshot(st)
    cfg = p.Config()
    before = [a.kind for a in p.options(informed, cfg)['a']]
    hold = p.action_for(actor, 'hold')
    held = p.assessment(informed, (hold,), cfg)
    assert rt.adopt(p.Selection((hold,), held, 'switch away', {}, {}), informed)
    after = rt.feedback_snapshot(replace(informed, now=11))
    after_opts = [a.kind for a in p.options(after, cfg)['a']]
    return dict(before_failed=sorted(informed.failed_probes),
                after_plan_change_failed=sorted(after.failed_probes),
                before_kinds=before, after_kinds=after_opts,
                same_epoch=after.epoch == informed.epoch,
                same_actor_authority=after.friendly == informed.friendly,
                memory_lost=('a', 'e', st.epoch) not in after.failed_probes,
                probe_reappeared='probe' not in before and 'probe' in after_opts)


def unit(uid, side=0, x=0, **kwargs):
    data = dict(id=uid, side=side, x=x, y=0., hp=100., max_hp=100., speed=5.,
                melee=10., ranged=0., ammo=40., armor=0., mounted=False,
                anti_mount=0., generation=1, authority=1, authorized=True,
                damage_dealt=0., suppression=0.)
    data.update(kwargs)
    return data


def probe_exit_resource():
    cav = unit('c', mounted=True)
    helper = unit('h', x=1)
    target = unit('t', side=1, x=20)
    guard = unit('g', side=1, x=21, anti_mount=1.)
    c = Controller(0, keeping=False, budget=80)
    c.exits['h'] = dict(actor='h', kind='retreat', x=-40., y=0., generation=1, authority=1)
    # Isolate the hard resource rule from all combat-model assumptions.
    def score(orders, obs):
        value = 100. if (orders['c']['kind'] == 'strike'
                         and orders['c'].get('target') == 't'
                         and orders['h']['kind'] == 'fix'
                         and orders['h'].get('target') == 'g') else 0.
        return value, [value] * 3
    c.forecast = score
    result = c.decide(dict(t=5., units=[cav, helper, target, guard], duties=[]))
    actual = next(o for o in result if o['actor'] == 'h')
    return dict(exit_still_recorded=c.exits.get('h'), issued_helper_order=actual,
                issued_orders=result,
                exit_resource_reassigned=actual['kind'] == 'fix',
                score_is_controlled_contract_probe=True)


def probe_release_task_revision():
    task = Task('move', 'a', 'move', start=0, end=3, revision=2)
    ledger = CommanderLedger(Plan((task,), revision=1))
    ledger.mark_dispatched('move')
    receipt = dict(source='execution_observation', complete=True, mission='research',
                   domain='domain', plan_revision=1, task='move', task_revision=1,
                   actor='a', generation=1, authority=1)
    accepted = ledger.acknowledge_release('move', receipt)
    return dict(current_task_revision=task.revision,
                receipt_task_revision=receipt['task_revision'],
                stale_task_revision_accepted=accepted,
                completed=sorted(ledger.completed))


if __name__ == '__main__':
    result = dict(scope='SYNTHETIC_CONTRACT_PROBES_ONLY',
        failure_memory=probe_failure_memory(),
        exit_resource=probe_exit_resource(),
        release_task_revision=probe_release_task_revision())
    output = json.dumps(result, ensure_ascii=False, indent=2)
    Path(__file__).with_name('contract_gap_results.json').write_text(output + '\n')
    print(output)

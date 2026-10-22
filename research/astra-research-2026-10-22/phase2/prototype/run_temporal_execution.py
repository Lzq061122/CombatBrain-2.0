"""Hand-authored temporal contract -> independent World execution demonstration.

This is not autonomous plan generation and makes no tactical superiority claim.
The adapter only maps observed effects, acknowledgements and current permissions;
it never reads private World state or execution variant coefficients.
"""
from __future__ import annotations
from copy import deepcopy
from dataclasses import asdict, replace
from pathlib import Path
import argparse
import json

from temporal import Task, JointCondition, Coverage, Plan, CommanderLedger, validate, observed_ready
from world import World


def scenario(revoke_guard=False):
    def unit(id, side, x, y, melee=0., ranged=0., **extra):
        return dict(id=id, side=side, x=x, y=y, hp=100., max_hp=100.,
                    speed=extra.pop('speed', 4.), melee=melee, ranged=ranged,
                    ammo=40., armor=extra.pop('armor', 3.), generation=1, authority=1,
                    authorized=True, **extra)
    units = [unit('fixer_a', 0, -18, -2, melee=4.),
             unit('fixer_b', 0, -18, 2, melee=4.),
             unit('striker', 0, -32, 18, melee=7., speed=6.),
             unit('gun', 0, -35, 40, ranged=6., speed=5.),
             unit('guard', 0, -40, -40),
             unit('home_asset', 0, -40, -45),
             unit('supporter', 1, 0, 0, melee=10., speed=6.),
             unit('strike_target', 1, 30, 18),
             unit('fire_target', 1, 35, 40)]
    return dict(units=units, bounds=[-100, 100, -80, 80],
                duties=[dict(asset='home_asset', owner='guard', side=0, coverage_radius=12.)],
                events=([dict(t=11., type='authority', actor='guard', authorized=False)]
                        if revoke_guard else []))


def manual_plan(mission="temporal_execution"):
    return Plan(tasks=(
        Task('fix_a', 'fixer_a', 'fix', target='supporter', start=0., end=24., strength=4.),
        Task('fix_b', 'fixer_b', 'fix', target='supporter', start=0., end=24., strength=4.),
        Task('strike', 'striker', 'strike', target='strike_target', start=6., end=20.),
        Task('move_gun', 'gun', 'move', start=0., end=6., x=-12., y=40.),
        Task('fire_gun', 'gun', 'fire', target='fire_target', start=6., end=22.,
             after=('move_gun',), x=-12., y=40.),
        Task('guard_duty', 'guard', 'protect', target='home_asset', start=0., end=24.,
             x=-40., y=-40.),
    ), conditions=(JointCondition('strike', 'supporter', ('fix_a', 'fix_b'), 6.5),),
       duties=(Coverage('home_asset', 'guard', 0., 24.),), revision=1,
       mission=mission, domain='side0_authorized_formations')


def live_units(obs):
    return {u['id']: u for u in obs['units']}


def command_permission(task, obs):
    """Use temporal.validate for each actual write's current actor identity.

    The command-scoped check does not claim to revalidate its task dependencies:
    those remain the ledger's responsibility before any release. No use of a
    projected snapshot or old authorization is permitted here.
    """
    check = replace(task, after=(), start=obs['t'], end=obs['t']+1.)
    return validate(Plan(tasks=(check,)), live_units(obs), travel=False)


def facts_from_effects(ledger, obs, battlefield):
    """Bind event -> task only with same actor, live identity, target and time."""
    units = live_units(obs)
    latest = {}
    for event in obs['events']:
        if event['type'] != 'fix_effect':
            continue
        latest[(event['actor'], event['target'])] = event
    facts = {}
    for task in ledger.plan.tasks:
        if task.kind != 'fix' or task.id in ledger.failed:
            continue
        event = latest.get((task.actor, task.target))
        actor, target = units.get(task.actor), units.get(task.target)
        if not event or not actor or not target:
            continue
        valid_identity = (actor['hp'] > 0 and actor['authorized'] and
                          event.get('generation') == actor['generation'] == task.generation and
                          event.get('authority') == actor['authority'] == task.authority and
                          event.get('target_generation') == target['generation'])
        currently_contacting = task.target in actor['contact_ids']
        fact = dict(source='execution_observation', battlefield=battlefield,
                    side=obs['side'], mission=ledger.plan.mission, domain=ledger.plan.domain,
                    actor=task.actor, target=task.target,
                    generation=event.get('generation'), authority=event.get('authority'),
                    target_generation=event.get('target_generation'),
                    plan_revision=ledger.plan.revision, task=task.id, task_revision=task.revision,
                    observed_at=event['t'],
                    valid_until=event['t']+.5,
                    effective=bool(valid_identity and currently_contacting and event['effective']),
                    strength=event['contribution'] if valid_identity and currently_contacting else 0.,
                    measured_suppression=event['suppression'])
        facts[task.id] = fact
    return facts


def order_for(task, delay=0.):
    kind = 'hold' if task.kind == 'protect' else task.kind
    result = dict(actor=task.actor, kind=kind, generation=task.generation,
                  authority=task.authority)
    if kind in ('fire', 'fix', 'strike', 'pressure'):
        result['target'] = task.target
    if kind in ('move', 'retreat'):
        result.update(x=task.x, y=task.y)
    if delay:
        result['delay'] = delay
    return result


def run_case(name, variant='nominal', second_delay=0., revoke_guard=False):
    initial = scenario(revoke_guard)
    original_plan = manual_plan(name)
    world = World(initial, variant)
    ledger = CommanderLedger(original_plan)
    initial_errors = validate(original_plan, live_units(world.observe(0)))
    if initial_errors:
        raise AssertionError(initial_errors)
    # An independent support mission is scheduled at t=8; the commander cannot
    # read its pending intent. It must infer the later actual suppressed movement.
    support_task = Task('enemy_support_move', 'supporter', 'move', start=0., end=24.,
                        x=40., y=0.)
    assert not command_permission(support_task, world.observe(1))
    support_receipt = world.issue(1, [order_for(support_task, delay=8.)])
    active, dispatched, trace, released = set(), {}, [], {}
    expiry_releases = []
    awaiting_stop_observation = {}
    release_acknowledgements = []
    cancellations, permission_checks, facts_examples = [], [], []
    first_ready, first_strike, duty_gap_times = None, None, []
    latest_good_facts = None
    for tick in range(25):
        obs = world.observe(0)
        units = live_units(obs)
        now = obs['t']
        # Accepted cancellation is not physical release. Wait for a subsequent
        # observed hold with zero actual velocity, then acknowledge the original
        # identity/revision (old revision if it was invalidated).
        for task_id, old in list(awaiting_stop_observation.items()):
            actor = units.get(old.actor)
            if (actor and actor['generation'] == old.generation and actor['authority'] == old.authority
                and actor.get('current_order', {}).get('kind') == 'hold'
                and actor.get('pending') is None and not actor['moving']
                and actor['phase'] in ('holding', 'engaged')):
                revision = (ledger.unreleased[task_id][1] if task_id in ledger.unreleased
                            else ledger.dispatch_revision.get(task_id, ledger.plan.revision))
                receipt = dict(source='execution_observation', complete=True,
                    mission=ledger.plan.mission, domain=ledger.plan.domain,
                    plan_revision=revision, task=task_id, actor=old.actor,
                    generation=old.generation, authority=old.authority,
                    observed_at=now, evidence='observed_hold_and_zero_velocity')
                accepted = ledger.acknowledge_release(task_id, receipt)
                release_acknowledgements.append(dict(t=now, receipt=receipt, accepted=accepted))
                if accepted:
                    del awaiting_stop_observation[task_id]
        # Validate the live ledger before repair; only the directly unavailable
        # tasks and dependent closure are invalidated, never unrelated commands.
        live_errors = validate(ledger.plan, units)
        bad = [t.id for t in ledger.plan.tasks if
               'unavailable:'+t.id in live_errors or 'identity:'+t.id in live_errors]
        if bad:
            affected = ledger.invalidate(bad)
            active.difference_update(affected)
            cancellations.append(dict(t=now, cause='current_identity_or_permission',
                                      directly_failed=bad, affected=affected))
        for event in obs['events']:
            if event['type'] != 'order_completed':
                continue
            for task in ledger.plan.tasks:
                if task.id not in active or task.kind != 'move' or task.actor != event['actor']:
                    continue
                if (event.get('generation') == task.generation and
                    event.get('authority') == task.authority and
                    event.get('reason') == 'arrived' and
                    not command_permission(task, obs)):
                    receipt = dict(source='execution_observation', complete=True,
                        mission=ledger.plan.mission, domain=ledger.plan.domain,
                        plan_revision=ledger.dispatch_revision.get(task.id, ledger.plan.revision),
                        task=task.id, actor=task.actor,
                        generation=event['generation'], authority=event['authority'],
                        observed_at=event['t'], evidence=deepcopy(event))
                    accepted = ledger.acknowledge_release(task.id, receipt)
                    release_acknowledgements.append(dict(t=now, receipt=receipt, accepted=accepted))
                    if accepted:
                        active.remove(task.id)
                        released[task.id] = dict(t=now, evidence=deepcopy(event), receipt=receipt)
        facts = facts_from_effects(ledger, obs, name)
        ready = ('strike' in {t.id for t in ledger.plan.tasks} and
                 observed_ready(ledger.plan, 'strike', facts, now, max_age=.5))
        if ready:
            first_ready = now if first_ready is None else first_ready
            latest_good_facts = deepcopy(facts)
            if not facts_examples:
                facts_examples.append(dict(t=now, facts=deepcopy(facts)))
        # TTL means freshness only. Check active dependencies each new observation;
        # observed success in an earlier tick is never a future-window guarantee.
        if 'strike' in active and not ready:
            old = next(t for t in ledger.plan.tasks if t.id == 'strike')
            affected = ledger.invalidate(['strike'])
            active.difference_update(affected)
            cancellations.append(dict(t=now, cause='observed_effect_lost',
                                      directly_failed=['strike'], affected=affected))
            checks = command_permission(old, obs)
            permission_checks.append(dict(t=now, task='strike_stop', errors=checks))
            if not checks:
                stop_order = dict(actor=old.actor, kind='hold', generation=old.generation, authority=old.authority)
                cancellations[-1]['stop_order'] = stop_order
                cancellations[-1]['stop_receipts'] = world.issue(0, [stop_order])
                awaiting_stop_observation[old.id] = old
        # Expires at the contractual end, with an explicit hold release. A task
        # whose actor lost permission is already removed; no stale write occurs.
        for task in list(ledger.plan.tasks):
            if task.id not in active or now < task.end:
                continue
            checks = command_permission(task, obs)
            permission_checks.append(dict(t=now, task=task.id+'_end', errors=checks))
            if not checks:
                stop_order = dict(actor=task.actor, kind='hold', generation=task.generation, authority=task.authority)
                expiry_releases.append(dict(t=now, task=task.id, order=stop_order,
                                             receipts=world.issue(0, [stop_order])))
                awaiting_stop_observation[task.id] = task
            active.remove(task.id)
        for task in ledger.releasable(now, facts):
            if task.id in active or task.id in dispatched:
                continue
            checks = command_permission(task, obs)
            permission_checks.append(dict(t=now, task=task.id, errors=checks))
            if checks:
                continue
            receipts = world.issue(0, [order_for(task, second_delay if task.id == 'fix_b' else 0.)])
            if receipts[0]['type'] in ('order_accepted', 'order_unchanged'):
                dispatched[task.id] = dict(t=now, receipts=receipts,
                                          prerequisite_facts=deepcopy(facts) if task.id == 'strike' else {})
                active.add(task.id)
                ledger.mark_dispatched(task.id)
                if task.id == 'strike':
                    first_strike = now
        post_errors = validate(ledger.plan, live_units(world.observe(0)))
        if any(e.startswith('duty_gap:') for e in post_errors):
            duty_gap_times.append(now)
        compact_units = [{k: u[k] for k in ('id', 'side', 'x', 'y', 'hp', 'ammo',
                            'generation', 'authority', 'authorized', 'moving', 'vx', 'vy',
                            'phase', 'suppression', 'contact_ids', 'damage_dealt', 'damage_received')}
                         for u in obs['units']]
        record = dict(t=now, plan_revision=ledger.plan.revision,
                      current_task_ids=[t.id for t in ledger.plan.tasks],
                      active=sorted(active), completed=sorted(ledger.completed),
                      failed=sorted(ledger.failed), unreleased=sorted(ledger.unreleased), strike_ready=ready,
                      facts=facts, validation_errors=post_errors,
                      units=compact_units, metrics=world.metrics(0))
        if tick < 24:
            record['step_events'] = world.step(1.)
        trace.append(record)
    # Negative facts use a time at which true complete facts were sufficient.
    negatives = {'forecast_without_observation_blocked': not observed_ready(original_plan, 'strike', {}, 6.)}
    if latest_good_facts:
        true_now = max(f['observed_at'] for f in latest_good_facts.values())+.25
        wrong_actor = deepcopy(latest_good_facts)
        wrong_actor['fix_b']['actor'] = 'fixer_a'
        stale = deepcopy(latest_good_facts)
        for f in stale.values():
            f['valid_until'] = true_now-.01
        wrong_generation = deepcopy(latest_good_facts)
        wrong_generation['fix_b']['generation'] += 1
        wrong_mission = deepcopy(latest_good_facts)
        wrong_mission['fix_b']['mission'] = 'other_mission'
        wrong_domain = deepcopy(latest_good_facts)
        wrong_domain['fix_b']['domain'] = 'other_control_domain'
        wrong_task = deepcopy(latest_good_facts)
        wrong_task['fix_b']['task'] = 'fix_a'
        wrong_task_revision = deepcopy(latest_good_facts)
        wrong_task_revision['fix_b']['task_revision'] += 1
        negatives.update(
            single_contributor_blocked=not observed_ready(original_plan, 'strike',
                                                          {'fix_a': latest_good_facts['fix_a']}, true_now),
            wrong_actor_blocked=not observed_ready(original_plan, 'strike', wrong_actor, true_now),
            expired_fact_blocked=not observed_ready(original_plan, 'strike', stale, true_now),
            wrong_generation_blocked=not observed_ready(original_plan, 'strike', wrong_generation, true_now),
            wrong_mission_blocked=not observed_ready(original_plan, 'strike', wrong_mission, true_now),
            wrong_domain_blocked=not observed_ready(original_plan, 'strike', wrong_domain, true_now),
            wrong_task_blocked=not observed_ready(original_plan, 'strike', wrong_task, true_now),
            wrong_task_revision_blocked=not observed_ready(original_plan, 'strike', wrong_task_revision, true_now))
    move_release = released.get('move_gun', {}).get('t')
    gun_fire = dispatched.get('fire_gun', {}).get('t')
    fire_damage = [e for r in trace for e in r.get('step_events', [])
                   if e['type']=='damage' and e['actor']=='gun' and e['channel']=='ranged']
    suppression = [e for r in trace for e in r.get('step_events', [])
                   if e['type']=='fix_effect' and e['effective']]
    movement_when_fixed = [r for r in trace if any(u['id']=='supporter' and
                           u['moving'] and u['suppression'] > 0 for u in r['units'])]
    preserved = {t.id for t in ledger.plan.tasks}
    checks = dict(initial_plan_legal=not initial_errors,
                  strike_waited_for_real_joint_effect=first_strike is not None and first_strike >= first_ready,
                  each_fixer_individually_below_6_5=all(f['strength'] < 6.5 for ex in facts_examples for f in ex['facts'].values()),
                  move_release_before_fire=move_release is not None and gun_fire is not None and move_release <= gun_fire,
                  actual_fire_after_move=bool(fire_damage) and min(e['t'] for e in fire_damage) >= move_release,
                  actual_joint_suppression=bool(suppression),
                  every_write_permission_checked=all(not x['errors'] for x in permission_checks),
                  complete_identity_release_receipts_accepted=all(x['accepted'] for x in release_acknowledgements),
                  negative_observations_rejected=all(negatives.values()),
                  unrelated_fire_preserved='fire_gun' in preserved)
    if variant == 'early_fix':
        checks.update(loss_cancels_consumer_only=any(c['cause']=='observed_effect_lost' and
                          c['affected']==['strike'] for c in cancellations),
                      duty_gap_explicit=bool(duty_gap_times),
                      duty_gap_has_actual_loss=world.metrics(0)['duty_coverage'] < 1.,
                      unrelated_fix_task_preserved='fix_a' in preserved and 'fix_b' in preserved,
                      unrelated_fire_continues_after_failure=any(e['t'] > 11. for e in fire_damage),
                      cancelled_strike_waits_for_actual_release=any(r['t']==10. and 'strike' in r['unreleased'] for r in trace)
                          and any(a['receipt']['task']=='strike' and a['t']==11. and a['accepted'] for a in release_acknowledgements),
                      revoked_guard_not_silently_released='guard_duty' in ledger.unreleased)
    else:
        checks['support_actual_movement_suppressed'] = bool(movement_when_fixed)
    return dict(name=name, evidence_class='hand_authored_mechanism_contract_execution',
                autonomous_plan_generation=False, tactical_benefit_claim=False,
                input=dict(scenario=initial, plan=asdict(original_plan), variant=variant,
                           second_fixer_delivery_delay=second_delay, enemy_support_order=order_for(support_task, delay=8.)),
                first_observed_joint_ready=first_ready, strike_dispatch=first_strike,
                move_release=move_release, fire_dispatch=gun_fire,
                support_receipt=support_receipt, dispatched=dispatched, releases=released,
                cancellations=cancellations, expiry_releases=expiry_releases, duty_gap_times=duty_gap_times,
                negative_checks=negatives, checks=checks, permission_checks=permission_checks,
                release_acknowledgements=release_acknowledgements, final_unreleased=sorted(ledger.unreleased),
                final_awaiting_stop_observation=sorted(awaiting_stop_observation),
                trace=trace, final_metrics=dict(side0=world.metrics(0), side1=world.metrics(1)))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path,
        default=Path(__file__).resolve().parents[1]/'results'/'temporal_execution.json')
    args = parser.parse_args()
    cases = [run_case('nominal_on_time'),
             run_case('nominal_second_fixer_late', second_delay=4.),
             run_case('early_fix_and_guard_revoke', variant='early_fix', revoke_guard=True)]
    contrast = dict(late_fixer_delays_observed_ready=cases[1]['first_observed_joint_ready'] > cases[0]['first_observed_joint_ready'],
                    late_fixer_delays_strike_release=cases[1]['strike_dispatch'] > cases[0]['strike_dispatch'])
    output = dict(schema_version=1, evidence_class='hand_authored_mechanism_contract_execution',
                  limitations=['No autonomous plan generation or tactical superiority claim.',
                    'Observed fact TTL is freshness, not a guarantee of future effect duration.',
                    'Every active dependency is rechecked once per second; detection latency is at most one external tick.',
                    'Protect tasks map to a prepared hold at an already covered asset; no automatic replacement guard search.',
                    'Duty gaps remain explicit after partial repair; unaffected authorized tasks continue.',
                    'Two deterministic timing cases are a controlled lateness contrast, not statistical seeds.'],
                  cases=cases, contrast_checks=contrast)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, ensure_ascii=False, indent=2)+'\n')
    failures = [name+':'+key for name, checks in [(c['name'], c['checks']) for c in cases]+[('contrast', contrast)]
                for key, passed in checks.items() if not passed]
    print(json.dumps(dict(output=str(args.output), cases=[dict(name=c['name'],
          ready=c['first_observed_joint_ready'], strike=c['strike_dispatch'],
          move_release=c['move_release'], fire=c['fire_dispatch'], cancellations=c['cancellations'],
          duty_coverage=c['final_metrics']['side0']['duty_coverage']) for c in cases],
          checks_passed=not failures, failures=failures), ensure_ascii=False, indent=2))
    if failures:
        raise SystemExit(1)


if __name__ == '__main__':
    main()

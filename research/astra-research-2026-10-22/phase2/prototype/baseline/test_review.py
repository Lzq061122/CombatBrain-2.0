"""Independent counterexamples for the research prototype, not Bannerlord tests.
Assertions describe required contracts; initial failures are preserved as evidence.
"""
import unittest
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from dataclasses import replace
from planner import (Group, Formation, Duty, Snapshot, Config, Selection, Runtime,
                     action_for, options, assessment, choose)


def unit(uid, x=0, y=0, n=100, melee=1.0, ranged=0.0, armour=.5,
         anti_mount=0.0, mounted=False, speed=10, **kw):
    return Formation(uid, x, y, speed,
                     (Group(n, melee, ranged, armour, anti_mount, mounted),), **kw)


def selection(st, acts, cfg=Config()):
    ev = assessment(st, acts, cfg)
    assert ev.feasible, ev.reasons
    return Selection(tuple(acts), ev, 'review_fixture', {}, {})


class ReviewCounterexamples(unittest.TestCase):
    def setUp(self):
        self.a = unit('a')
        self.e = unit('enemy', x=60)
        self.st = Snapshot((self.a,), (self.e,), mission='mission-A', domain='domain-A')

    def test_old_mission_report_rejected(self):
        rt = Runtime('mission-A', 'domain-A')
        rt.adopt(selection(self.st, (action_for(self.a, 'hold'),)), self.st)
        other = replace(self.st, mission='mission-B')
        self.assertFalse(rt.report('a', 1, 1, rt.revision, 1, {'effective': True}, other))

    def test_other_domain_report_rejected(self):
        rt = Runtime('mission-A', 'domain-A')
        other = replace(self.st, domain='domain-B')
        self.assertFalse(rt.report('a', 1, 1, rt.revision, 1, {'effective': True}, other))

    def test_adopt_revalidates_authority(self):
        sel = selection(self.st, (action_for(self.a, 'strike', 'enemy'),))
        changed = replace(self.st, friendly=(replace(self.a, authorized=False),))
        rt = Runtime('mission-A', 'domain-A')
        self.assertFalse(rt.adopt(sel, changed))

    def test_adopt_revalidates_generation(self):
        sel = selection(self.st, (action_for(self.a, 'strike', 'enemy'),))
        changed = replace(self.st, friendly=(replace(self.a, generation=2),))
        rt = Runtime('mission-A', 'domain-A')
        self.assertFalse(rt.adopt(sel, changed))

    def test_mandatory_duty_option_survives_optional_cap(self):
        asset = unit('asset', x=2)
        st = replace(self.st, friendly=(self.a, asset), duties=(Duty('asset', 'a'),))
        generated = options(st, Config(options=1))
        self.assertIn(action_for(self.a, 'protect', 'asset'), generated['a'])

    def test_effect_waits_for_arrival_and_setup(self):
        a = action_for(self.a, 'strike', 'enemy')
        sel = selection(self.st, (a,))
        self.assertGreater(sel.assessment.starts['a'], 0)
        rt = Runtime('mission-A', 'domain-A')
        self.assertTrue(rt.adopt(sel, self.st))
        self.assertNotEqual(rt.write(a, rt.revision, self.st), 'accepted_interface_only')

    def test_old_actor_generation_sequence_does_not_poison_new_reports(self):
        rt = Runtime('mission-A', 'domain-A')
        rt.adopt(selection(self.st, (action_for(self.a, 'hold'),)), self.st)
        self.assertTrue(rt.report('a', 1, 1, rt.revision, 100, {'effective': False}, self.st))
        anew = replace(self.a, generation=2)
        next_st = replace(self.st, friendly=(anew,))
        self.assertTrue(rt.adopt(selection(next_st, (action_for(anew, 'hold'),)), next_st))
        self.assertTrue(rt.report('a', 2, 1, rt.revision, 1, {'effective': True}, next_st))

    def test_safety_ignores_other_mission(self):
        rt = Runtime('mission-A', 'domain-A')
        rt.adopt(selection(self.st, (action_for(self.a, 'hold'),)), self.st)
        other = replace(self.st, mission='mission-B')
        result = rt.safety('a', other, True, False, True, False)
        self.assertNotEqual(result, 'minimal_exception_report')

    def test_late_fix_does_not_suppress_reinforcement_before_arrival(self):
        cfg = Config(uncertainty=False, scenarios=('reinforce',))
        shooter = unit('shooter', ranged=1, melee=0, armour=1)
        fixer = unit('fixer', x=60, n=40, melee=1, armour=1)
        target = unit('target', x=50, melee=.1, ranged=1, armour=.5)
        support = unit('support', x=60, melee=1, armour=.5)
        acts = (action_for(shooter, 'shoot', 'target'), action_for(fixer, 'fix', 'support'))
        early = Snapshot((shooter, fixer), (target, support), horizon=30)
        late = replace(early, friendly=(shooter, replace(fixer, x=-180)))
        def shooter_damage(st):
            ev = assessment(st, acts, cfg)
            self.assertTrue(ev.feasible, ev.reasons)
            # Two disjoint targets stay below saturation. Remove the fixer's direct
            # capacity damage to isolate the shooter's benefit from blocking support.
            f = st.own()['fixer']
            direct_fix = f.cap('melee') * .35 * (st.horizon-ev.starts['fixer'])/st.horizon
            direct_fix /= 1 + support.cap('armour') / support.size
            return ev.details['reinforce']['enemy_capacity_removed'] - direct_fix
        self.assertGreater(shooter_damage(early), shooter_damage(late) + 1e-6)

    def test_stationary_far_anti_mount_is_not_immediate_support(self):
        cavalry = unit('cavalry', mounted=True)
        target = unit('target', x=50, melee=.1, ranged=1)
        guard = unit('guard', x=110, anti_mount=1, speed=0)
        st = Snapshot((cavalry,), (target, guard))
        ev = assessment(st, (action_for(cavalry, 'strike', 'target'),), Config())
        self.assertTrue(ev.feasible, ev.reasons)

    def test_same_action_readoption_refreshes_delayed_arrival(self):
        a = action_for(self.a, 'strike', 'enemy')
        rt = Runtime('mission-A', 'domain-A')
        rt.adopt(selection(self.st, (a,)), self.st)
        # At the old arrival time the same target has moved 120 m farther away.
        delayed = replace(self.st, enemy=(replace(self.e, x=180),), now=8)
        self.assertTrue(rt.adopt(selection(delayed, (a,)), delayed))
        self.assertNotEqual(rt.write(a, rt.revision, delayed), 'accepted_interface_only')

    def test_valid_no_response_feedback_prevents_repeat_probe(self):
        probe = action_for(self.a, 'probe', 'enemy')
        rt = Runtime('mission-A', 'domain-A')
        rt.adopt(selection(self.st, (probe,)), self.st)
        self.assertTrue(rt.report('a', 1, 1, rt.revision, 1, {'no_response': True}, self.st))
        informed = rt.feedback_snapshot(self.st)
        self.assertNotIn(probe, options(informed, Config())['a'])
        self.assertFalse(assessment(informed, (probe,), Config()).feasible)

    def test_stale_report_cannot_block_current_route(self):
        a = action_for(self.a, 'strike', 'enemy')
        rt = Runtime('mission-A', 'domain-A')
        rt.adopt(selection(self.st, (a,)), self.st)
        self.assertFalse(rt.report('a', 1, 1, rt.revision-1, 1, {'route_blocked': True}, self.st))
        self.assertNotIn(('a', 'enemy'), rt.feedback_snapshot(self.st).blocked)

    def test_one_enemy_fire_budget_is_shared_across_attackers(self):
        shooters = tuple(unit('s'+str(i), ranged=1, melee=0, armour=0) for i in range(4))
        enemy = unit('e', x=50, n=100, ranged=1, melee=0, armour=0)
        st = Snapshot(shooters, (enemy,))
        aa = tuple(action_for(f, 'shoot', 'e') for f in shooters)
        ev = assessment(st, aa, Config(scenarios=('hold',), uncertainty=False))
        self.assertTrue(ev.feasible)
        # In this explicit abstract model one enemy has 100 ranged capacity and
        # .45 outgoing-loss coefficient per horizon, not .45 per recipient.
        self.assertLessEqual(ev.details['hold']['friendly_capacity_lost'], 45 + 1e-6)

    def test_effect_releases_after_time_and_observed_readiness(self):
        a = action_for(self.a, 'strike', 'enemy')
        sel = selection(self.st, (a,))
        rt = Runtime('mission-A', 'domain-A')
        rt.adopt(sel, self.st)
        later = replace(self.st, now=sel.assessment.starts['a'])
        self.assertEqual(rt.write(a, rt.revision, later, ready=frozenset({'a'})), 'accepted_interface_only')

    def test_reserve_cover_budget_not_reused_for_two_assets(self):
        asset1 = unit('asset1', y=-10, melee=0, armour=1)
        asset2 = unit('asset2', y=10, melee=0, armour=1)
        guard1 = unit('guard1', y=-10, n=1, melee=0, armour=1)
        guard2 = unit('guard2', y=10, n=1, melee=0, armour=1)
        reserve = unit('reserve', n=100, melee=1, armour=0)
        e1 = unit('e1', x=50, y=-10, melee=0, ranged=1, armour=0)
        e2 = unit('e2', x=50, y=10, melee=0, ranged=1, armour=0)
        duties = (Duty('asset1', 'guard1'), Duty('asset2', 'guard2'))
        st = Snapshot((asset1, asset2, guard1, guard2, reserve), (e1, e2), duties,
                      blocked=frozenset({('e1', 'asset2'), ('e2', 'asset1')}))
        acts = tuple(action_for(f, 'hold') for f in (asset1, asset2, reserve)) + (
            action_for(guard1, 'protect', 'asset1'), action_for(guard2, 'protect', 'asset2'))
        cfg = Config(scenarios=('counter',), uncertainty=False)
        with_reserve = assessment(st, acts, cfg)
        no_reserve = replace(st, friendly=(asset1, asset2, guard1, guard2))
        without = assessment(no_reserve, tuple(a for a in acts if a.actor != 'reserve'), cfg)
        self.assertTrue(with_reserve.feasible)
        self.assertTrue(without.feasible)
        gain = without.details['counter']['friendly_capacity_lost'] - with_reserve.details['counter']['friendly_capacity_lost']
        self.assertLessEqual(gain, reserve.cap('melee') * .5 * .3 + 1e-6)

    def test_duplicate_resource_is_infeasible(self):
        aa = (action_for(self.a, 'hold'), action_for(self.a, 'strike', 'enemy'))
        ev = assessment(self.st, aa, Config())
        self.assertFalse(ev.feasible)
        self.assertIn('double_resource', ev.reasons)

    def test_expired_current_authority_not_retained_by_hysteresis(self):
        current = (action_for(self.a, 'strike', 'enemy'),)
        changed = replace(self.st, friendly=(replace(self.a, generation=2),), major_change=False)
        pick = choose(changed, Config(), current=current)
        self.assertTrue(pick.assessment.feasible)
        self.assertTrue(all(a.generation == 2 for a in pick.actions))
        self.assertEqual(pick.reason, 'hard_invalid_repair')

    def test_cancel_preserves_critical_duty(self):
        asset = unit('asset', x=2)
        st = replace(self.st, friendly=(self.a, asset), duties=(Duty('asset', 'a', sacrifice=True),))
        rt = Runtime('mission-A', 'domain-A')
        acts = (action_for(self.a, 'protect', 'asset'), action_for(asset, 'hold'))
        rt.adopt(selection(st, acts), st)
        rt.cancel_tasks()
        self.assertEqual(rt.duties, st.duties)
        self.assertEqual(rt.safety('a', st, True, False, True, True), 'hold_if_feasible_report_blocked')

    def test_missing_dependency_does_not_pass_hard_feasibility(self):
        cav = unit('cavalry', x=0, mounted=True)
        target = unit('ranged', x=50, melee=.1, ranged=1)
        guard = unit('guard', x=55, anti_mount=1)
        st = Snapshot((cav,), (target, guard))
        ev = assessment(st, (action_for(cav, 'strike', 'ranged'),), Config())
        self.assertFalse(ev.feasible)
        self.assertIn('unfixed_support:cavalry:guard', ev.reasons)


if __name__ == '__main__':
    unittest.main(verbosity=2)

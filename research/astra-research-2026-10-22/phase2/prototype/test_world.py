"""Structural assertions about execution, never tactical superiority claims."""
import json
import unittest
from copy import deepcopy
from world import World, reactive_orders


def unit(id, side, x, y=0, **kw):
    return dict(id=id, side=side, x=x, y=y, hp=kw.pop('hp', 100),
                speed=kw.pop('speed', 4), melee=kw.pop('melee', 5),
                ranged=kw.pop('ranged', 0), **kw)


def by_id(world, id, side=0):
    return next(u for u in world.observe(side)['units'] if u['id'] == id)


class WorldContractTests(unittest.TestCase):
    def test_real_motion_rate_and_repeat_order_does_not_teleport(self):
        w = World({'units': [unit('a', 0, -50), unit('b', 1, 90)]})
        w.issue(0, [dict(actor='a', kind='move', x=0, y=0)])
        w.step(2)
        self.assertAlmostEqual(by_id(w, 'a')['x'], -42)
        self.assertTrue(by_id(w, 'a')['moving'])
        self.assertAlmostEqual(w.metrics(0)['distance_travelled'], 8)

    def test_setup_and_range_are_physical(self):
        w = World({'units': [unit('a', 0, 0, ranged=6, melee=0),
                            unit('b', 1, 40, melee=0)]})
        w.issue(0, [dict(actor='a', kind='fire', target='b')])
        w.step(.5)
        self.assertEqual(by_id(w, 'b')['hp'], 100)
        self.assertEqual(w.issue(0, [dict(actor='a', kind='fire', target='b')])[0]['type'], 'order_unchanged')
        w.step(.5)
        self.assertEqual(by_id(w, 'b')['hp'], 100)
        w.step(.5)
        self.assertLess(by_id(w, 'b')['hp'], 100)
        distant = World({'units': [unit('a', 0, -90, ranged=6, melee=0, speed=0),
                                  unit('b', 1, 90, melee=0)]})
        distant.issue(0, [dict(actor='a', kind='fire', target='b')])
        distant.step(10)
        self.assertEqual(distant.metrics(0)['damage_dealt'], 0)

    def test_ammo_and_output_are_conserved(self):
        w = World({'units': [unit('a', 0, 0, ranged=8, melee=0, ammo=.5),
                            unit('b', 1, 40, melee=0)]})
        w.step(4)  # Initially prepared hold fires at a visible in-range enemy.
        self.assertAlmostEqual(w.metrics(0)['ammo_used'], .5)
        self.assertAlmostEqual(by_id(w, 'a')['ammo'], 0.)
        self.assertAlmostEqual(w.metrics(0)['damage_dealt'], 8*.5*.9)
        self.assertTrue(all(u['ammo'] >= 0 for u in w.observe(0)['units']))

    def test_one_melee_budget_shared_across_targets(self):
        w = World({'units': [unit('a', 0, 0, melee=10),
                            unit('b', 1, 3, melee=0), unit('c', 1, -3, melee=0)]})
        events = w.step(1)
        self.assertAlmostEqual(w.metrics(0)['damage_dealt'], 9)
        self.assertAlmostEqual(by_id(w, 'b')['damage_received'], 4.5)
        self.assertAlmostEqual(by_id(w, 'c')['damage_received'], 4.5)
        self.assertAlmostEqual(sum(e['allocated_output'] for e in events
                                   if e['type'] == 'damage' and e['actor'] == 'a'), 10)

    def test_melee_and_ranged_cannot_both_spend_full_budget(self):
        w = World({'units': [unit('a', 0, 0, melee=10, ranged=10),
                            unit('b', 1, 3, melee=0), unit('c', 1, 30, melee=0)]})
        w.issue(0, [dict(actor='a', kind='fire', target='c')])
        w.step(1.25)
        self.assertEqual(by_id(w, 'c')['damage_received'], 0)
        self.assertEqual(w.metrics(0)['ammo_used'], 0)
        self.assertLessEqual(w.metrics(0)['damage_dealt'], 10*1.25*.9)

    def test_simultaneous_kill_and_order_independence(self):
        raw = [unit('a', 0, 0, melee=1000, hp=1), unit('b', 1, 1, melee=1000, hp=1)]
        one, two = World({'units': raw}), World({'units': list(reversed(raw))})
        one.step(.25)
        two.step(.25)
        for w in (one, two):
            self.assertEqual(by_id(w, 'a')['hp'], 0)
            self.assertEqual(by_id(w, 'b')['hp'], 0)
            self.assertAlmostEqual(w.metrics(0)['damage_dealt'], 1)
            self.assertAlmostEqual(w.metrics(1)['damage_dealt'], 1)
            w.step(1)
            self.assertEqual(w.metrics(0)['damage_dealt'], 1)

    def test_delayed_order_revalidation_on_authority_change(self):
        w = World({'units': [unit('a', 0, 0)], 'events': [
            dict(t=1, type='authority', actor='a', authorized=False),
            dict(t=2, type='authority', actor='a', authorized=True)]})
        w.issue(0, [dict(actor='a', kind='move', x=100, y=0, delay=3)])
        events = w.step(4)
        self.assertEqual(by_id(w, 'a')['x'], 0)
        self.assertEqual(by_id(w, 'a')['authority'], 2)
        self.assertTrue(any(e['type']=='order_rejected' for e in events))
        result = w.issue(0, [dict(actor='a', kind='move', x=10, y=0, authority=0)])
        self.assertEqual(result[0]['reason'], 'stale_authority')

    def test_revoked_actor_cannot_supply_fix_or_damage(self):
        w = World({'units': [unit('a', 0, 0, authorized=False), unit('b', 1, 3, melee=0)]})
        self.assertEqual(w.issue(0, [dict(actor='a', kind='fix', target='b')])[0]['reason'], 'unauthorized')
        w.step(3)
        self.assertEqual(w.metrics(0)['damage_dealt'], 0)
        self.assertEqual(by_id(w, 'b')['suppression'], 0)

    def test_generation_reuse_invalidates_delayed_commands(self):
        w = World({'units': [unit('a', 0, 0)], 'events': [dict(t=1, type='replace',
            actor='a', unit=unit('ignored', 0, 0))]})
        w.issue(0, [dict(actor='a', kind='move', x=100, y=0, delay=2)])
        w.step(3)
        self.assertEqual(by_id(w, 'a')['x'], 0)
        self.assertEqual(by_id(w, 'a')['generation'], 1)
        result = w.issue(0, [dict(actor='a', kind='move', x=10, y=0, generation=0)])
        self.assertEqual(result[0]['reason'], 'stale_generation')

    def test_observation_does_not_leak_enemy_intent_future_or_coefficients(self):
        raw = {'units': [unit('a', 0, -90), unit('b', 1, 90)], 'events': [
            dict(t=20, type='reinforcement', unit=unit('hidden', 1, 80))], 'private_rule': 'secret'}
        w = World(raw, 'weak_fix')
        w.issue(1, [dict(actor='b', kind='move', x=0, y=0, delay=8)])
        obs = w.observe(0)
        text = json.dumps(obs)
        self.assertNotIn('hidden', text)
        self.assertNotIn('fix_threshold', text)
        self.assertNotIn('weak_fix', text)
        self.assertNotIn('secret', text)
        enemy = next(u for u in obs['units'] if u['side'] == 1)
        self.assertNotIn('current_order', enemy)
        self.assertNotIn('pending', enemy)
        self.assertFalse(any(e['type'].startswith('order_') for e in obs['events']))
        obs['units'][0]['hp'] = 0
        self.assertEqual(by_id(w, 'a')['hp'], 100)
        self.assertEqual(raw['units'][0]['x'], -90)

    def test_fix_needs_actual_contact_readiness_and_sufficient_total(self):
        raw = [unit('a', 0, 0, melee=4), unit('c', 0, 0, y=2, melee=4),
               unit('b', 1, 5, melee=10, speed=0)]
        w = World({'units': raw})
        w.issue(0, [dict(actor='a', kind='fix', target='b')])
        self.assertFalse(any(e['type']=='fix_effect' for e in w.step(.5)))
        w.step(.75)
        self.assertEqual(by_id(w, 'b')['suppression'], 0)
        w.issue(0, [dict(actor='c', kind='fix', target='b')])
        events = w.step(1.5)
        self.assertGreater(by_id(w, 'b')['suppression'], 0)
        self.assertTrue(any(e['type']=='fix_effect' and e['effective'] for e in events))
        self.assertTrue(all('required_strength' not in e for e in events if e['type']=='fix_effect'))
        far = World({'units': [unit('a', 0, 0, speed=0), unit('b', 1, 40)]})
        far.issue(0, [dict(actor='a', kind='fix', target='b')])
        self.assertFalse(any(e['type']=='fix_effect' for e in far.step(5)))

    def test_fix_changes_actual_motion_and_has_damage_cost(self):
        raw = [unit('a', 0, 0, melee=10, speed=0, armor=10),
               unit('b', 1, 4, melee=5, speed=4, armor=10)]
        w = World({'units': raw})
        w.issue(0, [dict(actor='a', kind='fix', target='b')])
        w.step(1.25)
        self.assertGreater(w.metrics(0)['damage_received'], 0)
        self.assertGreater(w.metrics(0)['damage_dealt'], 0)
        w.issue(1, [dict(actor='b', kind='move', x=80, y=0)])
        w.step(2.5)
        self.assertLess(by_id(w, 'b')['x'], 6.)
        self.assertGreater(w.metrics(1)['suppression_seconds'], 0)

    def test_weak_and_early_fix_are_independent_execution_errors(self):
        raw = [unit('a', 0, 0, melee=5, speed=0, armor=100),
               unit('b', 1, 4, melee=5, speed=0, armor=100)]
        nominal, weak, early = [World({'units': raw}, v) for v in ('nominal', 'weak_fix', 'early_fix')]
        for w in (nominal, weak, early):
            w.issue(0, [dict(actor='a', kind='fix', target='b')])
            w.step(7)
        self.assertGreater(by_id(nominal, 'b')['suppression'], 0)
        self.assertEqual(by_id(weak, 'b')['suppression'], 0)
        self.assertEqual(by_id(early, 'b')['suppression'], 0)
        early.issue(0, [dict(actor='a', kind='hold')])
        early.step(.25)
        early.issue(0, [dict(actor='a', kind='fix', target='b')])
        early.step(1.5)
        self.assertEqual(by_id(early, 'b')['suppression'], 0)

    def test_exit_hysteresis_is_actual_latency(self):
        raw = [unit('a', 0, 0, melee=0, speed=4), unit('b', 1, 4, melee=0)]
        a, b = World({'units': raw}), World({'units': raw}, 'sticky_exit')
        for w in (a, b):
            w.issue(0, [dict(actor='a', kind='retreat', x=-80, y=0)])
            w.step(3)
        self.assertLess(by_id(a, 'a')['x'], 0)
        self.assertEqual(by_id(b, 'a')['x'], 0)

    def test_correlated_speed_variant_applies_to_both_sides(self):
        w = World({'units': [unit('a', 0, -80), unit('b', 1, 80)]}, 'slow_shared')
        w.issue(0, [dict(actor='a', kind='move', x=-40, y=0)])
        w.issue(1, [dict(actor='b', kind='move', x=40, y=0)])
        w.step(2)
        self.assertAlmostEqual(w.metrics(0)['distance_travelled'], 4*.72*2)
        self.assertAlmostEqual(w.metrics(1)['distance_travelled'], 4*.72*2)

    def test_obstacle_prevents_direct_route(self):
        w = World({'units': [unit('a', 0, -10, speed=8)], 'obstacles': [[-2, 2, -3, 3]]})
        w.issue(0, [dict(actor='a', kind='move', x=10, y=0)])
        events = w.step(4)
        self.assertLess(by_id(w, 'a')['x'], -2)
        self.assertTrue(any(e['type']=='route_blocked' for e in events))
        self.assertEqual(w.metrics(0)['route_blocks'], 1)

    def test_duty_samples_existing_alive_authorized_owner(self):
        w = World({'units': [unit('guard', 0, 0), unit('asset', 0, 10)],
            'duties': [dict(owner='guard', asset='asset', coverage_radius=12)],
            'events': [dict(t=1, type='authority', actor='guard', authorized=False)]})
        w.step(2)
        self.assertAlmostEqual(w.metrics(0)['duty_covered_time'], 1)
        self.assertAlmostEqual(w.metrics(0)['duty_required_time'], 2)
        self.assertAlmostEqual(w.metrics(0)['duty_coverage'], .5)

    def test_reinforcement_is_unavailable_until_arrival(self):
        w = World({'units': [unit('a', 0, 0)], 'events': [dict(t=2,
            type='reinforcement', unit=unit('b', 0, 20))]})
        self.assertEqual(w.issue(0, [dict(actor='b', kind='hold')])[0]['reason'], 'unknown_actor')
        w.step(2)
        self.assertEqual(len(w.observe(0)['units']), 2)
        self.assertEqual(w.metrics(0)['reinforcements'], 1)

    def test_reactive_policy_reads_only_observed_state(self):
        w = World({'units': [unit('asset', 1, 0, ranged=7, role='asset'),
                            unit('guard', 1, 10), unit('enemy', 0, 30)]})
        obs = w.observe(1)
        state = {}
        orders = reactive_orders(obs, 'support', state)
        guard = next(o for o in orders if o['actor']=='guard')
        self.assertEqual(guard['target'], 'enemy')
        self.assertEqual(guard['kind'], 'strike')
        before = deepcopy(obs)
        for mode in ('hold', 'support', 'counter', 'withdraw', 'feint'):
            reactive_orders(obs, mode, {})
        self.assertEqual(before, obs)
        self.assertNotIn('score', w.metrics(1))

    def test_reidentity_preserves_health_ammo_and_allows_retargeting(self):
        w = World({'units': [unit('a', 0, 0, melee=0, ranged=5, ammo=4),
                            unit('b', 1, 30, melee=0)], 'events': [dict(t=2, type='replace',
                                actor='b', unit=unit('ignored', 1, 30))]})
        w.issue(0, [dict(actor='a', kind='fire', target='b')])
        w.step(2)
        self.assertLess(by_id(w, 'b')['hp'], 100)
        self.assertEqual(w.metrics(1)['initial_hp'], 100)
        self.assertEqual(w.issue(0, [dict(actor='a', kind='fire', target='b')])[0]['type'], 'order_accepted')
        self.assertEqual(by_id(w, 'b')['generation'], 1)

    def test_invalid_commands_do_not_mutate_world(self):
        w = World({'units': [unit('a', 0, 0), unit('b', 1, 50)]})
        cases = [dict(actor='b', kind='hold'), dict(actor='a', kind='teleport'),
                 dict(actor='a', kind='move', x=float('nan'), y=0),
                 dict(actor='a', kind='strike', target='a')]
        self.assertTrue(all(e['type']=='order_rejected' for e in w.issue(0, cases)))
        self.assertEqual(by_id(w, 'a')['x'], 0)


if __name__ == '__main__':
    unittest.main(verbosity=2)

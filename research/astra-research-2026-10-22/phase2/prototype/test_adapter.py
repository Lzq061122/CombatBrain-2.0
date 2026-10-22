import unittest
from run_dynamic import scenario
from controller import Controller
from world import World


class AdapterTest(unittest.TestCase):
    def test_hold_is_targetless_and_all_baseline_orders_are_valid(self):
        w=World(scenario(7300)); c=Controller(0,mode='phase1',budget=0)
        orders=c.decide(w.observe(0))
        self.assertTrue(any(o['kind']=='hold' for o in orders))
        for o in orders:
            if o['kind']=='hold': self.assertNotIn('target',o)
        w.issue(0,orders)
        self.assertEqual(w.metrics(0)['orders_rejected'],0)
    def test_duty_and_action_capability_enter_baseline_before_selection(self):
        w=World(scenario(7300));c=Controller(0,mode='phase1',budget=80)
        obs=w.observe(0);orders,work,deps=c.choose_phase1(obs)
        actions={a.actor:a for a in c.current_phase1}
        self.assertEqual(actions['0_1'].kind,'protect')
        self.assertTrue(all(o['kind']!='pressure' or next(u for u in obs['units'] if u['id']==o['actor'])['ranged']>0 for o in orders.values()))

if __name__=='__main__': unittest.main()

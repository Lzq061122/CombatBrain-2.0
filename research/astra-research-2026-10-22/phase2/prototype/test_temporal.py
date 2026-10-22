import unittest
from dataclasses import replace
from temporal import *


class TemporalTests(unittest.TestCase):
    def setUp(self):
        self.units={x:{'hp':100,'speed':5,'authorized':True,'generation':1,'authority':1} for x in ('a','b','c')}
        self.plan=Plan((Task('fixa','a','fix','g',0,12,strength=3),
                        Task('fixb','b','fix','g',2,12,strength=3),
                        Task('strike','c','strike','t',3,10)),
                       (JointCondition('strike','g',('fixa','fixb'),5),))
    def test_joint_weak_contributions_share_window(self):
        self.assertEqual(validate(self.plan,self.units),[])
    def test_strength_sum_without_overlap_rejected(self):
        p=replace(self.plan,tasks=(replace(self.plan.tasks[0],end=5),replace(self.plan.tasks[1],start=5),self.plan.tasks[2]))
        self.assertIn('joint_window:strike',validate(p,self.units))
    def test_resource_reuse_needs_transfer(self):
        p=Plan((Task('one','a','move',start=0,end=4,x=0,y=0),Task('two','a','fire',start=5,end=10,after=('one',),x=20,y=0)))
        self.assertIn('transfer_time:two',validate(p,self.units))
        p.tasks=(p.tasks[0],replace(p.tasks[1],start=8,end=12))
        self.assertEqual(validate(p,self.units),[])
    def test_forecast_does_not_release_effect(self):
        self.assertFalse(observed_ready(self.plan,'strike',{},4))
    def test_fresh_joint_observation_required(self):
        facts={k:dict(source='execution_observation',actor={'fixa':'a','fixb':'b'}[k],target='g',generation=1,authority=1,mission='research',domain='domain',task=k,task_revision=1,observed_at=4,valid_until=7,effective=True,strength=3) for k in ('fixa','fixb')}
        self.assertTrue(observed_ready(self.plan,'strike',facts,5))
        self.assertFalse(observed_ready(self.plan,'strike',facts,8))
        facts['fixa']['authority']=2
        self.assertFalse(observed_ready(self.plan,'strike',facts,5))
        facts['fixa']['authority']=1;facts['fixa']['actor']='c'
        self.assertFalse(observed_ready(self.plan,'strike',facts,5))
        facts['fixa']['actor']='a';facts['fixa']['mission']='other'
        self.assertFalse(observed_ready(self.plan,'strike',facts,5))
    def test_repair_keeps_unrelated_identity(self):
        extra=Task('independent','b','fire','other',15,20)
        p=replace(self.plan,tasks=self.plan.tasks+(extra,))
        repaired,bad=repair(p,{'fixa'})
        self.assertEqual(bad,['fixa','strike'])
        self.assertIn(extra,repaired.tasks)
    def test_duty_not_deleted_by_task_failure(self):
        duty=Coverage('asset','a',0,20)
        p=Plan((Task('protect','a','protect','asset',0,20),),duties=(duty,))
        q,_=repair(p,{'protect'})
        self.assertEqual(q.duties,(duty,))
        self.assertIn('duty_gap:asset',validate(q,self.units))
    def test_unobserved_successor_is_not_duty_handoff(self):
        p=Plan((Task('p','b','protect','asset',0,20),),duties=(Coverage('asset','a',0,20),))
        self.assertIn('duty_gap:asset',validate(p,self.units))
    def test_cycles_rejected(self):
        p=Plan((Task('a','a','hold',start=0,end=2,after=('b',)),Task('b','b','hold',start=3,end=4,after=('a',))))
        self.assertIn('dependency_cycle',validate(p,self.units))
    def test_release_is_observed_not_elapsed(self):
        p=Plan((Task('a','a','move',start=0,end=3),Task('b','a','fire',start=3,end=9,after=('a',))))
        l=CommanderLedger(p)
        self.assertEqual(l.releasable(4,{}),[])
        receipt=dict(source='execution_observation',complete=True,mission='research',domain='domain',plan_revision=1,task='a',actor='a',generation=1,authority=1)
        self.assertFalse(l.acknowledge_release('a',True))
        self.assertTrue(l.acknowledge_release('a',receipt))
        self.assertEqual([x.id for x in l.releasable(4,{})],['b'])
    def test_cancel_does_not_release_resource(self):
        p=Plan((Task('a','a','move',start=0,end=3),Task('b','a','fire',start=3,end=9)))
        l=CommanderLedger(p);l.invalidate({'a'})
        self.assertEqual(l.releasable(4,{}),[])
        self.assertEqual(l.failed,{'a','b'})
    def test_unaffected_dispatched_task_keeps_release_scope_after_repair(self):
        p=Plan((Task('a','a','move',start=0,end=3),Task('b','b','move',start=0,end=3)))
        l=CommanderLedger(p);l.mark_dispatched('b');l.invalidate({'a'})
        receipt=dict(source='execution_observation',complete=True,mission='research',domain='domain',plan_revision=1,task='b',actor='b',generation=1,authority=1)
        self.assertTrue(l.acknowledge_release('b',receipt))
    def test_revoke_invalidates_future_reservation(self):
        units={**self.units,'a':{**self.units['a'],'authority':2}}
        self.assertIn('identity:fixa',validate(self.plan,units))

if __name__=='__main__': unittest.main()

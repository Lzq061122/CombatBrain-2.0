import unittest
from dataclasses import replace
from planner import *
from run_experiments import fixture

class CoreChecks(unittest.TestCase):
    def setUp(self): self.st=fixture(); self.cfg=Config()
    def test_dependency_exec_requires_observed_fix_not_just_assignment(self):
        st=self.st;f=st.friendly
        aa=(action_for(f[0],'strike','enemy_archers'),action_for(f[1],'fix','enemy_spears'),action_for(f[2],'hold'))
        ev=assessment(st,aa,self.cfg);self.assertTrue(ev.feasible)
        rt=Runtime(st.mission,st.domain);rt.adopt(Selection(aa,ev,'fixture',{},{}),st)
        later=replace(st,now=20)
        self.assertEqual(rt.write(aa[0],rt.revision,later,ready=frozenset({'cavalry'})),'wait_observed_dependency')
        self.assertEqual(rt.write(aa[0],rt.revision,later,ready=frozenset({'cavalry','infantry'})),'accepted_interface_only')
    def test_probe_feedback_changes_candidates_and_prevents_identical_retry(self):
        f=self.st.friendly[0];a=action_for(f,'probe','enemy_archers')
        aa=(a,)+tuple(action_for(f,'hold') for f in self.st.friendly[1:])
        ev=assessment(self.st,aa,self.cfg);self.assertTrue(ev.feasible)
        rt=Runtime(self.st.mission,self.st.domain);rt.adopt(Selection(aa,ev,'fixture',{},{}),self.st)
        self.assertTrue(rt.report(f.uid,1,1,rt.revision,1,{'no_response':True},self.st))
        nxt=rt.feedback_snapshot(self.st)
        self.assertNotIn(a,options(nxt,self.cfg)[f.uid])
        sel=choose(nxt,self.cfg,aa)
        self.assertEqual(sel.reason,'hard_invalid_repair')
        self.assertNotIn(a,sel.actions)
    def test_high_loss_report_does_not_cancel_effective_sacrifice_duty(self):
        st=replace(self.st,duties=(Duty('archers','infantry',sacrifice=True,effective=True),))
        rt=Runtime(st.mission,st.domain);sel=choose(st,self.cfg);rt.adopt(sel,st)
        before=rt.actions
        rt.report('infantry',1,1,rt.revision,1,{'high_loss':True,'progress':'blocked','duty_effective':True},st)
        self.assertEqual(rt.actions,before)
        self.assertEqual(rt.feedback_snapshot(st).duties,st.duties)
        self.assertEqual(rt.safety('infantry',st,True,False,True,True),'hold_if_feasible_report_blocked')
        self.assertEqual(rt.safety('infantry',st,True,False,True,False),'minimal_exception_report')
    def test_zero_extra_budget_keeps_duty(self):
        st=replace(self.st,duties=(Duty('archers','infantry'),))
        sel=choose(st,replace(self.cfg,expansions=0))
        self.assertTrue(sel.assessment.feasible)
        self.assertIn(action_for(st.friendly[1],'protect','archers'),sel.actions)
        self.assertEqual(sel.work['expansions'],0)
    def test_authority_revocation_blocks_old_write_and_safety(self):
        st=self.st;rt=Runtime(st.mission,st.domain);sel=choose(st);rt.adopt(sel,st)
        f=st.friendly[0];changed=replace(st,friendly=(replace(f,authorized=False,authority=2),)+st.friendly[1:])
        a=next(a for a in rt.actions if a.actor==f.uid)
        self.assertEqual(rt.write(a,rt.revision,changed),'lost_authority_or_identity')
        self.assertEqual(rt.safety(f.uid,changed,True,False,True,False),'no_authority')
    def test_enemy_output_conserved_across_attacks(self):
        e=Formation('e',20,0,4,(Group(30,1,1,.5),))
        own=tuple(Formation(str(i),0,i,4,(Group(30,1,0,.5),)) for i in range(4))
        st=Snapshot(own,(e,));aa=tuple(action_for(f,'strike','e') for f in own)
        ev=assessment(st,aa,self.cfg);self.assertTrue(ev.feasible)
        for d in ev.details.values(): self.assertLessEqual(d['enemy_output_used']['e'],d['enemy_output_budget']['e'])
    def test_no_response_memory_invalidated_only_by_material_epoch(self):
        st=self.st;failed=replace(st,failed_probes=frozenset({('cavalry','enemy_archers',st.epoch)}))
        a=action_for(st.friendly[0],'probe','enemy_archers')
        self.assertNotIn(a,options(replace(failed,now=300),self.cfg)['cavalry'])
        self.assertIn(a,options(replace(failed,epoch=2),self.cfg)['cavalry'])
    def test_duplicate_report_and_old_revision_cannot_replan(self):
        st=self.st;rt=Runtime(st.mission,st.domain);rt.adopt(choose(st),st)
        f=st.friendly[0];r=rt.revision
        self.assertTrue(rt.report(f.uid,1,1,r,1,{'high_loss':True},st))
        self.assertFalse(rt.report(f.uid,1,1,r,1,{'route_blocked':True},st))
        rt.cancel_tasks();self.assertFalse(rt.report(f.uid,1,1,r,2,{'route_blocked':True},st))

class TimingChecks(unittest.TestCase):
    def test_short_fix_cannot_cover_long_strike(self):
        st=fixture();f=st.friendly
        st=replace(st,friendly=(f[0],replace(f[1],endurance=5),f[2]))
        aa=(action_for(f[0],'strike','enemy_archers'),action_for(f[1],'fix','enemy_spears'),action_for(f[2],'hold'))
        ev=assessment(st,aa,Config())
        self.assertFalse(ev.feasible)
        self.assertTrue(any(x.startswith('support_duration') for x in ev.reasons))
    def test_observed_active_effect_has_no_second_setup_charge(self):
        st=fixture();a=action_for(st.friendly[2],'shoot','enemy_archers')
        st=replace(st,active_effects=frozenset({a.key()}))
        ev=assessment(st,(a,),Config())
        self.assertTrue(ev.feasible)
        self.assertEqual(ev.starts[a.actor],0)

if __name__=='__main__': unittest.main(verbosity=2)

"""Phase 2 feedback-consumption counterexamples; no game APIs."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import unittest
from dataclasses import replace
from baseline import planner as p
from run_baseline_challenges import complementarity


def fixture():
    a=p.Formation('a',0,0,10,(p.Group(100,1,0,.5),))
    e=p.Formation('e',50,0,10,(p.Group(100,1,0,.5),))
    st=p.Snapshot((a,),(e,),now=10)
    action=p.action_for(a,'strike','e')
    ev=p.assessment(st,(action,),p.Config())
    sel=p.Selection((action,),ev,'fixture',{}, {})
    rt=p.Runtime(st.mission,st.domain)
    assert rt.adopt(sel,st)
    assert rt.report('a',1,1,rt.revision,1,{'route_blocked':True},st)
    return a,e,st,rt


class FeedbackConsumption(unittest.TestCase):
    def test_authority_generation_change_drops_task_report(self):
        a,e,st,rt=fixture()
        changed=replace(st,friendly=(replace(a,authority=2),))
        self.assertNotIn(('a','e'),rt.feedback_snapshot(changed).blocked)

    def test_revoked_authority_drops_task_report(self):
        a,e,st,rt=fixture()
        changed=replace(st,friendly=(replace(a,authorized=False),))
        self.assertNotIn(('a','e'),rt.feedback_snapshot(changed).blocked)

    def test_membership_generation_change_drops_task_report(self):
        a,e,st,rt=fixture()
        changed=replace(st,friendly=(replace(a,generation=2),))
        self.assertNotIn(('a','e'),rt.feedback_snapshot(changed).blocked)

    def test_old_condition_epoch_drops_task_report(self):
        a,e,st,rt=fixture()
        self.assertNotIn(('a','e'),rt.feedback_snapshot(replace(st,epoch=2)).blocked)

    def test_same_valid_task_report_still_consumed(self):
        a,e,st,rt=fixture()
        self.assertIn(('a','e'),rt.feedback_snapshot(st).blocked)

    def test_wrong_mission_not_consumed(self):
        a,e,st,rt=fixture()
        self.assertNotIn(('a','e'),rt.feedback_snapshot(replace(st,mission='other')).blocked)


    def test_expired_task_report_removed_even_from_derived_snapshot(self):
        a,e,st,rt=fixture()
        derived=rt.feedback_snapshot(st)
        self.assertIn(('a','e'),derived.blocked)
        expired=rt.feedback_snapshot(replace(derived,now=st.now+rt.feedback_max_age+1))
        self.assertNotIn(('a','e'),expired.blocked)

    def test_target_replacement_invalidates_task_judgement(self):
        a,e,st,rt=fixture()
        self.assertNotIn(('a','e'),rt.feedback_snapshot(replace(st,enemy=(replace(e,generation=2),))).blocked)

    def test_independent_fresh_fact_retained_after_command_revoke_only_if_revalidated(self):
        a,e,st,rt=fixture()
        fact=p.EnvironmentFact('a','e','host.route-observation',10,15,st.mission,st.domain)
        self.assertTrue(rt.remember_environment(fact))
        changed=replace(st,friendly=(replace(a,authorized=False,authority=2),),now=11)
        self.assertNotIn(('a','e'),rt.feedback_snapshot(changed).blocked)
        self.assertNotIn(('a','e'),rt.feedback_snapshot(changed,lambda fact,st:False).blocked)
        self.assertIn(('a','e'),rt.feedback_snapshot(changed,lambda fact,st:True).blocked)

    def test_environment_fact_ttl_is_not_extended_by_revalidation(self):
        a,e,st,rt=fixture();rt.cancel_tasks()
        rt.remember_environment(p.EnvironmentFact('a','e','host.route-observation',10,15,st.mission,st.domain))
        self.assertNotIn(('a','e'),rt.feedback_snapshot(replace(st,now=15),lambda fact,st:True).blocked)

    def test_raw_independent_block_is_not_removed_as_task_overlay(self):
        a,e,st,rt=fixture()
        raw=replace(st,blocked=frozenset({('a','e')}))
        derived=rt.feedback_snapshot(raw)
        invalid=replace(derived,friendly=(replace(a,authorized=False),),now=100)
        self.assertIn(('a','e'),rt.feedback_snapshot(invalid).blocked)

    def test_cancel_retracts_old_task_overlay(self):
        a,e,st,rt=fixture();derived=rt.feedback_snapshot(st);rt.cancel_tasks()
        self.assertNotIn(('a','e'),rt.feedback_snapshot(derived).blocked)

    def test_future_observation_not_consumed(self):
        a,e,st,rt=fixture()
        self.assertNotIn(('a','e'),rt.feedback_snapshot(replace(st,now=9)).blocked)


class BoundedDependencyClosure(unittest.TestCase):
    def setUp(self):
        self.cfg=p.Config(expansions=5000,width=32,targets=3,options=16,uncertainty=False)

    def test_three_actor_package_reaches_exact_reference(self):
        st=complementarity()
        greedy=p.choose(st,self.cfg,method='greedy')
        closure=p.choose(st,self.cfg,method='closure')
        exact=p.choose(st,self.cfg,method='exhaustive')
        self.assertGreater(closure.assessment.interval[0],greedy.assessment.interval[0])
        self.assertAlmostEqual(closure.assessment.interval[0],exact.assessment.interval[0])
        self.assertTrue(p.assessment(st,closure.actions,self.cfg).feasible)
        self.assertGreater(closure.work['dependency_packages'],0)

    def test_local_basin_negative_result_is_preserved(self):
        st=complementarity(200)
        greedy=p.choose(st,self.cfg,method='greedy')
        closure=p.choose(st,self.cfg,method='closure')
        # A regression characterization, not a desired product behavior.
        self.assertLess(closure.assessment.interval[0],greedy.assessment.interval[0])

    def test_fixed_solver_exposes_candidate_omission(self):
        st=complementarity()
        narrow=p.choose(st,replace(self.cfg,targets=1),method='exhaustive')
        wide=p.choose(st,self.cfg,method='exhaustive')
        self.assertLess(narrow.assessment.interval[0],wide.assessment.interval[0])

    def test_budget_cutoffs_keep_authority_and_mandatory_duty(self):
        st=complementarity()
        st=replace(st,friendly=(st.friendly[0],replace(st.friendly[1],authorized=False),st.friendly[2]),
                   duties=(p.Duty('fixer1','fixer2'),))
        for budget in (0,1,2,8,32,128):
            for method in ('greedy','closure','beam'):
                with self.subTest(budget=budget,method=method):
                    cfg=replace(self.cfg,expansions=budget,options=1)
                    sel=p.choose(st,cfg,method=method)
                    self.assertTrue(p.assessment(st,sel.actions,cfg).feasible)
                    self.assertTrue(all(a.actor!='fixer1' for a in sel.actions))
                    self.assertTrue(any(a.actor=='fixer2' and a.kind=='protect' for a in sel.actions))
                    self.assertLessEqual(sel.work['search_model_calls'],budget)


if __name__=='__main__':unittest.main(verbosity=2)

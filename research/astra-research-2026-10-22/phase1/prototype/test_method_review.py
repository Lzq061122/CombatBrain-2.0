"""Independent research-validity probes; no Bannerlord claims or production edits.
Run directly. Reports accounting and representational counterexamples, not win rates.
"""
import json
import hashlib
from pathlib import Path
from dataclasses import replace
import planner as p
from run_experiments import sample


def run():
    cfg=p.Config(width=48, expansions=3000, targets=2, options=10)
    original=p.assessment
    accounting=[]
    for seed in range(1729,1765):
        st=sample(seed)
        row={'seed':seed}
        for method in ('beam','greedy','exhaustive'):
            calls={'all':0,'relaxed':0}
            def counted(*args,**kwargs):
                calls['all']+=1
                if len(args)>4 and args[4] is False: calls['relaxed']+=1
                return original(*args,**kwargs)
            p.assessment=counted
            try: result=p.choose(st,cfg,method=method)
            finally: p.assessment=original
            row[method]={'reported':result.work,'actual_assessment_calls':calls['all'],
                         'relaxed_calls':calls['relaxed'],'value':result.assessment.interval[0]}
        accounting.append(row)
    crossing=None
    robust=replace(cfg,uncertainty=False,switch_cost=0)
    for seed in range(1729,2129):
        st=sample(seed)
        current=p.choose(st,replace(robust,scenarios=('hold',)),method='exhaustive').actions
        ce=original(st,current,robust,current)
        fresh=p.choose(st,robust,method='exhaustive')
        retained=p.choose(st,replace(robust,retain_all_scenarios=True),current,method='exhaustive')
        default_retained=p.choose(st,robust,current,method='exhaustive')
        if (fresh.assessment.interval[0]>ce.interval[0]+1e-7 and
                p.signature(retained.actions)==p.signature(current)):
            crossing={'seed':seed,'current_outcomes':ce.outcomes,
                      'best_maximin_outcomes':fresh.assessment.outcomes,
                      'current_lower_value':ce.interval[0],
                      'best_maximin_lower_value':fresh.assessment.interval[0],
                      'optional_all_scenario_reason':retained.reason,
                      'default_reason':default_retained.reason,
                      'default_lower_value':default_retained.assessment.interval[0],
                      'default_reaches_maximin_value':abs(default_retained.assessment.interval[0]-fresh.assessment.interval[0])<1e-8,
                      'uncertainty':False,'switch_cost':0}
            break
    # Two weak fixers jointly meet effect threshold, but hard dependency accepts only
    # an individually sufficient fixer. This is a restricted model, not a game bug.
    cav=p.Formation('cavalry',0,0,10,(p.Group(100,1,0,.5,0,True),))
    f1=p.Formation('fix1',48,0,10,(p.Group(20,1),))
    f2=p.Formation('fix2',48,1,10,(p.Group(20,1),))
    target=p.Formation('target',50,0,4,(p.Group(60,.1,1),))
    guard=p.Formation('guard',52,0,4,(p.Group(100,1,0,.5,1),))
    st=p.Snapshot((cav,f1,f2),(target,guard))
    acts=(p.action_for(cav,'strike','target'),p.action_for(f1,'fix','guard'),p.action_for(f2,'fix','guard'))
    strict=original(st,acts,cfg)
    relaxed=original(st,acts,replace(cfg,dependencies=False))
    coalition={'sum_fix_melee':f1.cap('melee')+f2.cap('melee'),
               'required_fix_melee':.35*guard.cap('melee'),
               'strict_feasible':strict.feasible,'strict_reasons':strict.reasons,
               'without_hard_dependency_feasible':relaxed.feasible}
    budget_audit=[]
    for budget in (0,1,10,50,100):
        for method in ('beam','greedy'):
            calls={'all':0}
            def counted_budget(*args,**kwargs):
                calls['all']+=1
                return original(*args,**kwargs)
            p.assessment=counted_budget
            try: result=p.choose(sample(1729),replace(cfg,expansions=budget),method=method)
            finally: p.assessment=original
            budget_audit.append({'budget':budget,'method':method,'actual_calls':calls['all'],
                                 'work':result.work,
                                 'accounting_exact':calls['all']==result.work.get('model_calls'),
                                 'search_calls_within_budget':result.work.get('search_model_calls',-1)<=budget})
    # Finishing the second support wait can invalidate the first support's earlier
    # coverage check. The final strike interval must be checked against ALL supports.
    cav2=replace(cav,endurance=6)
    target2=replace(target,x=10)
    guard1=replace(guard,uid='g1',x=10)
    guard2=replace(guard,uid='g2',x=20)
    fixer1=replace(f1,uid='s1',x=10,y=0,groups=(p.Group(40,1),),endurance=8)
    fixer2=replace(f2,uid='s2',x=-40,y=0,groups=(p.Group(40,1),),endurance=10)
    two_support=p.Snapshot((cav2,fixer1,fixer2),(target2,guard1,guard2))
    aa=(p.action_for(cav2,'strike','target'),p.action_for(fixer1,'fix','g1'),p.action_for(fixer2,'fix','g2'))
    te=original(two_support,aa,cfg)
    support_check={'feasible':te.feasible,'reasons':te.reasons,'starts':te.starts,
                   'first_fix_end':te.starts['s1']+fixer1.endurance,
                   'final_strike_end':te.starts['cavalry']+cav2.endurance}
    # Verify the requested repair only: active observations exempt repeated setup,
    # and a shorter fixing interval no longer suppresses reinforcement forever.
    observed=p.action_for(f1,'strike','target')
    before=original(st,(observed,),cfg)
    after=original(replace(st,active_effects=frozenset((observed.key(),))),(observed,),cfg)
    active_check={'unstarted_start':before.starts['fix1'],'observed_active_start':after.starts['fix1']}
    shooter=p.Formation('shooter',0,0,10,(p.Group(100,0,1,.5),))
    fixer=p.Formation('fixer',60,0,10,(p.Group(40,1,0,.5),),endurance=30)
    enemy_target=p.Formation('enemy_target',50,0,4,(p.Group(100,.1,1,.5),))
    enemy_support=p.Formation('enemy_support',60,0,4,(p.Group(100,1,0,.5),))
    fire_st=p.Snapshot((shooter,fixer),(enemy_target,enemy_support))
    fire_acts=(p.action_for(shooter,'shoot','enemy_target'),p.action_for(fixer,'fix','enemy_support'))
    fire_cfg=replace(cfg,scenarios=('reinforce',),uncertainty=False)
    def isolated_fire(st):
        ev=original(st,fire_acts,fire_cfg)
        fixer_now=st.own()['fixer']
        direct_fix=fixer_now.cap('melee')*.35*min(st.horizon-ev.starts['fixer'],fixer_now.endurance)/st.horizon
        direct_fix/=1+enemy_support.cap('armour')/enemy_support.size
        return ev.details['reinforce']['enemy_capacity_removed']-direct_fix
    expiry_check={'long_fix_shoot_effect':isolated_fire(fire_st),
                  'short_fix_shoot_effect':isolated_fire(replace(fire_st,friendly=(shooter,replace(fixer,endurance=4))))}
    output={'source_sha256':hashlib.sha256(Path(p.__file__).read_bytes()).hexdigest(),
        'accounting_summary':{
        m:{'mean_actual_assessment_calls':sum(r[m]['actual_assessment_calls'] for r in accounting)/len(accounting),
           'mean_reported_evaluations':sum(r[m]['reported']['evaluations'] for r in accounting)/len(accounting),
           'mean_reported_model_calls':sum(r[m]['reported'].get('model_calls',-1) for r in accounting)/len(accounting)}
        for m in ('beam','greedy','exhaustive')},
        'accounting':accounting,'maximin_vs_all_scenario_retention':crossing,
        'two_weak_fixers':coalition,'budget_audit':budget_audit,
        'multiple_support_final_window':support_check,
        'active_effect_observation':active_check,'fix_expiry_effect':expiry_check,
        'scope':'synthetic algorithm accounting and stated-constraint counterexamples only'}
    dest=Path(__file__).parent/'results'/'method_review.json'
    dest.write_text(json.dumps(output,indent=2,ensure_ascii=False,allow_nan=False))
    print(json.dumps({k:v for k,v in output.items() if k!='accounting'},indent=2,ensure_ascii=False))


if __name__=='__main__': run()

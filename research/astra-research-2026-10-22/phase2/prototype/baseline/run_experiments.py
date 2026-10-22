"""Reproduce all synthetic experiments. Does not import any source repository code."""
import argparse, json, random, time, statistics, platform, sys
from pathlib import Path
from dataclasses import replace
from itertools import product
from planner import *

OUT=Path(__file__).parent/'results'


def sample(seed, n=3):
    r=random.Random(seed)
    friendly=[]
    for i in range(n):
        kind=i%3; count=r.randint(18,45)
        g=Group(count,melee=(1.5 if kind==0 else .7 if kind==1 else .25),
                ranged=(1.2 if kind==2 else 0),armour=r.uniform(.3,.9),
                anti_mount=(.8 if kind==1 else 0),mounted=kind==0,known=r.random()>.15)
        friendly.append(Formation('f'+str(i),r.uniform(-20,65),r.uniform(-30,30),
                                  8 if kind==0 else 4,(g,),ammo=r.uniform(.1,1),los=r.random()>.15))
    foes=(Formation('e0',r.uniform(70,125),r.uniform(-20,25),4,
                    (Group(r.randint(20,45),.2,1.2,.25),)),
          Formation('e1',r.uniform(60,140),r.uniform(-30,40),4,
                    (Group(r.randint(18,40),.8,0,.7,1.0),)))
    duty=(Duty('f2','f1'),) if n>=3 and seed%3==0 else ()
    return Snapshot(tuple(friendly),foes,duty)


def fixture():
    return Snapshot((
        Formation('cavalry',0,0,9,(Group(35,1.8,0,.8,0,True),)),
        Formation('infantry',60,15,4,(Group(38,.9,0,.6,.5),)),
        Formation('archers',-10,0,4,(Group(35,.2,1.4,.3),)),
    ),(
        Formation('enemy_archers',110,0,4,(Group(45,.15,1.4,.2),)),
        Formation('enemy_spears',95,20,4,(Group(25,.7,0,.7,1.2),)),
    ))


def detailed(sel):
    return {'actions':serial(sel.actions),'assessment':serial(sel.assessment),
            'reason':sel.reason,'work':sel.work,'rejected':sel.rejected}


def run():
    OUT.mkdir(exist_ok=True)
    cfg=Config(width=48,expansions=3000,targets=2,options=10)
    matrix=[]; trace={}; durations=[]
    # Fixed seed range; no per-case score/tuning or scenario name branching.
    for seed in range(1729,1765):
        st=sample(seed)
        started=time.perf_counter()
        results={m:choose(st,cfg,method=m) for m in ['beam','greedy','exhaustive']}
        durations.append(1000*(time.perf_counter()-started))
        ref=results['exhaustive'].assessment.interval[0]
        row={'seed':seed,'reference':ref}
        for m,s in results.items():
            row[m]={'value':s.assessment.interval[0],'regret':ref-s.assessment.interval[0],
                    'feasible':s.assessment.feasible,**s.work}
        # Same observations/action choices, altered decision mechanism, strict post-audit.
        for name,c in [('no_dependency',replace(cfg,dependencies=False)),
                       ('nominal_only',replace(cfg,scenarios=('hold',),uncertainty=False)),
                       ('single_scenario',replace(cfg,scenarios=('hold',))),
                       ('no_uncertainty',replace(cfg,uncertainty=False))]:
            sel=choose(st,c,method='beam')
            truth=assessment(st,sel.actions,cfg)
            row[name]={'strict_feasible':truth.feasible,'strict_reasons':truth.reasons,
                       'strict_value':truth.interval[0] if truth.feasible else None,
                       'same_as_full':signature(sel.actions)==signature(results['beam'].actions)}
        matrix.append(row)
        if seed<1732: trace[str(seed)]={m:detailed(s) for m,s in results.items()}
    # ETA, dependence, protection, missing ammo/LOS, later actually-present reinforcement.
    st=fixture()
    cases={'base':st,
           'support_far':replace(st,enemy=(st.enemy[0],replace(st.enemy[1],x=350))),
           'fixer_late':replace(st,friendly=(st.friendly[0],replace(st.friendly[1],x=-50),st.friendly[2])),
           'protect_competition':replace(st,duties=(Duty('archers','infantry'),)),
           'ammo_zero_los_blocked':replace(st,friendly=(st.friendly[0],st.friendly[1],replace(st.friendly[2],ammo=0,los=False))),
           'actual_reinforcement':replace(st,friendly=st.friendly+(Formation('reinforcement',70,-10,4,(Group(30,.9,0,.8,.6),)),),duties=(Duty('archers','infantry'),))}
    trace['constructed']={k:detailed(choose(v,cfg)) for k,v in cases.items()}
    # Multi-stage dependency evaluated independently of which plan wins.
    package=(action_for(st.friendly[0],'strike','enemy_archers'),action_for(st.friendly[1],'fix','enemy_spears'),action_for(st.friendly[2],'shoot','enemy_archers'))
    trace['dependency_package']={k:serial(assessment(v,package,cfg)) for k,v in cases.items() if k in ('base','support_far','fixer_late','protect_competition')}
    # Metamorphic checks: permutation/rename/translation should preserve objective, ties may change action IDs.
    metamorphic=[]
    for seed in range(1770,1780):
        a=sample(seed); ref=choose(a,cfg,method='exhaustive').assessment.interval[0]
        b=replace(a,friendly=tuple(reversed(a.friendly)),enemy=tuple(reversed(a.enemy)))
        c=replace(a,friendly=tuple(replace(f,x=f.x+1000,y=f.y-700) for f in a.friendly),enemy=tuple(replace(f,x=f.x+1000,y=f.y-700) for f in a.enemy))
        names={f.uid:'rename_'+str(i) for i,f in enumerate(a.friendly+a.enemy)}
        d=replace(a,friendly=tuple(replace(f,uid=names[f.uid]) for f in a.friendly),enemy=tuple(replace(f,uid=names[f.uid]) for f in a.enemy),duties=tuple(replace(x,asset=names[x.asset],owner=names[x.owner]) for x in a.duties))
        vals=[choose(x,cfg,method='exhaustive').assessment.interval[0] for x in (b,c,d)]
        metamorphic.append({'seed':seed,'max_objective_delta':max(abs(v-ref) for v in vals)})
    # No preselected winner: record sensitivity of both choices and score, don't assert fixed outputs.
    sensitivity=[]
    for seed in range(1800,1810):
        a=sample(seed); refsig=signature(choose(a,cfg).actions)
        for loss,etas in product((.5,1.,2.),(.8,1.,1.2)):
            c=replace(cfg,loss_weight=loss,eta_scale=etas)
            s=choose(a,c)
            sensitivity.append({'seed':seed,'loss_weight':loss,'eta_scale':etas,
                                'changed':signature(s.actions)!=refsig,'value':s.assessment.interval[0]})
    # Repeated decision sequence: observation jitter vs hard loss of authorization.
    seq=[]
    for mechanism,c in [('full',replace(cfg,uncertainty=False)),('no_switch_cost',replace(cfg,uncertainty=False,switch_cost=0))]:
        previous=();changes=0;rows=[]
        a=sample(1811)
        for t in range(12):
            f=a.friendly[0]
            cur=replace(a,now=t*3,friendly=(replace(f,x=f.x+(-1)**t*2),)+a.friendly[1:])
            if t>=8: cur=replace(cur,friendly=(replace(cur.friendly[0],authorized=False,authority=2),)+cur.friendly[1:])
            sel=choose(cur,c,previous)
            if previous and signature(previous)!=signature(sel.actions): changes+=1
            rows.append({'tick':t,'reason':sel.reason,'actions':serial(sel.actions),'value':sel.assessment.interval[0]})
            previous=sel.actions
        seq.append({'mechanism':mechanism,'changes':changes,'trace':rows})
    # Tiny budget behavior is observed, never called game frame timing.
    budgets=[]
    for budget in (0,10,50,300,3000):
        sel=choose(cases['protect_competition'],replace(cfg,expansions=budget))
        budgets.append({'budget':budget,**sel.work,'feasible':sel.assessment.feasible,'actions':serial(sel.actions)})
    summary={'environment':{'python':sys.version,'platform':platform.platform()},
             'config':serial(cfg),'n':len(matrix),
             'comparison':{m:{'mean_regret':statistics.mean(x[m]['regret'] for x in matrix),
                              'max_regret':max(x[m]['regret'] for x in matrix),
                              'exact_count':sum(abs(x[m]['regret'])<1e-8 for x in matrix),
                              'feasible_count':sum(x[m]['feasible'] for x in matrix),
                              'mean_evaluations':statistics.mean(x[m]['evaluations'] for x in matrix),
                              'mean_model_calls':statistics.mean(x[m]['model_calls'] for x in matrix),
                              'mean_expansions':statistics.mean(x[m]['expansions'] for x in matrix)} for m in ('beam','greedy')},
             'ablation':{m:{'invalid':sum(not x[m]['strict_feasible'] for x in matrix),'different':sum(not x[m]['same_as_full'] for x in matrix)} for m in ('no_dependency','nominal_only','single_scenario','no_uncertainty')},
             'metamorphic_max_delta':max(x['max_objective_delta'] for x in metamorphic),
             'sensitivity_changes':sum(x['changed'] for x in sensitivity),'sensitivity_n':len(sensitivity),
             'sequence_changes':{x['mechanism']:x['changes'] for x in seq},
             'per_case_three_methods_ms':{'median':statistics.median(durations),'max':max(durations)},
             'limitations':['synthetic capacity proxy, not victory probability','same model used by evaluator and reference; no game validation','beam heuristic can miss feasible packages','three-method timing includes exhaustive search']}
    for name,data in [('summary',summary),('matrix',matrix),('traces',trace),('metamorphic',metamorphic),('sensitivity',sensitivity),('sequences',seq),('budgets',budgets)]:
        (OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2,allow_nan=False))
    # Structured fixtures contain observations, not pre-scored candidate plans.
    (Path(__file__).parent/'cases.json').write_text(json.dumps({k:serial(v) for k,v in cases.items()},ensure_ascii=False,indent=2))
    print(json.dumps(summary,ensure_ascii=False,indent=2))


if __name__=='__main__': run()

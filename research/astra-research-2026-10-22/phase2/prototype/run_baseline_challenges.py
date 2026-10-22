"""Phase 2 finite, same-model baseline challenges. Standard library only.

Exact enumeration is an unrestricted reference in the declared candidate domain,
not a budget-fair competitor. Wall times are whole choose() calls, measured one
method at a time; repeating a fixture measures timing, not independent evidence.
"""
from dataclasses import replace
from pathlib import Path
from time import perf_counter_ns
import hashlib
import json
import math
import platform
import random
import statistics
import sys
from baseline import planner as p

ROOT=Path(__file__).resolve().parents[1]
SEED=2026102202
METHODS=('greedy','closure','beam')


def formation(uid,n,melee,armour,x=0,y=0,anti=0,mounted=False,ranged=0,speed=10):
    return p.Formation(uid,x,y,speed,(p.Group(n,melee,ranged,armour,anti,mounted),))


def complementarity(cavalry_n=500):
    # Two different anti-mounted supports must be fixed by different resources.
    return p.Snapshot((formation('cavalry',cavalry_n,1,0,mounted=True),
                       formation('fixer1',35,1,0),formation('fixer2',35,1,0)),
                      (formation('target',500,0,0,x=100,ranged=1),
                       formation('guard1',100,1,1,x=100,y=-30,anti=1),
                       formation('guard2',100,1,1,x=100,y=30,anti=1)))


def clean(x):
    if hasattr(x,'__dataclass_fields__'):
        return {k:clean(getattr(x,k)) for k in x.__dataclass_fields__}
    if isinstance(x,dict): return {str(k):clean(v) for k,v in x.items()}
    if isinstance(x,(set,frozenset)): return [clean(v) for v in sorted(x)]
    if isinstance(x,(tuple,list)): return [clean(v) for v in x]
    if isinstance(x,float) and not math.isfinite(x): return str(x)
    return x


def inputs(st,cfg):
    opts=p.options(st,cfg)
    return {'snapshot':clean(st),'config':clean(cfg),'options':clean(opts),
            'candidate_product':math.prod(map(len,opts.values())),
            'support_relations':sum(len(p.protectors(f,e,st,cfg)) for f in st.friendly for e in st.enemy)}


def run(st,cfg,method,repeats=1,current=()):
    times=[]; first=None
    for _ in range(repeats):
        begin=perf_counter_ns(); sel=p.choose(st,cfg,current,method);times.append((perf_counter_ns()-begin)/1e6)
        if first is not None:
            assert p.signature(first.actions)==p.signature(sel.actions), 'deterministic repeat changed'
        first=sel
    check=p.assessment(st,sel.actions,cfg,current)
    return {'method':method,'value':sel.assessment.interval[0],
            'feasible':sel.assessment.feasible,'rechecked_feasible':check.feasible,
            'actions':clean(sel.actions),'outcomes':sel.assessment.outcomes,
            'work':sel.work,'rejected':sel.rejected,'reason':sel.reason,
            'work_attribution':{'full_assessments':sel.work['evaluations'],
                                'partial_relaxed_assessments':sel.work['model_calls']-sel.work['evaluations'],
                                'candidate_generation_passes':1,
                                'complete_plan_constraint_checks':sel.work['evaluations'],
                                'external_result_recheck_excluded_from_wall_time':1},
            'wall_ms':times}


def environment():
    code=Path(p.__file__).read_bytes()
    return {'python':sys.version,'executable':sys.executable,'platform':platform.platform(),
            'machine':platform.machine(),'processor':platform.processor(),
            'seed':SEED,'baseline_sha256':hashlib.sha256(code).hexdigest(),
            'runner_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            'units':'abstract equal-weight capacity loss; not victory or measured game damage',
            'timing':'whole choose(), serial methods, perf_counter_ns; no warmup excluded',
            'budget':'search model calls capped; 1 mandatory fallback call separate; closure nodes separately capped at same limit; candidate generation included only in wall time',
            'scope':'single snapshot; inherited model with uncertainty=False; same-model search checks only'}


def write(name,data):
    target=ROOT/'results'/name;target.parent.mkdir(exist_ok=True)
    target.write_text(json.dumps({'environment':environment(),**data},ensure_ascii=False,indent=2)+'\n')
    print(str(target),flush=True)


def higher_order():
    cfg=p.Config(expansions=5000,width=32,targets=3,options=16,uncertainty=False)
    cases=[]
    for label,n in [('three_way_positive',500),('closure_local_basin_negative',200)]:
        st=complementarity(n)
        rows=[run(st,cfg,m,3) for m in METHODS]
        rows.append(run(st,cfg,'exhaustive',3))
        rows.append({**run(st,replace(cfg,width=1000),'beam',3),'variant':'wide_1000'})
        cases.append({'id':label,**inputs(st,cfg),'runs':rows})
    write('baseline_high_order.json',{'cases':cases,
          'interpretation':'Candidate-domain optimum only. Closure is not a universal dominance improvement; wide beam spends extra evaluations.'})


def candidates():
    st=complementarity()
    base=p.Config(expansions=5000,width=32,targets=3,options=16,uncertainty=False)
    cases=[]
    for label,cfg in [('nearest_target_only',replace(base,targets=1)),
                      ('optional_actions_2',replace(base,options=2)),('wide_reference',base)]:
        cases.append({'id':label,**inputs(st,cfg),'run':run(st,cfg,'exhaustive',3)})
    write('baseline_candidates.json',{'cases':cases,
          'interpretation':'Fixed exhaustive solver; narrower generation deletes fixer-to-guard options before search. The larger domain remains finite, not a proof of all tactical possibilities.'})


def generated(n,seed):
    rng=random.Random(seed)
    own=[];foes=[]
    for i in range(n):
        mounted=i%3==0
        own.append(formation(f'f{i:02}',rng.randint(40,140),rng.uniform(.35,1),rng.uniform(.15,.8),
                             rng.uniform(-20,20),rng.uniform(-65,65),anti=.15,mounted=mounted,
                             ranged=rng.uniform(0,.7) if i%3==2 else 0,speed=12 if mounted else 8))
    for i in range(3):
        foes.append(formation(f'e{i:02}',rng.randint(50,180),rng.uniform(.35,1),rng.uniform(.15,.8),
                              rng.uniform(60,100),-38+38*i,anti=.8 if i!=1 else 0,
                              ranged=rng.uniform(.2,.8) if i==1 else 0,speed=8))
    return p.Snapshot(tuple(own),tuple(foes))


def scale_runs():
    records=[]; summaries=[]
    for n in (3,5,8):
        for index in range(3):
            seed=SEED+n*100+index; st=generated(n,seed)
            cfg=p.Config(expansions=1024,width=32,targets=3,options=10,uncertainty=False)
            rows=[]
            for budget in (64,256,1024):
                for method in METHODS:
                    rows.append({'budget':budget,**run(st,replace(cfg,expansions=budget),method,3)})
            reference=run(st,cfg,'exhaustive') if n==3 else None
            records.append({'id':f'n{n}_seed{seed}',**inputs(st,cfg),'runs':rows,'exact_reference':reference})
        for budget in (64,256,1024):
            for method in METHODS:
                subset=[r for c in records if len(c['snapshot']['friendly'])==n for r in c['runs'] if r['budget']==budget and r['method']==method]
                times=sorted(t for r in subset for t in r['wall_ms'])
                summaries.append({'friendly':n,'budget':budget,'method':method,'independent_fixtures':len(subset),
                                  'timing_repeats_per_fixture':3,'wall_ms_median':statistics.median(times),
                                  'wall_ms_p90':times[math.ceil(.9*len(times))-1],'wall_ms_max':max(times),
                                  'min_value':min(r['value'] for r in subset),'max_value':max(r['value'] for r in subset),
                                  'infeasible_count':sum(not r['rechecked_feasible'] for r in subset),
                                  'truncated_count':sum(r['work']['truncated'] for r in subset),
                                  'max_search_calls':max(r['work']['search_model_calls'] for r in subset),
                                  'max_closure_nodes':max(r['work'].get('closure_nodes',0) for r in subset)})
    write('baseline_scale.json',{'cases':records,'summary':summaries,
          'interpretation':'Only 3 generated fixtures per size, 3 timing repeats. No optimality claim for 5/8 actors. Model-call budgets omit option/constraint costs except through end-to-end time; not C# or game-frame timings.'})


def sensitivities():
    base=generated(5,SEED+500)
    mapping={f.uid:f'z{len(base.friendly)-i:02}' for i,f in enumerate(base.friendly)}
    def rename(f): return replace(f,uid=mapping.get(f.uid,f.uid))
    variants={'original':base,'reverse_input_order':replace(base,friendly=tuple(reversed(base.friendly)),enemy=tuple(reversed(base.enemy))),
              'rename_reverse_sort':replace(base,friendly=tuple(rename(f) for f in base.friendly)),
              'translate_rotate_90':replace(base,friendly=tuple(replace(f,x=200-f.y,y=-300+f.x) for f in base.friendly),
                                            enemy=tuple(replace(f,x=200-f.y,y=-300+f.x) for f in base.enemy))}
    cfg=p.Config(expansions=256,width=32,targets=3,options=10,uncertainty=False)
    cases=[]
    for label,st in variants.items():
        cases.append({'id':label,**inputs(st,cfg),'runs':[run(st,cfg,m,3) for m in METHODS]})
    # Required duty and one unauthorized actor must survive ANY bounded cutoff.
    st=complementarity()
    st=replace(st,friendly=(st.friendly[0],replace(st.friendly[1],authorized=False),st.friendly[2]),
               duties=(p.Duty('fixer1','fixer2'),))
    cuts=[]
    for budget in (0,1,2,8,32,128,512):
        cutcfg=replace(cfg,expansions=budget,options=1)
        for method in METHODS:
            row=run(st,cutcfg,method)
            row['budget']=budget
            row['authorized_only']=all(st.own()[a['actor']].authorized for a in row['actions'])
            row['duty_preserved']=any(a['kind']=='protect' and a['actor']=='fixer2' and a['target']=='fixer1' for a in row['actions'])
            cuts.append(row)
    write('baseline_sensitivity.json',{'cases':cases,'rename_map':mapping,
          'budget_cutoff_fixture':inputs(st,replace(cfg,options=1)),'budget_cutoffs':cuts,
          'interpretation':'Renaming changes sorted expansion order under bounded search; reverse inputs should not. Rigid transform preserves geometry. Budget legality checked separately from objective quality.'})


def runtime_costs():
    rows=[]
    for n in (3,5,8):
        st=generated(n,SEED+n*100)
        cfg=p.Config(expansions=64,targets=3,options=10,uncertainty=False)
        opts=p.options(st,cfg)
        current=tuple(next((a for a in opts[f.uid] if a.kind=='probe'),opts[f.uid][0]) for f in st.friendly)
        ev=p.assessment(st,current,cfg)
        assert ev.feasible
        rt=p.Runtime(st.mission,st.domain)
        assert rt.adopt(p.Selection(current,ev,'fixture',{},{}),st,cfg)
        for a in current:
            assert rt.report(a.actor,a.generation,a.authority,rt.revision,1,{'no_response':True},st)
        components={'candidate_generation_ms':[],'current_plan_recheck_ms':[],
                    'feedback_consumption_ms':[],'valid_report_receive_ms':[]}
        for repeat in range(10):
            start=perf_counter_ns();p.options(st,cfg);components['candidate_generation_ms'].append((perf_counter_ns()-start)/1e6)
            start=perf_counter_ns();p.assessment(st,current,cfg,current);components['current_plan_recheck_ms'].append((perf_counter_ns()-start)/1e6)
            start=perf_counter_ns();derived=rt.feedback_snapshot(st);components['feedback_consumption_ms'].append((perf_counter_ns()-start)/1e6)
            a=current[0]
            start=perf_counter_ns();rt.report(a.actor,a.generation,a.authority,rt.revision,repeat+2,{'no_response':True},st)
            components['valid_report_receive_ms'].append((perf_counter_ns()-start)/1e6)
        valid=[run(st,cfg,m,3,current) for m in METHODS]
        revoked=replace(st,friendly=(replace(st.friendly[0],authorized=False),)+st.friendly[1:])
        invalid=[run(revoked,cfg,m,3,current) for m in METHODS]
        rows.append({'friendly':n,**inputs(st,cfg),'current':clean(current),
                     'received_reports':len(rt.reports),'derived_failed_probes':len(derived.failed_probes),
                     'isolated_component_timings':components,'valid_current_runs':valid,
                     'revoked_first_actor_snapshot':clean(revoked),'invalid_current_runs':invalid})
    write('baseline_runtime_costs.json',{'cases':rows,
          'interpretation':'Isolated component microtimings include Python timer overhead. They are diagnostics, not additive to whole-call timings. Every choose includes fallback and current reassessment, outside discretionary cap. Feedback receive/consume are separate lifecycle steps.'})


if __name__=='__main__':
    higher_order();candidates();scale_runs();sensitivities();runtime_costs()

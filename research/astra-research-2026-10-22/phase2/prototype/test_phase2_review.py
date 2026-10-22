"""Independent contract probes. No game outcomes; monkeypatched scores isolate bookkeeping.
Run directly to write an audit JSON. A false safe flag is a reproducible contract gap.
"""
import argparse
import hashlib
import json
from pathlib import Path
from controller import Controller
from temporal import Task, Plan, JointCondition, validate, CommanderLedger, observed_ready


def unit(id, side='A', x=0, **extra):
    return dict(id=id, side=side, x=x, y=0, hp=100., max_hp=100., speed=5.,
                melee=10., ranged=0., ammo=40., armor=0., mounted=False,
                anti_mount=0., generation=1, authority=1, authorized=True,
                damage_dealt=0., suppression=0., **extra)


def commit(u, order, depends=(), start=0):
    return dict(order=dict(actor=u['id'],generation=1,authority=1,**order),start=start,
                start_hp=100.,start_damage=0.,start_xy=(0,0),identity=(1,1),depends=depends)


def temporal_probes():
    units={a:dict(hp=100,speed=5,authorized=True,generation=1,authority=1) for a in ('f','c')}
    p=Plan((Task('fix','f','fix','guard',0,10,strength=20),Task('strike','c','strike','target',2,8)),
           (JointCondition('strike','guard',('fix','fix','missing'),35),))
    errors=validate(p,units)
    try:
        ready=observed_ready(p,'strike',{},3)
        ready_error=None
    except Exception as exc:
        ready=None; ready_error=repr(exc)
    duplicate=dict(validation_errors=errors,observation_exception=ready_error,
                   safe=bool(errors) and ready_error is None)
    reuse=Plan((Task('first','f','move',start=0,end=3),Task('second','f','fire',start=3,end=9)))
    ledger=CommanderLedger(reuse)
    released=[x.id for x in ledger.releasable(4,{})]
    reuse_result=dict(validation_errors=validate(reuse,units),eligible_without_observed_release=released,
                      safe='second' not in released)
    ledger=CommanderLedger(reuse)
    ledger.invalidate({'first'})
    after_failure=[x.id for x in ledger.releasable(4,{})]
    cancelled_resource=dict(eligible_after_cancel_before_release=after_failure,
                            safe='second' not in after_failure)
    # A stale unscoped Boolean release contains no generation/authority/revision evidence.
    scoped=Plan((Task('first','f','move',start=0,end=3,generation=2,authority=2),
                 Task('second','f','fire',start=3,end=9,generation=2,authority=2,after=('first',))))
    ledger=CommanderLedger(scoped)
    try:
        ledger.acknowledge_release('first',True)
        stale=[x.id for x in ledger.releasable(4,{})]
        scope_result=dict(eligible_after_unscoped_bool=stale,safe='second' not in stale)
    except (TypeError, ValueError) as exc:
        scope_result=dict(rejected_unscoped_bool=repr(exc),safe=True)
    return dict(duplicate_missing_contributor=duplicate,implicit_actor_reuse=reuse_result,
                cancelled_resource_reuse=cancelled_resource,
                unscoped_release=scope_result)


def controller_probes():
    cav=unit('c'); cav['mounted']=True
    helper=unit('h',x=1)
    target=unit('t','B',20)
    guard=unit('g','B',21); guard['anti_mount']=1
    obs=dict(t=1,units=[cav,helper,target,guard],duties=[])
    c=Controller('A',keeping=False,budget=40)
    c.commitments['c']=commit(cav,dict(kind='strike',target='t'))
    def score(orders,obs):
        value=100. if orders.get('h',{}).get('kind')=='fix' and orders.get('c',{}).get('target')=='t' else 0.
        return value,[value]*3
    c.forecast=score
    orders=c.decide(obs)
    resulting=c.commitments.get('c',{})
    new_dep=dict(orders=orders,stored_dependencies=resulting.get('depends',()),
                 safe=bool(resulting.get('depends')) and next(o for o in orders if o['actor']=='c')['kind']=='hold')
    c2=Controller('A',mode='single',keeping=False,budget=40)
    c2.commitments['c']=commit(cav,dict(kind='strike',target='t'),depends=('h',))
    c2.commitments['h']=commit(helper,dict(kind='fix',target='g'))
    target['suppression']=.7  # Different enemy suppressed; required guard is not.
    def reassign_score(orders,obs):
        value=100. if orders.get('h',{}).get('kind')=='strike' and orders['h'].get('target')=='t' else 0.
        return value,[value]*3
    c2.forecast=reassign_score
    obs['t']=5; first=c2.decide(obs)
    obs['t']=6; second=c2.decide(obs)
    invalid_dep=dict(first=first,second=second,commitments=c2.commitments,
                     safe=next(o for o in second if o['actor']=='c')['kind']!='strike')
    try:
        neg=Controller('A',keeping=False,budget=-1)
        neg.decide(dict(t=1,units=[helper,target],duties=[]))
        budget=dict(evaluations=neg.work[-1]['evaluation'],safe=neg.work[-1]['evaluation']<=1)
    except ValueError as exc:
        budget=dict(rejected=repr(exc),safe=True)
    stalled=Controller('A',budget=0)
    archer=unit('a'); archer.update(ranged=5, x=10)
    far=unit('z','B',100)
    stalled.commitments['a']=commit(archer,dict(kind='advance_fire',target='z',x=60,y=0))
    stalled.last_observation=dict(t=99,units=[dict(archer),dict(far)])
    affected=stalled.update(dict(t=100,units=[archer,far]))
    no_recent_progress=dict(affected=sorted(affected),still_committed='a' in stalled.commitments,
                           safe='a' not in stalled.commitments)
    remaining=Controller('A',budget=0)
    archer=unit('a'); archer.update(ranged=5,phase='attacking')
    near=unit('z','B',30)
    order=dict(actor='a',kind='fire',target='z',generation=1,authority=1)
    warm_obs=dict(t=10,units=[{**archer,'current_order':order},near])
    cold_obs=dict(t=10,units=[{**archer,'phase':'holding','current_order':dict(actor='a',kind='hold')},near])
    warm=remaining.forecast({'a':order},warm_obs)[0]
    cold=remaining.forecast({'a':order},cold_obs)[0]
    remaining_cost=dict(warm_fire_value=warm,cold_fire_value=cold,
                        safe=warm>cold)
    algebra=Controller('A',keeping=True,budget=40)
    a,b,e=unit('a'),unit('b',x=1),unit('e','B',20)
    algebra.commitments['a']=commit(a,dict(kind='strike',target='e'))
    def accounting_score(orders,obs):
        ka,kb=orders['a']['kind'],orders['b']['kind']
        value={('strike','hold'):10.,('hold','hold'):0.,
               ('strike','strike'):20.,('hold','strike'):20.5}[(ka,kb)]
        return value,[value]*3
    algebra.forecast=accounting_score
    actual=algebra.decide(dict(t=10,units=[a,b,e],duties=[]))
    chosen={o['actor']:o for o in actual}
    raw=accounting_score(chosen,None)[0]
    changes=int(chosen['a']['kind']!='strike')+int(chosen['b']['kind']!='hold')
    adjusted=raw-.8*changes
    switch_accounting=dict(orders=actual,raw=raw,global_switches=changes,global_adjusted=adjusted,
                           available_both_strike_adjusted=19.2,safe=adjusted>=19.2-1e-9)
    return dict(new_dependency_same_action=new_dep,repurposed_helper=invalid_dep,
                negative_budget=budget,stalled_after_initial_progress=no_recent_progress,
                observed_setup_remaining_cost=remaining_cost,switch_cost_same_reference=switch_accounting)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--output',type=Path,default=Path(__file__).resolve().parents[1]/'results/review_initial.json')
    args=parser.parse_args()
    base=Path(__file__).parent
    out=dict(evidence='SYNTHETIC_CONTRACT_PROBES_NOT_GAME',
             files={p:hashlib.sha256((base/p).read_bytes()).hexdigest() for p in ('temporal.py','controller.py')},
             temporal=temporal_probes(),controller=controller_probes())
    args.output.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
    print(args.output)
    for kind in ('temporal','controller'):
        for name,result in out[kind].items():
            print(kind,name,'SAFE' if result['safe'] else 'GAP')


if __name__=='__main__': main()

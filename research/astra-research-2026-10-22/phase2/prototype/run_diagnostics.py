"""Targeted policy/model diagnostics, separate from random performance samples."""
import json, statistics
from copy import deepcopy
from pathlib import Path
from world import World, reactive_orders, VARIANTS
from controller import Controller, dist
from run_dynamic import episode,scenario,controller

ROOT=Path(__file__).resolve().parents[1]


def active_case():
    return {'units':[
        dict(id='archer',side=0,x=0,y=0,hp=100,max_hp=100,speed=6,melee=1,ranged=8,ammo=30,armor=.3,generation=1,authority=1),
        dict(id='enemy',side=1,x=70,y=0,hp=100,max_hp=100,speed=3,melee=4,ranged=0,ammo=0,armor=.3,generation=1,authority=1)]}


def execute_candidate(initial,orders,variant,opponent):
    w=World(initial,variant); state={}; rows=[]
    for t in range(16):
        own=w.observe(0); units={u['id']:u for u in own['units']}; issued=[]
        for original in orders.values():
            o=dict(original); u=units[o['actor']]; e=units.get(o.get('target'))
            if u['hp']<=0: continue
            if o['kind']=='advance_fire': o['kind']='fire' if e and dist(u,e)<=55 else 'move'
            issued.append(o)
        w.issue(0,issued)
        if t%3==0: w.issue(1,reactive_orders(w.observe(1),opponent,state))
        w.step(1)
    m=w.metrics(0)
    return dict(actual_net=m['damage_dealt']-m['damage_received'],actual_enemy_loss=m['damage_dealt'],actual_own_loss=m['damage_received'])


def main():
    root=ROOT/'results'; root.mkdir(exist_ok=True)
    active=[]
    for variant in ('nominal','slow_shared','sticky_exit'):
        for opponent in ('hold','counter','withdraw','feint'):
            for method in ('staged','no_active','simple'):
                row=episode(active_case(),method,opponent,variant,full_trace=True)
                row['initial']=active_case(); active.append(row)
    (root/'active_cases.json').write_text(json.dumps(active))
    # Independent 16s outcomes for the SAME finite action alternatives; no tuning from these results.
    challenges=[]
    for seed in range(2100,2108):
        initial=scenario(seed); w=World(initial); obs=w.observe(0); c=Controller(0)
        own=[u for u in obs['units'] if u['side']==0]; foes=[u for u in obs['units'] if u['side']==1]
        holds={u['id']:{'actor':u['id'],'kind':'hold','generation':1,'authority':1} for u in own}
        options=[holds]
        for u in own:
            options.extend({**holds,u['id']:o} for o in c.candidate_options(u,foes)[1:])
        predictions=[c.forecast(o,obs)[0] for o in options]
        selected=max(range(len(options)),key=lambda i:predictions[i])
        for variant in VARIANTS:
            for opponent in ('hold','counter'):
                actual=[execute_candidate(initial,o,variant,opponent) for o in options]
                best=max(a['actual_net'] for a in actual)
                challenges.append(dict(seed=seed,variant=variant,opponent=opponent,
                    candidates=options,predictions=predictions,actual=actual,selected=selected,
                    realized_regret=best-actual[selected]['actual_net'],
                    estimated_benefit=predictions[selected]-predictions[0],
                    realized_benefit=actual[selected]['actual_net']-actual[0]['actual_net']))
    (root/'model_challenges.json').write_text(json.dumps(challenges))
    # Decision budget includes mandatory current/coverage even at zero discretionary budget.
    checks=[]
    for n in (3,5,8):
        s=scenario(2100,n); obs=World(s).observe(0)
        for budget in (0,1,5,20,80):
            c=Controller(0,budget=budget); orders=c.decide(obs); w=World(s); receipts=w.issue(0,orders)
            checks.append(dict(n=n,budget=budget,orders=orders,work=c.work[-1],receipts=receipts))
            assert c.work[-1]['evaluation']<=budget+1
            assert len({o['actor'] for o in orders})==len(orders)
    (root/'dynamic_budget_checks.json').write_text(json.dumps(checks,indent=2))
    # Equal geometry transforms and renaming under bounded search; legality and quality are separate.
    transforms=[]
    for seed in range(2110,2113):
        initial=scenario(seed,5)
        renamed=deepcopy(initial); mapping={u['id']:f'z{len(initial['units'])-i}' for i,u in enumerate(initial['units'])}
        for u in renamed['units']: u['id']=mapping[u['id']]
        for d in renamed['duties']:
            d['owner']=mapping[d['owner']];d['asset']=mapping[d['asset']]
        reversed_case=deepcopy(initial);reversed_case['units'].reverse()
        translated=deepcopy(initial)
        for u in translated['units']:u['x']+=20;u['y']-=10
        translated['bounds']=[-140,180,-130,110]
        for label,case in [('original',initial),('renamed',renamed),('reversed',reversed_case),('translated',translated)]:
            row=episode(case,'staged','counter',budget=20)
            transforms.append(dict(seed=seed,transform=label,net=row['metrics']['net_damage'],orders_rejected=row['metrics']['orders_rejected']))
    (root/'dynamic_transform.json').write_text(json.dumps(transforms,indent=2))
    summary=dict(active_examples=[dict(method=r['method'],variant=r['variant'],opponent=r['opponent'],net=r['metrics']['net_damage'],issued=r['issued_kinds'],progress=r['controller']['progress']) for r in active],
        model_challenge_count=len(challenges),positive_regret=sum(r['realized_regret']>1e-7 for r in challenges),
        max_regret=max(r['realized_regret'] for r in challenges),
        misleading_positive=sum(r['estimated_benefit']>1e-7 and r['realized_benefit']< -1e-7 for r in challenges),
        budget_cases=len(checks),transforms=transforms)
    (root/'diagnostics_summary.json').write_text(json.dumps(summary,indent=2))
    print(json.dumps({k:v for k,v in summary.items() if k not in ('active_examples','transforms')},indent=2))


if __name__=='__main__': main()

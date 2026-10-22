"""Paired incremental research experiments. Run --suite dev before frozen holdout.
Execution World never receives method names and never calls planner.forecast.
"""
import argparse, json, random, statistics, hashlib, time
from pathlib import Path
from copy import deepcopy
from collections import defaultdict
from world import World, reactive_orders, VARIANTS
from controller import Controller

ROOT=Path(__file__).resolve().parents[1]
RESULTS=ROOT/'results'


def scenario(seed,n=3):
    r=random.Random(seed); units=[]
    for side in (0,1):
        for i in range(n):
            role=i%3; x=(-50 if side==0 else 50)+r.uniform(-8,8)
            y=(i-(n-1)/2)*19+r.uniform(-4,4)
            units.append(dict(id=f'{side}_{i}',side=side,x=x,y=y,hp=100,max_hp=100,
                speed=7 if role==0 else 4,melee=(7 if role==0 else 6 if role==1 else 1.5),
                ranged=6 if role==2 else 0,ammo=35,armor=r.uniform(.2,.5),mounted=role==0,
                anti_mount=.8 if role==1 else 0,generation=1,authority=1,authorized=True,role=('cavalry','line','ranged')[role]))
    # Duty is a geometry fixture and not a claim of actual game protection.
    duties=[dict(asset='0_2',owner='0_1',coverage_radius=38)] if n>=3 and seed%4==0 else []
    return dict(units=units,duties=duties,bounds=[-160,160,-120,120],events=[])


def controller(side,method,budget=80):
    options={
        'phase1':dict(mode='phase1'),
        'simple':dict(mode='simple',active=False),
        'staged':dict(mode='staged'),
        'no_active':dict(mode='staged',active=False),
        'no_closure':dict(mode='simple',active=True),
        'single_window':dict(mode='staged',sequential=False),
        'full_replan':dict(mode='staged',local_repair=False),
        'no_keep':dict(mode='staged',keeping=False),
        'no_memory':dict(mode='staged',memory=False),
        'maximin':dict(mode='staged',risk='maximin'),
    }
    return Controller(side,budget=budget,**options[method])


def episode(initial,method='staged',opponent='support',variant='nominal',offset=0,delay=0,duration=48,budget=80,full_trace=False):
    world=World(deepcopy(initial),variant)
    left=controller(0,method,budget)
    right=controller(1,opponent.split(':')[1],budget) if opponent.startswith('planner:') else None
    reaction={}; frames=[]; stalled=0; max_stall=0; last_hp=sum(u['hp'] for u in world.units.values())
    starting=dict(world.metrics(0)); executed=[]; started=time.perf_counter()
    for tick in range(duration):
        if tick%3==0:
            orders=left.decide(world.observe(0))
            for o in orders: o['delay']=delay
            world.issue(0,orders)
        if tick%3==offset%3:
            obs=world.observe(1)
            orders=right.decide(obs) if right else reactive_orders(obs,opponent,reaction)
            for o in orders: o['delay']=delay
            world.issue(1,orders)
        events=world.step(1.)
        hp=sum(u['hp'] for u in world.units.values())
        moving=any(u.get('moving') for u in world.units.values() if u['hp']>0)
        both_alive=all(any(u['side']==s and u['hp']>0 for u in world.units.values()) for s in (0,1))
        if both_alive and last_hp-hp<.01 and not moving: stalled+=1
        else: stalled=0
        max_stall=max(max_stall,stalled); last_hp=hp
        if full_trace:
            frames.append(dict(t=world.t,units=[{k:u.get(k) for k in ('id','side','x','y','hp','ammo','phase','suppression','authority','authorized')} for u in world.units.values()],events=events))
        executed.extend(e for e in events if e['type'] in ('damage','fix_effect','authority_changed','order_rejected','order_completed'))
    metrics=world.metrics(0); other=world.metrics(1); cm=left.metrics()
    metrics.update(net_damage=metrics['damage_dealt']-metrics['damage_received'],max_stall_seconds=max_stall)
    kinds=defaultdict(int)
    for row in left.trace:
        for order in row['orders']: kinds[order['kind']]+=1
    fixes=[e for e in executed if e['type']=='fix_effect' and e.get('effective')]
    out=dict(method=method,opponent=opponent,variant=variant,offset=offset,delay=delay,
             metrics=metrics,opponent_metrics=other,controller={k:v for k,v in cm.items() if k!='work'},
             issued_kinds=dict(kinds),effective_fix_events=len(fixes),decision_work=cm['work'],
             episode_ms=(time.perf_counter()-started)*1000)
    if full_trace: out.update(frames=frames,decisions=left.trace,right_decisions=right.trace if right else [])
    return out


def summarize(rows):
    groups=defaultdict(list)
    for row in rows: groups[(row['variant'],row['opponent'],row['method'],row['n'])].append(row)
    stats=[]
    for key,rs in sorted(groups.items()):
        vals=[r['metrics']['net_damage'] for r in rs]
        stats.append(dict(variant=key[0],opponent=key[1],method=key[2],n=key[3],count=len(rs),
            mean_net=statistics.mean(vals),median_net=statistics.median(vals),min_net=min(vals),max_net=max(vals),
            mean_own_loss=statistics.mean(r['metrics']['damage_received'] for r in rs),
            mean_enemy_loss=statistics.mean(r['metrics']['damage_dealt'] for r in rs),
            mean_cancel=statistics.mean(r['controller']['cancellations'] for r in rs),
            mean_stall=statistics.mean(r['metrics']['max_stall_seconds'] for r in rs),
            mean_decision_ms=statistics.mean(w['total_ms'] for r in rs for w in r['decision_work']),
            max_decision_ms=max(w['total_ms'] for r in rs for w in r['decision_work'])))
    pair=defaultdict(dict)
    for r in rows: pair[(r['seed'],r['n'],r['variant'],r['opponent'],r['offset'],r['delay'])][r['method']]=r
    differences=defaultdict(list)
    for key,rs in pair.items():
        if 'staged' not in rs: continue
        for method,r in rs.items():
            if method!='staged': differences[(key[2],key[3],key[1],method)].append(rs['staged']['metrics']['net_damage']-r['metrics']['net_damage'])
    pairs=[]
    for key,ds in sorted(differences.items()):
        # Bootstrap seeds/initial states, never frames or deterministic repeated copies.
        rng=random.Random(9901); boots=sorted(statistics.mean(rng.choices(ds,k=len(ds))) for _ in range(800))
        pairs.append(dict(variant=key[0],opponent=key[1],n=key[2],baseline=key[3],count=len(ds),
            mean_delta=statistics.mean(ds),min_delta=min(ds),max_delta=max(ds),
            positive=sum(d>1e-6 for d in ds),negative=sum(d< -1e-6 for d in ds),
            bootstrap_90_descriptive=[boots[40],boots[759]]))
    return dict(groups=stats,paired=pairs,scope='Deterministic synthetic scenarios; intervals describe sampled initial states only, not game generalization')


def run(suite):
    RESULTS.mkdir(exist_ok=True)
    if suite=='smoke': configs=[(2101,3,v,o,m) for v in ('nominal','weak_fix') for o in ('hold','support') for m in ('phase1','simple','staged')]
    elif suite=='dev': configs=[(seed,3,v,o,m) for seed in range(2100,2108) for v in ('nominal','slow_shared','weak_fix') for o in ('hold','counter') for m in ('phase1','simple','staged')]
    elif suite=='holdout': configs=[(seed,3,v,o,m) for seed in range(7300,7312) for v in VARIANTS for o in ('hold','support','counter','withdraw') for m in ('phase1','simple','staged')]
    elif suite=='confirm': configs=[(seed,3,v,o,m) for seed in range(8300,8312) for v in VARIANTS for o in ('hold','support','counter','withdraw') for m in ('phase1','simple','staged')]
    elif suite=='final': configs=[(seed,3,v,o,m) for seed in range(9300,9312) for v in VARIANTS for o in ('hold','support','counter','withdraw') for m in ('phase1','simple','staged')]
    elif suite=='ablations': configs=[(seed,3,v,o,m) for seed in range(7400,7406) for v in ('nominal','weak_fix') for o in ('support','counter') for m in ('staged','no_active','no_closure','single_window','full_replan','no_keep','no_memory','maximin')]
    elif suite=='scale': configs=[(seed,n,v,'support',m) for seed in range(7500,7504) for n in (3,5,8) for v in ('nominal','slow_shared') for m in ('simple','staged')]
    elif suite=='bilateral': configs=[(seed,3,v,o,m) for seed in range(7600,7604) for v in ('nominal','sticky_exit') for o in ('planner:staged','planner:simple','feint') for m in ('simple','staged')]
    else: raise ValueError(suite)
    identities={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (Path(__file__).parent/'controller.py',Path(__file__).parent/'world.py',Path(__file__))}
    (RESULTS/f'{suite}_identity.json').write_text(json.dumps(identities,indent=2))
    rows=[]; traces=[]
    path=RESULTS/f'{suite}_raw.jsonl'
    with path.open('w') as output:
        for i,(seed,n,variant,opponent,method) in enumerate(configs):
            initial=scenario(seed,n); offset=seed%3 if suite=='bilateral' else 0; delay=seed%2 if suite=='bilateral' else 0
            traced=(suite=='smoke' or (seed==configs[0][0] and n==3))
            row=episode(initial,method,opponent,variant,offset,delay,full_trace=traced)
            row.update(seed=seed,n=n)
            if traced:
                traces.append({**row,'initial':initial})
                row={k:v for k,v in row.items() if k not in ('frames','decisions','right_decisions')}
            rows.append(row); output.write(json.dumps(row)+'\n'); output.flush()
            if (i+1)%50==0: print(suite,i+1,'/',len(configs),flush=True)
    (RESULTS/f'{suite}_summary.json').write_text(json.dumps(summarize(rows),indent=2))
    (RESULTS/f'{suite}_traces.json').write_text(json.dumps(traces))
    print(json.dumps(dict(suite=suite,runs=len(rows),sources=identities,raw=str(path))))

if __name__=='__main__':
    ap=argparse.ArgumentParser(); ap.add_argument('--suite',default='smoke'); run(ap.parse_args().suite)

"""Short-horizon Commander research controller; sees only World.observe output.
Estimates use fixed declared assumptions, independently of execution coefficients.
No exploration reward, hidden policy reads, or algorithm-name access in the world.
"""
from copy import deepcopy
from math import hypot
from time import perf_counter
from itertools import combinations


def dist(a,b): return hypot(a['x']-b['x'],a['y']-b['y'])
def alive(u): return u['hp']>0 and u.get('authorized',True)
def sig(o): return (o.get('kind','hold'),o.get('target',''),round(o.get('x',0),2),round(o.get('y',0),2))
def hold(u): return {'actor':u['id'],'kind':'hold','generation':u.get('generation',1),'authority':u.get('authority',1)}


class Controller:
    def __init__(self,side,mode='staged',active=True,local_repair=True,memory=True,
                 keeping=True,risk='balanced',budget=120,sequential=True):
        if budget<0: raise ValueError('budget must be nonnegative')
        self.side=side; self.mode=mode; self.active=active; self.local_repair=local_repair
        self.memory=memory; self.keeping=keeping; self.risk=risk; self.budget=budget
        self.sequential=sequential
        self.commitments={}; self.failed={}; self.last_t=-1e9; self.trace=[]; self.work=[]
        self.cancellations=0; self.progress=0; self.failed_retries=0; self.repair_count=0
        self.gated=0; self.pressure_loss=0.; self.loss_stops=0; self.model_alarm=0
        self.last_observation=None; self.current_phase1=(); self.model_scale=1.
        self.pressure_spent={}; self.exits={}

    def fingerprint(self,u,e):
        # Time and plan revision deliberately absent. Genuine geometry/membership change permits retry.
        return (u['id'],e['id'],u.get('generation',1),u.get('authority',1),e.get('generation',1),
                int(e['x']//20),int(e['y']//20),int(e['hp']//25),bool(u.get('ammo',0)>0))

    def update(self,obs):
        t=obs['t']; units={u['id']:u for u in obs['units']}; affected=set()
        before={u['id']:u for u in self.last_observation['units']} if self.last_observation else {}
        for actor,order in list(self.exits.items()):
            u=units.get(actor)
            if (not u or not alive(u) or order['authority']!=u.get('authority',1)
                or hypot(u['x']-order['x'],u['y']-order['y'])<3): self.exits.pop(actor,None)
        for actor,c in list(self.commitments.items()):
            u=units.get(actor); e=units.get(c['order'].get('target'))
            if (not u or not alive(u) or (u.get('generation',1),u.get('authority',1))!=c['identity']
                or (c['order'].get('target') and (not e or e['hp']<=0))):
                affected.add(actor); continue
            o=c['order']
            old_u=before.get(actor);old_e=before.get(o.get('target'))
            made_progress=(u.get('damage_dealt',0)> (old_u or u).get('damage_dealt',0)+.1)
            if e and old_e and old_u: made_progress |= dist(old_u,old_e)-dist(u,e)>.5
            if o['kind']=='fix' and e: made_progress |= e.get('suppression',0)>.45
            if made_progress: c['last_progress']=t
            if o['kind']=='advance_fire' and e and dist(u,e)<=55:
                c['order']={**o,'kind':'fire'}; c['released']=True; self.progress+=1
            if o['kind']=='pressure':
                increment=max(0.,before.get(actor,u)['hp']-u['hp'])
                self.pressure_spent[actor]=self.pressure_spent.get(actor,0)+increment
                self.pressure_loss+=increment
                lost=max(0.,c['start_hp']-u['hp']); gain=u.get('damage_dealt',0)-c['start_damage']
                # Bounded total exposure, even if repeated small updates occur.
                if self.pressure_spent[actor]>0.12*u.get('max_hp',100) or (t-c['start']>=8 and gain<1):
                    if e and self.memory: self.failed[self.fingerprint(u,e)]=dict(reason='cost_or_no_effect',time=t)
                    self.exits[actor]={**hold(u),'kind':'retreat','x':c['start_xy'][0],'y':c['start_xy'][1]}
                    self.loss_stops+=1; affected.add(actor)
            if t-c.get('last_progress',c['start'])>18 and o['kind'] in ('advance_fire','fix','strike'):
                if not made_progress:
                    if e and self.memory: self.failed[self.fingerprint(u,e)]=dict(reason='no_progress',time=t)
                    affected.add(actor); self.model_alarm+=1
        if self.last_observation:
            # Observable failure of effective control: stop trusting the forecast and narrow commitments.
            for u in obs['units']:
                old=before.get(u['id'])
                if old and u['side']==self.side and old['hp']-u['hp']>15:
                    self.model_alarm+=1; self.model_scale=max(.4,self.model_scale*.8)
        if affected:
            self.repair_count+=1
            if not self.local_repair: affected=set(self.commitments)
            else:
                changed=True
                while changed:
                    changed=False
                    for actor,c in self.commitments.items():
                        if actor not in affected and any(x in affected for x in c.get('depends',())):
                            affected.add(actor); changed=True
            for actor in affected:
                if actor in self.commitments: del self.commitments[actor]; self.cancellations+=1
        self.last_observation=deepcopy(obs)
        return affected

    def forecast(self,orders,obs):
        """Finite coherent reactions. Each enemy output/support is allocated once per branch.
        Direct effects and reachable follow-up are integrated in one 16-second horizon.
        This is a proxy; there is no positive terminal bonus or summed marginal-credit term.
        """
        own={u['id']:u for u in obs['units'] if u['side']==self.side and alive(u)}
        foes={u['id']:u for u in obs['units'] if u['side']!=self.side and u['hp']>0}
        H=16.; outcomes=[]; conditionally_fixed={}
        for actor,o in orders.items():
            if o['kind']=='fix' and o.get('target') in foes:
                u=own[actor]; e=foes[o['target']]
                arrive=max(0.,dist(u,e)-6)/max(.1,u['speed'])+1.5
                conditionally_fixed.setdefault(e['id'],[]).append((arrive,u['melee']*u['hp']/u['max_hp']))
        for reaction in ('hold','support','withdraw'):
            damage={e:0. for e in foes}; projected={}; exposure={a:0. for a in own}
            for actor,o in orders.items():
                u=own[actor]; e=foes.get(o.get('target')); kind=o['kind']; pos=dict(u)
                if kind in ('advance_fire','move','retreat'):
                    pos.update(x=o.get('x',u['x']),y=o.get('y',u['y']))
                elif e and kind in ('fix','strike','pressure'): pos.update(x=e['x'],y=e['y'])
                projected[actor]=pos
                if not e or kind in ('hold','move','retreat'): continue
                if kind in ('fire','advance_fire','pressure'):
                    reach=50.; rate=u['ranged']*min(1.,u.get('ammo',0)/max(1,H/2))
                    if kind=='pressure': rate*=.7; reach=42.
                else: reach=6.; rate=u['melee']*(.35 if kind=='fix' else 1.)
                observed_order=u.get('current_order',{})
                already_ready=(observed_order.get('kind')==kind and observed_order.get('target')==o.get('target')
                               and u.get('phase') not in ('preparing','exiting','dead','released') and not u.get('pending'))
                setup=0. if already_ready else 1.5
                eta=max(0,dist(u,e)-reach)/max(.1,u['speed'])+setup
                if kind=='fire' and dist(u,e)>55: rate=0
                if kind=='advance_fire': eta=dist(u,pos)/max(.1,u['speed'])+1.5
                if reaction=='withdraw': eta*=1.4
                duration=max(0,H-eta)
                hit=rate*u['hp']/u['max_hp']*duration/(1+e.get('armor',0))
                damage[e['id']]+=hit*self.model_scale
                exposure[actor]=duration
            # Resource-conserving opponent: one target per enemy in each coherent branch.
            incoming={a:0. for a in own}
            for enemy in foes.values():
                if not projected: continue
                actor=min(projected,key=lambda a:dist(enemy,projected[a]))
                u=own[actor]; d=dist(enemy,projected[actor]); reach=55 if enemy['ranged']>0 else 6
                eta=max(0,d-reach)/max(.1,enemy['speed'])+1.5
                if reaction=='hold' and d>reach: continue
                if reaction=='withdraw': continue
                fixes=conditionally_fixed.get(enemy['id'],[])
                ready=[x for x in fixes if x[0]<=H/2]
                if sum(x[1] for x in ready)>=.65*enemy['melee']:
                    # Predicted reduction, separately gated by actual effect at execution.
                    eta+=H*.5
                rate=max(enemy['melee'],enemy['ranged'])
                incoming[actor]+=rate*enemy['hp']/enemy['max_hp']*max(0,H-eta)/(1+u.get('armor',0))
            # Damage to each enemy saturates at its actual current resource.
            reward=sum(min(foes[k]['hp'],v) for k,v in damage.items())
            own_loss=sum(min(own[a]['hp'],amount) for a,amount in incoming.items())
            outcomes.append(reward-own_loss)
        value=min(outcomes) if self.risk=='maximin' else sum(outcomes)/len(outcomes)
        return value,outcomes

    def candidate_options(self,u,foes):
        opts=[hold(u)]
        if not foes: return opts
        for e in sorted(foes,key=lambda e:dist(u,e))[:3]:
            if self.memory and self.fingerprint(u,e) in self.failed: continue
            base={**hold(u),'target':e['id']}
            if u.get('ranged',0)>0 and u.get('ammo',0)>0:
                if dist(u,e)<=55: opts.append({**base,'kind':'fire'})
                elif self.active:
                    d=dist(u,e); ratio=max(0.,(d-44)/max(d,.1))
                    opts.append({**base,'kind':'advance_fire' if self.sequential else 'move','x':u['x']+(e['x']-u['x'])*ratio,
                                 'y':u['y']+(e['y']-u['y'])*ratio})
                if self.active and dist(u,e)<80 and self.pressure_spent.get(u['id'],0)<=.12*u['max_hp']:
                    opts.append({**base,'kind':'pressure'})
            if u.get('melee',0)>0: opts.append({**base,'kind':'strike'})
        return opts

    def choose_phase1(self,obs):
        from baseline import planner as p
        units=[]
        for u in obs['units']:
            if u['hp']<=0: continue
            g=p.Group(max(1,round(u['hp'])),u['melee']/10,u['ranged']/10,u.get('armor',0),u.get('anti_mount',0),u.get('mounted',False))
            f=p.Formation(u['id'],u['x'],u['y'],u['speed'],(g,),generation=u.get('generation',1),authority=u.get('authority',1),authorized=u.get('authorized',True),ammo=min(1,u.get('ammo',0)/40))
            units.append((u['side'],f))
        duties=tuple(p.Duty(d['asset'],d['owner']) for d in obs.get('duties',[]) if any(side==self.side and f.uid==d.get('owner') for side,f in units))
        st=p.Snapshot(tuple(f for side,f in units if side==self.side),tuple(f for side,f in units if side!=self.side),duties=duties,now=obs['t'])
        # Capability adapter: this world has ranged pressure only. Filter before selection;
        # never silently execute a melee probe as a non-damaging ranged order.
        # This script is single-threaded; production should pass a candidate provider explicitly.
        original_options=p.options
        def executable_options(snapshot,cfg):
            generated=original_options(snapshot,cfg); friendly=snapshot.own()
            return {actor:[a for a in actions if a.kind!='probe' or friendly[actor].cap('ranged')>0]
                    for actor,actions in generated.items()}
        p.options=executable_options
        try:
            choice=p.choose(st,p.Config(expansions=self.budget,options=10,targets=2),self.current_phase1,method='greedy')
        finally:
            p.options=original_options
        self.current_phase1=choice.actions
        orders={a.actor:dict(actor=a.actor,kind={'shoot':'fire','probe':'pressure','protect':'hold'}.get(a.kind,a.kind),target=a.target,generation=a.generation,authority=a.authority) for a in choice.actions}
        for order in orders.values():
            if order['kind']=='hold' or not order.get('target'): order.pop('target',None)
        return orders,choice.work,choice.assessment.dependencies

    def decide(self,obs):
        started=perf_counter(); affected=self.update(obs); t=obs['t']
        # Input-array permutation must not decide which actor gets the finite search budget.
        ordered=sorted(obs['units'],key=lambda u:(u['x'],u['y'],u.get('role',''),u['id']))
        own={u['id']:u for u in ordered if u['side']==self.side and alive(u)}
        foes=[u for u in ordered if u['side']!=self.side and u['hp']>0]
        allunits={u['id']:u for u in obs['units']}; work={'generation':0,'constraints':0,'evaluation':0,'feedback_units':len(obs['units']),'current_review':1}
        base={a:hold(u) for a,u in own.items()}; duties=set()
        for d in obs.get('duties',[]):
            if d.get('owner') in own and d.get('asset') in allunits:
                actor=d['owner']; asset=allunits[d['asset']]; duties.add(actor)
                base[actor]={**hold(own[actor]),'kind':'move','x':asset['x'],'y':asset['y']}
        for a,c in self.commitments.items():
            if a in own and a not in duties: base[a]=dict(c['order'])
        for a,order in self.exits.items():
            if a in own and a not in duties: base[a]=dict(order)
        depends={}
        if self.mode=='phase1':
            selected,extra,depends=self.choose_phase1(obs); selected.update({a:base[a] for a in duties}); work['phase1']=extra
            value=0.; outcomes=[]
        else:
            value,outcomes=self.forecast(base,obs); work['evaluation']+=1; selected=deepcopy(base)
            # No expiry reward. Commit preparation for a short interval, but hard failures already bypassed it.
            locked={a for a,c in self.commitments.items() if self.keeping and t-c['start']<3 and a not in affected}
            options={a:self.candidate_options(u,foes) for a,u in own.items() if a not in duties|locked|set(self.exits)}
            work['generation']=sum(map(len,options.values()))
            candidates=[]
            for a,opts in options.items():
                for o in opts:
                    cand={**base,a:o}; candidates.append((cand,{}))
                    if self.mode!='staged' or o['kind']!='strike' or not own[a].get('mounted',False): continue
                    target=allunits[o['target']]
                    guards=[e for e in foes if e['id']!=target['id'] and e.get('anti_mount',0)>0 and dist(e,target)<45]
                    bundle=dict(cand); used={a}|duties|locked; helpers=[]; feasible=True
                    for guard in guards:
                        strength=0.
                        for helper in sorted((u for uid,u in own.items() if uid not in used),key=lambda u:dist(u,guard)):
                            used.add(helper['id']); helpers.append(helper['id']); strength+=helper['melee']*helper['hp']/helper['max_hp']
                            bundle[helper['id']]={**hold(helper),'kind':'fix','target':guard['id']}
                            if strength>=.65*guard['melee']: break
                        if strength<.65*guard['melee']: feasible=False; break
                    if guards and feasible: candidates.append((bundle,{a:helpers}))
            # Count all candidates constructed, including unscored tail; evaluation budget is explicit.
            work['candidate_bundles']=len(candidates)
            for cand,deps in candidates[:self.budget]:
                work['constraints']+=len(cand)
                if any(a not in own for a in cand): continue
                score,vec=self.forecast(cand,obs); work['evaluation']+=1
                switch=sum(sig(cand[a])!=sig(base[a]) for a in cand)
                adjusted=score-(.8*switch if self.keeping else 0)
                if adjusted>value+1e-9:
                    value=adjusted; outcomes=vec; selected=cand; depends=deps
            # One second pass of independent additions, using same remaining total evaluation budget.
            for a,opts in options.items():
                if a in {h for hs in depends.values() for h in hs} or a in depends: continue
                for o in opts:
                    if work['evaluation']>=self.budget+1: break
                    cand={**selected,a:o}; score,vec=self.forecast(cand,obs); work['evaluation']+=1; work['constraints']+=len(cand)
                    total_switches=sum(sig(cand[x])!=sig(base[x]) for x in cand)
                    adjusted=score-(.8*total_switches if self.keeping else 0)
                    if adjusted>value+1e-9:
                        selected=cand; value=adjusted; outcomes=vec
        issued=[]
        for a,o in selected.items():
            u=own[a]; previous=self.commitments.get(a)
            new_dependencies=tuple(depends.get(a,previous.get('depends',()) if previous and sig(previous['order'])==sig(o) else ()))
            dependency_changed=previous is not None and tuple(previous.get('depends',()))!=new_dependencies
            if not previous or sig(previous['order'])!=sig(o) or dependency_changed:
                if previous: self.cancellations+=1
                self.commitments[a]=dict(order=dict(o),start=t,start_hp=u['hp'],start_damage=u.get('damage_dealt',0),
                                         start_xy=(u['x'],u['y']),last_progress=t,identity=(u.get('generation',1),u.get('authority',1)),depends=new_dependencies,
                                         dependency_targets={h:selected[h].get('target') for h in new_dependencies if h in selected})
            c=self.commitments[a]
            command=dict(c['order'])
            if command['kind']=='advance_fire': command['kind']='move'
            dependencies=c.get('depends',())
            if command['kind']=='strike' and dependencies:
                required=c.get('dependency_targets',{})
                targets=[required.get(h) for h in dependencies]
                matching=all(h in selected and selected[h].get('kind')=='fix' and selected[h].get('target')==required.get(h) for h in dependencies)
                if not matching or len(targets)<len(dependencies) or any(allunits.get(x,{}).get('suppression',0)<.45 for x in targets):
                    command=hold(u); self.gated+=1
                    if t-c['start']>10:
                        e=allunits.get(o.get('target'))
                        if e and self.memory: self.failed[self.fingerprint(u,e)]=dict(reason='effect_not_observed',time=t)
                        self.commitments.pop(a,None); self.cancellations+=1; self.model_alarm+=1
            issued.append(command)
        work['selection_actors']=len(selected); work['total_ms']=(perf_counter()-started)*1000
        self.work.append(work)
        self.trace.append(dict(t=t,orders=deepcopy(issued),value=value,outcomes=outcomes,
                               affected=sorted(affected),commitments=deepcopy(self.commitments),
                               failures=len(self.failed),model_scale=self.model_scale))
        self.last_t=t
        return issued

    def metrics(self):
        return dict(cancellations=self.cancellations,progress=self.progress,local_repairs=self.repair_count,
                    effect_gate_waits=self.gated,pressure_loss=self.pressure_loss,cost_stops=self.loss_stops,
                    remembered_failures=len(self.failed),model_alarms=self.model_alarm,
                    decision_calls=len(self.work),total_ms=sum(w['total_ms'] for w in self.work),
                    work=self.work)

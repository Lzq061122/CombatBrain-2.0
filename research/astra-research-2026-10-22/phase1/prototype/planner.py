"""Independent research prototype. No Bannerlord API or combat simulator.
Units: metres, seconds, abstract capability units. All coefficients are assumptions.
"""
from __future__ import annotations
from dataclasses import dataclass, field, asdict, replace
from itertools import product
from math import hypot, isfinite
import json


@dataclass(frozen=True)
class Group:
    n: int
    melee: float = 0.5
    ranged: float = 0.0
    armour: float = 0.5
    anti_mount: float = 0.0
    mounted: bool = False
    known: bool = True


@dataclass(frozen=True)
class Formation:
    uid: str
    x: float
    y: float
    speed: float
    groups: tuple[Group, ...]
    generation: int = 1
    authority: int = 1
    authorized: bool = True
    present: bool = True
    ammo: float = 1.0
    los: bool = True
    mobility: float = 1.0
    executable: bool = True
    endurance: float = 30.0  # remaining supported engagement duration, seconds; synthetic assumption

    def cap(self, axis):
        # Unknown members use a broad generic nonzero profile; uncertainty is separate.
        return sum(g.n * getattr(g, axis) for g in self.groups)

    @property
    def size(self): return sum(g.n for g in self.groups)
    @property
    def strength(self): return self.cap('melee') + self.cap('ranged') + self.cap('armour')
    @property
    def mounted(self): return sum(g.n for g in self.groups if g.mounted) / max(1, self.size)
    @property
    def uncertainty(self): return 0.10 + 0.40 * sum(g.n for g in self.groups if not g.known) / max(1, self.size)


@dataclass(frozen=True)
class Duty:
    asset: str
    owner: str
    deadline: float = 8.0
    # Only independently observed ready successors may replace a current critical owner.
    ready_successors: tuple[str, ...] = ()
    sacrifice: bool = False
    effective: bool | None = True


@dataclass
class Snapshot:
    friendly: tuple[Formation, ...]
    enemy: tuple[Formation, ...]
    duties: tuple[Duty, ...] = ()
    horizon: float = 30.0
    mission: str = 'synthetic'
    domain: str = 'legal-domain-fixture'
    now: float = 0.0
    epoch: int = 1
    # Path facts are adapter inputs, not computed from straight-line geometry in production.
    blocked: frozenset[tuple[str, str]] = frozenset()
    failed_probes: frozenset[tuple[str, str, int]] = frozenset()
    major_change: bool = True
    active_effects: frozenset[tuple[str,str,str]] = frozenset()

    def own(self): return {f.uid: f for f in self.friendly if f.present}
    def foes(self): return {f.uid: f for f in self.enemy if f.present}


@dataclass(frozen=True)
class Action:
    actor: str
    kind: str
    target: str = ''
    generation: int = 1
    authority: int = 1
    def key(self): return (self.actor, self.kind, self.target)


@dataclass
class Assessment:
    feasible: bool
    reasons: list[str]
    outcomes: dict[str, float] = field(default_factory=dict)
    interval: tuple[float, float] = (0.0, 0.0)
    starts: dict[str, float] = field(default_factory=dict)
    dependencies: dict[str, list[str]] = field(default_factory=dict)
    details: dict = field(default_factory=dict)


@dataclass
class Selection:
    actions: tuple[Action, ...]
    assessment: Assessment
    reason: str
    work: dict
    rejected: dict


@dataclass(frozen=True)
class Config:
    width: int = 32
    expansions: int = 3000
    options: int = 12
    targets: int = 3
    loss_weight: float = 1.0
    switch_cost: float = 2.0
    uncertainty: bool = True
    retain_all_scenarios: bool = False  # optional conservative ablation; default aligns with maximin objective
    dependencies: bool = True
    scenarios: tuple[str, ...] = ('hold', 'reinforce', 'counter')
    eta_scale: float = 1.0
    setup_seconds: float = 2.0
    strike_seconds: float = 6.0
    guard_radius: float = 75.0
    # Research parameters, NOT calibrated game settings.


def distance(a, b): return hypot(a.x - b.x, a.y - b.y)

def eta(a, b, cfg, st, reach=0.0):
    if (a.uid, b.uid) in st.blocked: return float('inf')
    remaining=max(0.0,distance(a,b)-reach)
    if remaining==0: return 0.0
    if a.speed<=0 or a.mobility<=0: return float('inf')
    return remaining/(a.speed*a.mobility)*cfg.eta_scale


def action_for(f, kind, target=''):
    return Action(f.uid, kind, target, f.generation, f.authority)


def eligible(f):
    return f.present and f.authorized and f.executable and f.size > 0


def options(st, cfg):
    """Bounded primitives from facts. No scenario IDs or pre-scored plan tables."""
    out = {}
    foes = tuple(st.foes().values())
    for f in st.friendly:
        if not eligible(f): continue
        arr = [action_for(f, 'hold')]
        # Geometric ordering; ID breaks only exact ties. Mandatory duty options are never capped out.
        for d in st.duties:
            if f.uid == d.owner or f.uid in d.ready_successors:
                arr.append(action_for(f, 'protect', d.asset))
        mandatory = len(arr)
        targets = sorted(foes, key=lambda e: (distance(f,e), -e.strength, e.uid))[:cfg.targets]
        for e in targets:
            if f.cap('melee') > 0 and eta(f,e,cfg,st) + cfg.setup_seconds + cfg.strike_seconds <= st.horizon:
                arr.append(action_for(f, 'strike', e.uid))
                arr.append(action_for(f, 'fix', e.uid))
            if f.cap('ranged') * f.ammo > 0 and f.los and eta(f,e,cfg,st,80) + cfg.setup_seconds < st.horizon:
                arr.append(action_for(f, 'shoot', e.uid))
            if (f.uid,e.uid,st.epoch) not in st.failed_probes and eta(f,e,cfg,st,60) + 4 <= st.horizon:
                arr.append(action_for(f, 'probe', e.uid))
        out[f.uid] = tuple(dict.fromkeys(arr[:mandatory] + arr[mandatory:mandatory+cfg.options]))
    return out


def protectors(f, target, st, cfg):
    if f.mounted < .5: return ()
    # A support relation is not a culture/formation-name label.
    return tuple(g for g in st.foes().values() if g.uid != target.uid and
                 g.cap('anti_mount') >= 0.15 * max(1.0, f.cap('melee')) and
                 distance(g,target) <= cfg.guard_radius and
                 eta(g,target,cfg,st,15) <= eta(f,target,cfg,st)+cfg.setup_seconds+cfg.strike_seconds)


def assessment(st, acts, cfg, current=(), check_dependencies=True):
    own, foes = st.own(), st.foes()
    reasons=[]; starts={}; deps={}; byactor={}; fixes={}; guards={}
    if len({a.actor for a in acts}) != len(acts): reasons.append('double_resource')
    for a in acts:
        f=own.get(a.actor)
        if not f or not eligible(f) or (f.generation,f.authority)!=(a.generation,a.authority):
            reasons.append('identity_or_authority:'+a.actor); continue
        byactor[a.actor]=a
        if a.kind not in ('hold','protect','strike','shoot','fix','probe'):
            reasons.append('unknown_action'); continue
        if a.kind=='hold': starts[a.actor]=0; continue
        t=own.get(a.target) if a.kind=='protect' else foes.get(a.target)
        if not t: reasons.append('missing_target:'+a.actor); continue
        reach=80 if a.kind=='shoot' else 60 if a.kind=='probe' else 15 if a.kind=='protect' else 0
        start=eta(f,t,cfg,st,reach)+cfg.setup_seconds
        if a.kind=='protect' and any(d.owner==a.actor and d.asset==a.target for d in st.duties):
            start=0.0  # incumbent duty is an observed input, not newly inferred arrival.
        if a.key() in st.active_effects: start=0.0
        starts[a.actor]=start
        duration=cfg.strike_seconds if a.kind=='strike' else 4
        if not isfinite(start) or start+duration>st.horizon: reasons.append('window:'+a.actor)
        if a.kind=='shoot' and (not f.los or f.ammo<=0 or f.cap('ranged')<=0): reasons.append('no_fire:'+a.actor)
        if a.kind=='fix': fixes.setdefault(a.target,[]).append(a)
        if a.kind=='protect': guards.setdefault(a.target,[]).append(a)
        if a.kind=='probe' and (a.actor,a.target,st.epoch) in st.failed_probes: reasons.append('repeat_probe:'+a.actor)
    for d in st.duties:
        coverage=[a for a in guards.get(d.asset,[]) if a.actor in (d.owner,)+d.ready_successors and starts.get(a.actor,float('inf'))<=d.deadline]
        if not coverage: reasons.append('critical_duty:'+d.asset)
    if cfg.dependencies and check_dependencies:
        for a in acts:
            if a.kind!='strike' or a.actor not in own or a.target not in foes: continue
            f,t=own[a.actor],foes[a.target]
            required=[]; start=starts.get(a.actor,float('inf'))
            for g in protectors(f,t,st,cfg):
                candidates=[b for b in fixes.get(g.uid,[]) if b.actor!=a.actor and
                            own[b.actor].cap('melee') >= .35*g.cap('melee')]
                if not candidates: reasons.append('unfixed_support:'+a.actor+':'+g.uid); continue
                b=min(candidates,key=lambda x:(starts[x.actor],x.actor))
                start=max(start,starts[b.actor]+cfg.setup_seconds)
                required.append(b.actor)
            strike_end=min(st.horizon,start+f.endurance)
            for actor in required:
                fix_end=min(st.horizon,starts[actor]+own[actor].endurance)
                if strike_end>fix_end: reasons.append('support_duration:'+a.actor+':'+actor)
            starts[a.actor]=start;deps[a.actor]=required
            if start+cfg.strike_seconds>st.horizon: reasons.append('dependency_window:'+a.actor)
    if reasons: return Assessment(False,sorted(set(reasons)),starts=starts,dependencies=deps)
    vals={}; details={}; total_error=0.0
    for reaction in cfg.scenarios:
        damage={k:0.0 for k in foes}; loss={k:0.0 for k in own}; fix_strength={}
        loss_sources={k:{f:0.0 for f in own} for k in foes}
        fixed_at={}; fixed_until={}
        for target,aa in fixes.items():
            fix_strength[target]=sum(own[a.actor].cap('melee') for a in aa)
            accumulated=0.0; earliest_end=st.horizon
            for b in sorted(aa,key=lambda x:starts[x.actor]):
                accumulated+=own[b.actor].cap('melee')
                earliest_end=min(earliest_end,starts[b.actor]+own[b.actor].endurance)
                if accumulated>=.35*foes[target].cap('melee'):
                    fixed_at[target]=starts[b.actor]+cfg.setup_seconds
                    fixed_until[target]=earliest_end
                    break
        for a in acts:
            if a.kind in ('hold','protect'): continue
            f,t=own[a.actor],foes[a.target]
            start=starts[a.actor]
            seconds=min(st.horizon-start,8.0,f.endurance) if a.kind=='probe' else min(st.horizon-start,f.endurance)
            contact=max(0,seconds)/st.horizon
            power=f.cap('ranged')*f.ammo if a.kind=='shoot' else f.cap('melee')
            if a.kind=='probe': power=max(f.cap('ranged')*f.ammo,f.cap('melee')*.35)
            if a.kind=='fix': power*=.35
            resist=1+t.cap('armour')/max(1,t.size)
            attack=power*contact/resist
            support_loss=0.0
            if a.kind=='strike':
                for g in protectors(f,t,st,cfg):
                    locked=fixed_at.get(g.uid,float('inf'))<=start and fixed_until.get(g.uid,-1)>=start+seconds
                    if not locked:
                        attack*=.35
                        loss_sources[g.uid][f.uid]+=g.cap('anti_mount')*contact*.7
            if reaction=='reinforce' and a.kind in ('strike','shoot'):
                # Time-overlap ledger: a late fixer cannot erase earlier reinforcement pressure.
                mobile=0.0
                for g in foes.values():
                    if g.uid==t.uid: continue
                    available_from=max(start,eta(g,t,cfg,st))
                    available_until=start+seconds
                    locked_overlap=max(0.0,min(available_until,fixed_until.get(g.uid,-1))-max(available_from,fixed_at.get(g.uid,float('inf'))))
                    free_fraction=max(0.0,available_until-available_from-locked_overlap)/max(.001,seconds)
                    mobile+=g.cap('melee')*free_fraction
                attack/=1+mobile/max(1,t.strength)
            if reaction=='counter' and a.kind=='probe':
                support_loss+=t.cap('melee')*contact*.35
            damage[t.uid]+=attack
            incoming=t.cap('ranged')*t.ammo if a.kind=='shoot' else t.cap('melee')
            loss_sources[t.uid][f.uid]+=(incoming*contact*.45/(1+f.cap('armour')/max(1,f.size))+support_loss)
        # Residual enemy pressure against protected assets, after reductions: no independent threat bonus.
        reserve_assignment={}
        if reaction=='counter':
            for a in acts:
                if a.kind!='hold': continue
                reachable=[d.asset for d in st.duties if d.asset in own and eta(own[a.actor],own[d.asset],cfg,st)<=8]
                if reachable:
                    reserve_assignment[a.actor]=max(reachable,key=lambda uid:(own[uid].strength,uid))
        for d in st.duties:
            asset=own.get(d.asset)
            if not asset: continue
            pressure={}
            for e in foes.values():
                remaining=max(0,1-damage[e.uid]/max(1,e.strength))
                if eta(e,asset,cfg,st,80)<=st.horizon:
                    pressure[e.uid]=e.cap('ranged')*e.ammo*.30*remaining if e.los else 0.0
                    if reaction=='counter': pressure[e.uid]+=e.cap('melee')*.20*remaining
            incoming=sum(pressure.values())
            coverage=sum(own[a.actor].cap('melee')+own[a.actor].cap('anti_mount') for a in guards.get(d.asset,[]))
            # A held reserve may intercept a counter if physically close; no fixed reserve percentage.
            if reaction=='counter':
                coverage+=sum(own[a.actor].cap('melee')*.5 for a in acts if reserve_assignment.get(a.actor)==d.asset)
            residual=max(0,incoming-coverage*.30)/max(.001,incoming)
            for enemy_id,amount in pressure.items(): loss_sources[enemy_id][d.asset]+=amount*residual
        # Each enemy has ONE aggregate output budget, shared across all victims and duties.
        # Proportional allocation is an explicit abstract approximation, not game target AI.
        output_budget={}
        for eid,recipients in loss_sources.items():
            e=foes[eid]
            budget=(max(e.cap('melee'),e.cap('anti_mount'))+e.cap('ranged')*e.ammo)*.45
            output_budget[eid]=budget
            factor=min(1.0,budget/max(.001,sum(recipients.values())))
            for uid,amount in recipients.items(): loss[uid]+=amount*factor
        removed=sum(min(foes[k].strength,v) for k,v in damage.items())
        lost=sum(min(own[k].strength,v) for k,v in loss.items())
        old={a.actor:a.key() for a in current}
        switches=sum(a.actor in old and a.key()!=old[a.actor] for a in acts)
        # Future redeployment cost only; incurred/sunk historical losses are NOT charged again.
        transition=cfg.switch_cost*switches
        val=removed-cfg.loss_weight*lost-transition
        error=sum(own[a.actor].uncertainty for a in acts if a.kind!='hold')*(removed+lost)*.025
        total_error=max(total_error,error)
        vals[reaction]=val
        details[reaction]={'enemy_capacity_removed':removed,'friendly_capacity_lost':lost,'transition_cost':transition,'model_error':error,'reserve_assignment':reserve_assignment,'enemy_output_budget':output_budget,'enemy_output_used':{k:min(output_budget[k],sum(v.values())) for k,v in loss_sources.items()}}
    err=total_error if cfg.uncertainty else 0
    interval=(min(vals.values())-err,max(vals.values())+err)
    return Assessment(True,[],vals,interval,starts,deps,details)


def baseline(st,cfg):
    """Guaranteed only if observed incumbents still authorized/executable; else explicit unresolved duty."""
    duty_by={d.owner:d for d in st.duties}
    return tuple(action_for(f,'protect',duty_by[f.uid].asset) if f.uid in duty_by else action_for(f,'hold')
                 for f in st.friendly if eligible(f))


def signature(acts): return tuple(sorted(a.key() for a in acts))


def choose(st,cfg=Config(),current=(),method='greedy'):
    opt=options(st,cfg); actors=sorted(opt)
    fallback=baseline(st,cfg)
    rej={}; evaluated=0; expansions=0; truncated=False; search_calls=0; mandatory_calls=0
    feasible={}
    def consider(aa, mandatory=False):
        nonlocal evaluated,search_calls,mandatory_calls,truncated
        if not mandatory and method!='exhaustive' and search_calls>=cfg.expansions:
            truncated=True; return None
        if mandatory: mandatory_calls+=1
        else: search_calls+=1
        aa=tuple(aa); ev=assessment(st,aa,cfg,current); evaluated+=1
        if ev.feasible: feasible[signature(aa)]=(aa,ev)
        else:
            for r in ev.reasons: rej[r]=rej.get(r,0)+1
        return ev
    # Always reserve mandatory work independently of extra-search budget.
    fallback_ev=consider(fallback,True)
    current_ev=consider(current,True) if current else None
    if method=='exhaustive':
        for aa in product(*(opt[k] for k in actors)):
            consider(aa);expansions+=1
    elif method=='greedy':
        # Competitive simple baseline: evaluate all one-actor changes AND two-actor packages,
        # including fix+strike. Two passes capture further independent improvements.
        incumbent=max(feasible.values(),key=lambda x:x[1].interval[0])[0] if feasible else fallback
        for _ in range(2):
            best=incumbent; bestval=feasible.get(signature(best),(None,fallback_ev))[1].interval[0] if feasible else float('-inf')
            base={a.actor:a for a in incumbent}
            def moves():
                for i,k in enumerate(actors):
                    for a in opt[k]: yield {k:a}
                    for j in range(i+1,len(actors)):
                        l=actors[j]
                        for a,b in product(opt[k],opt[l]): yield {k:a,l:b}
            for change in moves():
                if search_calls>=cfg.expansions: truncated=True;break
                aa=tuple(change.get(k,base.get(k,action_for(st.own()[k],'hold'))) for k in actors)
                ev=consider(aa);expansions+=1
                if ev.feasible and ev.interval[0]>bestval+1e-9: best,bestval=aa,ev.interval[0]
            incumbent=best
            if truncated: break
    else:
        # Enumerate in layers. Partial score relaxes future support/critical-duty requirements.
        # This is a heuristic beam, NOT an admissible optimality bound.
        beam=[()]
        for k in actors:
            layer=[]
            for partial in beam:
                for a in opt[k]:
                    if search_calls>=cfg.expansions: truncated=True;break
                    aa=partial+(a,);expansions+=1
                    # Score on a relaxed snapshot so a not-yet-assigned protector does not prune a bundle.
                    relaxed=replace(st,duties=())
                    ev=assessment(relaxed,aa,replace(cfg,dependencies=False),current,False)
                    search_calls+=1
                    if ev.feasible: layer.append((ev.interval[0],aa))
                if truncated: break
            if not layer: break
            layer.sort(key=lambda x:(-x[0],signature(x[1])))
            beam=[aa for _,aa in layer[:cfg.width]]
            # Fill with existing duties/hold to retain a complete usable incumbent at every layer.
            base={a.actor:a for a in fallback}
            for aa in beam:
                filled={a.actor:a for a in aa}
                consider(tuple(filled.get(w,base[w]) for w in actors))
            if truncated: break
    if not feasible:
        return Selection((),Assessment(False,['no_feasible_plan']), 'unresolved_duty_or_execution',
                         {'expansions':expansions,'evaluations':evaluated,'model_calls':search_calls+mandatory_calls,'search_model_calls':search_calls,'truncated':truncated},rej)
    best,bev=max(feasible.values(),key=lambda x:(x[1].interval[0],tuple(reversed(signature(x[0])))))
    reason='best_available'
    if current:
        cev=current_ev
        if cev.feasible:
            # Compare remaining effects at SAME snapshot and horizon, against freshly evaluated current.
            paired=(min(bev.outcomes[s]-cev.outcomes[s] for s in cfg.scenarios) if cfg.retain_all_scenarios else min(bev.outcomes.values())-min(cev.outcomes.values()))
            error=(max(x['model_error'] for x in bev.details.values())+
                   max(x['model_error'] for x in cev.details.values())) if cfg.uncertainty else 0
            changed=signature(best)!=signature(current)
            if changed and (not st.major_change or paired<=error+1e-9):
                best,bev,reason=tuple(current),cev,'retain_current:uncertain_or_no_global_trigger'
        else: reason='hard_invalid_repair'
    return Selection(best,bev,reason,{'expansions':expansions,'evaluations':evaluated,'model_calls':search_calls+mandatory_calls,'search_model_calls':search_calls,'truncated':truncated,
                                    'actors':len(actors),'options':sum(map(len,opt.values()))},rej)


@dataclass
class Runtime:
    mission: str
    domain: str
    revision: int = 1
    actions: tuple[Action,...] = ()
    duties: tuple[Duty,...] = ()
    reports: dict = field(default_factory=dict)
    not_before: dict = field(default_factory=dict)
    # This is an in-process fixture, not a distributed queue, lease service or Bannerlord executor.
    def adopt(self, selection, st, cfg=Config()):
        if not selection.assessment.feasible: return False
        if (self.mission,self.domain)!=(st.mission,st.domain): return False
        fresh=assessment(st,selection.actions,cfg,self.actions)
        if not fresh.feasible: return False
        if self.actions!=selection.actions:
            self.revision+=1
            self.not_before={uid:st.now+start for uid,start in fresh.starts.items()}
        self.actions=selection.actions; self.duties=st.duties
        return True
    def report(self, uid, generation, authority, revision, sequence, health, st):
        if (self.mission,self.domain)!=(st.mission,st.domain): return False
        f=st.own().get(uid)
        if not f or not f.authorized or (f.generation,f.authority,revision)!=(generation,authority,self.revision): return False
        key=(self.mission,self.domain,uid,generation,authority,revision)
        if sequence<=self.reports.get(key,(-1,{}))[0]: return False
        self.reports[key]=(sequence,dict(health))
        return True
    def feedback_snapshot(self,st):
        # Validated health changes inputs; only choose/adopt may replace the plan.
        if (self.mission,self.domain)!=(st.mission,st.domain): return st
        blocked=set(st.blocked); failed=set(st.failed_probes)
        for a in self.actions:
            key=(self.mission,self.domain,a.actor,a.generation,a.authority,self.revision)
            health=self.reports.get(key,(-1,{}))[1]
            if health.get('route_blocked') and a.target: blocked.add((a.actor,a.target))
            if health.get('no_response') and a.kind=='probe': failed.add((a.actor,a.target,st.epoch))
            # High loss/progress blocked do not cancel an effective duty or become global defeat.
        return replace(st,blocked=frozenset(blocked),failed_probes=frozenset(failed))
    def cancel_tasks(self):
        self.actions=(); self.revision+=1  # Does not release duties.
    def write(self,a,revision,st,ready=frozenset(),cfg=Config(),enforce_dependencies=True):
        f=st.own().get(a.actor)
        if (self.mission,self.domain)!=(st.mission,st.domain) or revision!=self.revision or a not in self.actions: return 'stale_plan'
        if not f or not eligible(f) or (f.generation,f.authority)!=(a.generation,a.authority): return 'lost_authority_or_identity'
        ev=assessment(st,self.actions,cfg)
        if not ev.feasible: return 'plan_invalid'
        if st.now<self.not_before.get(a.actor,float('inf')): return 'wait_time_window'
        if a.kind not in ('hold','protect') and a.actor not in ready: return 'wait_observed_readiness'
        if enforce_dependencies and any(x not in ready for x in ev.dependencies.get(a.actor,[])): return 'wait_observed_dependency'
        return 'accepted_interface_only'
    def safety(self,uid,st,imminent,ordinary_feasible,escape_feasible,duty_effective):
        if (self.mission,self.domain)!=(st.mission,st.domain): return 'no_authority'
        f=st.own().get(uid)
        if not f or not eligible(f): return 'no_authority'
        d=next((d for d in self.duties if d.owner==uid),None)
        if ordinary_feasible: return 'within_envelope'
        if imminent and escape_feasible and (not d or not d.sacrifice or duty_effective is False):
            return 'minimal_exception_report'
        return 'hold_if_feasible_report_blocked'


def load(path):
    d=json.load(open(path))
    def parse(f):
        f=dict(f);f['groups']=tuple(Group(**g) for g in f['groups']);return Formation(**f)
    return Snapshot(tuple(map(parse,d['friendly'])),tuple(map(parse,d['enemy'])),
                    tuple(Duty(**x) for x in d.get('duties',[])),horizon=d.get('horizon',30),
                    blocked=frozenset(tuple(x) for x in d.get('blocked',[])))


def serial(x):
    if hasattr(x,'__dataclass_fields__'): return {k:serial(v) for k,v in asdict(x).items()}
    if isinstance(x,dict): return {str(k):serial(v) for k,v in x.items()}
    if isinstance(x,(tuple,list,set,frozenset)): return [serial(v) for v in x]
    return x

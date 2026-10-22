"""Small, deterministic Formation execution abstraction; not a game simulator.

No planner imports, objectives, allocation scores, or algorithm-name branches.
All rates are synthetic. See evidence/world_contract.md for the validity boundary.
"""
from __future__ import annotations

from copy import deepcopy
from math import hypot, isfinite
from typing import Any

KINDS = frozenset(('hold', 'move', 'fire', 'fix', 'strike', 'pressure', 'retreat'))
VARIANTS = {
    'nominal': dict(speed=1.0, setup=1.0, fix_threshold=.65, suppression=.70,
                    fix_duration=18.0, exit_delay=2.0, anti_mount=.25),
    'slow_shared': dict(speed=.72, setup=1.8, fix_threshold=.65, suppression=.70,
                       fix_duration=18.0, exit_delay=2.0, anti_mount=.25),
    'weak_fix': dict(speed=1.0, setup=1.0, fix_threshold=1.5, suppression=.25,
                    fix_duration=18.0, exit_delay=2.0, anti_mount=.25),
    'early_fix': dict(speed=1.0, setup=1.0, fix_threshold=.65, suppression=.70,
                     fix_duration=5.0, exit_delay=2.0, anti_mount=.25),
    'counter_matchup': dict(speed=1.0, setup=1.0, fix_threshold=.65, suppression=.70,
                           fix_duration=18.0, exit_delay=2.0, anti_mount=1.25),
    'sticky_exit': dict(speed=1.0, setup=1.0, fix_threshold=.65, suppression=.70,
                       fix_duration=18.0, exit_delay=5.0, anti_mount=.25),
}
SETUP = dict(hold=0.0, move=0.0, fire=1.0, fix=1.0, strike=1.0,
             pressure=1.0, retreat=.5)
CONTACT = 6.0
EPS = 1e-9


def distance(a: dict, b: dict) -> float:
    return hypot(a['x'] - b['x'], a['y'] - b['y'])


def _unit(raw: dict) -> dict:
    u = deepcopy(raw)
    if 'id' not in u or 'side' not in u:
        raise ValueError('unit requires id and side')
    defaults = dict(x=0., y=0., hp=100., speed=5., melee=5., ranged=0., ammo=40.,
                    armor=0., mounted=False, anti_mount=0., role='line',
                    authorized=True, authority=0, generation=0, range=55.)
    for key, value in defaults.items():
        u.setdefault(key, value)
    u.setdefault('max_hp', u['hp'])
    for key in ('x', 'y', 'hp', 'max_hp', 'speed', 'melee', 'ranged', 'ammo',
                'armor', 'anti_mount', 'range'):
        u[key] = float(u[key])
        if not isfinite(u[key]):
            raise ValueError('non-finite unit field: ' + key)
        if key not in ('x', 'y') and u[key] < 0:
            raise ValueError('negative unit field: ' + key)
    if u['max_hp'] <= 0 or u['hp'] > u['max_hp']:
        raise ValueError('hp must be between zero and positive max_hp')
    u.update(vx=0., vy=0., moving=False, phase='holding', contact_ids=[],
             suppression=0., damage_dealt=0., damage_received=0.)
    return u


def _signature(order: dict) -> tuple:
    return (order['kind'], order.get('target'), order.get('x'), order.get('y'))


def _segment_blocked(a: dict, x: float, y: float, rectangles: list) -> bool:
    """Closed segment/AABB clipping. No navigation or collision solver."""
    for rect in rectangles:
        if isinstance(rect, dict):
            left, right, bottom, top = (rect[k] for k in ('xmin', 'xmax', 'ymin', 'ymax'))
        else:
            left, right, bottom, top = rect
        enter, leave = 0., 1.
        for start, delta, lo, hi in ((a['x'], x-a['x'], left, right),
                                     (a['y'], y-a['y'], bottom, top)):
            if abs(delta) < EPS:
                if start < lo or start > hi:
                    enter, leave = 1., 0.
                    break
            else:
                t1, t2 = sorted(((lo-start)/delta, (hi-start)/delta))
                enter, leave = max(enter, t1), min(leave, t2)
        if enter <= leave and leave >= 0 and enter <= 1:
            return True
    return False


class World:
    """Order delivery -> setup -> physical effect -> observed feedback.

    Observations are fresh copies; all actual state is visible except enemy
    command intent, future scenario events, and execution variant coefficients.
    `events` is the last step's batch plus commands issued since that step.
    Reading never consumes events; both sides see the same public batch.
    """
    def __init__(self, scenario: dict, variant: str = 'nominal'):
        if variant not in VARIANTS:
            raise ValueError('unknown world variant: ' + variant)
        self._coef = dict(VARIANTS[variant])
        self.t = 0.
        self.bounds = list(scenario.get('bounds', [-150., 150., -100., 100.]))
        self._obstacles = deepcopy(scenario.get('obstacles', []))
        self.duties = deepcopy(scenario.get('duties', []))
        self.objectives = deepcopy(scenario.get('objectives', []))
        self.units = {}
        self._orders = {}
        self._pending = {}
        self._fix_windows = {}
        self._events = []
        self._scheduled = sorted(deepcopy(scenario.get('events', [])), key=lambda e: e.get('t', 0.))
        self._stats = {}
        self._initial_hp = {}
        for raw in scenario.get('units', []):
            self._add_unit(raw, initial=True)
        self._refresh_contacts()

    def _stats_for(self, side):
        return self._stats.setdefault(side, dict(damage_dealt=0., damage_received=0.,
            distance_travelled=0., ammo_used=0., orders_accepted=0,
            orders_rejected=0, order_changes=0, orders_completed=0,
            route_blocks=0, attack_seconds=0., fixing_seconds=0.,
            suppression_seconds=0., duty_covered_time=0., duty_required_time=0.,
            deaths=0, reinforcements=0))

    def _add_unit(self, raw, initial=False):
        u = _unit(raw)
        if u['id'] in self.units:
            raise ValueError('duplicate unit id: ' + str(u['id']))
        self.units[u['id']] = u
        self._orders[u['id']] = dict(kind='hold', actor=u['id'], ready_at=self.t,
                                    generation=u['generation'], authority=u['authority'],
                                    completed=False, exit_until=None)
        stats = self._stats_for(u['side'])
        self._initial_hp[u['side']] = self._initial_hp.get(u['side'], 0.) + u['hp']
        if not initial:
            stats['reinforcements'] += 1
            self._emit('reinforcement', actor=u['id'], side=u['side'])

    def _emit(self, type, **kwargs):
        event = dict(t=round(self.t, 6), type=type, **kwargs)
        actor = self.units.get(event.get('actor'))
        if actor is not None:
            event.setdefault('generation', actor['generation'])
            event.setdefault('authority', actor['authority'])
        target = self.units.get(event.get('target'))
        if target is not None:
            event.setdefault('target_generation', target['generation'])
        self._events.append(event)
        return event

    def observe(self, side) -> dict:
        units = []
        # Do not retain arbitrary scenario metadata: whitelist visible fields.
        visible = ('id', 'side', 'x', 'y', 'hp', 'max_hp', 'speed', 'melee', 'ranged',
                   'ammo', 'armor', 'mounted', 'anti_mount', 'range', 'role',
                   'authorized', 'authority', 'generation', 'vx', 'vy', 'moving',
                   'phase', 'contact_ids', 'suppression', 'damage_dealt', 'damage_received')
        for u in self.units.values():
            out = {key: deepcopy(u[key]) for key in visible}
            if u['side'] == side:
                out['current_order'] = {k: deepcopy(v) for k, v in self._orders[u['id']].items()
                                        if k in ('actor', 'kind', 'target', 'x', 'y', 'generation', 'authority')}
                pending = self._pending.get(u['id'])
                out['pending'] = None if pending is None else {
                    k: deepcopy(v) for k, v in pending.items()
                    if k in ('actor', 'kind', 'target', 'x', 'y', 'deliver_at', 'generation', 'authority')}
            units.append(out)
        # Command acceptance/rejection/ready are own receipts, not enemy intent.
        events = [deepcopy(e) for e in self._events if not e['type'].startswith('order_')
                  or self.units.get(e.get('actor'), {}).get('side') == side]
        return dict(t=self.t, side=side, units=units, events=events,
                    duties=[deepcopy(d) for d in self.duties
                            if d.get('side', self.units.get(d.get('owner'), {}).get('side')) == side],
                    bounds=list(self.bounds),
                    objectives=deepcopy(self.objectives), obstacles=deepcopy(self._obstacles))

    def issue(self, side, orders: list[dict]) -> list[dict]:
        receipts = []
        for raw in orders:
            order = deepcopy(raw)
            actor, kind = order.get('actor'), order.get('kind')
            unit = self.units.get(actor)
            reason = None
            if unit is None:
                reason = 'unknown_actor'
            elif unit['side'] != side:
                reason = 'wrong_side'
            elif unit['hp'] <= 0:
                reason = 'dead'
            elif not unit['authorized']:
                reason = 'unauthorized'
            elif order.get('generation', unit['generation']) != unit['generation']:
                reason = 'stale_generation'
            elif order.get('authority', unit['authority']) != unit['authority']:
                reason = 'stale_authority'
            elif kind not in KINDS:
                reason = 'unknown_kind'
            elif kind in ('move', 'retreat') and not all(k in order for k in ('x', 'y')):
                reason = 'missing_destination'
            elif kind in ('fire', 'fix', 'strike', 'pressure') and order.get('target') not in self.units:
                reason = 'unknown_target'
            elif kind in ('fire', 'fix', 'strike', 'pressure') and self.units[order['target']]['side'] == side:
                reason = 'friendly_target'
            elif kind in ('fire', 'fix', 'strike', 'pressure') and self.units[order['target']]['hp'] <= 0:
                reason = 'target_unavailable'
            elif any(not isinstance(order.get(k, 0), (float, int)) or not isfinite(order.get(k, 0))
                     for k in ('delay', 'x', 'y')) or order.get('delay', 0) < 0:
                reason = 'invalid_number'
            if reason:
                self._stats_for(side)['orders_rejected'] += 1
                receipts.append(self._emit('order_rejected', actor=actor, kind=kind, reason=reason))
                continue
            order['generation'], order['authority'] = unit['generation'], unit['authority']
            order['target_generation'] = self.units[order['target']]['generation'] if order.get('target') in self.units else None
            existing = self._pending.get(actor) or self._orders[actor]
            if (_signature(order) == _signature(existing)
                and existing.get('generation') == order['generation']
                and existing.get('authority') == order['authority']
                and existing.get('target_generation') == order.get('target_generation')):
                receipts.append(dict(t=self.t, type='order_unchanged', actor=actor, kind=kind))
                continue
            self._stats_for(side)['orders_accepted'] += 1
            self._stats_for(side)['order_changes'] += 1
            receipts.append(self._emit('order_accepted', actor=actor, kind=kind,
                                       delay=order.get('delay', 0.)))
            self._pending.pop(actor, None)
            if order.get('delay', 0) > EPS:
                order['deliver_at'] = self.t + order['delay']
                self._pending[actor] = order
            else:
                self._activate(order)
        return deepcopy(receipts)

    def _activate(self, order):
        actor = order['actor']
        u = self.units.get(actor)
        if (u is None or u['hp'] <= 0 or not u['authorized'] or
            u['generation'] != order['generation'] or u['authority'] != order['authority']):
            self._emit('order_rejected', actor=actor, kind=order['kind'], reason='stale_at_delivery')
            if u:
                self._stats_for(u['side'])['orders_rejected'] += 1
            return
        if order.get('target') is not None:
            target = self.units.get(order['target'])
            if target is None or target['hp'] <= 0 or target['generation'] != order.get('target_generation'):
                self._emit('order_rejected', actor=actor, kind=order['kind'], reason='target_unavailable')
                self._stats_for(u['side'])['orders_rejected'] += 1
                return
        order = deepcopy(order)
        order.update(ready_at=self.t + SETUP[order['kind']]*self._coef['setup'],
                     completed=False, exit_until=None)
        order.pop('deliver_at', None)
        self._orders[actor] = order
        if order['ready_at'] <= self.t + EPS:
            order['announced_ready'] = True
            self._emit('order_ready', actor=actor, kind=order['kind'])

    def _refresh_contacts(self):
        alive = [u for u in self.units.values() if u['hp'] > 0]
        for u in self.units.values():
            u['contact_ids'] = [v['id'] for v in alive if v['side'] != u['side'] and
                                distance(u, v) <= CONTACT + EPS] if u['hp'] > 0 else []

    def _external_events(self):
        while self._scheduled and self._scheduled[0].get('t', 0.) <= self.t + EPS:
            e = self._scheduled.pop(0)
            kind = e.get('type')
            if kind in ('authority', 'authority_changed'):
                u = self.units[e['actor']]
                u['authorized'] = bool(e.get('authorized', not u['authorized']))
                u['authority'] = max(u['authority']+1, e.get('authority', u['authority']+1))
                # Revoke/regrant invalidates the active order and all delayed commands.
                stale = self._pending.pop(u['id'], None)
                self._orders[u['id']] = dict(actor=u['id'], kind='hold', ready_at=self.t,
                    generation=u['generation'], authority=u['authority'], completed=False, exit_until=None)
                if stale:
                    self._emit('order_rejected', actor=u['id'], kind=stale['kind'], reason='authority_changed')
                    self._stats_for(u['side'])['orders_rejected'] += 1
                self._emit('authority_changed', actor=u['id'], authorized=u['authorized'], authority=u['authority'])
            elif kind == 'reinforcement':
                self._add_unit(e['unit'])
            elif kind == 'replace':
                old = self.units[e['actor']]
                new = dict(e['unit'], id=old['id'], generation=old['generation']+1,
                           authority=old['authority']+1, side=old['side'],
                           hp=old['hp'], max_hp=old['max_hp'], ammo=old['ammo'])
                replacement = _unit(new)
                replacement['damage_dealt'] = old['damage_dealt']
                replacement['damage_received'] = old['damage_received']
                self._pending.pop(old['id'], None)
                self.units[old['id']] = replacement
                self._orders[old['id']] = dict(actor=old['id'], kind='hold', ready_at=self.t,
                    generation=new['generation'], authority=new['authority'],
                    completed=False, exit_until=None)
                self._emit('generation_changed', actor=new['id'], generation=new['generation'])
            else:
                raise ValueError('unknown scenario event: ' + str(kind))

    def _fix_effects(self, dt):
        contributors = {}
        active_pairs = set()
        for u in self.units.values():
            u['suppression'] = 0.
            order = self._orders[u['id']]
            target = self.units.get(order.get('target'))
            if (u['hp'] <= 0 or not u['authorized'] or order['kind'] != 'fix' or
                self.t + EPS < order['ready_at'] or target is None or target['hp'] <= 0 or
                target['generation'] != order.get('target_generation') or distance(u, target) > CONTACT + EPS):
                continue
            pair = (u['id'], target['id'])
            active_pairs.add(pair)
            window = self._fix_windows.setdefault(pair, dict(active=0., away=0.))
            window['away'] = 0.
            contribution = u['melee'] * u['hp']/u['max_hp']
            if window['active'] >= self._coef['fix_duration'] - EPS:
                contribution = 0.
            window['active'] += dt
            contributors.setdefault(target['id'], []).append((u, contribution))
        for pair, window in self._fix_windows.items():
            if pair not in active_pairs:
                a, b = self.units.get(pair[0]), self.units.get(pair[1])
                # Merely relabeling the order does not refresh the physical fix window.
                if a is None or b is None or distance(a, b) > CONTACT + EPS:
                    window['away'] += dt
                    if window['away'] >= 4.:
                        window['active'] = 0.
        for target_id, fixers in contributors.items():
            target = self.units[target_id]
            strength = sum(s for _, s in fixers)
            required = self._coef['fix_threshold'] * max(.5, target['melee']*target['hp']/target['max_hp'])
            effective = strength >= required and strength > 0.
            target['suppression'] = self._coef['suppression'] if effective else 0.
            if effective:
                self._stats_for(target['side'])['suppression_seconds'] += dt
            for actor, contribution in fixers:
                if contribution > 0:
                    self._stats_for(actor['side'])['fixing_seconds'] += dt
                self._emit('fix_effect', actor=actor['id'], target=target_id,
                    contribution=contribution, total_strength=strength,
                    effective=effective, suppression=target['suppression'])

    def _destination(self, u, order):
        kind = order['kind']
        if kind in ('move', 'retreat'):
            return (float(order['x']), float(order['y']), 0.)
        if kind in ('fire', 'pressure', 'fix', 'strike'):
            target = self.units.get(order.get('target'))
            if target and target['hp'] > 0 and target['generation'] == order.get('target_generation'):
                reach = u['range']*.96 if kind in ('fire', 'pressure') else CONTACT*.90
                return (target['x'], target['y'], reach)
        return None

    def _move(self, dt):
        updates = {}
        for actor, u in self.units.items():
            u['vx'] = u['vy'] = 0.
            u['moving'] = False
            order = self._orders[actor]
            if u['hp'] <= 0:
                u['phase'] = 'dead'
                continue
            if not u['authorized']:
                u['phase'] = 'released'
                continue
            if self.t + EPS < order['ready_at']:
                u['phase'] = 'preparing'
                continue
            if not order.get('announced_ready'):
                self._emit('order_ready', actor=actor, kind=order['kind'])
                order['announced_ready'] = True
            dest = self._destination(u, order)
            if dest is None:
                u['phase'] = 'engaged' if u['contact_ids'] else 'holding'
                continue
            x, y, reach = dest
            x, y = max(self.bounds[0], min(self.bounds[1], x)), max(self.bounds[2], min(self.bounds[3], y))
            dist = hypot(x-u['x'], y-u['y'])
            if dist <= reach + EPS:
                u['phase'] = 'engaged' if u['contact_ids'] else 'holding'
                if order['kind'] in ('move', 'retreat') and not order['completed']:
                    order['completed'] = True
                    self._stats_for(u['side'])['orders_completed'] += 1
                    self._emit('order_completed', actor=actor, kind=order['kind'], reason='arrived')
                continue
            # Contact exit friction applies to a destination increasing distance from all contacts.
            leaving = bool(u['contact_ids']) and all(
                hypot(x-self.units[v]['x'], y-self.units[v]['y']) > distance(u, self.units[v])+EPS
                for v in u['contact_ids'])
            if leaving and order.get('exit_until') is None:
                order['exit_until'] = self.t + self._coef['exit_delay']
            if order.get('exit_until') is not None and self.t + EPS < order['exit_until']:
                u['phase'] = 'exiting'
                continue
            travel = min(dist-reach, u['speed']*self._coef['speed']*(1-u['suppression'])*dt)
            nx, ny = u['x'] + (x-u['x'])/dist*travel, u['y'] + (y-u['y'])/dist*travel
            if _segment_blocked(u, nx, ny, self._obstacles):
                u['phase'] = 'blocked'
                if not order.get('blocked_reported'):
                    self._emit('route_blocked', actor=actor, x=nx, y=ny)
                    self._stats_for(u['side'])['route_blocks'] += 1
                    order['blocked_reported'] = True
                continue
            updates[actor] = (nx, ny)
            u['phase'] = 'moving' if travel > EPS else 'holding'
            self._stats_for(u['side'])['distance_travelled'] += travel
        for actor, (x, y) in updates.items():
            u = self.units[actor]
            u['vx'], u['vy'] = (x-u['x'])/dt, (y-u['y'])/dt
            u['moving'] = hypot(u['vx'], u['vy']) > EPS
            u['x'], u['y'] = x, y

    def _attacks(self, dt):
        proposed = []
        for actor, u in self.units.items():
            if u['hp'] <= 0 or not u['authorized']:
                continue
            order = self._orders[actor]
            ready = self.t + EPS >= order['ready_at']
            kind = order['kind']
            enemies = [v for v in self.units.values() if v['side'] != u['side'] and v['hp'] > 0]
            contacts = [v for v in enemies if distance(u, v) <= CONTACT+EPS]
            designated = self.units.get(order.get('target'))
            if designated and (designated['hp'] <= 0 or designated['generation'] != order.get('target_generation')):
                if not order.get('completed'):
                    order['completed'] = True
                    self._stats_for(u['side'])['orders_completed'] += 1
                    self._emit('order_completed', actor=actor, kind=kind, reason='target_unavailable')
                designated = None
            targets, channel, factor = [], 'melee', 1.
            if contacts:
                if ready and kind in ('fix', 'strike') and designated in contacts:
                    targets = [designated]
                    factor = .35 if kind == 'fix' else 1.
                else:
                    # Defensive contact response divides the same finite actor budget.
                    targets = contacts
                    factor = .55 if kind == 'retreat' or u['phase'] in ('preparing', 'exiting') else 1.
            elif ready and kind in ('fire', 'pressure') and designated:
                if distance(u, designated) <= u['range']+EPS:
                    targets, channel = [designated], 'ranged'
                    factor = .7 if kind == 'pressure' else 1.
            elif ready and kind == 'hold' and u['ranged'] > 0:
                in_range = [v for v in enemies if distance(u, v) <= u['range']+EPS]
                if in_range:
                    targets, channel = [min(in_range, key=lambda v: (distance(u, v), str(v['id'])))], 'ranged'
            if not targets:
                continue
            health = u['hp']/u['max_hp']
            budget = u[channel]*health*factor*dt
            if channel == 'ranged':
                desired_ammo = health*dt
                spent = min(u['ammo'], desired_ammo)
                budget *= spent/desired_ammo if desired_ammo > 0 else 0.
                u['ammo'] -= spent
                self._stats_for(u['side'])['ammo_used'] += spent
            if budget <= 0:
                continue
            self._stats_for(u['side'])['attack_seconds'] += dt
            each = budget/len(targets)
            for target in targets:
                matchup = 1. + self._coef['anti_mount']*min(1., u['anti_mount']/max(u['melee'], .5)) if target['mounted'] and channel == 'melee' else 1.
                amount = each * .9 * matchup/(1.+target['armor'])
                proposed.append((actor, target['id'], amount, channel, each))
        # Scale overkill attribution against one pre-damage snapshot, then apply together.
        totals = {}
        for _, target, damage, _, _ in proposed:
            totals[target] = totals.get(target, 0.) + damage
        for actor, target, amount, channel, raw_budget in proposed:
            victim = self.units[target]
            actual = amount * min(1., victim['hp']/totals[target])
            attacker = self.units[actor]
            attacker['damage_dealt'] += actual
            self._stats_for(attacker['side'])['damage_dealt'] += actual
            self._emit('damage', actor=actor, target=target, amount=actual,
                       channel=channel, allocated_output=raw_budget)
        for target, amount in totals.items():
            victim = self.units[target]
            actual = min(victim['hp'], amount)
            victim['hp'] = max(0., victim['hp']-actual)
            victim['damage_received'] += actual
            self._stats_for(victim['side'])['damage_received'] += actual
            if victim['hp'] <= EPS:
                victim['hp'] = 0.
                victim['phase'] = 'dead'
                victim['vx'] = victim['vy'] = 0.
                victim['moving'] = False
                self._pending.pop(target, None)
                self._stats_for(victim['side'])['deaths'] += 1
                self._emit('death', actor=target, side=victim['side'])

    def _sample_duties(self, dt):
        for d in self.duties:
            if not (d.get('start', 0.) <= self.t < d.get('end', float('inf'))):
                continue
            owner = self.units.get(d.get('owner'))
            asset = self.units.get(d.get('asset'))
            side = d.get('side', owner['side'] if owner else asset['side'] if asset else None)
            if side is None:
                continue
            covered = bool(owner and asset and owner['hp'] > 0 and owner['authorized'] and
                           asset['hp'] > 0 and distance(owner, asset) <= d.get('coverage_radius', 20.))
            stats = self._stats_for(side)
            stats['duty_required_time'] += dt
            if covered:
                stats['duty_covered_time'] += dt

    def step(self, dt: float = 1.) -> list[dict]:
        if not isfinite(dt) or dt <= 0:
            raise ValueError('dt must be finite and positive')
        self._events = []
        end = self.t + dt
        while self.t < end-EPS:
            self._external_events()
            for actor, order in list(self._pending.items()):
                if order['deliver_at'] <= self.t+EPS:
                    self._pending.pop(actor, None)
                    self._activate(order)
            # Honor timed deliveries/events/setup transitions without stepping over them.
            boundaries = [end, self.t+.25]
            boundaries += [e.get('t', 0.) for e in self._scheduled[:1] if e.get('t', 0.) > self.t+EPS]
            boundaries += [o['deliver_at'] for o in self._pending.values() if o['deliver_at'] > self.t+EPS]
            boundaries += [o['ready_at'] for o in self._orders.values() if o['ready_at'] > self.t+EPS]
            h = min(boundaries)-self.t
            self._refresh_contacts()
            self._fix_effects(h)
            self._move(h)
            self._refresh_contacts()
            self._attacks(h)
            self._sample_duties(h)
            self.t += h
        self.t = end
        self._external_events()
        self._refresh_contacts()
        return deepcopy(self._events)

    def metrics(self, side) -> dict:
        own = [u for u in self.units.values() if u['side'] == side]
        stats = deepcopy(self._stats_for(side))
        hp = sum(u['hp'] for u in own)
        stats.update(t=self.t, side=side, hp=hp, initial_hp=self._initial_hp.get(side, 0.),
                     hp_lost=stats['damage_received'], alive=sum(u['hp'] > 0 for u in own),
                     authorized_alive=sum(u['hp'] > 0 and u['authorized'] for u in own),
                     ammo=sum(u['ammo'] for u in own),
                     positions={u['id']: [u['x'], u['y']] for u in own},
                     duty_coverage=(stats['duty_covered_time']/stats['duty_required_time']
                                    if stats['duty_required_time'] > 0 else None))
        return stats


def reactive_orders(obs: dict, mode: str = 'hold', state: dict | None = None) -> list[dict]:
    """A finite visible-state policy, not the evaluated planner.

    Caller owns `state`; only feint phase and home coordinates are retained.
    No read of enemy current_order, pending delivery, variant, or future event.
    """
    if mode not in ('hold', 'support', 'counter', 'withdraw', 'feint'):
        raise ValueError('unknown reaction mode: ' + mode)
    if state is None:
        state = {}
    own = [u for u in obs['units'] if u['side'] == obs['side'] and u['hp'] > 0 and u['authorized']]
    enemies = [u for u in obs['units'] if u['side'] != obs['side'] and u['hp'] > 0]
    homes = state.setdefault('homes', {})
    for u in own:
        homes.setdefault(u['id'], (u['x'], u['y']))
    orders = []
    assets = [u for u in own if u['ranged'] > u['melee'] or u['role'] in ('asset', 'archer', 'ranged')]
    threatened = [(asset, foe) for asset in assets for foe in enemies if distance(asset, foe) < 45.]
    support_assignment = {}
    available = [u for u in own if u not in assets]
    for asset, foe in sorted(threatened, key=lambda pair: distance(*pair)):
        if available and foe['id'] not in support_assignment.values():
            supporter = min(available, key=lambda u: distance(u, asset))
            available.remove(supporter)
            support_assignment[supporter['id']] = foe['id']
    for u in own:
        order = dict(actor=u['id'], kind='hold', generation=u['generation'], authority=u['authority'])
        if not enemies:
            orders.append(order)
            continue
        nearest = min(enemies, key=lambda e: (distance(u, e), str(e['id'])))
        if mode == 'hold':
            pass
        elif mode == 'support':
            if u['id'] in support_assignment:
                order.update(kind='strike', target=support_assignment[u['id']])
            elif u in assets and distance(u, nearest) <= u.get('range', 55.):
                order.update(kind='fire', target=nearest['id'])
        elif mode == 'counter':
            # Moving enemies are visible opportunities, not foreknowledge of intent.
            moving = [e for e in enemies if e.get('moving')]
            target = min(moving or enemies, key=lambda e: distance(u, e))
            order.update(kind='fire' if u['ranged'] > u['melee'] else 'strike', target=target['id'])
        elif mode == 'withdraw':
            dx, dy = u['x']-nearest['x'], u['y']-nearest['y']
            length = hypot(dx, dy) or 1.
            order.update(kind='retreat', x=u['x']+dx/length*25., y=u['y']+dy/length*25.)
        elif mode == 'feint':
            switched = state.setdefault('withdrawing', {})
            if obs['t'] >= 10. or distance(u, nearest) <= 18.:
                switched[u['id']] = True
            if switched.get(u['id']):
                hx, hy = homes[u['id']]
                direction = -1. if hx <= nearest['x'] else 1.
                order.update(kind='retreat', x=hx+direction*25., y=hy)
            else:
                order.update(kind='strike', target=nearest['id'])
        orders.append(order)
    return orders

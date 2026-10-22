"""Small Commander-owned temporal reservation model; no engine calls.
All intervals are half-open seconds. Forecast legality is not observed readiness.
"""
from dataclasses import dataclass, field, asdict
from math import hypot


@dataclass(frozen=True)
class Task:
    id: str
    actor: str
    kind: str
    target: str = ''
    start: float = 0.0
    end: float = 1.0
    generation: int = 1
    authority: int = 1
    after: tuple[str, ...] = ()
    x: float | None = None
    y: float | None = None
    strength: float = 0.0
    release_required: bool = True
    revision: int = 1


@dataclass(frozen=True)
class JointCondition:
    consumer: str
    target: str
    contributors: tuple[str, ...]
    threshold: float


@dataclass(frozen=True)
class Coverage:
    asset: str
    owner: str
    start: float
    end: float
    ready_successors: tuple[str, ...] = ()


@dataclass
class Plan:
    tasks: tuple[Task, ...]
    conditions: tuple[JointCondition, ...] = ()
    duties: tuple[Coverage, ...] = ()
    revision: int = 1
    mission: str = 'research'
    domain: str = 'domain'


def union_covers(intervals, start, end):
    cursor = start
    for a, b in sorted(intervals):
        if b <= cursor: continue
        if a > cursor + 1e-9: return False
        cursor = max(cursor, b)
        if cursor >= end - 1e-9: return True
    return cursor >= end - 1e-9


def validate(plan, units, travel=True):
    """Independent interval/identity/resource checks, not a value evaluator."""
    errors = []
    nodes = {t.id: t for t in plan.tasks}
    if len(nodes) != len(plan.tasks): errors.append('duplicate_task_id')
    visiting, visited = set(), set()
    def visit(node):
        if node in visiting: errors.append('dependency_cycle'); return
        if node in visited or node not in nodes: return
        visiting.add(node)
        for dep in nodes[node].after: visit(dep)
        visiting.remove(node); visited.add(node)
    for key in nodes: visit(key)
    grouped = {}
    for task in plan.tasks:
        unit = units.get(task.actor)
        if not unit or unit.get('hp', 1) <= 0 or not unit.get('authorized', True):
            errors.append('unavailable:' + task.id)
        elif (unit.get('generation', 1), unit.get('authority', 1)) != (task.generation, task.authority):
            errors.append('identity:' + task.id)
        if task.start < 0 or task.end <= task.start: errors.append('invalid_interval:' + task.id)
        grouped.setdefault(task.actor, []).append(task)
        for dep in task.after:
            if dep not in nodes: errors.append('missing_dependency:' + task.id)
            elif nodes[dep].end > task.start + 1e-9: errors.append('precedence:' + task.id)
    for actor, tasks in grouped.items():
        tasks.sort(key=lambda t: (t.start, t.id))
        for first, second in zip(tasks, tasks[1:]):
            if first.end > second.start + 1e-9: errors.append('resource_overlap:' + actor)
            if travel and None not in (first.x, first.y, second.x, second.y):
                distance = hypot(second.x-first.x, second.y-first.y)
                speed = units.get(actor, {}).get('speed', 0)
                eta = distance/speed if speed > 0 else (0 if distance == 0 else float('inf'))
                if first.end + eta > second.start + 1e-9: errors.append('transfer_time:' + second.id)
    for condition in plan.conditions:
        use = nodes.get(condition.consumer)
        if not use: errors.append('missing_consumer'); continue
        contributors = [nodes[x] for x in set(condition.contributors) if x in nodes]
        if len(condition.contributors) != len(set(condition.contributors)) or any(x not in nodes for x in condition.contributors):
            errors.append('duplicate_or_missing_contributor:' + condition.consumer)
        if any(t.kind != 'fix' or t.target != condition.target for t in contributors):
            errors.append('wrong_effect:' + condition.consumer)
        boundaries = sorted({use.start, use.end} | {
            p for t in contributors for p in (t.start, t.end) if use.start < p < use.end})
        for lo, hi in zip(boundaries, boundaries[1:]):
            strength = sum(t.strength for t in contributors if t.start <= lo+1e-9 and t.end >= hi-1e-9)
            if strength + 1e-9 < condition.threshold:
                errors.append('joint_window:' + condition.consumer); break
        # Fix cannot depend on the effect it is meant to establish.
        if any(condition.consumer in t.after for t in contributors): errors.append('effect_cycle')
    for duty in plan.duties:
        owners = (duty.owner,) + duty.ready_successors
        intervals = [(t.start, t.end) for t in plan.tasks if t.kind == 'protect'
                     and t.target == duty.asset and t.actor in owners]
        if not union_covers(intervals, duty.start, duty.end): errors.append('duty_gap:' + duty.asset)
    return sorted(set(errors))


def observed_ready(plan, task_id, observations, now, max_age=2.0):
    """Facts have source, target, actor identity and expiry; forecast never suffices."""
    nodes = {t.id: t for t in plan.tasks}
    task = nodes.get(task_id)
    if task is None: return False
    for condition in plan.conditions:
        if condition.consumer != task_id: continue
        strength = 0.0
        for key in set(condition.contributors):
            node = nodes.get(key)
            if node is None: return False
            fact = observations.get(key, {})
            if (fact.get('source') == 'execution_observation' and fact.get('actor') == node.actor
                and fact.get('target') == condition.target
                and fact.get('mission') == plan.mission and fact.get('domain') == plan.domain
                and fact.get('task') == node.id and fact.get('task_revision') == node.revision
                and fact.get('generation') == node.generation and fact.get('authority') == node.authority
                and 0 <= now-fact.get('observed_at', -1e9) <= max_age
                and fact.get('valid_until', -1) >= now and fact.get('effective', False)):
                strength += max(0.0, fact.get('strength', 0))
        if strength + 1e-9 < condition.threshold: return False
    return True


def repair(plan, failed):
    """Commander computes affected closure; unaffected tasks retain IDs/intervals."""
    bad = set(failed)
    changed = True
    while changed:
        changed = False
        for t in plan.tasks:
            if t.id not in bad and any(p in bad for p in t.after):
                bad.add(t.id); changed = True
            if t.id not in bad and any(x.id in bad and x.actor==t.actor and x.end<=t.start
                                      and x.release_required for x in plan.tasks):
                bad.add(t.id); changed = True
        for c in plan.conditions:
            if c.consumer not in bad and any(x in bad for x in c.contributors):
                # Conservative closure; re-solving a smaller sufficient coalition is explicit future work.
                bad.add(c.consumer); changed = True
    remaining = tuple(t for t in plan.tasks if t.id not in bad)
    conditions = tuple(c for c in plan.conditions if c.consumer not in bad)
    return Plan(remaining, conditions, plan.duties, plan.revision+1,plan.mission,plan.domain), sorted(bad)


class CommanderLedger:
    """One owner for phase state. Host reports facts; it never advances a plan itself."""
    def __init__(self, plan):
        self.plan = plan
        self.completed = set()
        self.failed = set()
        self.unreleased = {}
        self.dispatched = set()
        self.dispatch_revision = {}

    def mark_dispatched(self, task_id):
        if task_id in {t.id for t in self.plan.tasks}:
            self.dispatched.add(task_id)
            self.dispatch_revision.setdefault(task_id,self.plan.revision)

    def acknowledge_release(self, task_id, receipt):
        task=next((t for t in self.plan.tasks if t.id==task_id),None)
        expected_revision=self.dispatch_revision.get(task_id,self.plan.revision)
        if not task and task_id in self.unreleased:
            task,expected_revision=self.unreleased[task_id]
        if not task or not isinstance(receipt,dict): return False
        if (receipt.get('source')!='execution_observation' or not receipt.get('complete')
            or receipt.get('mission')!=self.plan.mission or receipt.get('domain')!=self.plan.domain
            or receipt.get('plan_revision')!=expected_revision or receipt.get('task')!=task_id
            or receipt.get('actor')!=task.actor or receipt.get('generation')!=task.generation
            or receipt.get('authority')!=task.authority): return False
        self.completed.add(task_id);self.unreleased.pop(task_id,None)
        return True

    def releasable(self, now, facts):
        def resource_released(task):
            if any(t.actor==task.actor for t,_ in self.unreleased.values()): return False
            earlier=[x for x in self.plan.tasks if x.actor==task.actor and x.id!=task.id
                     and x.end<=task.start and x.release_required]
            return all(x.id in self.completed for x in earlier)
        return [t for t in self.plan.tasks if t.id not in self.completed | self.failed
                and t.start <= now < t.end and all(x in self.completed for x in t.after)
                and resource_released(t)
                and observed_ready(self.plan, t.id, facts, now)]

    def invalidate(self, task_ids):
        old=self.plan
        self.plan, affected = repair(self.plan, task_ids)
        for t in old.tasks:
            if t.id in affected and t.id in self.dispatched and t.id not in self.completed and t.release_required:
                self.unreleased[t.id]=(t,self.dispatch_revision.get(t.id,old.revision))
        self.failed.update(affected)
        return affected

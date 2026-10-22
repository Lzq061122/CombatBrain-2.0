"""Executed feedback traces, explicitly constructed observations, no game physics."""
import json
from pathlib import Path
from dataclasses import replace
from planner import *
from run_experiments import fixture,detailed

st=fixture();cfg=Config();rt=Runtime(st.mission,st.domain)
rows=[]
a=action_for(st.friendly[0],'probe','enemy_archers')
acts=(a,)+tuple(action_for(f,'hold') for f in st.friendly[1:])
rt.adopt(Selection(acts,assessment(st,acts,cfg),'bounded_probe_fixture',{},{}),st)
rows.append({'phase':'probe_committed','actions':serial(rt.actions),'revision':rt.revision})
# Observation of no response is a fixture, not a simulated prediction or measured game result.
rt.report(a.actor,1,1,rt.revision,1,{'no_response':True},st)
changed=rt.feedback_snapshot(st)
sel=choose(changed,cfg,rt.actions);rt.adopt(sel,changed)
rows.append({'phase':'no_response_repair','selection':detailed(sel)})
# A blocked route observed on an actually selected action invalidates just that edge.
b=next((x for x in rt.actions if x.target and x.kind!='protect'),None)
if b:
 rt.report(b.actor,b.generation,b.authority,rt.revision,1,{'route_blocked':True},changed)
 changed=rt.feedback_snapshot(changed)
 sel=choose(changed,cfg,rt.actions);rt.adopt(sel,changed)
 rows.append({'phase':'blocked_edge_repair','selection':detailed(sel)})
# Opponent update order test: the two planners consume a frozen frame, with isolated runtimes.
# This checks schedule dependence in pure planning, NOT coupled game-motion stability.
other=Snapshot(st.enemy,st.friendly,mission=st.mission,domain='enemy-legal-domain')
orders=[]
for order in ((st,other),(other,st)):
 result={}
 for frame in order: result[frame.domain]=signature(choose(frame,cfg).actions)
 orders.append(result)
rows.append({'phase':'two_sides_frozen_frame_order','equal':orders[0]==orders[1], 'outputs':orders,
             'limitation':'does not simulate co-adaptive movement; game oscillation unverified'})
Path(__file__).with_name('results').mkdir(exist_ok=True)
Path(__file__).with_name('results').joinpath('feedback.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2))
print('saved',len(rows),'feedback stages')

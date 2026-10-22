"""Narrow translation of frozen CB2 AssignGuardAndSupport. Not C#/engine execution."""
from math import hypot
from itertools import permutations
import json
from pathlib import Path

guard_point=(0.,0.);support_point=(10.,0.)
formations=[('A',4.,0.),('B',-5.,0.)] # stable input order as in CB2
D=lambda f,p:hypot(f[1]-p[0],f[2]-p[1])
guard=min(formations,key=lambda f:D(f,guard_point))
support=min(formations,key=lambda f:D(f,support_point))
if guard[0]==support[0]:support=min((f for f in formations if f[0]!=guard[0]),key=lambda f:D(f,support_point))
chosen={'guard':guard[0],'support':support[0],'travel_sum_m':D(guard,guard_point)+D(support,support_point)}
reference=min(({'guard':g[0],'support':s[0],'travel_sum_m':D(g,guard_point)+D(s,support_point)} for g,s in permutations(formations)),key=lambda x:x['travel_sum_m'])
result={'source_sha':'dd28965238a83c29ae824f7225d90e3b2c8bfce4','symbol':'PlanBuilder.AssignGuardAndSupport','input':{'formations':formations,'guard_point':guard_point,'support_point':support_point},'translated_cb2':chosen,'joint_travel_reference':reference,'scope':'fixed geometric role subproblem only; Guard-first prioritization may be intentional. Not a production bug verdict, C# build, full CB2 reproduction or game performance comparison.'}
Path(__file__).with_name('results').joinpath('cb2_baseline_probe.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))

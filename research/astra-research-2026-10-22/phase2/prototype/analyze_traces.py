"""Descriptive ABA/target-change checks on the explicitly saved representative traces."""
import json
from pathlib import Path
from collections import defaultdict


def main():
    root=Path(__file__).resolve().parents[1]/'results'; rows=[]
    for name in ('final_traces.json','selected_failure_traces.json','bilateral_close_traces.json'):
        for run in json.loads((root/name).read_text()):
            for side,key in ((0,'decisions'),(1,'right_decisions')):
                histories=defaultdict(list)
                for d in run.get(key,[]):
                    for order in d['orders']:
                        sig=(order['kind'],order.get('target'),round(order.get('x',0),3),round(order.get('y',0),3))
                        history=histories[order['actor']]
                        if not history or history[-1][1]!=sig:history.append((d['t'],sig))
                if not histories:continue
                aba=[];targets=0
                for actor,history in histories.items():
                    for a,b,c in zip(history,history[1:],history[2:]):
                        if a[1]==c[1] and a[1]!=b[1]:aba.append(dict(actor=actor,times=[a[0],b[0],c[0]],sequence=[a[1],b[1],c[1]]))
                    targets+=sum(a[1][1] is not None and b[1][1] is not None and a[1][1]!=b[1][1] for a,b in zip(history,history[1:]))
                rows.append(dict(source=name,seed=run.get('seed'),method=run['method'],variant=run['variant'],opponent=run['opponent'],side=side,aba_count=len(aba),aba=aba,target_switches=targets))
    out={'scope':'Saved representative/posthoc traces only; command signatures, not every episode or game oscillation proof',
         'rows':rows,'max_aba':max(r['aba_count'] for r in rows),'rows_with_aba':sum(r['aba_count']>0 for r in rows)}
    (root/'trace_behavior_analysis.json').write_text(json.dumps(out,indent=2))
    print(json.dumps({k:v for k,v in out.items() if k!='rows'}))


if __name__=='__main__':main()

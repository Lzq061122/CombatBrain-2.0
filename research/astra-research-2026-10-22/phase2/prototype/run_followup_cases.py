"""Post-hoc diagnostic traces, NOT held-out confirmation or parameter tuning.
Close symmetric starts test active bilateral coupling after far symmetric starts stalled.
"""
import json
from pathlib import Path
from run_dynamic import scenario,episode,summarize


def main():
    root=Path(__file__).resolve().parents[1]/'results'
    traces=[]
    for seed,variant,opponent in [(9301,'nominal','counter'),(9304,'nominal','counter'),(9304,'sticky_exit','counter')]:
        initial=scenario(seed)
        row=episode(initial,'staged',opponent,variant,full_trace=True)
        row.update(seed=seed,n=3,initial=initial,selection_reason='posthoc pressure and cost-stop tail, no parameter tuning')
        traces.append(row)
    (root/'selected_failure_traces.json').write_text(json.dumps(traces))
    rows=[]; full=[]
    for seed in range(7700,7704):
        for method in ('simple','staged'):
            for opponent in ('planner:simple','planner:staged'):
                for variant in ('nominal','sticky_exit'):
                    initial=scenario(seed)
                    for u in initial['units']: u['x']+=30 if u['side']==0 else -30
                    row=episode(initial,method,opponent,variant,offset=seed%3,delay=seed%2,full_trace=seed==7700)
                    row.update(seed=seed,n=3,geometry='symmetric close-start',initial=initial)
                    if seed==7700: full.append(row.copy())
                    rows.append({k:v for k,v in row.items() if k not in ('frames','decisions','right_decisions')})
    (root/'bilateral_close_raw.jsonl').write_text(''.join(json.dumps(x)+'\n' for x in rows))
    (root/'bilateral_close_summary.json').write_text(json.dumps(summarize(rows),indent=2))
    (root/'bilateral_close_traces.json').write_text(json.dumps(full))
    print(json.dumps({'posthoc_close_runs':len(rows),'with_actual_damage':sum(x['metrics']['damage_dealt']>0 for x in rows),'orders_rejected':sum(x['metrics']['orders_rejected'] for x in rows)}))


if __name__=='__main__': main()

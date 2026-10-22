"""Portable Phase 2 reproduction entry point; Python standard library only.

Run in a copy if you want to preserve the supplied results: this command updates
named result files and timings. It never accesses the network or production code.
Historical invalid comparator batches are deliberately not regenerated.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parent


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--full', action='store_true', help='also rerun the final comparison and all current diagnostic suites')
    args = parser.parse_args()
    evidence = ROOT / 'evidence'
    evidence.mkdir(exist_ok=True)
    (ROOT / 'results').mkdir(exist_ok=True)
    env = dict(os.environ, PYTHONDONTWRITEBYTECODE='1')
    runs = []
    start = time.perf_counter()
    with (evidence / 'reproduction.log').open('w', encoding='utf-8') as log:
        def run(label, argv, expected=0):
            print(label, flush=True)
            tick = time.perf_counter()
            result = subprocess.run([sys.executable, *argv], cwd=ROOT, env=env,
                                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                    text=True, timeout=600)
            log.write(f'\n[{label}] {argv!r}\n{result.stdout}\n')
            log.flush()
            runs.append(dict(label=label, arguments=argv, returncode=result.returncode,
                             expected_returncode=expected, seconds=time.perf_counter()-tick))
            if result.returncode != expected:
                print(result.stdout)
                raise RuntimeError(f'{label}: exit {result.returncode}, expected {expected}')

        for label, folder, pattern in (
            ('inherited_baseline_tests', 'prototype/baseline', 'test_*.py'),
            ('feedback_and_search_tests', 'prototype', 'test_feedback_phase2.py'),
            ('temporal_tests', 'prototype', 'test_temporal.py'),
            ('independent_world_tests', 'prototype', 'test_world.py'),
            ('phase1_adapter_tests', 'prototype', 'test_adapter.py'),
            ('calibration_tests', 'calibration', 'test_analyzer.py'),
        ):
            run(label, ['-m', 'unittest', 'discover', '-s', folder, '-p', pattern, '-v'])
        run('independent_review_probes', ['prototype/test_phase2_review.py', '--output', 'results/review_final.json'])
        review = json.loads((ROOT / 'results/review_final.json').read_text())
        assert all(v['safe'] for kind in ('temporal', 'controller') for v in review[kind].values())
        run('observed_temporal_execution', ['prototype/run_temporal_execution.py'])
        bridge = json.loads((ROOT / 'results/temporal_execution.json').read_text())
        assert all(all(c[k].values()) for c in bridge['cases'] for k in ('checks', 'negative_checks'))
        assert all(bridge['contrast_checks'].values())
        run('current_smoke', ['prototype/run_dynamic.py', '--suite', 'smoke'])
        run('synthetic_calibration_example', ['calibration/analyze.py', 'calibration/example.csv', '--output', 'calibration/example_analysis.json'])
        run('reject_synthetic_as_game', ['calibration/analyze.py', 'calibration/example.csv', '--require-game'], expected=2)
        if args.full:
            run('same_model_baseline_challenges', ['prototype/run_baseline_challenges.py'])
            for suite in ('final', 'ablations', 'scale', 'bilateral'):
                run('dynamic_' + suite, ['prototype/run_dynamic.py', '--suite', suite])
            run('model_active_budget_transform_diagnostics', ['prototype/run_diagnostics.py'])
            run('posthoc_failure_and_close_bilateral', ['prototype/run_followup_cases.py'])
            run('representative_trace_analysis', ['prototype/analyze_traces.py'])
    sources = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest()
               for p in sorted(ROOT.rglob('*.py')) if '__pycache__' not in p.parts}
    receipt = dict(status='PASS', mode='full' if args.full else 'quick',
                   python=sys.version, executable=sys.executable, platform=platform.platform(),
                   elapsed_seconds=time.perf_counter()-start, commands=runs, sources=sources,
                   game_runtime='NOT_RUN', evidence='SYNTHETIC_AND_CONTRACT_ONLY',
                   note='Timings and absolute log paths may differ. Historical invalid batches are retained, not regenerated.')
    (evidence / 'reproduction.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(f'PASS: {len(runs)} commands; {receipt["elapsed_seconds"]:.2f}s; game NOT_RUN', flush=True)


if __name__ == '__main__':
    main()

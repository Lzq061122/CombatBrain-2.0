"""Build, independently extract, fully reproduce, and verify this research ZIP.

Local-only release helper. The archive and its checksum are written beside this
directory. Third-party paper PDFs/text extracts and Python caches are excluded.
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import zipfile

ROOT = Path(__file__).resolve().parent
ARCHIVE = ROOT.with_suffix('.zip')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def included():
    return sorted(p for p in ROOT.rglob('*') if p.is_file()
                  and not any(x in p.parts for x in ('__pycache__', '.git'))
                  and p.name != '.DS_Store'
                  and p.suffix != '.pdf' and not p.name.endswith('.pdf.selected.txt'))


def build():
    files = [p for p in included() if p.name != 'MANIFEST.sha256']
    manifest = ROOT / 'MANIFEST.sha256'
    manifest.write_text(''.join(f'{digest(p)}  {p.relative_to(ROOT).as_posix()}\n' for p in files))
    with zipfile.ZipFile(ARCHIVE, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in included():
            archive.write(path, f'{ROOT.name}/{path.relative_to(ROOT).as_posix()}')
    return len(files)


def verify_manifest(root):
    rows = (root / 'MANIFEST.sha256').read_text().splitlines()
    for row in rows:
        expected, name = row.split('  ', 1)
        assert digest(root / name) == expected, name
    return len(rows)


def semantic(value):
    # Strip only measured wall-clock fields; preserve physical time, decisions,
    # resource accounting, model-call counts, and all outcomes.
    if isinstance(value, dict):
        return {k: semantic(v) for k, v in value.items()
                if not k.endswith('_ms') and k not in ('total_ms', 'episode_ms')}
    if isinstance(value, list):
        return [semantic(v) for v in value]
    return value


def read_result(path):
    if path.suffix == '.jsonl':
        return [json.loads(line) for line in path.read_text().splitlines() if line]
    return json.loads(path.read_text())


def main():
    start = time.perf_counter()
    before = {p.relative_to(ROOT).as_posix(): digest(p) for p in included()}
    build()
    print('Built archive; extracting into independent temporary directory', flush=True)
    with tempfile.TemporaryDirectory(prefix='combatbrain-phase2-replay-') as temp:
        temp = Path(temp)
        with zipfile.ZipFile(ARCHIVE) as archive:
            assert archive.testzip() is None
            archive.extractall(temp)
        copy = temp / ROOT.name
        count = verify_manifest(copy)
        print(f'Manifest PASS: {count} files; running full reproduction', flush=True)
        result = subprocess.run([sys.executable, str(copy / 'reproduce.py'), '--full'],
                                cwd=temp, env=dict(os.environ, PYTHONDONTWRITEBYTECODE='1'))
        assert result.returncode == 0, 'extracted full reproduction failed'
        replay = json.loads((copy / 'evidence/reproduction.json').read_text())
        comparison_files = [
            'results/smoke_raw.jsonl', 'results/final_raw.jsonl',
            'results/ablations_raw.jsonl', 'results/scale_raw.jsonl',
            'results/bilateral_raw.jsonl', 'results/bilateral_close_raw.jsonl',
            'results/temporal_execution.json', 'results/model_challenges.json',
            'results/active_cases.json', 'results/dynamic_transform.json',
            'results/dynamic_budget_checks.json', 'results/selected_failure_traces.json',
            'results/trace_behavior_analysis.json', 'calibration/example_analysis.json',
        ]
        comparisons = {}
        for name in comparison_files:
            same = semantic(read_result(ROOT / name)) == semantic(read_result(copy / name))
            comparisons[name] = same
            assert same, f'deterministic replay mismatch: {name}'
        (ROOT / 'evidence/delivery_reproduction.log').write_text(
            (copy / 'evidence/reproduction.log').read_text())
        # Preserve source data and its original timing. Store only independent
        # replay receipts; do not silently replace the reported experiments.
        receipt = dict(status='PASS', test='extract + manifest + full reproduction + semantic result comparison',
                       checked_manifest_files=count, replay=replay, semantic_comparisons=comparisons,
                       stripped_comparison_fields='wall-clock keys ending _ms only',
                       elapsed_seconds=time.perf_counter()-start, game='NOT_RUN')
        (ROOT / 'evidence/delivery_check.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n')
    # Only release metadata and the manifest may change in the source tree.
    allowed = {'MANIFEST.sha256', 'evidence/delivery_check.json', 'evidence/delivery_reproduction.log'}
    assert all(digest(ROOT / p) == h for p, h in before.items() if p not in allowed)
    final_count = build()
    with zipfile.ZipFile(ARCHIVE) as archive:
        assert archive.testzip() is None
        for line in (ROOT / 'MANIFEST.sha256').read_text().splitlines():
            expected, name = line.split('  ', 1)
            assert hashlib.sha256(archive.read(f'{ROOT.name}/{name}')).hexdigest() == expected, name
    checksum = digest(ARCHIVE)
    ARCHIVE.with_suffix('.zip.sha256').write_text(f'{checksum}  {ARCHIVE.name}\n')
    print(json.dumps(dict(status='PASS', archive=str(ARCHIVE), bytes=ARCHIVE.stat().st_size,
                          files=final_count+1, sha256=checksum), ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()

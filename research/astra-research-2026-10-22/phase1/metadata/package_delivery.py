"""Package this research and replay the portable artifact, without network/game access.
Re-running updates delivery receipts, MANIFEST.sha256 and the sibling ZIP.
"""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ARCHIVE = ROOT.parent / (ROOT.name + '.zip')
DIRS = ('prototype', 'evidence', 'metadata', 'sources/cb2_default',
        'sources/cb2_research', 'sources/legacy', 'sources/tcl/default',
        'sources/tcl/phase10', 'sources/cbath/default')
FILES = ('README.md', '主报告.md', 'sources/external/README.md',
         'sources/external/manifest.json')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inputs():
    paths = {ROOT / x for x in FILES}
    for directory in DIRS:
        paths.update(p for p in (ROOT / directory).rglob('*') if p.is_file()
                     and not any(x in ('.git', '__pycache__', '.DS_Store') for x in p.parts)
                     and p.suffix != '.pyc')
    return sorted(paths)


def package():
    paths = inputs()
    manifest = ROOT / 'MANIFEST.sha256'
    manifest.write_text(''.join(f'{sha(p)}  {p.relative_to(ROOT).as_posix()}\n' for p in paths))
    with zipfile.ZipFile(ARCHIVE, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as out:
        for p in paths + [manifest]:
            out.write(p, (Path(ROOT.name) / p.relative_to(ROOT)).as_posix())
    with zipfile.ZipFile(ARCHIVE) as check:
        assert check.testzip() is None
    return len(paths)


def main():
    # Deliverable links are checked here; upstream repository docs retain their own links.
    links = []
    for name in ('主报告.md', 'README.md', 'prototype/README.md'):
        path = ROOT / name
        for dest in re.findall(r'\]\(([^)]+)\)', path.read_text()):
            if '://' in dest or dest.startswith('#'):
                continue
            target = (path.parent / dest.split('#')[0]).resolve()
            assert target.is_file(), (name, dest)
            links.append({'document': name, 'target': dest})
    package()
    receipt = {'scope': 'Extracted ZIP portability replay; no game or C# execution',
               'python': sys.executable, 'planner_sha256': sha(ROOT / 'prototype/planner.py'),
               'report_sha256': sha(ROOT / '主报告.md'), 'local_links': links, 'commands': []}
    with tempfile.TemporaryDirectory(prefix='combatbrain_delivery_') as tmp:
        with zipfile.ZipFile(ARCHIVE) as bundle:
            bundle.extractall(tmp)
        extracted = Path(tmp) / ROOT.name
        verified = 0
        for line in (extracted / 'MANIFEST.sha256').read_text().splitlines():
            digest, name = line.split('  ', 1)
            assert sha(extracted / name) == digest, name
            verified += 1
        receipt['extracted_manifest_files_verified'] = verified
        commands = [
            ['-m', 'unittest', 'discover', '-s', 'prototype', '-p', 'test_*.py', '-v'],
            ['prototype/run_experiments.py'],
            ['prototype/run_feedback.py'],
            ['prototype/cb2_baseline_probe.py'],
            ['prototype/test_method_review.py'],
            ['metadata/verify_native_snapshots.py'],
        ]
        logs = ROOT / 'metadata/delivery_outputs'
        logs.mkdir(exist_ok=True)
        for i, args in enumerate(commands, 1):
            result = subprocess.run([sys.executable, *args], cwd=extracted,
                                    text=True, capture_output=True, timeout=120)
            logfile = logs / f'{i:02d}.txt'
            logfile.write_text(result.stdout + result.stderr)
            receipt['commands'].append({'command': ['python3', *args],
                                        'exit_code': result.returncode,
                                        'output': logfile.relative_to(ROOT).as_posix()})
            assert result.returncode == 0, args
            if i == 1:
                assert 'Ran 30 tests' in result.stderr and '\nOK\n' in result.stderr
                receipt['unittest_count'] = 30
        original = json.loads((ROOT / 'prototype/results/summary.json').read_text())
        replay = json.loads((extracted / 'prototype/results/summary.json').read_text())
        fields = ['config', 'n', 'comparison', 'ablation', 'metamorphic_max_delta',
                  'sensitivity_changes', 'sensitivity_n', 'sequence_changes']
        for field in fields:
            assert original[field] == replay[field], field
        receipt['deterministic_summary_fields_match'] = fields
        checks = json.loads((extracted / 'prototype/results/method_review.json').read_text())
        assert checks['source_sha256'] == receipt['planner_sha256']
        assert all(r['accounting_exact'] and r['search_calls_within_budget'] for r in checks['budget_audit'])
        assert not checks['multiple_support_final_window']['feasible']
        assert checks['maximin_vs_all_scenario_retention']['default_reaches_maximin_value']
        receipt['method_probe_key_repairs_confirmed'] = True
    receipt['status'] = 'PASS'
    (ROOT / 'metadata/delivery_check.json').write_text(json.dumps(receipt, indent=2, ensure_ascii=False) + '\n')
    count = package()  # Include replay evidence; verify final ZIP CRC after writing.
    print(json.dumps({'status': 'PASS', 'archive': str(ARCHIVE),
                      'bytes': ARCHIVE.stat().st_size, 'files_plus_manifest': count + 1,
                      'sha256': sha(ARCHIVE), 'commands_replayed': len(commands)},
                     ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()

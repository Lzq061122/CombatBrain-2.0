import copy
import csv
import tempfile
import unittest
from pathlib import Path
from analyze import FIELDS, analyze, load
from make_example import create


class AnalysisTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.path = Path(self.tmp.name) / 'fixture.csv'
        create(self.path)

    def tearDown(self):
        self.tmp.cleanup()

    def rewrite(self, fn):
        with self.path.open(newline='') as f:
            rows = list(csv.DictReader(f))
        fn(rows)
        with self.path.open('w', newline='') as f:
            w = csv.DictWriter(f, fieldnames=FIELDS)
            w.writeheader(); w.writerows(rows)

    def test_synthetic_and_censoring(self):
        result = analyze(load(self.path))
        self.assertFalse(result['game_evidence_present'])
        eta = next(x for x in result['cohorts'] if x['cohort']['experiment'] == 'ETA')['analysis']['ready_at_s']
        self.assertEqual(eta['censored_rows'], 1)
        self.assertEqual(eta['interval_misses_proven_by_censor'], 1)
        self.assertEqual(eta['signed_error_s']['n'], 8)
        fix = next(x for x in result['cohorts'] if x['cohort']['experiment'] == 'FIX')['analysis']
        self.assertEqual(fix['support_censored_rows'], 1)
        self.assertEqual(fix['paired']['joint_minus_none']['support_arrival_s']['left_minus_right']['n'], 4)

    def test_no_information_causal_claim_without_matched_prefix(self):
        rows = load(self.path)
        for r in rows:
            if r['experiment'] == 'PRESSURE' and r['arm'] == 'blind':
                r['physical_prefix_id'] = 'different-prefix'
        pressure = next(x for x in analyze(rows)['cohorts'] if x['cohort']['experiment'] == 'PRESSURE')['analysis']
        self.assertEqual(pressure['paired']['observe_minus_blind']['enemy_killed']['left_minus_right']['n'], 0)

    def test_reject_nonfinite_and_duplicate(self):
        self.rewrite(lambda rows: rows[0].update(value='NaN'))
        with self.assertRaises(ValueError):
            load(self.path)
        create(self.path)
        self.rewrite(lambda rows: rows.append(copy.deepcopy(rows[0])))
        with self.assertRaises(ValueError):
            load(self.path)

    def test_do_not_pool_rbm_or_morale(self):
        rows = load(self.path)
        extra = copy.deepcopy(rows[0])
        extra.update(mod_profile='SYNTHETIC-RBM', morale='deathmatch')
        result = analyze(rows + [extra])
        self.assertEqual(len(result['cohorts']), 5)

    def test_reject_fixture_relabelled_as_game(self):
        self.rewrite(lambda rows: [r.update(evidence_kind='GAME') for r in rows])
        with self.assertRaises(ValueError):
            load(self.path)


if __name__ == '__main__':
    unittest.main()

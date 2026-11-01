import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('analyze_session', Path(__file__).parents[1] / 'analyze_session.py')
analysis = importlib.util.module_from_spec(spec)
spec.loader.exec_module(analysis)


class AnalysisTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.folder = Path(self.temp.name)
        (self.folder / 'journal').mkdir()
        self.meta = dict(participant_id='SOFTWARE_TEST', session_id='test', mode='DEVELOPMENT', experiment_type='A', config_hash='test_hash')
        self.write_meta()

    def tearDown(self):
        self.temp.cleanup()

    def write_meta(self):
        text = json.dumps(self.meta)
        (self.folder / 'session_metadata.json').write_text(text)
        (self.folder / 'session_metadata.json.sha256').write_text(hashlib.sha256(text.encode()).hexdigest())

    def trial(self, index, **updates):
        trial = dict(self.meta, condition_theta=0, technical_failure=False, is_training=False,
                     joint_accuracy=0, game_success=False, timeout=False, game_first_response_seconds=-1)
        trial.update(updates)
        payload = json.dumps(trial, separators=(',', ':'))
        digest = hashlib.sha256(payload.encode()).hexdigest()
        (self.folder / 'journal' / f'{index}.committed.json').write_text('{"sha256":"' + digest + '","record":' + payload + '}')

    def test_no_response_is_zero_and_technical_failure_excluded(self):
        self.trial(1, joint_accuracy=0, timeout=True)
        self.trial(2, joint_accuracy=1)
        self.trial(3, technical_failure=True, joint_accuracy=1)
        result = analysis.summarize(self.folder)['by_theta'][0]
        self.assertEqual(result['valid'], 2)
        self.assertEqual(result['joint_accuracy'], .5)
        self.assertEqual(result['timeout_rate'], .5)
        self.assertAlmostEqual(result['technical_failure_rate'], 1 / 3)

    def test_game_success_keeps_timeout_in_denominator(self):
        self.meta['experiment_type'] = 'B'
        self.write_meta()
        self.trial(1, game_success=True, game_first_response_seconds=1.5)
        self.trial(2, timeout=True)
        result = analysis.summarize(self.folder)['by_theta'][0]
        self.assertEqual(result['game_success_rate'], .5)
        self.assertEqual(result['successful_game_response_seconds'], 1.5)

    def test_corruption_refused(self):
        self.trial(1)
        path = self.folder / 'journal' / '1.committed.json'
        path.write_text(path.read_text().replace('SOFTWARE_TEST', 'CHANGED'))
        with self.assertRaisesRegex(ValueError, 'checksum mismatch'):
            analysis.summarize(self.folder)

    def test_provenance_mismatch_refused(self):
        self.trial(1, mode='MAIN')
        with self.assertRaisesRegex(ValueError, 'Provenance mismatch'):
            analysis.summarize(self.folder)

    def test_training_is_not_participant_evidence(self):
        self.trial(1, is_training=True, joint_accuracy=1)
        result = analysis.summarize(self.folder)['by_theta'][0]
        self.assertEqual(result['valid'], 0)
        self.assertIsNone(result['joint_accuracy'])


if __name__ == '__main__':
    unittest.main()

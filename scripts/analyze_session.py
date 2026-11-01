#!/usr/bin/env python3
"""Read-only descriptive summaries. Does not pool protocols, modes, experiments or participants."""
import argparse
import csv
import hashlib
import json
from pathlib import Path


def summarize(folder):
    folder = Path(folder)
    raw = (folder / 'session_metadata.json').read_text(encoding='utf-8')
    if hashlib.sha256(raw.encode()).hexdigest() != (folder / 'session_metadata.json.sha256').read_text():
        raise ValueError('Metadata checksum mismatch')
    meta = json.loads(raw)
    records = []
    for file in sorted((folder / 'journal').glob('*.committed.json')):
        text = file.read_text(encoding='utf-8')
        envelope = json.loads(text)
        # The journal hashes the exact payload bytes, including Unity JSON whitespace.
        prefix = '{"sha256":"' + envelope['sha256'] + '","record":'
        if not text.startswith(prefix) or not text.endswith('}'):
            raise ValueError(f'Malformed envelope: {file.name}')
        payload = text[len(prefix):-1]
        if hashlib.sha256(payload.encode()).hexdigest() != envelope['sha256']:
            raise ValueError(f'Journal checksum mismatch: {file.name}')
        trial = envelope['record']
        for key in ('participant_id', 'session_id', 'experiment_type', 'mode', 'config_hash'):
            if trial[key] != meta[key]:
                raise ValueError(f'Provenance mismatch: {file.name}: {key}')
        records.append(trial)
    by_theta = []
    for theta in sorted({t['condition_theta'] for t in records}):
        trials = [t for t in records if t['condition_theta'] == theta]
        valid = [t for t in trials if not t['technical_failure'] and not t['is_training']]
        def mean(key, rows=valid):
            values = [t[key] for t in rows if t.get(key) is not None and t[key] >= 0]
            return sum(values) / len(values) if values else None
        by_theta.append({
            'theta': theta, 'committed': len(trials), 'valid': len(valid),
            'technical_failure_rate': sum(t['technical_failure'] for t in trials) / len(trials),
            'joint_accuracy': mean('joint_accuracy') if meta['experiment_type'] == 'A' else None,
            'timeout_rate': sum(t['timeout'] for t in valid) / len(valid) if valid else None,
            'game_success_rate': sum(t['game_success'] for t in valid) / len(valid) if valid and meta['experiment_type'] == 'B' else None,
            'successful_game_response_seconds': mean('game_first_response_seconds', [t for t in valid if t['game_success']]),
        })
    return {'provenance': {k: meta[k] for k in ('participant_id', 'session_id', 'mode', 'experiment_type', 'config_hash')},
            'interpretation': 'Descriptive only. Simulation and training are not participant evidence. Failures/timeouts remain in the B success-rate denominator.',
            'by_theta': by_theta}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('session_folder', type=Path)
    args = parser.parse_args()
    print(json.dumps(summarize(args.session_folder), ensure_ascii=False, indent=2))

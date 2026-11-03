#!/usr/bin/env python3
"""Read-only descriptive summaries. Does not pool protocols, modes, experiments or participants."""
import argparse
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
        if meta.get('software_version') == 'dts-full-circle-v3' or trial.get('protocol_version') == 'dts-full-circle-v3':
            if trial.get('protocol_version') != meta.get('software_version'):
                raise ValueError(f'Protocol mismatch: {file.name}')
        records.append(trial)
    by_theta = []
    for theta in sorted({t['condition_theta'] for t in records}):
        trials = [t for t in records if t['condition_theta'] == theta]
        valid = [t for t in trials if not t['technical_failure'] and not t['is_training']]
        def mean(key, rows=valid):
            values = [t[key] for t in rows if t.get(key) is not None and t[key] >= 0]
            return sum(values) / len(values) if values else None
        # Full-circle development records validate plumbing and stimulus geometry only.
        performance = valid if meta.get('software_version') != 'dts-full-circle-v3' or meta['mode'] in ('PILOT', 'MAIN') else []
        rates = [(t['total_threat_count'] - t['retained_count']) / t['total_threat_count']
                 for t in valid if t.get('total_threat_count', 0) > 0 and 'retained_count' in t]
        by_theta.append({
            'theta': theta, 'committed': len(trials), 'valid': len(valid),
            'technical_failure_rate': sum(t['technical_failure'] for t in trials) / len(trials),
            'joint_accuracy': mean('joint_accuracy', performance) if meta['experiment_type'] == 'A' else None,
            'retained_count': mean('retained_count'),
            'suppression_rate': sum(rates) / len(rates) if rates else None,
            'retained_recognition_rate': mean('retained_recognition_rate', performance),
            'direction_error': mean('direction_error', performance),
            'direction_accuracy': mean('direction_accuracy', performance),
            'distance_accuracy': mean('distance_accuracy', performance),
            'response_time': mean('response_time', performance),
            'confusion_rating': mean('confusion_rating', performance),
            'response_precision': mean('response_precision', performance),
            'timeout_rate': sum(t['timeout'] for t in performance) / len(performance) if performance else None,
            'game_success_rate': sum(t['game_success'] for t in valid) / len(valid) if valid and meta['experiment_type'] == 'B' else None,
            'successful_game_response_seconds': mean('game_first_response_seconds', [t for t in valid if t['game_success']]),
        })
    return {'provenance': {k: meta[k] for k in ('participant_id', 'session_id', 'mode', 'experiment_type', 'config_hash')},
            'protocol_version': meta.get('software_version'),
            'interpretation': 'Descriptive only. Simulation and training are not participant evidence. Failures/timeouts remain in the B success-rate denominator.',
            'by_theta': by_theta}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('session_folder', type=Path)
    args = parser.parse_args()
    print(json.dumps(summarize(args.session_folder), ensure_ascii=False, indent=2))

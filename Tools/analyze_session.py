"""Descriptive summaries only; no inference of a hardware or haptic benefit."""
import csv
import statistics
import sys
from collections import defaultdict

with open(sys.argv[1], newline='', encoding='utf-8-sig') as f:
    rows = list(csv.DictReader(f))
if not rows:
    raise SystemExit('No recorded trials.')
if any(r['practice'] == 'True' or r['udp_enabled_at_onset'] != 'True' for r in rows):
    print('PROTOTYPE / PRACTICE / NO OUTPUT: do not interpret as measured haptic benefit.')
print('All timings are software request timings. Physical output is unverified.')
groups = defaultdict(list)
for row in rows:
    groups[row['condition']].append(row)
for condition, group in groups.items():
    answered = [r for r in group if r['outcome'] == 'answered']
    print(f'\n{condition}: {len(answered)} answered / {len(group)} recorded')
    print('  timeout:', sum(r['outcome'] == 'timeout' for r in group),
          'interrupted:', sum(r['outcome'] in ('aborted', 'focus_lost') for r in group))
    if answered:
        for metric in ('target_correct', 'face_correct', 'height_correct', 'distance_correct'):
            print(f'  {metric}: {sum(r[metric] == "True" for r in answered) / len(answered):.1%} (answered only)')
        print(f'  median selection: {statistics.median(float(r["selection_ms"]) for r in answered):.1f} ms')

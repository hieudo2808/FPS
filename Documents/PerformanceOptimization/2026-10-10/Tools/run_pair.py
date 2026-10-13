"""Interleave baseline/candidate to expose drift; three independent Players each."""
import argparse
import pathlib
import json
import re
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument('baseline')
parser.add_argument('candidate')
parser.add_argument('output', type=pathlib.Path)
args = parser.parse_args()

def valid_scenarios(folder):
    valid = set()
    for run in folder:
        if not run.is_dir() or (run/'excluded.txt').exists() or not (run/'focus.jsonl').exists():
            continue
        events = [json.loads(line) for line in (run/'focus.jsonl').read_text().splitlines()]
        for fixture in run.glob('*-r1.txt'):
            fields = dict(re.findall(r'(\w+)=([^;\n]+)', fixture.read_text()))
            start, end = (int(fields[k])/int(fields['qpcFrequency']) for k in ['qpcStart', 'qpcEnd'])
            previous = [event for event in events if event['qpcSeconds'] <= start]
            if fields['valid'] == 'True' and previous and previous[-1]['playerForeground'] and all(
                event['playerForeground'] for event in events if start < event['qpcSeconds'] <= end):
                valid.add(fixture.stem.split('-r')[0])
    return valid

for run in range(1, 4):
    for label, executable, variant in [('NoOcclusion', args.baseline, 'no-occlusion'),
                                       ('Occlusion', args.candidate, 'normal')]:
        folder = args.output/label
        while True:
            existing = list(folder.glob(f'Run{run}*'))
            missing = [scenario for scenario in ['factory3', 'factory43', 'lab27', 'lab70', 'asylum10']
                       if scenario not in valid_scenarios(existing)]
            if not missing:
                break
            attempt = folder/f'Run{run}' if not existing else folder/f'Run{run}Retry{len(existing)}'
            assert not attempt.exists(), 'Never overwrite benchmark evidence'
            result = subprocess.run([sys.executable, str(pathlib.Path(__file__).with_name('run_player.py')),
                executable, str(attempt), '--repeats', '1', '--variant', variant, '--scenarios', ','.join(missing)])
            if result.returncode and not (attempt/'failed.txt').exists():
                raise RuntimeError('Player or collector failed; inspect '+str(attempt))
            if (attempt/'failed.txt').exists() and not (attempt/'failed.txt').read_text().startswith('Invalid measurement '):
                raise RuntimeError((attempt/'failed.txt').read_text())

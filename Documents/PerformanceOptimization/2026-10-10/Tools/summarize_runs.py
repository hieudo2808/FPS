"""Summarize valid fixture windows; keep memory accounting and inclusive timings separate."""
import argparse
import csv
import json
import pathlib
import re
import statistics

parser = argparse.ArgumentParser()
parser.add_argument('folder', type=pathlib.Path)
args = parser.parse_args()

def stats(values):
    values = sorted(values)
    if not values:
        return 'unavailable'
    return {'mean': statistics.mean(values), 'p95': values[int((len(values)-1)*.95)],
            'p99': values[int((len(values)-1)*.99)], 'max': values[-1], 'count': len(values)}

assert stats([1, 2, 3])['mean'] == 2 and stats([]) == 'unavailable'

def foreground_valid(events, start, end):
    previous = [e for e in events if e['qpcSeconds'] <= start]
    return bool(previous and previous[-1]['playerForeground'] and all(
        e['playerForeground'] for e in events if start < e['qpcSeconds'] <= end))

assert foreground_valid([{'qpcSeconds': 0, 'playerForeground': True}], 1, 2)
assert not foreground_valid([{'qpcSeconds': 0, 'playerForeground': True},
    {'qpcSeconds': 1.5, 'playerForeground': False}], 1, 2)
assert not foreground_valid([], 1, 2)
rows = []
for run in sorted(args.folder.glob('*/*')):
    if not run.is_dir():
        continue
    system_file = run/'system.jsonl'
    system = [json.loads(line) for line in system_file.read_text().splitlines()] if system_file.exists() else []
    trace_file = run/'presentation.csv'
    trace = list(csv.DictReader(trace_file.open())) if trace_file.exists() else []
    log = (run/'PresentMon.log').read_bytes().decode('utf-16', errors='replace') if (run/'PresentMon.log').exists() else ''
    trace_lost = 'lost' in log.lower()
    focus_file = run/'focus.jsonl'
    focus = [json.loads(line) for line in focus_file.read_text().splitlines()] if focus_file.exists() else []
    for fixture in sorted(run.glob('*-r1.txt')):
        fields = dict(re.findall(r'(\w+)=([^;\n]+)', fixture.read_text()))
        if fields.get('valid') != 'True' or (run/'excluded.txt').exists():
            rows.append({'file': str(fixture), 'valid': False, 'reason': fields.get('unfocusedFrames', 'excluded')})
            continue
        data = list(csv.DictReader(fixture.with_suffix('.csv').open()))
        frames = [float(r['deltaMs']) for r in data]
        assert len(frames) == int(fields['frames']) and fields['warmup'] == '30' and fields['seconds'] == '60'
        start, end, frequency = (int(fields[k]) for k in ['qpcStart', 'qpcEnd', 'qpcFrequency'])
        if not foreground_valid(focus, start/frequency, end/frequency):
            rows.append({'file': str(fixture), 'valid': False, 'reason': 'external foreground check failed or unavailable'})
            continue
        metrics = [r for r in system if start/frequency <= r['qpcSeconds'] <= end/frequency]
        # Exclude boundary frames whose Present includes the audit's post-window file output.
        presents = [r for r in trace if r.get('CPUStartQPC', 'NA') != 'NA' and r.get('TimeInQPC', 'NA') != 'NA'
                    and start <= int(r['CPUStartQPC']) <= int(r['TimeInQPC']) <= end]
        trace_valid = not trace_lost and abs(len(presents)-len(frames)) <= max(3, len(frames)*.01)
        row = {'file': str(fixture), 'valid': True, 'scenario': fixture.stem.split('-r')[0],
               'variant': fields['variant'], 'run': run.name, 'frames': stats(frames),
               'over33ms': sum(v > 33.333 for v in frames), 'over50ms': sum(v > 50 for v in frames),
               'setPass': fields.get('setPass'), 'triangles': fields.get('triangles'),
               'textureCurrentMB': fields.get('textureCurrentMB'), 'textureNonStreamingMB': fields.get('textureNonStreamingMB'),
               'externalTraceValid': trace_valid, 'presentationRows': len(presents)}
        for key in ['privateBytes', 'workingSetBytes', 'availablePhysicalBytes', 'systemMemoryLoad']:
            row[key] = stats([r[key] for r in metrics if r.get(key) is not None])
        row['gpuDedicatedInstances'] = {name: stats([r['gpuDedicatedInstances'][name] for r in metrics
            if name in (r.get('gpuDedicatedInstances') or {})]) for name in
            {name for r in metrics for name in (r.get('gpuDedicatedInstances') or {})}}
        for key in ['MsBetweenPresents', 'MsCPUBusy', 'MsGPUTime', 'MsGPUBusy', 'MsGPUWait']:
            row[key] = stats([float(r[key]) for r in presents if r.get(key, 'NA') != 'NA']) if trace_valid else 'unavailable'
        rows.append(row)

(args.folder/'summary.json').write_text(json.dumps(rows, indent=2), encoding='utf8')
for scenario in sorted({r['scenario'] for r in rows if r['valid']}):
    variants = {}
    for variant in ['no-occlusion', 'normal']:
        values = [r['frames']['p95'] for r in rows if r['valid'] and r['scenario'] == scenario and r['variant'] == variant]
        if values:
            variants[variant] = {'runs': len(values), 'p95Median': statistics.median(values), 'p95Range': [min(values), max(values)]}
    if len(variants) == 2:
        before, after = variants['no-occlusion'], variants['normal']
        variants['p95ChangePercent'] = (1-after['p95Median']/before['p95Median'])*100
        variants['threeIndependentRuns'] = before['runs'] == after['runs'] == 3
    print(scenario, json.dumps(variants))

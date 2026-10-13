"""Three independent Players, each running the same locked scenario sequence."""
import argparse
import pathlib
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument('exe')
parser.add_argument('output', type=pathlib.Path)
parser.add_argument('--scenarios', default='factory3,factory43,lab27,lab70,asylum10')
parser.add_argument('--variant', default='normal')
parser.add_argument('--runs', type=int, default=3)
args = parser.parse_args()
assert 1 <= args.runs <= 10
for run in range(1, args.runs + 1):
    subprocess.run([sys.executable, str(pathlib.Path(__file__).with_name('run_player.py')),
        args.exe, str(args.output/f'Run{run}'), '--repeats', '1', '--scenarios', args.scenarios,
        '--variant', args.variant], check=True)

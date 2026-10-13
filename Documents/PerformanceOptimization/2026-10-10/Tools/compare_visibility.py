"""Rank paused, same-camera occlusion pairs for visual review; no pass/fail inference."""
import argparse
import json
import pathlib
from PIL import Image, ImageChops, ImageStat

parser = argparse.ArgumentParser()
parser.add_argument('folder', type=pathlib.Path)
args = parser.parse_args()
rows = []
for reference in args.folder.glob('*-reference.png'):
    candidate = reference.with_name(reference.name.replace('-reference.png', '-occlusion.png'))
    if not candidate.exists():
        raise RuntimeError('Missing pair: ' + str(candidate))
    with Image.open(reference) as before, Image.open(candidate) as after:
        assert before.size == after.size == (1920, 1080)
        difference = ImageChops.difference(before.convert('RGB'), after.convert('RGB'))
        # Exclude the top/bottom HUD from the scene comparison.
        scene = difference.crop((0, 270, 1920, 810))
        changed = scene.convert('L').point(lambda v: 255 if v > 8 else 0).histogram()[255]
        rows.append({'reference': reference.name, 'candidate': candidate.name,
            'meanAbsoluteRgb': sum(ImageStat.Stat(scene).mean)/3,
            'changedFractionOver8': changed/(1920*540), 'bbox': scene.getbbox()})
assert rows, 'No pairs found'
rows.sort(key=lambda row: row['changedFractionOver8'], reverse=True)
(args.folder/'visibility-comparison.json').write_text(json.dumps(rows, indent=2), encoding='utf8')
print(json.dumps({'pairs': len(rows), 'largestDifferences': rows[:10]}, indent=2))

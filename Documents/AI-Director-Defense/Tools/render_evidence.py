"""Render measured JSONL only; assert event/stat consistency before publishing charts."""
import json
from pathlib import Path
import statistics
import sys
from PIL import Image, ImageDraw, ImageFont

folder = Path(__file__).resolve().parents[1] / "Captures"
captures = []
for path in ([folder / sys.argv[1]] if len(sys.argv) > 1 else sorted(folder.glob("*.jsonl"))):
    rows = [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines()]
    samples = [r for r in rows if r["recordType"] == "sample" and r["directorFound"]]
    spawns = [r for r in rows if r["recordType"] == "common-spawn"]
    if samples:
        captures.append((path, rows[0]["label"], samples, spawns))

path, label, samples, spawns = max(captures, key=lambda item: len(item[3]))
prefix = sys.argv[2] if len(sys.argv) > 2 else 'runtime'
assert "controlled" in label and len(spawns) > 0
groups = {}
for row in spawns:
    m = round(row["multiplier"], 2)
    assert abs(row["hp"] - 100 * m) < .01, row
    assert abs(row["damage"] - 10 * m) < .01, row
    assert abs(row["speed"] - 5) < .01, row
    groups.setdefault(m, []).append(row)
assert {0.9, 1.0, 1.1}.issubset(groups), groups.keys()

font_path = 'C:/Windows/Fonts/segoeui.ttf'
fonts = {size: ImageFont.truetype(font_path, size) for size in (20, 23, 26, 30, 38)}
white, ink, grid = '#ffffff', '#213547', '#dbe3e9'
colors = ['#257c63', '#276678', '#bc5138']
phases = {'Calm': 0, 'BuildUp': 1, 'Combat': 2, 'Peak': 3, 'Relax': 4}
t = [r['gameSeconds'] for r in samples]
lo, hi = min(t), max(t)
canvas = Image.new('RGB', (1800, 1280), white)
draw = ImageDraw.Draw(canvas)
draw.text((60, 24), 'AI Director / DDA — dữ liệu đo trong Unity Play Mode', font=fonts[38], fill=ink)
draw.text((60, 80), 'Lượt có kiểm soát: ép pha, bơm telemetry, đổi vị trí. Không phải thử nghiệm người chơi.', font=fonts[26], fill=ink)
x0, x1 = 210, 1735

def panel(top, low, high, title, ticks):
    bottom = top + 195
    draw.text((60, top - 36), title, font=fonts[26], fill=ink)
    for value, label in ticks:
        y = bottom - (value - low) / (high - low) * 170
        draw.line((x0, y, x1, y), fill=grid, width=1)
        draw.text((60, y - 13), str(label), font=fonts[23], fill=ink)
    for i in range(7):
        value = lo + (hi - lo) * i / 6
        x = x0 + (value - lo) / (hi - lo) * (x1 - x0)
        draw.line((x, top + 25, x, bottom), fill=grid, width=1)
    return lambda v: bottom - (v - low) / (high - low) * 170

def plot(values, scale, color, step=True):
    points = [(x0 + (time - lo) / (hi - lo) * (x1 - x0), scale(v)) for time, v in zip(t, values)]
    for a, b in zip(points, points[1:]):
        if step: draw.line([a, (b[0], a[1]), b], fill=color, width=3)
        else: draw.line([a, b], fill=color, width=3)

scale = panel(185, 0, 4, 'Pha adaptive thực tế', [(v,k) for k,v in phases.items()])
plot([phases[r['adaptivePhase']] for r in samples], scale, colors[1])
scale = panel(440, .85, 1.15, 'Multiplier đọc từ Director', [(.9,'0.90'), (1,'1.00'), (1.1,'1.10')])
plot([r['multiplier'] for r in samples], scale, colors[2])
max_alive = max(30, max(r['observedLivingEnemyHealth'] for r in samples))
scale = panel(695, 0, max_alive, 'Quái sống đo từ scene (xanh); bộ đếm Director (cam)', [(0,0),(10,10),(20,20),(30,30)])
plot([r['directorAlive'] for r in samples], scale, '#e1a647')
plot([r['observedLivingEnemyHealth'] for r in samples], scale, colors[0])
scale = panel(950, 80, 120, 'HP của quái tại sự kiện factory spawn thành công', [(90,90),(100,100),(110,110)])
for row in spawns:
    x = x0 + (row['gameSeconds']-lo)/(hi-lo)*(x1-x0)
    y = scale(row['hp'])
    draw.ellipse((x-5,y-5,x+5,y+5), fill='#754dab')
for i in range(7):
    value = lo + (hi-lo)*i/6
    x = x0 + (value-lo)/(hi-lo)*(x1-x0)
    draw.text((x-24, 1170), f'{value:.0f}', font=fonts[23], fill=ink)
draw.text((720,1210), 'Thời gian game (giây)', font=fonts[26], fill=ink)
canvas.save(folder/f'{prefix}-timeline.png')

levels = sorted(groups)
canvas = Image.new('RGB',(1800,690),white)
draw = ImageDraw.Draw(canvas)
draw.text((50,20), 'Giá trị đọc từ component của quái spawn thật', font=fonts[38], fill=ink)
draw.text((50,80), 'Độ khó tĩnh Medium, một host. Mỗi cột là dữ liệu trong log; quái cũ giữ stats lúc spawn.',font=fonts[26],fill=ink)
for panel_index,(key,title) in enumerate(zip(['hp','damage','speed'],['Máu quái mới','Sát thương cấu hình','Tốc độ cấu hình'])):
    left=60+panel_index*580
    draw.text((left,155),title,font=fonts[30],fill=ink)
    values=[statistics.mean(r[key] for r in groups[m]) for m in levels]
    maximum=max(values)*1.2
    draw.line((left,530,left+500,530),fill=ink,width=2)
    for i,(m,value) in enumerate(zip(levels,values)):
        x=left+35+i*160
        y=530-value/maximum*280
        draw.rectangle((x,y,x+100,530),fill=colors[i])
        draw.text((x+22,y-36),f'{value:g}',font=fonts[30],fill=ink)
        draw.text((x+24,550),f'{m:.1f}',font=fonts[26],fill=ink)
        draw.text((x+10,590),f'n={len(groups[m])}',font=fonts[23],fill=ink)
    draw.text((left+155,635),'DDA lúc spawn',font=fonts[26],fill=ink)
canvas.save(folder/f'{prefix}-stats.png')

summary = {"trace": path.name, "label": label, "sampleCount": len(samples),
           "phasesObserved": sorted(set(r["adaptivePhase"] for r in samples)),
           "playerHpObserved": sorted(set(p["hp"] for r in samples for p in r["players"])),
           "spawnGroups": []}
for m in levels:
    rows = groups[m]
    intervals = [b["gameSeconds"] - a["gameSeconds"] for a, b in zip(rows, rows[1:])
                 if a["adaptivePhase"] == b["adaptivePhase"] == "Combat"
                 and b["gameSeconds"] - a["gameSeconds"] < 5]
    summary["spawnGroups"].append({"multiplier": m, "count": len(rows),
        "hp": sorted(set(r["hp"] for r in rows)), "damage": sorted(set(r["damage"] for r in rows)),
        "speed": sorted(set(r["speed"] for r in rows)), "intervalPairs": len(intervals),
        "medianConsecutiveCombatIntervalSeconds": statistics.median(intervals) if intervals else None,
        "nearestPlayerDistanceMin": min(r["nearestPlayerDistance"] for r in rows),
        "nearestPlayerDistanceMax": max(r["nearestPlayerDistance"] for r in rows)})
(folder / f"{prefix}-summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(summary, ensure_ascii=False, indent=2))

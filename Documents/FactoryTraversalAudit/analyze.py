"""Analyze Unity-exported standing surfaces; geometric candidates are not gameplay verdicts.

Run with the bundled Python (numpy and Pillow), or use --self-check for the small graph check.
"""
import argparse
import collections
import json
import math
from pathlib import Path


def components(points, maximum_rise):
    """Optimistic connectivity: adjacent standing cells, ignoring wall sweeps and jump timing."""
    parents = list(range(len(points)))
    sizes = [1] * len(points)
    columns = collections.defaultdict(list)
    for i, point in enumerate(points):
        columns[int(point[0]), int(point[1])].append(i)

    def find(i):
        while parents[i] != i:
            parents[i] = parents[parents[i]]
            i = parents[i]
        return i

    falls = []
    for (x, z), current in columns.items():
        for dx, dz in ((1, 0), (0, 1), (1, 1), (-1, 1)):
            for a in current:
                for b in columns.get((x + dx, z + dz), ()):
                    delta = float(points[b][2] - points[a][2])
                    if abs(delta) > maximum_rise:
                        falls.append((a, b) if delta < 0 else (b, a))
                        continue
                    ra, rb = find(a), find(b)
                    if ra == rb:
                        continue
                    if sizes[ra] < sizes[rb]:
                        ra, rb = rb, ra
                    parents[rb] = ra
                    sizes[ra] += sizes[rb]
    return [find(i) for i in range(len(points))], falls


def self_check():
    # A ramp reconnects two levels; an isolated two-metre drop has only the downward edge.
    points = [(0, 0, 2), (1, 0, 2), (2, 0, 0), (3, 0, 0)]
    labels, falls = components(points, .85)
    assert labels[0] == labels[1] and labels[2] == labels[3]
    assert labels[1] != labels[2] and (1, 2) in falls
    ramp = [(0, 0, 2), (1, 0, 1.5), (2, 0, 1), (3, 0, .5), (4, 0, 0)]
    labels, _ = components(ramp, .85)
    assert len(set(labels)) == 1
    # A same-column stacked floor must remain distinct without a physical access cell.
    labels, _ = components([(0, 0, 0), (0, 0, 2)], .85)
    assert len(set(labels)) == 2
    assert optimistic_hop(3, 0, .5, 5, 9.81)
    assert not optimistic_hop(3.5, 0, .5, 5, 9.81)
    assert not optimistic_hop(.5, 2, .5, 5, 9.81)
    assert optimistic_hop(4, -2, .5, 5, 9.81)
    print("PASS: isolated drop, ramp escape, stacked floors, and directional jump limits")


def optimistic_hop(distance, height_difference, rise, speed, gravity):
    """A generous ballistic limit, before checking obstacles, headroom or landing shape."""
    if height_difference > rise:
        return False
    velocity = math.sqrt(2 * gravity * rise)
    airtime = (velocity + math.sqrt(max(0, velocity**2 - 2 * gravity * height_difference))) / gravity
    return distance <= speed * airtime + 1e-6


def return_routes(points, labels, falls, main, metadata, rise):
    """Add optimistic wide hops and directed drops, then find potential return routes."""
    columns = collections.defaultdict(list)
    for i, point in enumerate(points):
        columns[int(point[0]), int(point[1])].append(i)
    graph = collections.defaultdict(set)
    for a, b in falls:
        if labels[a] != labels[b]:
            graph[labels[a]].add(labels[b])
    speed = max(p["speed"] for p in metadata["players"])
    gravity = 9.81 * min(p["gravity"] for p in metadata["players"])
    spacing = metadata["spacing"]
    lowest = min(float(p[2]) for p in points)
    witnesses = {}
    for (x, z), current in columns.items():
        for a in current:
            label = labels[a]
            if label == main:
                continue
            # Interior samples of an already-connected patch cannot introduce a new exit.
            if all(any(labels[b] == label for b in columns.get((x + dx, z + dz), ()))
                   for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                continue
            height = float(points[a][2])
            velocity = math.sqrt(2 * gravity * rise)
            maximum_time = (velocity + math.sqrt(velocity**2 + 2 * gravity * max(0, height - lowest))) / gravity
            radius = math.ceil(speed * maximum_time / spacing)
            for dx in range(-radius, radius + 1):
                for dz in range(-radius, radius + 1):
                    if abs(dx) <= 1 and abs(dz) <= 1:
                        continue
                    distance = math.hypot(dx, dz) * spacing
                    for b in columns.get((x + dx, z + dz), ()):
                        other = labels[b]
                        if other == label or other in graph[label]:
                            continue
                        if optimistic_hop(distance, float(points[b][2]) - height, rise, speed, gravity):
                            graph[label].add(other)
                            witnesses[label, other] = (a, b)
    reverse = collections.defaultdict(set)
    for a, destinations in graph.items():
        for b in destinations:
            reverse[b].add(a)
    can_return = {main}
    pending = [main]
    while pending:
        for source in reverse[pending.pop()]:
            if source not in can_return:
                can_return.add(source)
                pending.append(source)
    return can_return, witnesses


def analyze(folder):
    import numpy as np
    from PIL import Image, ImageDraw, ImageFont

    metadata = json.loads((folder / "inventory.json").read_text(encoding="utf-8"))
    progress = json.loads((folder / "progress.json").read_text(encoding="utf-8"))
    if progress["status"] != "complete" or progress["completedRows"] != metadata["depth"]:
        raise RuntimeError("The Unity surface scan is incomplete.")
    points = np.loadtxt(folder / "surfaces.csv", delimiter=",", skiprows=1, ndmin=2)
    if len(points) != progress["samples"]:
        raise RuntimeError("The exported sample count does not match the completed scan.")
    # Include step and skin in this deliberately generous upper bound. Real jump checks follow.
    rise = max(p["jump"] / p["gravity"] + p["step"] + p["skin"] for p in metadata["players"])
    labels, falls = components(points, rise)
    groups = collections.defaultdict(list)
    for i, label in enumerate(labels):
        groups[label].append(i)
    main = max(groups, key=lambda g: sum(1 for i in groups[g] if 1 < points[i, 2] < 3.5))
    can_return, wide_hops = return_routes(points, labels, falls, main, metadata, rise)
    spacing = metadata["spacing"]
    records = {}
    for label, indices in groups.items():
        sample = points[indices]
        x = sample[:, 0] * spacing + metadata["minX"]
        z = sample[:, 1] * spacing + metadata["minZ"]
        middle = np.array([np.median(x), np.median(sample[:, 2]), np.median(z)])
        representative = int(np.argmin((x - middle[0]) ** 2 + (z - middle[2]) ** 2))
        counts = collections.Counter(sample[:, 3].astype(int))
        records[label] = {
            "component": label, "samples": len(indices), "area_m2": round(len(indices) * spacing**2, 2),
            "bounds": {"minX": float(x.min()), "maxX": float(x.max()), "minZ": float(z.min()), "maxZ": float(z.max()),
                       "minY": float(sample[:, 2].min()), "maxY": float(sample[:, 2].max())},
            "representative": {"x": float(x[representative]), "y": float(sample[representative, 2]), "z": float(z[representative])},
            "nav_fraction": float(sample[:, 7].mean()),
            "optimistic_return_possible": label in can_return,
            "supports": [{"path": metadata["colliders"][c]["path"], "samples": n} for c, n in counts.most_common(5)],
            "incoming": [], "outgoing": [],
        }
    links = collections.defaultdict(list)
    for high, low in falls:
        a, b = labels[high], labels[low]
        if a != b:
            links[a, b].append((high, low))
    for (a, b), edges in links.items():
        records[a]["outgoing"].append(b)
        records[b]["incoming"].append(a)
    raised = points[groups[main], 2]
    main_floor = float(np.median(raised[(raised >= 1.9) & (raised < 2.5)]))
    candidates = []
    for label, record in records.items():
        if label == main or record["samples"] < 4 or record["representative"]["y"] >= main_floor - rise:
            continue
        if main not in record["incoming"]:
            continue
        examples = links[main, label]
        high, low = max(examples, key=lambda edge: points[edge[0], 2] - points[edge[1], 2])
        record["drop_m"] = float(points[high, 2] - points[low, 2])
        record["fall_edge"] = {
            "from": {"x": float(points[high, 0] * spacing + metadata["minX"]), "y": float(points[high, 2]), "z": float(points[high, 1] * spacing + metadata["minZ"])},
            "to": {"x": float(points[low, 0] * spacing + metadata["minX"]), "y": float(points[low, 2]), "z": float(points[low, 1] * spacing + metadata["minZ"])},
        }
        record["classification"] = ("candidate: possible return via other patches or a wide hop; obstacles require validation"
                                    if label in can_return else "candidate: no return even in optimistic jump/drop graph; refine access and verify CharacterController")
        candidates.append(record)
    candidates.sort(key=lambda r: r["area_m2"], reverse=True)
    for i, record in enumerate(candidates, 1):
        record["id"] = f"F{i:02}"
    output = {"scan": progress, "maximum_optimistic_rise": rise, "main_component": main,
              "main_floor_median": main_floor, "components": list(records.values()), "candidates": candidates,
              "wide_hops": [{"from_component": a, "to_component": b, "from_sample": edge[0], "to_sample": edge[1]}
                            for (a, b), edge in wide_hops.items()],
              "limitations": "0.5 m standing grid; adjacency/wide hops ignore wall sweeps and jump headroom. Candidates need refinement and targeted CharacterController checks."}
    (folder / "analysis.json").write_text(json.dumps(output, indent=2, ensure_ascii=False), encoding="utf-8")
    np.save(folder / "component-labels.npy", np.asarray(labels, dtype=np.int32))

    canvas = Image.new("RGB", (metadata["width"], metadata["depth"]), (21, 26, 33))
    pixels = canvas.load()
    for i, sample in enumerate(points):
        gx, gz = int(sample[0]), int(sample[1])
        if sample[2] > 6:
            continue
        height = max(0, min(1, sample[2] / 2.2))
        pixels[gx, metadata["depth"] - 1 - gz] = (int(50 + 75 * height), int(75 + 90 * height), int(90 + 110 * height))
    danger = {c["component"] for c in candidates}
    for i, label in enumerate(labels):
        if label in danger:
            pixels[int(points[i, 0]), metadata["depth"] - 1 - int(points[i, 1])] = (239, 106, 69)
    canvas = canvas.resize((canvas.width * 2, canvas.height * 2), Image.Resampling.NEAREST)
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 22)
    for record in candidates:
        p = record["representative"]
        x = int((p["x"] - metadata["minX"]) / spacing * 2)
        z = int((metadata["depth"] - 1 - (p["z"] - metadata["minZ"]) / spacing) * 2)
        draw.text((x + 4, z + 4), record["id"], font=font, fill="white", stroke_width=3, stroke_fill="black")
    canvas.save(folder / "candidate-map.png")
    print(json.dumps({"samples": len(points), "components": len(groups), "main_area_m2": records[main]["area_m2"],
                      "candidates": [{k: r[k] for k in ("id", "area_m2", "representative", "drop_m", "nav_fraction")} for r in candidates]}, indent=2))


def low_regions(folder):
    """Separate low yards from the broad component joined by ramps; retain local drop edges."""
    import numpy as np
    m = json.loads((folder / "inventory.json").read_text(encoding="utf-8"))
    p = np.loadtxt(folder / "surfaces.csv", delimiter=",", skiprows=1, ndmin=2)
    x = p[:, 0] * m["spacing"] + m["minX"]
    z = p[:, 1] * m["spacing"] + m["minZ"]
    mask = (p[:, 2] < 1.15) & (x <= 205) & (z <= 185)
    low = p[mask]
    labels, _ = components(low, .38)
    groups = collections.defaultdict(list)
    cols = collections.defaultdict(list)
    for i, s in enumerate(p):
        cols[int(s[0]), int(s[1])].append(i)
    for i, label in enumerate(labels):
        groups[label].append(i)
    rise = max(a["jump"] / a["gravity"] + a["step"] + a["skin"] for a in m["players"])
    records = []
    for label, indices in groups.items():
        if len(indices) < 4:
            continue
        cells = low[indices]
        lx = cells[:, 0] * m["spacing"] + m["minX"]
        lz = cells[:, 1] * m["spacing"] + m["minZ"]
        drop_edges, exits = [], []
        def pos(s):
            return {"x": float(s[0] * m["spacing"] + m["minX"]), "y": float(s[2]),
                    "z": float(s[1] * m["spacing"] + m["minZ"])}
        for s in cells:
            for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1)):
                for j in cols.get((int(s[0]) + dx, int(s[1]) + dz), ()):
                    t = p[j]
                    if not 1.15 <= t[2] < 4:
                        continue
                    if t[2] - s[2] > rise:
                        drop_edges.append((s, t))
                    else:
                        exits.append((s, t))
        if not drop_edges:
            continue
        middle = int(np.argmin((lx - np.median(lx)) ** 2 + (lz - np.median(lz)) ** 2))
        supports = collections.Counter(cells[:, 3].astype(int))
        records.append({"component": label, "samples": len(indices), "area_m2": len(indices) * m["spacing"]**2,
            "bounds": [float(lx.min()), float(lx.max()), float(lz.min()), float(lz.max())],
            "representative": pos(cells[middle]), "nav_fraction": float(cells[:, 7].mean()),
            "drop_contacts": len(drop_edges), "maximum_drop": max(float(t[2]-s[2]) for s,t in drop_edges),
            "drop_examples": [{"low": pos(s), "high": pos(t)} for s,t in drop_edges[::max(1,len(drop_edges)//12)][:12]],
            "potential_exits": len(exits),
            "exit_examples": [{"low": pos(s), "high": pos(t)} for s,t in exits[::max(1,len(exits)//12)][:12]],
            "supports": [{"path": m["colliders"][c]["path"], "samples": n} for c,n in supports.most_common(3)]})
    records.sort(key=lambda r:r["area_m2"],reverse=True)
    for i,r in enumerate(records,1):
        r["id"] = f"L{i:02}"
    def pos(s):
        return {"x": float(s[0]*m["spacing"]+m["minX"]), "y": float(s[2]),
                "z": float(s[1]*m["spacing"]+m["minZ"])}
    probes = []
    for r in records:
        if r["id"] == "L01":
            continue
        v = r["representative"]
        gx = int(round((v["x"]-m["minX"])/m["spacing"]))
        gz = int(round((v["z"]-m["minZ"])/m["spacing"]))
        start = min(cols[gx,gz],key=lambda i:abs(p[i,2]-v["y"]))
        queue = collections.deque([start]); parents = {start:None}; found = None
        while queue and len(parents) < 50000:
            a = queue.popleft(); s = p[a]
            support = m["colliders"][int(s[3])]["path"]
            if 1.9 <= s[2] < 2.5 and ("Road" in support or "Apron" in support):
                found = a; break
            for dx,dz in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,1),(1,-1),(-1,-1)):
                for b in cols.get((int(s[0])+dx,int(s[1])+dz),()):
                    if b not in parents and abs(p[b,2]-s[2]) <= rise:
                        parents[b] = a; queue.append(b)
        if found is None:
            r["adjacent_route"] = "none within 50,000 explored samples; wider hops require separate checks"
        else:
            route = []
            while found is not None:
                route.append(pos(p[found])); found = parents[found]
            route.reverse()
            r["adjacent_route"] = route
            probes.append({"name": r["id"]+"_optimistic_route", "route":route, "jump":True})
        for i,edge in enumerate(r["drop_examples"][:4]):
            probes.append({"name":r["id"]+f"_edge_{i}_down", "route":[edge["high"],edge["low"]], "jump":False})
            probes.append({"name":r["id"]+f"_edge_{i}_up", "route":[edge["low"],edge["high"]], "jump":True})
    (folder / "probes.json").write_text(json.dumps({"probes":probes},indent=2),encoding="utf-8")
    (folder / "low-regions.json").write_text(json.dumps(records,indent=2),encoding="utf-8")
    print(json.dumps([{k:r[k] for k in ("id","area_m2","bounds","representative","potential_exits","maximum_drop")} for r in records],indent=2))


def boundary_probes(folder):
    """Check exposed lips across every low region instead of a few representative edges."""
    import numpy as np
    m = json.loads((folder / "inventory.json").read_text(encoding="utf-8"))
    p = np.loadtxt(folder / "surfaces.csv", delimiter=",", skiprows=1)
    regions = json.loads((folder / "low-regions.json").read_text(encoding="utf-8"))
    x = p[:, 0]*m["spacing"]+m["minX"]
    z = p[:, 1]*m["spacing"]+m["minZ"]
    selected = np.flatnonzero((p[:, 2] < 1.15) & (x <= 205) & (z <= 185))
    low = p[selected]
    labels, _ = components(low, .38)
    ids = {r["component"]:r["id"] for r in regions}
    column, low_column = collections.defaultdict(list), collections.defaultdict(list)
    for i,s in enumerate(p):
        column[int(s[0]),int(s[1])].append(i)
    for i,s in enumerate(low):
        low_column[int(s[0]),int(s[1])].append(i)
    probes, seen = [], set()
    coverage = {r["id"]:{"low_samples":0,"covered_samples":0,"adjacent_drop_pairs":0,"native_drop_probes":0} for r in regions}
    pos = lambda s:{"x":float(s[0]*m["spacing"]+m["minX"]),"y":float(s[2]),"z":float(s[1]*m["spacing"]+m["minZ"])}
    for i,s in enumerate(low):
        if labels[i] not in ids:
            continue
        name = ids[labels[i]]; c = coverage[name]; c["low_samples"] += 1
        # A low support under another floor is not an exposed top-down landing.
        covered = any(s[2]+.3 < p[a,2] < s[2]+2.8 for a in column[int(s[0]),int(s[1])])
        if covered:
            c["covered_samples"] += 1
        for dx,dz in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,1),(1,-1),(-1,-1)):
            for a in column.get((int(s[0])+dx,int(s[1])+dz),()):
                h = p[a]
                if not s[2]+1.18 < h[2] < 4:
                    continue
                c["adjacent_drop_pairs"] += 1
                if covered:
                    continue
                key = (name,int(h[0])//2,int(h[1])//2,dx,dz,round(float(h[2]),1))
                if key in seen:
                    continue
                seen.add(key)
                target = s
                for distance in (4,3,2,1):
                    choices = [j for j in low_column.get((int(s[0])-distance*dx,int(s[1])-distance*dz),()) if labels[j]==labels[i]]
                    if choices:
                        target = low[choices[0]]; break
                # Start a metre back from a broad ledge when that support exists.
                start = h
                choices = [b for b in column.get((int(h[0])+2*dx,int(h[1])+2*dz),()) if abs(p[b,2]-h[2]) < .12]
                if choices:
                    start = p[choices[0]]
                probes.append({"name":name+f"_boundary_{c['native_drop_probes']}","route":[pos(start),pos(target)],"maxTicks":360,"jump":False})
                c["native_drop_probes"] += 1
    (folder / "boundary-coverage.json").write_text(json.dumps(coverage,indent=2),encoding="utf-8")
    (folder / "boundary-probes-input.json").write_text(json.dumps({"probes":probes},indent=2),encoding="utf-8")
    print(json.dumps({"probes":len(probes),"regions_with_exposed_lips":[k for k,v in coverage.items() if v["native_drop_probes"]]},indent=2))


def remaining_probes(folder):
    """Cover the connection and drop landings above the low-yard cutoff."""
    import numpy as np
    m = json.loads((folder / "inventory.json").read_text(encoding="utf-8"))
    p = np.loadtxt(folder / "surfaces.csv", delimiter=",", skiprows=1)
    columns = collections.defaultdict(list)
    for i,s in enumerate(p):
        columns[int(s[0]),int(s[1])].append(i)
    probes, seen = [], set()
    pos = lambda s:{"x":float(s[0]*m["spacing"]+m["minX"]),"y":float(s[2]),"z":float(s[1]*m["spacing"]+m["minZ"])}
    for h in p:
        is_connection = "FactoryAsylumConnection" in m["colliders"][int(h[3])]["path"]
        if not 1.18 < h[2] < 5.5:
            continue
        for dx,dz in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,1),(1,-1),(-1,-1)):
            for i in columns.get((int(h[0])+dx,int(h[1])+dz),()):
                s = p[i]
                if h[2]-s[2] <= 1.18 or s[2] < 0 or s[2] >= 4:
                    continue
                if not is_connection and not (s[2] >= 1.15 or h[2] >= 4):
                    continue
                if any(s[2]+.3 < p[j,2] < s[2]+2.8 for j in columns[int(s[0]),int(s[1])]):
                    continue
                key = (int(h[0])//2,int(h[1])//2,dx,dz,round(float(h[2]),1))
                if key in seen:
                    continue
                seen.add(key)
                start, target = h, s
                choices = [j for j in columns.get((int(h[0])-2*dx,int(h[1])-2*dz),()) if abs(p[j,2]-h[2]) < .12]
                if choices: start = p[choices[0]]
                for distance in (4,3,2):
                    choices = [j for j in columns.get((int(s[0])+distance*dx,int(s[1])+distance*dz),()) if abs(p[j,2]-s[2]) < .3]
                    if choices: target = p[choices[0]]; break
                name = ("CON" if is_connection else "UPPER")+f"_drop_{len(probes)}"
                probes.append({"name":name,"route":[pos(start),pos(target)],"maxTicks":360,"jump":False})
    (folder / "remaining-probes-input.json").write_text(json.dumps({"probes":probes},indent=2),encoding="utf-8")
    print(json.dumps({"native_probes":len(probes),"connection":sum(v["name"].startswith("CON") for v in probes)},indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--self-check", action="store_true")
    parser.add_argument("--low-regions", action="store_true")
    parser.add_argument("--boundary-probes", action="store_true")
    parser.add_argument("--remaining-probes", action="store_true")
    args = parser.parse_args()
    if args.self_check:
        self_check()
    elif args.low_regions:
        low_regions(Path(__file__).resolve().parent)
    elif args.boundary_probes:
        boundary_probes(Path(__file__).resolve().parent)
    elif args.remaining_probes:
        remaining_probes(Path(__file__).resolve().parent)
    else:
        analyze(Path(__file__).resolve().parent)

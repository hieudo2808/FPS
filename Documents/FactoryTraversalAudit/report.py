"""Produce the evidence table and coordinate map from the completed Unity measurements."""
import csv
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont


def build():
    folder = Path(__file__).resolve().parent
    read = lambda name: json.loads((folder / name).read_text(encoding="utf-8-sig"))
    metadata, progress = read("inventory.json"), read("progress.json")
    points = np.loadtxt(folder / "surfaces.csv", delimiter=",", skiprows=1)
    assert progress["status"] == "complete" and progress["completedRows"] == metadata["depth"]
    assert len(points) == progress["samples"]
    regions = read("low-regions.json")
    edges = read("edge-probes.json")["results"]
    hops = read("hop-probes.json")["results"]
    routes = read("route-probes.json")["results"]
    all_routes = read("all-nav-controller-probes.json")["results"]
    final = read("final-probes.json")["results"]
    boundary = read("boundary-results.json")["results"]
    remaining = read("remaining-results.json")["results"]
    followup = read("followup-probes.json")["results"]
    escape_checks = read("upper-escape-results.json")["results"]
    initial_returns = read("landing-return-results.json")["results"]
    final_validation = read("final-validation-results.json")["results"]
    retries = read("return-retry-results.json")["results"]
    detours = read("detour-results.json")["results"]
    upper_exits = read("upper-last-exit-results.json")["results"]
    assert len(upper_exits) == 5 and all(t["reached"] for t in upper_exits)
    corner_retries = {t["name"][len("corner_"):]: {**t, "name": t["name"][len("corner_"):]} for t in final_validation if t["name"].startswith("corner_")}
    assert set(corner_retries) == {t["name"] for t in initial_returns if not t["reached"]}
    assert all(t["reached"] for t in corner_retries.values())
    landing_returns = [corner_retries.get(t["name"], t) for t in initial_returns]
    assert all(t["reached"] for t in final_validation if t["name"].startswith("exit_"))
    landing_paths = read("landing-paths.json")["paths"]
    coverage = read("boundary-coverage.json")
    assert len(boundary) == 10476 and len(remaining) == 9918
    assert len(landing_returns) == sum(t["status"] == "PathComplete" for t in landing_paths)
    assert len({t["name"] for t in landing_returns}) == len(landing_returns)
    nav = {r["id"]: r["result"] for r in read("all-nav-paths.json")}
    fine = read("refinement-analysis.json")
    assert not fine["upper_floor_reached"]
    assert all(not r["reached"] for r in routes if r["name"].startswith("L30_escape_"))
    verified_walk = {r["name"][:3] for r in all_routes if r["reached"]}
    assert {"L02", "L03", "L61"} <= verified_walk
    assert next(r for r in followup if r["name"] == "L01_long_nav_walk")["reached"]
    assert next(r for r in escape_checks if r["name"] == "L15_full_walk_exit")["reached"]
    verified_walk.update(("L01", "L15"))
    assert next(r for r in final if r["name"] == "L15_after_hop_nav_walk")["reached"]

    classifications = {
        "L01": "Đã kiểm chứng đường đi bộ vòng tới Logistics; NavMesh dài 366 m",
        "L02": "Đã kiểm chứng đường đi bộ tới Logistics",
        "L03": "Đã kiểm chứng đường đi bộ tới Logistics",
        "L61": "Đã kiểm chứng đường đi bộ tới Logistics",
        "L15": "Đã kiểm chứng đi bộ qua mép đông nam rồi tới Logistics; không cần nhảy",
        "L26": "Ngoài backstop; bước ra mép sẽ rơi qua độ cao recovery, chưa thử callback runtime",
        "L30": "Vùng kẹt tái hiện bằng CharacterController trong Edit Mode; ưu tiên sửa",
    }
    columns = {}
    for i, s in enumerate(points):
        columns.setdefault((int(s[0]), int(s[1])), []).append(i)
    rows = []
    for r in regions:
        tests = [t for t in edges if t["name"].startswith(r["id"] + "_")]
        lips = [t for t in boundary if t["name"].startswith(r["id"] + "_")]
        returns = [t for t in landing_returns if t["name"].startswith("return_" + r["id"] + "_")]
        v = r["representative"]
        column = columns[round((v["x"] - metadata["minX"]) / .5), round((v["z"] - metadata["minZ"]) / .5)]
        covered = any(v["y"] + .3 < points[i, 2] < v["y"] + 3
                      and "Road" in metadata["colliders"][int(points[i, 3])]["path"] for i in column)
        rows.append({"id": r["id"], "x": v["x"], "y": v["y"], "z": v["z"],
            "min_x": r["bounds"][0], "max_x": r["bounds"][1], "min_z": r["bounds"][2], "max_z": r["bounds"][3],
            "sample_area_m2": r["area_m2"], "maximum_local_drop_m": r["maximum_drop"],
            "nav_status": nav[r["id"]]["status"], "nav_fraction": r["nav_fraction"],
            "drop_tests": sum(t["name"].endswith("_down") for t in tests),
            "drop_tests_reached": sum(t["reached"] and t["name"].endswith("_down") for t in tests),
            "boundary_probes": len(lips), "boundary_landings_reached": sum(t["reached"] for t in lips),
            "nav_return_probes": len(returns), "native_nav_returns_reached": sum(t["reached"] for t in returns),
            "covered_low_sample_fraction": coverage[r["id"]]["covered_samples"] / coverage[r["id"]]["low_samples"],
            "road_floor_above_representative": covered,
            "classification": classifications.get(r["id"], "Các mép rơi đã thử chưa tái hiện tiếp đất; chưa có bằng chứng hố kẹt"),
            "support": r["supports"][0]["path"]})
    with (folder / "regions.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader(); writer.writerows(rows)

    summary = {"standing_samples": len(points), "geometry_regions": len(regions),
        "authored_colliders": len(metadata["colliders"]),
        "active_solid_colliders": sum(c["active"] and c["enabled"] and not c["trigger"] for c in metadata["colliders"]),
        "controller_probes": sum(map(len, (edges, hops, routes, all_routes, final, boundary, remaining, followup, escape_checks, initial_returns, retries, detours, final_validation, upper_exits))),
        "boundary_probes": len(boundary), "boundary_landings": sum(t["reached"] for t in boundary),
        "upper_and_connection_probes": len(remaining), "upper_and_connection_landings": sum(t["reached"] for t in remaining),
        "landing_nav_paths": {s: sum(t["status"] == s for t in landing_paths) for s in ("PathComplete", "PathPartial", "PathInvalid")},
        "native_landing_returns": len(landing_returns), "native_landing_returns_reached": sum(t["reached"] for t in landing_returns),
        "initial_native_returns_reached": sum(t["reached"] for t in initial_returns),
        "nav_corner_tolerance_rechecks": len(corner_retries),
        "l15_native_exit_routes": sum(t["reached"] and t["name"].startswith("exit_") for t in final_validation),
        "upper_native_exit_routes": len(upper_exits),
        "scene_sha256": metadata["sceneSha256"],
        "scope": "Saved Edit Mode geometry and native movement tick; dynamic mission gates and network recovery callbacks are not simulated",
        "walk_routes_confirmed": sorted(verified_walk), "fine_scan": fine,
        "single_jump_apex_above_rest": next(r["maximumY"] - r["end"]["y"] for r in final if r["name"] == "Single_jump_flat_ground")}
    verified_returns = {t["name"][len("return_"):]: t for t in landing_returns}
    verified_returns.update({t["name"][len("exit_"):]: t for t in final_validation if t["name"].startswith("exit_")})
    verified_returns.update({t["name"][len("upper_exit_"):]: t for t in upper_exits})
    landing_rows = []
    for t in landing_paths:
        v = t["start"]
        if t["name"] in verified_returns:
            status = "Lối về không cần nhảy đã kiểm chứng"
        elif t["name"].startswith("L30_") or (t["name"].startswith("UPPER_") and 10.5 <= v["x"] <= 29.75 and -103 <= v["z"] <= -95.5 and abs(v["y"]) < .1):
            status = "L30: vùng kẹt đã tái hiện"
        elif t["name"].startswith("CON_") and v["x"] > 120:
            status = "Trong/sau cổng nhiệm vụ đang đóng; chưa kết luận khi chạy nhiệm vụ"
        elif t["name"].startswith("CON_") and v["z"] == 185:
            status = "Mép WorldBase; thử bước ra ngoài rơi qua cao độ cứu hộ"
        else:
            raise AssertionError("Unclassified reached landing: " + t["name"])
        landing_rows.append({"probe": t["name"], "x": v["x"], "y": v["y"], "z": v["z"], "nav_status": t["status"], "classification": status})
    with (folder / "landings.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(landing_rows[0]))
        writer.writeheader(); writer.writerows(landing_rows)
    summary["classified_landings"] = {s: sum(t["classification"] == s for t in landing_rows) for s in sorted({t["classification"] for t in landing_rows})}
    (folder / "summary.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")

    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 19)
    title = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 25)
    small = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 15)
    canvas = Image.new("RGB", (1660, 1040), "#121b25")
    draw = ImageDraw.Draw(canvas)
    draw.text((25, 18), "FACTORY — CHÊNH CAO, VÙNG KẸT VÀ ĐƯỜNG THOÁT", font=title, fill="white")
    panels = [(25, 110, 570, 850, -175, 265, -185.5, 425, "Toàn vùng quét, gồm đường nối"),
              (635, 110, 995, 850, -55, 135, -130, 45, "Phóng to khu Factory cũ")]
    for left, top, width, height, min_x, max_x, min_z, max_z, label in panels:
        base = Image.new("RGB", (metadata["width"], metadata["depth"]), "#16212b")
        pixels = base.load()
        # Keep the highest standing floor below 3 m; roof samples are omitted from this plan view.
        floor_height = np.full((metadata["depth"], metadata["width"]), -10., dtype=np.float32)
        for s in points:
            if s[2] <= 3:
                gx, gz = int(s[0]), int(s[1])
                if s[2] > floor_height[gz, gx]:
                    floor_height[gz, gx] = s[2]
                    h = max(0, min(1, float(s[2]) / 2.2))
                    pixels[gx, metadata["depth"] - 1 - gz] = (int(45+75*h), int(61+95*h), int(72+110*h))
        crop = ((min_x-metadata["minX"])/.5, metadata["depth"]-1-(max_z-metadata["minZ"])/.5,
                (max_x-metadata["minX"])/.5, metadata["depth"]-1-(min_z-metadata["minZ"])/.5)
        canvas.paste(base.crop(tuple(int(v) for v in crop)).resize((width,height)), (left,top))
        def xy(v):
            return (left+(v["x"]-min_x)/(max_x-min_x)*width, top+(max_z-v["z"])/(max_z-min_z)*height)
        route_layer = Image.new("RGBA", (width, height))
        route_draw = ImageDraw.Draw(route_layer)
        draw.text((left, top-32), label, font=font, fill="white")
        tick = 40 if max_x - min_x > 300 else 20
        for x in range(int(min_x//tick)*tick, int(max_x)+1, tick):
            px = left+(x-min_x)/(max_x-min_x)*width
            if left <= px <= left+width:
                draw.line((px,top,px,top+height),fill="#36414b")
                draw.text((px-10,top+height+5),str(x),font=small,fill="#c8d4df")
        for z in range(int(min_z//tick)*tick, int(max_z)+1, tick):
            py = top+(max_z-z)/(max_z-min_z)*height
            if top <= py <= top+height:
                draw.line((left,py,left+width,py),fill="#36414b")
                draw.text((left+3,py+2),str(z),font=small,fill="#c8d4df")
        for r in regions:
            v = r["representative"]
            if min_x <= v["x"] <= max_x and min_z <= v["z"] <= max_z:
                x,z = xy(v)
                color = "#a0aab4"
                if r["id"] in verified_walk: color = "#43dcad"
                elif r["id"] == "L15": color = "#ffbc61"
                elif r["id"] == "L30": color = "#ff5353"
                draw.ellipse((x-3,z-3,x+3,z+3),fill=color)
                if r["id"] in classifications:
                    draw.text((x+5,z-20),r["id"],font=font,fill=color,stroke_width=2,stroke_fill="#101820")
        pit = fine["bounds"]
        a = xy({"x":pit[0],"z":pit[3]}); b = xy({"x":pit[1],"z":pit[2]})
        draw.rectangle((*a,*b),outline="#ff5353",width=4)
        for v in read("all-nav-paths.json"):
            if v["id"] in verified_walk and v["result"]["status"] == "PathComplete":
                corners = [xy(c) for c in v["result"]["corners"]]
                route_draw.line([(x-left,z-top) for x,z in corners],fill="#43dcad",width=2)
        corners = [xy(c) for c in read("upper-escape-input.json")["probes"][0]["route"]]
        route_draw.line([(x-left,z-top) for x,z in corners],fill="#43dcad",width=2)
        escape = next(h for h in followup if h["name"] == "L15_walk")
        corners = [xy(escape["start"]),xy(escape["target"])]
        route_draw.line([(x-left,z-top) for x,z in corners],fill="#43dcad",width=4)
        canvas.paste(route_layer, (left,top), route_layer)
        target = xy({"x":100.304,"z":-77.464})
        draw.ellipse((target[0]-5,target[1]-5,target[0]+5,target[1]+5),fill="#ffe084")
        draw.text((target[0]+8,target[1]+5),"Logistics",font=font,fill="#ffe084",stroke_width=2,stroke_fill="#101820")
    draw.text((25,985),"Đỏ: L30 kẹt tái hiện   •   Xanh: lối đi bộ đã kiểm chứng   •   Xám: điểm hình học, chưa chứng minh hố kẹt",font=font,fill="white")
    draw.text((25,1013),"Trục ngang X, trục dọc Z (m). Bản đồ mặt bằng không biểu diễn đầy đủ các tầng chồng nhau.",font=small,fill="#b2c3d1")
    canvas.save(folder / "factory-traversal-map.png")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    build()

#!/usr/bin/env python3
"""매장 평면도(docs/images/shop-layout.svg)와 좌표 표를 만든다.

    python3 Tools/docs/shop_layout.py            # SVG 저장 + 좌표 표 출력

좌표의 원본은 Assets/_Project/Data/shop_layout.json 이다. 그레이박스 씬 빌더(ShopSceneBuilder)도
같은 파일을 읽으므로, 배치를 바꿀 때는 JSON만 고치고 이 스크립트를 다시 실행한다.
좌표 단위는 미터. x는 동쪽(+), z는 북쪽(+). Unity의 x, z와 같다.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LAYOUT_PATH = ROOT / "Assets" / "_Project" / "Data" / "shop_layout.json"
CONTENT_PATH = ROOT / "Assets" / "_Project" / "Data" / "content.json"
OUT = ROOT / "docs" / "images" / "shop-layout.svg"

LAYOUT = json.loads(LAYOUT_PATH.read_text(encoding="utf-8"))
_UNLOCK_LEVEL = {u["id"]: u["requiredLevel"] for u in json.loads(CONTENT_PATH.read_text(encoding="utf-8"))["unlocks"]}


def _furniture_row(f):
    """(id, 이름, 중심 x, 중심 z, 가로(x) 크기, 세로(z) 크기, 높이, 종류). 잠긴 가구는 이름에 해금 레벨을 붙인다."""
    name, kind = f["name"], f["kind"]
    if "unlockId" in f:
        name = f"{name} (Lv.{_UNLOCK_LEVEL[f['unlockId']]})"
        kind = "locked"
    return (f["id"], name, f["x"], f["z"], f["sizeX"], f["sizeZ"], f["height"], kind)


OBJECTS = [_furniture_row(f) for f in LAYOUT["furniture"]]
POINTS = [(p["name"], p["x"], p["z"], p["kind"]) for p in LAYOUT["points"]]   # (이름, x, z, 종류)


def _box(b):
    return (b["xMin"], b["zMin"], b["xMax"], b["zMax"])


ROOM = _box(LAYOUT["room"])            # x0, z0, x1, z1
SIDEWALK = _box(LAYOUT["sidewalk"])
DOOR = (LAYOUT["door"]["xMin"], LAYOUT["door"]["xMax"])   # 남쪽 벽의 문 (x 범위)
ZONES = {z["id"]: _box(z) for z in LAYOUT["zones"]}
STAFF_PATH = [tuple(p) for p in LAYOUT["staffPath"]]

SCALE = 44
PAD = 40

COLORS = {
    "furniture": ("#d9d2c5", "#6b6457"),
    "wall": ("#8a8478", "#5a554c"),
    "shelf": ("#f2c078", "#9a6a1e"),
    "locked": ("#ffffff", "#9a6a1e"),
    "oven": ("#e8866a", "#8f3b25"),
    "storage": ("#9cc3e6", "#2f6aa0"),
}
POINT_COLORS = {"player": "#2f6aa0", "customer": "#3c8d5a", "delivery": "#8f3b25", "sign": "#7a4fa3"}


def sx(x):
    return PAD + (x - SIDEWALK[0]) * SCALE


def sy(z):
    return PAD + (ROOM[3] - z) * SCALE


def rect(x0, z0, x1, z1, fill, stroke, width=1.5, dash=None, extra=""):
    d = f' stroke-dasharray="{dash}"' if dash else ""
    return (f'<rect x="{sx(x0):.1f}" y="{sy(z1):.1f}" width="{(x1 - x0) * SCALE:.1f}" height="{(z1 - z0) * SCALE:.1f}" '
            f'fill="{fill}" stroke="{stroke}" stroke-width="{width}"{d}{extra}/>')


def text(x, z, s, size=12, anchor="middle", fill="#2b2b2b", weight="400", dy=0):
    return (f'<text x="{sx(x):.1f}" y="{sy(z) + dy:.1f}" font-size="{size}" text-anchor="{anchor}" fill="{fill}" '
            f'font-weight="{weight}" font-family="Pretendard, \'Noto Sans CJK KR\', \'Malgun Gothic\', sans-serif">{s}</text>')


def build_svg():
    width = PAD * 2 + (SIDEWALK[2] - SIDEWALK[0]) * SCALE
    height = PAD * 2 + (ROOM[3] - SIDEWALK[1]) * SCALE + 30
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width:.0f} {height:.0f}" width="{width:.0f}" height="{height:.0f}">',
           f'<rect width="{width:.0f}" height="{height:.0f}" fill="#ffffff"/>']
    # 인도, 매장 바닥
    out.append(rect(*SIDEWALK, "#eeeeee", "#bbbbbb", 1))
    out.append(text(6.5, -8.6, "인도 (손님이 걸어오는 길)", 12, fill="#666666"))
    out.append(rect(*ROOM, "#fbf7ee", "none"))
    # 구역 표시
    out.append(rect(*ZONES["kitchen"], "#fdebd8", "none"))
    out.append(text(2.6, 4.1, "주방", 13, fill="#b0703a", weight="600"))
    out.append(rect(*ZONES["checkout"], "#e4eef8", "none"))
    out.append(text(2.4, -5.3, "계산 구역", 13, fill="#2f6aa0", weight="600"))
    out.append(text(-2.6, 2.3, "매장", 13, fill="#8a7a50", weight="600"))
    # 벽 (문 자리만 뚫음)
    wall = 'stroke="#3d3a34" stroke-width="5" stroke-linecap="square"'
    x0, z0, x1, z1 = ROOM
    segs = [(x0, z1, x1, z1), (x0, z0, x0, z1), (x1, z0, x1, z1), (x0, z0, DOOR[0], z0), (DOOR[1], z0, x1, z0)]
    for (a, b, c, d) in segs:
        out.append(f'<line x1="{sx(a):.1f}" y1="{sy(b):.1f}" x2="{sx(c):.1f}" y2="{sy(d):.1f}" {wall}/>')
    out.append(text((DOOR[0] + DOOR[1]) / 2, -6.0, "출입문", 12, fill="#3d3a34", weight="600", dy=18))
    # 가구
    for (oid, name, cx, cz, w, d, _h, kind) in OBJECTS:
        fill, stroke = COLORS[kind]
        dash = "5 4" if kind == "locked" else None
        out.append(rect(cx - w / 2, cz - d / 2, cx + w / 2, cz + d / 2, fill, stroke, 1.5, dash))
    labels = {
        "counter.01": (2.6, -3.4, 0), "pc.01": (4.5, -5.5, 0), "divider": (-1.3, 3.2, 0), "table.01": (-2.6, 5.5, 0),
        "oven.01": (-0.5, 5.5, 0), "oven.02": (1.2, 5.5, 0), "storage.01": (4.5, 5.0, 0), "trash.01": (-4.5, 4.4, -22),
        "shelf.01": (-4.6, -2.0, 0), "shelf.02": (-4.6, 1.0, 0), "shelf.03": (-1.3, -2.05, 0), "shelf.04": (-0.7, 1.15, 0),
    }
    for (oid, name, cx, cz, w, d, _h, kind) in OBJECTS:
        if oid not in labels:
            continue
        lx, lz, dy = labels[oid]
        if oid in ("shelf.01", "shelf.02", "storage.01"):
            out.append(f'<text transform="translate({sx(lx):.1f},{sy(lz):.1f}) rotate(-90)" font-size="11" text-anchor="middle" dy="4" fill="#2b2b2b" '
                       f'font-family="Pretendard, \'Noto Sans CJK KR\', \'Malgun Gothic\', sans-serif">{name}</text>')
        else:
            out.append(text(lx, lz, name, 11, dy=4 + dy))
    # 지점
    for (name, x, z, kind) in POINTS:
        color = POINT_COLORS[kind]
        out.append(f'<circle cx="{sx(x):.1f}" cy="{sy(z):.1f}" r="5" fill="{color}"/>')
    point_labels = {
        "플레이어 시작": ("middle", 0, -10), "계산 자리": ("middle", 0, 18), "손님 계산 위치": ("start", 10, 4),
        "줄 4": ("start", 10, 4), "손님 입장 지점": ("start", 10, 4), "손님 생성·퇴장 (서)": ("middle", 0, 20),
        "손님 생성·퇴장 (동)": ("middle", 0, 20), "배송 지점": ("middle", 0, 20), "영업 팻말": ("start", 8, -8),
    }
    for (name, x, z, kind) in POINTS:
        if name not in point_labels:
            continue
        anchor, dx, dy = point_labels[name]
        label = "계산 줄 (4자리)" if name == "줄 4" else name
        out.append(f'<text x="{sx(x) + dx:.1f}" y="{sy(z) + dy:.1f}" font-size="11" text-anchor="{anchor}" fill="{POINT_COLORS[kind]}" '
                   f'font-family="Pretendard, \'Noto Sans CJK KR\', \'Malgun Gothic\', sans-serif">{label}</text>')
    # 직원 동선
    path = STAFF_PATH
    pts = " ".join(f"{sx(x):.1f},{sy(z):.1f}" for x, z in path)
    out.append(f'<polyline points="{pts}" fill="none" stroke="#2f6aa0" stroke-width="2" stroke-dasharray="6 5"/>')
    out.append(f'<text transform="translate({sx(4.3):.1f},{sy(-0.2):.1f}) rotate(-90)" font-size="11" text-anchor="middle" fill="#2f6aa0" '
               f'font-family="Pretendard, \'Noto Sans CJK KR\', \'Malgun Gothic\', sans-serif">직원 동선 (약 14m)</text>')
    # 축척, 방위
    bx, bz = -8.6, 5.4
    out.append(f'<line x1="{sx(bx):.1f}" y1="{sy(bz):.1f}" x2="{sx(bx + 2):.1f}" y2="{sy(bz):.1f}" stroke="#333" stroke-width="2"/>')
    out.append(text(bx + 1, bz, "2 m", 11, dy=-6))
    out.append(text(bx + 1, bz - 0.9, "북 ↑ (+z)   동 → (+x)", 11, dy=0))
    # 범례
    ly = height - 22
    legend = [("#f2c078", "진열대"), ("#ffffff", "잠긴 가구 (레벨 해금)"), ("#e8866a", "오븐"), ("#9cc3e6", "창고"), ("#d9d2c5", "기타 가구")]
    x = PAD
    for color, name in legend:
        dash = ' stroke-dasharray="4 3"' if name.startswith("잠긴") else ""
        out.append(f'<rect x="{x}" y="{ly - 11}" width="14" height="14" fill="{color}" stroke="#666"{dash}/>')
        out.append(f'<text x="{x + 20}" y="{ly}" font-size="11" fill="#333" font-family="Pretendard, \'Noto Sans CJK KR\', \'Malgun Gothic\', sans-serif">{name}</text>')
        x += 40 + len(name) * 12
    for kind, name in [("customer", "손님 지점"), ("player", "플레이어 지점"), ("delivery", "배송")]:
        out.append(f'<circle cx="{x + 7}" cy="{ly - 4}" r="5" fill="{POINT_COLORS[kind]}"/>')
        out.append(f'<text x="{x + 20}" y="{ly}" font-size="11" fill="#333" font-family="Pretendard, \'Noto Sans CJK KR\', \'Malgun Gothic\', sans-serif">{name}</text>')
        x += 40 + len(name) * 12
    out.append("</svg>")
    return "\n".join(out)


def tables():
    rows = ["| ID | 이름 | 중심 (x, z) | 크기 (x × z) | 높이 |", "| --- | --- | --- | --- | --- |"]
    for (oid, name, cx, cz, w, d, h, _k) in OBJECTS:
        rows.append(f"| `{oid}` | {name} | ({cx:g}, {cz:g}) | {w:g} × {d:g} | {h:g} |")
    rows.append("")
    rows += ["| 지점 | 위치 (x, z) |", "| --- | --- |"]
    for (name, x, z, _k) in POINTS:
        rows.append(f"| {name} | ({x:g}, {z:g}) |")
    rows.append("")
    rows += ["| 진열대 | 바라보는 방향 | 칸 0 / 1 / 2 의 z | 손님이 서는 x |", "| --- | --- | --- | --- |"]
    facing = {"east": "동쪽", "west": "서쪽"}
    for sh in LAYOUT["shelves"]:
        slots = " / ".join(f"{z:g}".replace("-", "−") for z in sh["slotZ"])
        rows.append(f"| `{sh['id']}` | {facing[sh['facing']]} | {slots} | {sh['standX']:g} |".replace("| -", "| −"))
    rows.append("")
    rows += ["| 대상 | 서는 위치 (x, z) |", "| --- | --- |"]
    for st in LAYOUT["playerStands"]:
        rows.append(f"| `{st['target']}` | ({st['x']:g}, {st['z']:g}) |")
    return "\n".join(rows)


if __name__ == "__main__":
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(build_svg() + "\n", encoding="utf-8")
    print(f"wrote {OUT.relative_to(ROOT)}\n")
    print(tables())

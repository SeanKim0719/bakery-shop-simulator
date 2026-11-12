#!/usr/bin/env python3
"""content.json을 사람이 읽는 표(docs/design/12-content-tables.md)로 만든다.

    python3 Tools/docs/content_tables.py

content.json을 고친 뒤에는 이 스크립트와 Tools/balance/simulate.py를 다시 실행한다.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Assets" / "_Project" / "Data" / "content.json"
OUT = ROOT / "docs" / "design" / "12-content-tables.md"


def won(v):
    return f"{int(round(v)):,}"


def table(headers, rows):
    out = ["| " + " | ".join(headers) + " |", "| " + " | ".join("---" for _ in headers) + " |"]
    out += ["| " + " | ".join(str(c) for c in row) + " |" for row in rows]
    return "\n".join(out)


def main():
    data = json.loads(SRC.read_text(encoding="utf-8"))
    cfg = data["config"]
    ing = {i["id"]: i for i in data["ingredients"]}
    rec = {r["id"]: r for r in data["recipes"]}
    unit_cost = {}
    order_level = {}
    for p in data["products"]:
        if p["kind"] == "IngredientPack":
            for c in p["contents"]:
                unit_cost[c["id"]] = p["cost"] / c["count"]
                order_level[c["id"]] = p["requiredLevel"]
    license_of = {u["target"]: u for u in data["unlocks"] if u["kind"] == "RecipeLicense"}

    out = []
    w = out.append
    w("# 12. 콘텐츠 수치표\n")
    w("> 이 문서는 [`content.json`](../../Assets/_Project/Data/content.json)에서 자동으로 만든다. 직접 고치지 말고 "
      "`content.json`을 고친 뒤 `python3 Tools/docs/content_tables.py`를 실행한다.\n")
    w("게임의 모든 수치는 `content.json` 한 파일에 있다. 에디터 메뉴 **BakeryShop → 콘텐츠 가져오기**가 이 파일을 읽어 "
      "ScriptableObject 에셋의 수치를 채우고, 아이콘·프리팹 같은 외형은 에셋의 인스펙터에서 따로 연결한다 "
      "([ADR 0009](../decisions/0009-content-json-pipeline.md)). 밸런스 시뮬레이터도 같은 파일을 읽는다.\n")

    w("## 재료\n")
    rows = []
    for i in data["ingredients"]:
        cost = unit_cost.get(i["id"])
        how = f"발주 (레벨 {order_level[i['id']]})" if cost else "박스에서만"
        key = next((r["name"] for r in data["recipes"] if r.get("keyIngredient") == i["id"]), "")
        rows.append([i["name"], f"`{i['id']}`", i["rarity"], won(cost) if cost else "-", won(i["resellPrice"]), how, key])
    w(table(["재료", "ID", "희귀도", "발주 단가", "되팔기", "얻는 방법", "핵심 재료로 쓰는 레시피"], rows))
    w("")

    w("## 레시피\n")
    rows = []
    for r in data["recipes"]:
        ings = ", ".join(f"{ing[x['id']]['name']} {x['count']}" for x in r["ingredients"])
        cost = sum(unit_cost.get(x["id"], 0) * x["count"] for x in r["ingredients"])
        if r["secret"]:
            how = f"실험으로 발견 ({ing[r['keyIngredient']]['name']})"
        elif r["id"] in cfg["economy"]["startRecipes"]:
            how = "처음부터"
        else:
            u = license_of[r["id"]]
            how = f"레벨 {u['requiredLevel']}, {won(u['cost'])}원"
        rows.append([r["name"], f"`{r['id']}`", ings, r["yield"], won(r["marketPrice"]), won(cost), won(r["yield"] * r["marketPrice"]),
                     f"{r['bakeSeconds']}초", f"{r['shelfLifeDays']}일", how])
    w(table(["레시피", "ID", "재료 (한 판)", "개수", "시세", "재료비/판", "매출/판", "굽는 시간", "보관", "얻는 방법"], rows))
    w("")
    first = data["recipes"][0]
    w(f"- 모든 레시피 공통: 다 익은 뒤 {first['perfectWindowSeconds']}초 안에 꺼내면 완벽, {first['burnAfterSeconds']}초를 넘기면 탄다. "
      f"굽는 시간의 {int(cfg['oven']['minCollectProgress'] * 100)}%가 지나야 꺼낼 수 있다 (덜 익음).")
    w("- 비밀 레시피의 재료비에는 핵심 재료가 빠져 있다.")
    w("- 보관 N일: 구운 날을 0일로 세어 N일차까지 팔 수 있고, 그날 정산 때 남아 있으면 폐기된다.\n")
    hints = [[r["name"], r["hint"], len(r["ingredients"]) - 1] for r in data["recipes"] if r["secret"]]
    w("비밀 레시피의 힌트:\n")
    w(table(["레시피", "도감 힌트 문장", "실험에서 채울 칸 수"], hints))
    w("")

    w("## 발주 상품\n")
    rows = []
    for p in data["products"]:
        if p["kind"] == "IngredientPack":
            content = ", ".join(f"{ing[c['id']]['name']} {c['count']}개" for c in p["contents"])
        else:
            content = f"확률표 `{p['lootTable']}` ([09 문서](09-mystery-box-and-recipe-book.md))"
        rows.append([p["name"], f"`{p['id']}`", won(p["cost"]), f"레벨 {p['requiredLevel']}", content])
    w(table(["상품", "ID", "가격", "주문 가능", "내용물"], rows))
    w(f"\n주문하면 {cfg['delivery']['seconds']}초 뒤 문 앞 배송 지점에 상품 하나당 박스 하나가 놓인다.\n")

    w("## 손님 성향\n")
    rows = []
    for p in data["customerProfiles"]:
        rows.append([p["name"], f"`{p['id']}`", " / ".join(f"{x:g}" for x in p["spawnWeightByLevel"]), f"{won(p['budgetMin'])} ~ {won(p['budgetMax'])}",
                     f"1 ~ {p['maxItems']}", f"{p['patienceSeconds']}초", f"{int(p['cashRatio'] * 100)}%"])
    w(table(["성향", "ID", "등장 비중 (레벨 1/2/3/4/5)", "예산", "사려는 개수", "인내심", "현금 결제"], rows))
    w("")
    w("좋아하는 빵 (가중치가 클수록 자주 찾는다):\n")
    rows = []
    for r in data["recipes"]:
        row = [r["name"]]
        for p in data["customerProfiles"]:
            pref = {x["id"]: x["weight"] for x in p["preferences"]}
            row.append(f"{pref.get(r['id'], p['defaultPreference']):g}")
        rows.append(row)
    w(table(["빵"] + [p["name"] for p in data["customerProfiles"]], rows))
    w("\n가격 민감도 곡선은 [08 문서](08-progression-and-economy.md#구매-확률)에 있다.\n")

    w("## 해금\n")
    rows = [[u["name"], f"`{u['id']}`", u["kind"], f"`{u['target']}`" if u["target"] else "-", f"레벨 {u['requiredLevel']}", won(u["cost"])] for u in data["unlocks"]]
    w(table(["해금", "ID", "종류", "대상", "필요 레벨", "가격"], rows))
    w(f"\n해금 총액: {won(sum(u['cost'] for u in data['unlocks']))}원\n")

    w("## 설정값\n")
    d, e, c, r, p = cfg["day"], cfg["economy"], cfg["customers"], cfg["reputation"], cfg["progression"]
    rows = [
        ["영업 시간", f"{d['openMinutes'] // 60:02d}:00 ~ {d['closeMinutes'] // 60:02d}:00 (실제 {d['openRealSeconds']}초)"],
        ["시간대별 손님 곡선 (시각: 배율)", ", ".join(f"{int(m) // 60}시: {v:g}" for m, v in d["spawnCurve"])],
        ["시작 자금 / 지원금 기준", f"{won(e['startMoney'])}원 / {won(e['moneyFloor'])}원"],
        ["임대료 (레벨 1~5)", " / ".join(won(x) for x in e["rentByLevel"])],
        ["품질 가격 배율", ", ".join(f"{k} {v:g}" for k, v in e["qualityPriceMultiplier"].items())],
        ["판매가 범위", f"시세의 {e['priceMinRatio']:g} ~ {e['priceMaxRatio']:g}배, {e['priceStep']}원 단위"],
        ["시작 트레이 / 시작 레시피", f"{e['startTrays']}개 / " + ", ".join(rec[x]["name"] for x in e["startRecipes"])],
        ["시작 재료", ", ".join(f"{ing[s['id']]['name']} {s['count']}" for s in e["startStorage"])],
        ["진열대", f"칸 {cfg['shelf']['slotsPerShelf']}개, 칸당 {cfg['shelf']['slotCapacity']}개"],
        ["하루 기본 손님 수 (레벨 1~5)", " / ".join(str(x) for x in c["baseCountByLevel"])],
        ["동시에 매장에 있는 손님 상한 / 계산 줄 자리", f"{c['maxConcurrent']}명 / {c['queueSlots']}자리"],
        ["평판에 따른 손님 수 배율", f"{c['reputationSpawnMin']:g} ~ {c['reputationSpawnMax']:g}"],
        ["하루 지난 빵의 가격 배율", f"{c['dayOldPriceMultiplier']:g}"],
        ["덜 익은 빵의 구매 확률 배율", f"{c['undercookedMultiplier']:g}"],
        ["빵 하나를 둘러보는 시간", f"{c['browseSecondsPerItem']}초"],
        ["빵 냄새", f"{c['aromaSeconds']}초 동안 손님 생성 ×{c['aromaSpawnMultiplier']:g}"],
        ["평판", f"시작 {r['start']}, 범위 {r['min']} ~ {r['max']}"],
        ["평판 변화", f"결제 {r['saleCompleted']:+d}, 재고 없어 빈손 {r['leftNoStock']:+d}, 비싸서 빈손 {r['leftTooExpensive']:+d}, "
                   f"줄 이탈 {r['leftQueueTimeout']:+d}, 계산 불만 {r['paymentComplaint']:+d}, 레시피 발견 {r['recipeDiscovered']:+d}"],
        ["레벨 경험치 기준 (레벨 1~5)", " / ".join(str(x) for x in p["xpThresholds"])],
        ["경험치", f"빵 1개 +{p['xpPerBread']}, 완벽 +{p['xpPerfectBonus']}, 비밀 레시피 +{p['xpSecretBonus']}, 발견 +{p['xpDiscovery']}, 하루 마감 +{p['xpDayComplete']}"],
    ]
    w(table(["항목", "값"], rows))
    w("")
    OUT.write_text("\n".join(out), encoding="utf-8")
    print(f"wrote {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()

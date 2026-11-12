#!/usr/bin/env python3
"""빵집 시뮬레이터 밸런스 검증 도구.

게임과 같은 수치 파일(Assets/_Project/Data/content.json)을 읽어 하루 영업, 가격·생산량 전략,
미스터리 박스, 레벨 성장 속도를 시뮬레이션하고 결과를 마크다운으로 출력한다.

    python3 Tools/balance/simulate.py            # 표준 출력
    python3 Tools/balance/simulate.py --out Tools/balance/latest_report.md

주의: 플레이어가 계산·굽기에 쓰는 시간(ASSUME)은 가정값이다. 실제 플레이 테스트로 잰 값으로 바꿔서
다시 돌려야 한다. 이 도구의 목적은 "수치가 서로 말이 되는지"를 구현 전에 확인하는 것이다.
"""
import argparse
import json
import math
import random
import statistics
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT_PATH = ROOT / "Assets" / "_Project" / "Data" / "content.json"

# 플레이어 조작 시간 가정 (초). 플레이 테스트 후 실측값으로 교체한다.
ASSUME = {
    "checkout_base": 3.0,       # 손님 한 명당 기본 (바라보기, 확정)
    "checkout_per_item": 1.2,   # 빵 하나 스캔
    "pay_card": 4.0,            # 카드: 키패드 입력
    "pay_cash": 7.0,            # 현금: 거스름돈 만들기
    "start_bake": 9.0,          # 계산대 → 오븐 → 패널에서 굽기 시작 → 복귀
    "collect_and_stock": 14.0,  # 오븐에서 꺼내 선반에 진열하고 복귀
    "restock_from_tray": 7.0,   # 작업대의 트레이에서 선반 보충
    "walk_to_oven": 4.0,        # 계산대에서 오븐까지 걸어가는 시간
    "enter_walk": 3.0,          # 손님이 입구에서 매장 안까지
    "step": 0.5,
}


# 규칙 비교 실험용 스위치. 기본값이 게임의 규칙이다.
#   day_old = "discount"    : 하루 지난 빵은 표시가를 할인하고 구매 확률은 그대로 (현재 규칙, ADR 0010)
#   day_old = "probability" : 가격은 그대로 두고 구매 확률을 깎는다 (v1 규칙. --compare-day-old 로 비교)
RULES = {"day_old": "discount", "day_old_probability": 0.6}


def load_content():
    with open(CONTENT_PATH, encoding="utf-8") as f:
        return json.load(f)


def curve(points, x):
    """구간별 선형 보간. 게임의 PiecewiseCurve.Evaluate와 같은 규칙."""
    if x <= points[0][0]:
        return points[0][1]
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        if x <= x1:
            t = (x - x0) / (x1 - x0)
            return y0 + (y1 - y0) * t
    return points[-1][1]


def scale100(price, multiplier):
    """가격에 배율을 곱해 100원 단위로 반올림한다 (50원은 올림). 게임의 PriceMath.Scale과 같은 규칙.

    배율을 퍼센트 정수로 바꿔 정수로만 계산한다. 부동소수로 곱하면 5,500 × 0.7 = 3,849.9999…가 되어
    3,900원이어야 할 값이 3,800원으로 내려가는 경우가 생긴다.
    """
    percent = int(round(multiplier * 100))
    return (int(price) * percent + 5000) // 10000 * 100


class Content:
    def __init__(self, raw):
        self.raw = raw
        self.cfg = raw["config"]
        self.ingredients = {i["id"]: i for i in raw["ingredients"]}
        self.recipes = {r["id"]: r for r in raw["recipes"]}
        self.products = {p["id"]: p for p in raw["products"]}
        self.loot = {t["id"]: t for t in raw["lootTables"]}
        self.profiles = raw["customerProfiles"]
        self.unlocks = raw["unlocks"]
        self.unit_cost = self._unit_costs()
        curve_pts = self.cfg["day"]["spawnCurve"]
        lo, hi = self.cfg["day"]["openMinutes"], self.cfg["day"]["closeMinutes"]
        samples = [curve(curve_pts, lo + (hi - lo) * (i + 0.5) / 200) for i in range(200)]
        self.spawn_curve_mean = sum(samples) / len(samples)

    def _unit_costs(self):
        costs = {}
        for p in self.raw["products"]:
            if p["kind"] == "IngredientPack":
                for c in p["contents"]:
                    costs[c["id"]] = p["cost"] / c["count"]
        return costs

    def batch_cost(self, recipe_id, include_key=False):
        r = self.recipes[recipe_id]
        total = 0.0
        for ing in r["ingredients"]:
            if ing["id"] in self.unit_cost:
                total += self.unit_cost[ing["id"]] * ing["count"]
            elif include_key:
                total += self.ingredients[ing["id"]]["resellPrice"] * ing["count"]
        return total

    def rarity(self, ing_id):
        return self.ingredients[ing_id]["rarity"]


RARITY_ORDER = {"Common": 0, "Fine": 1, "Rare": 2, "Legendary": 3}


# ----------------------------------------------------------------------------
# 손님
# ----------------------------------------------------------------------------

def pick_weighted(rng, weights):
    total = sum(weights)
    x = rng.random() * total
    acc = 0.0
    for i, w in enumerate(weights):
        acc += w
        if x < acc:
            return i
    return len(weights) - 1


def make_plan(content, rng, level, menu):
    """손님 한 명의 방문 계획: 성향, 원하는 빵 목록, 예산, 결제 수단."""
    profs = content.profiles
    weights = [p["spawnWeightByLevel"][level - 1] for p in profs]
    prof = profs[pick_weighted(rng, weights)]
    prefs = {p["id"]: p["weight"] for p in prof["preferences"]}
    candidates = list(menu)
    cand_w = [prefs.get(r, prof["defaultPreference"]) for r in candidates]
    count = rng.randint(1, prof["maxItems"])
    wanted = []
    for _ in range(min(count, len(candidates))):
        i = pick_weighted(rng, cand_w)
        wanted.append(candidates.pop(i))
        cand_w.pop(i)
    return {
        "profile": prof,
        "wanted": wanted,
        "budget": rng.randint(prof["budgetMin"] // 100, prof["budgetMax"] // 100) * 100,
        "cash": rng.random() < prof["cashRatio"],
        "patience": prof["patienceSeconds"],
    }


def expected_wants(content, level, menu, samples=3000, seed=7):
    """손님 한 명이 각 빵을 원할 확률 (몬테카를로)."""
    rng = random.Random(seed)
    counts = defaultdict(int)
    for _ in range(samples):
        for r in make_plan(content, rng, level, menu)["wanted"]:
            counts[r] += 1
    return {r: counts[r] / samples for r in menu}


# ----------------------------------------------------------------------------
# 하루 영업
# ----------------------------------------------------------------------------

class Shop:
    """하루를 넘어 이어지는 매장 상태."""

    def __init__(self, content, level=1, licensed=None, shelves=2, ovens=1, trays=None, reputation=None):
        cfg = content.cfg
        self.content = content
        self.level = level
        self.licensed = list(licensed or cfg["economy"]["startRecipes"])
        self.shelves = shelves
        self.ovens = ovens
        self.trays = trays or cfg["economy"]["startTrays"]
        self.reputation = cfg["reputation"]["start"] if reputation is None else reputation
        self.price_ratio = {}            # recipe -> 판매가/시세
        self.leftover = defaultdict(list)  # recipe -> [(daysOld, quality), ...] (선반에 남은 빵)
        self.secret_batches = defaultdict(int)  # recipe -> 오늘 구울 수 있는 판 수 (핵심 재료 수)

    def price(self, recipe_id):
        market = self.content.recipes[recipe_id]["marketPrice"]
        return scale100(market, self.price_ratio.get(recipe_id, 1.0))


class Policy:
    def __init__(self, fill=1.0, jit=True, reorder_point=2, oven_first=True):
        self.fill = fill              # 준비 단계에 예상 수요의 몇 배를 미리 구울지
        self.jit = jit                # 영업 중에 품절 임박 시 추가로 굽는지
        self.reorder_point = reorder_point
        self.oven_first = oven_first  # 다 구워진 오븐을 계산보다 먼저 처리하는지


def simulate_day(shop, policy, rng):
    c = shop.content
    cfg = c.cfg
    A = ASSUME
    dt = A["step"]
    open_secs = cfg["day"]["openRealSeconds"]
    lo, hi = cfg["day"]["openMinutes"], cfg["day"]["closeMinutes"]
    slot_cap = cfg["shelf"]["slotCapacity"]
    total_slots = shop.shelves * cfg["shelf"]["slotsPerShelf"]
    cust_cfg = cfg["customers"]
    rep_cfg = cfg["reputation"]
    prog = cfg["progression"]

    rep_factor = cust_cfg["reputationSpawnMin"] + (cust_cfg["reputationSpawnMax"] - cust_cfg["reputationSpawnMin"]) * shop.reputation / rep_cfg["max"]
    n_expected = cust_cfg["baseCountByLevel"][shop.level - 1] * rep_factor

    # 메뉴: 라이선스 레시피는 항상, 비밀 레시피는 오늘 구울 수 있을 때만
    secret_today = [r for r, n in shop.secret_batches.items() if n > 0]
    menu = list(shop.licensed) + secret_today
    wants = expected_wants(c, shop.level, menu, samples=800, seed=rng.randint(0, 10**9))
    demand = {r: wants[r] * n_expected * 0.9 for r in menu}

    # 칸 배정: 메뉴마다 1칸, 남는 칸은 칸당 수요가 가장 큰 빵에
    ranked = sorted(menu, key=lambda r: -demand[r])
    slots = {r: 0 for r in menu}
    for r in ranked[:total_slots]:
        slots[r] = 1
    free = total_slots - sum(slots.values())
    while free > 0:
        r = max(menu, key=lambda r: demand[r] / (slots[r] * slot_cap) if slots[r] else -1)
        slots[r] += 1
        free -= 1
    cap = {r: slots[r] * slot_cap for r in menu}

    stats = defaultdict(float)
    sold_by = defaultdict(int)
    missed_by = defaultdict(int)
    pricey_by = defaultdict(int)

    # 선반 재고: recipe -> [(daysOld, quality)], 앞이 먼저 팔림. 어제 남은 빵부터.
    stock = {r: list(shop.leftover.get(r, []))[:cap.get(r, 0)] for r in menu}
    for r, olds in shop.leftover.items():
        if r not in stock:
            stats["waste_count"] += len(olds)
            stats["waste_value"] += len(olds) * shop.price(r)
    buffer = defaultdict(list)   # 작업대 트레이 위의 빵
    trays_free = shop.trays
    secret_left = dict(shop.secret_batches)

    def bake_cost(r):
        stats["ingredient_cost"] += c.batch_cost(r)
        stats["batches"] += 1

    def can_bake(r):
        rec = c.recipes[r]
        if rec["secret"] and secret_left.get(r, 0) <= 0:
            return False
        return True

    def use_key(r):
        if c.recipes[r]["secret"]:
            secret_left[r] -= 1

    # --- 준비 단계: 시간 제한이 없으므로 모두 '완벽'으로 굽는다고 본다 ---
    for r in ranked:
        if cap[r] == 0:
            continue
        rec = c.recipes[r]
        target = policy.fill * demand[r] - len(stock[r])
        while target > 0 and len(stock[r]) + len(buffer[r]) < cap[r] + (rec["yield"] if trays_free > 1 else 0) and can_bake(r):
            use_key(r)
            bake_cost(r)
            made = [(0, "Perfect")] * rec["yield"]
            room = cap[r] - len(stock[r])
            stock[r].extend(made[:room])
            rest = made[room:]
            if rest:
                if trays_free <= 1:      # 오븐용 트레이 하나는 남긴다
                    stats["waste_count"] += len(rest)
                    break
                buffer[r].extend(rest)
                trays_free -= 1
            target -= rec["yield"]

    # --- 영업 ---
    ovens = [None] * shop.ovens          # None 또는 {"r", "t"}
    customers = []                       # 매장 안 손님
    queue = []                           # 계산 줄
    player_busy_until = 0.0
    player_task = None
    busy_time = 0.0
    aroma_until = -1.0
    t = 0.0
    arrivals = 0
    max_queue = 0

    def tag_price(r, days_old):
        # 표시가: 하루 지난 빵은 할인 스티커가 붙는다. 구매 판단과 결제의 기준.
        mult = cust_cfg["dayOldPriceMultiplier"] if days_old >= 1 and RULES["day_old"] == "discount" else 1.0
        return scale100(shop.price(r), mult)

    def inflight(r):
        return sum(1 for o in ovens if o and o["r"] == r) * c.recipes[r]["yield"]

    def mark_pull(idx):
        # 품질은 플레이어가 오븐에 도착해 꺼내는 순간 정해진다 (계산대에서 걸어가는 시간 포함).
        o = ovens[idx]
        o["pulled_over"] = o["t"] - c.recipes[o["r"]]["bakeSeconds"] + A["walk_to_oven"]

    def finish_checkout(cust):
        total = 0
        for (r, days_old, quality) in cust["basket"]:
            mult = cfg["economy"]["qualityPriceMultiplier"][quality]
            total += scale100(tag_price(r, days_old), mult)
            sold_by[r] += 1
            stats["xp"] += prog["xpPerBread"]
            if quality == "Perfect":
                stats["xp"] += prog["xpPerfectBonus"]
            if c.recipes[r]["secret"]:
                stats["xp"] += prog["xpSecretBonus"]
        stats["revenue"] += total
        stats["served"] += 1
        stats["items"] += len(cust["basket"])
        stats["rep"] += rep_cfg["saleCompleted"]

    while t < open_secs or customers or queue or player_task:
        clock = lo + (hi - lo) * min(t, open_secs) / open_secs
        # 손님 도착
        if t < open_secs and len(customers) + len(queue) < cust_cfg["maxConcurrent"]:
            rate = n_expected / open_secs * curve(cfg["day"]["spawnCurve"], clock) / c.spawn_curve_mean
            if t < aroma_until:
                rate *= cust_cfg["aromaSpawnMultiplier"]
            if rng.random() < rate * dt:
                plan = make_plan(c, rng, shop.level, [r for r in menu if not c.recipes[r]["secret"] or stock[r] or buffer[r]] or menu)
                plan["timer"] = A["enter_walk"] + cust_cfg["browseSecondsPerItem"] * len(plan["wanted"])
                plan["basket"] = []
                customers.append(plan)
                arrivals += 1

        # 둘러보기 끝난 손님: 구매 판단
        for cust in list(customers):
            cust["timer"] -= dt
            if cust["timer"] > 0:
                continue
            customers.remove(cust)
            spent = 0
            any_missed = False
            for r in cust["wanted"]:
                if not stock.get(r):
                    missed_by[r] += 1
                    any_missed = True
                    continue
                days_old, quality = stock[r][0]
                price = tag_price(r, days_old)
                ratio = price / c.recipes[r]["marketPrice"]
                p = curve(cust["profile"]["priceSensitivity"], ratio)
                if quality == "Undercooked":
                    p *= cust_cfg["undercookedMultiplier"]
                if days_old >= 1 and RULES["day_old"] == "probability":
                    p *= RULES["day_old_probability"]
                if spent + price > cust["budget"] or rng.random() >= p:
                    pricey_by[r] += 1
                    continue
                stock[r].pop(0)
                cust["basket"].append((r, days_old, quality))
                spent += price
            if cust["basket"]:
                queue.append(cust)
            else:
                if any_missed:
                    stats["left_no_stock"] += 1
                    stats["rep"] += rep_cfg["leftNoStock"]
                else:
                    stats["left_too_expensive"] += 1
                    stats["rep"] += rep_cfg["leftTooExpensive"]

        # 줄에서 기다리는 손님의 인내심 (계산 중인 맨 앞 손님 제외)
        serving = player_task and player_task[0] == "checkout"
        for i, cust in enumerate(list(queue)):
            if i == 0 and serving:
                continue
            cust["patience"] -= dt
            if cust["patience"] <= 0:
                queue.remove(cust)
                stats["left_queue"] += 1
                stats["rep"] += rep_cfg["leftQueueTimeout"]
                for (r, days_old, quality) in cust["basket"]:
                    if len(stock[r]) < cap[r]:
                        stock[r].insert(0, (days_old, quality))
                    else:
                        stats["waste_count"] += 1
                        stats["waste_value"] += shop.price(r)
        max_queue = max(max_queue, len(queue))

        # 오븐 진행
        for o in ovens:
            if o:
                o["t"] += dt

        # 플레이어
        if player_task and t >= player_busy_until:
            kind, arg = player_task
            player_task = None
            if kind == "checkout":
                queue.remove(arg)
                finish_checkout(arg)
            elif kind == "collect":
                idx = arg
                o = ovens[idx]
                rec = c.recipes[o["r"]]
                over = o["pulled_over"]
                ovens[idx] = None
                if over > rec["burnAfterSeconds"]:
                    stats["burnt_batches"] += 1
                    stats["waste_count"] += rec["yield"]
                    trays_free += 1
                else:
                    quality = "Perfect" if over <= rec["perfectWindowSeconds"] else "Normal"
                    stats["perfect_batches" if quality == "Perfect" else "normal_batches"] += 1
                    made = [(0, quality)] * rec["yield"]
                    room = cap[o["r"]] - len(stock[o["r"]])
                    stock[o["r"]].extend(made[:room])
                    rest = made[room:]
                    if rest:
                        buffer[o["r"]].extend(rest)   # 트레이는 계속 쓰는 중
                    else:
                        trays_free += 1
                    if t < open_secs:
                        aroma_until = t + cust_cfg["aromaSeconds"]
            elif kind == "restock":
                r = arg
                room = cap[r] - len(stock[r])
                moved = buffer[r][:room]
                stock[r].extend(moved)
                del buffer[r][:room]
                if not buffer[r]:
                    trays_free += 1

        if player_task is None:
            done_ovens = [i for i, o in enumerate(ovens) if o and o["t"] >= c.recipes[o["r"]]["bakeSeconds"]]
            task = None
            if done_ovens and (policy.oven_first or not queue):
                task = ("collect", done_ovens[0], A["collect_and_stock"])
                mark_pull(done_ovens[0])
            elif queue:
                cust = queue[0]
                dur = A["checkout_base"] + A["checkout_per_item"] * len(cust["basket"]) + (A["pay_cash"] if cust["cash"] else A["pay_card"])
                task = ("checkout", cust, dur)
            elif done_ovens:
                task = ("collect", done_ovens[0], A["collect_and_stock"])
                mark_pull(done_ovens[0])
            else:
                low_buffer = [r for r in menu if buffer[r] and len(stock[r]) <= policy.reorder_point and cap[r] > 0]
                if low_buffer:
                    task = ("restock", low_buffer[0], A["restock_from_tray"])
                elif policy.jit and t < open_secs - 60 and None in ovens and trays_free > 0:
                    need = [r for r in ranked if cap[r] > 0 and can_bake(r) and not buffer[r]
                            and len(stock[r]) + inflight(r) <= policy.reorder_point
                            and demand[r] * (1 - t / open_secs) >= 1.5]
                    if need:
                        r = need[0]
                        use_key(r)
                        bake_cost(r)
                        trays_free -= 1
                        ovens[ovens.index(None)] = {"r": r, "t": -A["start_bake"] * 0.5}
                        task = ("start", r, A["start_bake"])
            if task:
                player_task = (task[0], task[1])
                player_busy_until = t + task[2]
                busy_time += task[2]

        t += dt
        if t > open_secs + 600:   # 안전장치
            break

    # --- 하루 마무리 ---
    for o in ovens:
        if o:
            stats["waste_count"] += c.recipes[o["r"]]["yield"]
    new_leftover = defaultdict(list)
    for r in menu:
        life = c.recipes[r]["shelfLifeDays"]
        for (days_old, quality) in stock[r] + buffer[r]:
            if days_old + 1 > life:
                stats["waste_count"] += 1
                stats["waste_value"] += shop.price(r)
            else:
                new_leftover[r].append((days_old + 1, quality))
    shop.leftover = new_leftover

    rent = cfg["economy"]["rentByLevel"][shop.level - 1]
    stats["rent"] = rent
    stats["net"] = stats["revenue"] - stats["ingredient_cost"] - rent
    stats["xp"] += prog["xpDayComplete"]
    stats["arrivals"] = arrivals
    stats["utilization"] = busy_time / max(t, 1.0)
    stats["max_queue"] = max_queue
    stats["missed"] = sum(missed_by.values())
    stats["too_expensive"] = sum(pricey_by.values())
    return stats, sold_by, missed_by, pricey_by


LEVEL_SETUPS = {
    1: dict(licensed=["bread.white", "bread.redbean", "bread.croissant"], shelves=2, ovens=1, trays=3),
    2: dict(licensed=["bread.white", "bread.redbean", "bread.croissant", "bread.saltbutter"], shelves=3, ovens=1, trays=4),
    3: dict(licensed=["bread.white", "bread.redbean", "bread.croissant", "bread.saltbutter", "bread.baguette"], shelves=3, ovens=2, trays=5),
    4: dict(licensed=["bread.white", "bread.redbean", "bread.croissant", "bread.saltbutter", "bread.baguette", "bread.choco_cornet"], shelves=4, ovens=2, trays=6),
    5: dict(licensed=["bread.white", "bread.redbean", "bread.croissant", "bread.saltbutter", "bread.baguette", "bread.choco_cornet"], shelves=4, ovens=2, trays=6),
}


def average_days(content, level, policy, seeds=40, days=12, warmup=2, price_ratio=None, reputation=None, setup=None):
    keys = ["arrivals", "served", "items", "revenue", "ingredient_cost", "waste_count", "waste_value", "rent", "net", "xp", "rep",
            "left_no_stock", "left_too_expensive", "left_queue", "missed", "too_expensive", "utilization", "max_queue",
            "batches", "perfect_batches", "normal_batches", "burnt_batches"]
    acc = defaultdict(list)
    for seed in range(seeds):
        rng = random.Random(1000 * level + seed)
        shop = Shop(content, level=level, reputation=reputation, **(setup or LEVEL_SETUPS[level]))
        if price_ratio:
            shop.price_ratio = dict(price_ratio) if isinstance(price_ratio, dict) else {r: price_ratio for r in content.recipes}
        for d in range(days):
            stats, *_ = simulate_day(shop, policy, rng)
            if d >= warmup:
                for k in keys:
                    acc[k].append(stats.get(k, 0.0))
    return {k: (statistics.mean(acc[k]) if acc[k] else 0.0) for k in keys}


# ----------------------------------------------------------------------------
# 미스터리 박스
# ----------------------------------------------------------------------------

def open_box(content, table_id, rng, pity_state):
    """박스 하나를 연다. 게임의 LootService.Open과 같은 규칙."""
    table = content.loot[table_id]
    entries = table["entries"]
    weights = [e["weight"] for e in entries]
    min_rank = RARITY_ORDER[table["pityMinRarity"]]
    drops = []
    got_rare = False
    forced = pity_state.get(table_id, 0) >= table["pityThreshold"]
    for i in range(table["rollsPerBox"]):
        last = i == table["rollsPerBox"] - 1
        if forced and last and not got_rare:
            idxs = [j for j, e in enumerate(entries) if RARITY_ORDER[content.rarity(e["id"])] >= min_rank]
            j = idxs[pick_weighted(rng, [weights[k] for k in idxs])]
        else:
            j = pick_weighted(rng, weights)
        e = entries[j]
        drops.append((e["id"], e["count"]))
        if RARITY_ORDER[content.rarity(e["id"])] >= min_rank:
            got_rare = True
    pity_state[table_id] = 0 if got_rare else pity_state.get(table_id, 0) + 1
    return drops, (forced and got_rare)


def loot_report(content, table_id, boxes=200000, seed=11):
    rng = random.Random(seed)
    table = content.loot[table_id]
    pity = {}
    rare_boxes = 0
    forced_count = 0
    value_buy = 0.0
    value_resell = 0.0
    key_counts = defaultdict(int)
    longest_dry = 0
    dry = 0
    for _ in range(boxes):
        drops, forced = open_box(content, table_id, rng, pity)
        got = False
        for ing_id, count in drops:
            value_buy += content.unit_cost.get(ing_id, 0) * count
            value_resell += content.ingredients[ing_id]["resellPrice"] * count
            if ing_id not in content.unit_cost or content.rarity(ing_id) != "Common":
                key_counts[ing_id] += count
            if RARITY_ORDER[content.rarity(ing_id)] >= RARITY_ORDER[table["pityMinRarity"]]:
                got = True
        rare_boxes += got
        forced_count += forced
        dry = 0 if got else dry + 1
        longest_dry = max(longest_dry, dry)
    total_w = sum(e["weight"] for e in table["entries"])
    p_roll = sum(e["weight"] for e in table["entries"] if RARITY_ORDER[content.rarity(e["id"])] >= 2) / total_w
    theory_no_pity = 1 - (1 - p_roll) ** table["rollsPerBox"]
    price = next(p["cost"] for p in content.raw["products"] if p.get("lootTable") == table_id)
    return {
        "price": price,
        "p_roll": p_roll,
        "theory_no_pity": theory_no_pity,
        "rare_box_rate": rare_boxes / boxes,
        "forced_rate": forced_count / boxes,
        "longest_dry": longest_dry,
        "value_buy": value_buy / boxes,
        "value_resell": value_resell / boxes,
        "per_box": {k: v / boxes for k, v in sorted(key_counts.items())},
    }


def boxes_until(content, table_id, target_ids, trials=20000, seed=5):
    rng = random.Random(seed)
    results = []
    for _ in range(trials):
        pity = {}
        n = 0
        while True:
            n += 1
            drops, _ = open_box(content, table_id, rng, pity)
            if any(d[0] in target_ids for d in drops):
                break
        results.append(n)
    results.sort()
    return statistics.mean(results), results[len(results) // 2], results[int(len(results) * 0.9)]


# ----------------------------------------------------------------------------
# 성장 속도
# ----------------------------------------------------------------------------

def progression_run(content, seed, max_days=30, buy_boxes=True):
    rng = random.Random(seed)
    cfg = content.cfg
    eco = cfg["economy"]
    th = cfg["progression"]["xpThresholds"]
    shop = Shop(content)
    policy = Policy(fill=0.8, jit=True)
    money = eco["startMoney"]
    xp = 0
    owned = set()
    key_inventory = defaultdict(int)
    discovered = set()
    pity = {}
    key_to_recipe = {r["keyIngredient"]: r["id"] for r in content.raw["recipes"] if r.get("secret")}
    reached = {1: 1}
    log = []
    first_discovery_day = None
    subsidy_days = 0
    for day in range(1, max_days + 1):
        spent_unlock = 0
        # 아침: 해금 (라이선스 → 트레이 → 가구 순, 여유 자금 20,000원 남김)
        order = {"RecipeLicense": 0, "Tray": 1, "Furniture": 2}
        for u in sorted(content.unlocks, key=lambda u: (order[u["kind"]], u["cost"])):
            if u["id"] in owned or u["requiredLevel"] > shop.level:
                continue
            if money - u["cost"] >= 20000:
                money -= u["cost"]
                spent_unlock += u["cost"]
                owned.add(u["id"])
                if u["kind"] == "RecipeLicense":
                    shop.licensed.append(u["target"])
                elif u["kind"] == "Tray":
                    shop.trays += 1
                elif u["target"].startswith("shelf"):
                    shop.shelves += 1
                else:
                    shop.ovens += 1
        # 아침: 미스터리 박스
        box_spend = 0
        box_credit = 0
        if buy_boxes:
            table = None
            if shop.level >= 4 and money >= 100000:
                table, price = "loot.premium", content.products["product.box.premium"]["cost"]
            elif shop.level >= 2 and money >= 45000:
                table, price = "loot.basic", content.products["product.box.basic"]["cost"]
            if table:
                money -= price
                box_spend = price
                drops, _ = open_box(content, table, rng, pity)
                for ing_id, count in drops:
                    if ing_id in key_to_recipe:
                        key_inventory[ing_id] += count
                        if key_to_recipe[ing_id] not in discovered:
                            discovered.add(key_to_recipe[ing_id])
                            xp += cfg["progression"]["xpDiscovery"]
                            shop.reputation = min(cfg["reputation"]["max"], shop.reputation + cfg["reputation"]["recipeDiscovered"])
                            money -= 4000   # 실험 3~4회의 재료비
                            if first_discovery_day is None:
                                first_discovery_day = day
                    else:
                        box_credit += content.unit_cost.get(ing_id, 0) * count   # 발주를 대신한 만큼
        # 오늘 구울 비밀 레시피 (핵심 재료 1개 = 1판, 하루 최대 2판씩)
        shop.secret_batches = defaultdict(int)
        for ing_id, n in key_inventory.items():
            if n > 0:
                shop.secret_batches[key_to_recipe[ing_id]] = min(n, 2)
        shop.price_ratio = {r["id"]: (1.25 if r.get("secret") else 1.05) for r in content.raw["recipes"]}
        before = dict(shop.secret_batches)
        stats, sold, missed, pricey = simulate_day(shop, policy, rng)
        # 쓴 핵심 재료 차감 (간단히: 그날 올린 판 수만큼)
        for ing_id in list(key_inventory):
            rid = key_to_recipe[ing_id]
            used = min(before.get(rid, 0), math.ceil(sold.get(rid, 0) / content.recipes[rid]["yield"]) or (1 if before.get(rid, 0) else 0))
            key_inventory[ing_id] -= used
        money += stats["revenue"] - max(0.0, stats["ingredient_cost"] - box_credit) - stats["rent"]
        if money < eco["moneyFloor"]:
            money = eco["moneyFloor"]
            subsidy_days += 1
        xp += stats["xp"]
        shop.reputation = max(0, min(cfg["reputation"]["max"], shop.reputation + stats["rep"]))
        while shop.level < len(th) and xp >= th[shop.level]:
            shop.level += 1
            reached[shop.level] = day
        log.append(dict(day=day, level=shop.level, money=money, revenue=stats["revenue"], net=stats["net"], rep=shop.reputation,
                        served=stats["served"], unlock=spent_unlock, box=box_spend, discovered=len(discovered)))
    return reached, log, first_discovery_day, subsidy_days, len(discovered), len(owned)


# ----------------------------------------------------------------------------
# 리포트
# ----------------------------------------------------------------------------

def won(v):
    return f"{int(round(v)):,}"


def table(headers, rows):
    out = ["| " + " | ".join(headers) + " |", "| " + " | ".join("---" for _ in headers) + " |"]
    for row in rows:
        out.append("| " + " | ".join(str(x) for x in row) + " |")
    return "\n".join(out)


def build_report(content, quick=False):
    seeds = 12 if quick else 40
    out = []
    w = out.append
    w("# 밸런스 시뮬레이션 결과\n")
    w("`Tools/balance/simulate.py`가 `Assets/_Project/Data/content.json`을 읽어 만든 결과다. "
      "플레이어 조작 시간은 가정값이므로, 절대 수치보다 **전략 사이의 상대적 차이**와 **수치가 서로 맞물리는지**를 본다.\n")
    w("가정한 조작 시간(초): " + ", ".join(f"{k} {v}" for k, v in ASSUME.items() if k != "step") + "\n")

    # 1. 레시피 경제성
    w("## 1. 레시피 한 판의 경제성\n")
    rows = []
    for r in content.raw["recipes"]:
        cost = content.batch_cost(r["id"])
        rev = r["yield"] * r["marketPrice"]
        profit = rev - cost
        rows.append([r["name"] + (" (비밀)" if r["secret"] else ""), r["yield"], won(r["marketPrice"]), won(cost), won(rev),
                     f"{cost / rev * 100:.0f}%", won(profit), won(profit / r["bakeSeconds"]), won(profit / r["yield"])])
    w(table(["레시피", "개수", "시세", "재료비/판", "매출/판", "원가율", "이익/판", "이익/오븐 1초", "이익/빵 1개"], rows))
    w("\n비밀 레시피의 재료비에는 핵심 재료(박스에서만 나옴)가 빠져 있다.\n")

    # 2. 레벨별 기준 하루
    w("## 2. 레벨별 기준 하루 (시세대로 판매, 예상 수요만큼 준비, 영업 중 추가 굽기)\n")
    rows = []
    base = {}
    for level in range(1, 6):
        s = average_days(content, level, Policy(), seeds=seeds)
        base[level] = s
        rows.append([level, f"{s['arrivals']:.1f}", f"{s['served']:.1f}", f"{s['items']:.1f}", won(s["revenue"]), won(s["ingredient_cost"]),
                     won(s["rent"]), won(s["net"]), f"{s['waste_count']:.1f}", f"{s['left_no_stock'] + s['left_too_expensive']:.1f}",
                     f"{s['left_queue']:.1f}", f"{s['utilization'] * 100:.0f}%", f"{s['xp']:.0f}", f"{s['rep']:+.0f}"])
    w(table(["레벨", "방문", "결제", "판매 개수", "매출", "재료비", "임대료", "순이익", "폐기(개)", "빈손 이탈", "줄 이탈", "플레이어 바쁨", "XP", "평판"], rows))
    w("")

    # 3. 가격
    w("## 3. 가격을 일괄로 올리면 (레벨 3)\n")
    rows = []
    for ratio in [0.9, 1.0, 1.05, 1.1, 1.15, 1.2, 1.3, 1.4, 1.5]:
        s = average_days(content, 3, Policy(), seeds=seeds, price_ratio=ratio)
        rows.append([f"{ratio:.2f}", f"{s['served']:.1f}", f"{s['items']:.1f}", won(s["revenue"]), won(s["net"]), f"{s['too_expensive']:.1f}", f"{s['rep']:+.0f}"])
    w(table(["판매가/시세", "결제 손님", "판매 개수", "매출", "순이익", "비싸서 안 산 횟수", "평판"], rows))
    w("")
    w("### 손님 성향별 기대 매출 (판매가/시세 × 구매 확률)\n")
    ratios = [1.0, 1.1, 1.2, 1.3, 1.4, 1.6]
    rows = []
    for p in content.profiles:
        rows.append([p["name"]] + [f"{r * curve(p['priceSensitivity'], r):.2f}" for r in ratios])
    w(table(["성향"] + [f"{r:.1f}배" for r in ratios], rows))
    w("")

    # 4. 생산량
    w("## 4. 준비 단계에 얼마나 구울까 (레벨 3)\n")
    rows = []
    for jit in (True, False):
        for fill in [0.4, 0.7, 1.0, 1.3, 1.6]:
            s = average_days(content, 3, Policy(fill=fill, jit=jit), seeds=seeds)
            rows.append(["굽는다" if jit else "안 굽는다", f"{fill:.1f}", f"{s['arrivals']:.1f}", f"{s['items']:.1f}", won(s["revenue"]), won(s["ingredient_cost"]),
                         won(s["net"]), f"{s['waste_count']:.1f}", f"{s['missed']:.1f}", f"{s['left_queue']:.1f}", f"{s['utilization'] * 100:.0f}%"])
    w(table(["영업 중 추가 굽기", "준비량(예상 수요 대비)", "방문", "판매 개수", "매출", "재료비", "순이익", "폐기(개)", "놓친 수요", "줄 이탈", "플레이어 바쁨"], rows))
    w("")

    # 5. 오븐 우선 vs 계산 우선
    w("## 5. 다 구워진 오븐과 계산 줄 중 무엇을 먼저 (레벨 4)\n")
    rows = []
    for oven_first in (True, False):
        s = average_days(content, 4, Policy(fill=0.7, oven_first=oven_first), seeds=seeds)
        total = max(1.0, s["perfect_batches"] + s["normal_batches"] + s["burnt_batches"])
        rows.append(["오븐 먼저" if oven_first else "계산 먼저", won(s["net"]), f"{s['perfect_batches'] / total * 100:.0f}%", f"{s['burnt_batches'] / total * 100:.0f}%",
                     f"{s['left_queue']:.1f}", f"{s['rep']:+.0f}"])
    w(table(["우선순위", "순이익", "영업 중 구운 판의 완벽 비율", "탄 비율", "줄 이탈", "평판"], rows))
    w("")

    # 6. 평판
    w("## 6. 평판에 따른 차이 (레벨 3)\n")
    rows = []
    for rep in [200, 500, 800, 1000]:
        s = average_days(content, 3, Policy(), seeds=seeds, reputation=rep)
        rows.append([rep, f"{s['arrivals']:.1f}", won(s["revenue"]), won(s["net"])])
    w(table(["평판", "방문", "매출", "순이익"], rows))
    w("")

    # 7. 미스터리 박스
    w("## 7. 미스터리 박스\n")
    rows = []
    reports = {}
    for tid in content.loot:
        r = loot_report(content, tid, boxes=40000 if quick else 200000)
        reports[tid] = r
        rows.append([tid, won(r["price"]), f"{r['p_roll'] * 100:.1f}%", f"{r['theory_no_pity'] * 100:.1f}%", f"{r['rare_box_rate'] * 100:.1f}%",
                     f"{r['forced_rate'] * 100:.1f}%", r["longest_dry"], won(r["value_buy"]), f"{r['value_buy'] / r['price'] * 100:.0f}%"])
    w(table(["확률표", "가격", "뽑기 1회 Rare 이상", "박스당 Rare 이상 (천장 없음, 이론)", "박스당 Rare 이상 (천장 포함, 실측)", "천장 발동 비율",
             "최장 연속 꽝(박스)", "일반 재료 가치(발주가 기준)", "가격 대비"], rows))
    w("")
    rows = []
    for tid, r in reports.items():
        for ing_id, per in r["per_box"].items():
            if content.rarity(ing_id) == "Common":
                continue
            rows.append([tid, content.ingredients[ing_id]["name"], content.rarity(ing_id), f"{per:.3f}", f"{1 / per:.1f}" if per else "-"])
    w(table(["확률표", "재료", "희귀도", "박스당 평균 개수", "1개 얻는 데 필요한 박스(평균)"], rows))
    w("")
    rows = []
    for label, tid, targets in [
        ("일반 박스에서 첫 아몬드", "loot.basic", {"ing.almond"}),
        ("일반 박스에서 첫 Rare 이상", "loot.basic", {"ing.matcha", "ing.truffle_salt", "ing.golden_butter"}),
        ("일반 박스에서 첫 황금 버터", "loot.basic", {"ing.golden_butter"}),
        ("고급 박스에서 첫 Rare 이상", "loot.premium", {"ing.matcha", "ing.truffle_salt", "ing.golden_butter"}),
        ("고급 박스에서 첫 황금 버터", "loot.premium", {"ing.golden_butter"}),
    ]:
        mean, median, p90 = boxes_until(content, tid, targets, trials=4000 if quick else 20000)
        rows.append([label, f"{mean:.1f}", median, p90])
    w(table(["목표", "평균 박스 수", "중앙값", "운이 나쁜 10%"], rows))
    w("")

    # 8. 성장 속도
    w("## 8. 성장 속도 (자동 플레이: 여유 자금이 되면 바로 해금, 하루 1박스)\n")
    runs = [progression_run(content, seed) for seed in range(20 if quick else 60)]
    rows = []
    for level in range(2, 6):
        days = [r[0][level] for r in runs if level in r[0]]
        if days:
            days.sort()
            rows.append([level, f"{statistics.mean(days):.1f}", days[0], days[-1], f"{len(days)}/{len(runs)}"])
        else:
            rows.append([level, "-", "-", "-", f"0/{len(runs)}"])
    w(table(["레벨", "도달 평균(일차)", "가장 빠름", "가장 느림", "30일 안에 도달"], rows))
    w("")
    disc = [r[2] for r in runs if r[2]]
    w(f"- 첫 비밀 레시피 발견: 평균 {statistics.mean(disc):.1f}일차 ({len(disc)}/{len(runs)}회에서 발견)" if disc else "- 첫 비밀 레시피 발견: 없음")
    w(f"- 30일 동안 발견한 비밀 레시피 수: 평균 {statistics.mean(r[4] for r in runs):.1f}종 / 4종")
    w(f"- 지원금을 받은 날: 평균 {statistics.mean(r[3] for r in runs):.2f}일")
    all_unlocks = len(content.unlocks)
    full = [next((e['day'] for e in r[1] if e['level'] >= 5), None) for r in runs]
    w(f"- 30일차에 구매한 해금: 평균 {statistics.mean(r[5] for r in runs):.1f}개 / {all_unlocks}개\n")
    sample = runs[0][1]
    rows = [[e["day"], e["level"], won(e["money"]), won(e["revenue"]), won(e["net"]), f"{e['served']:.0f}", e["rep"], won(e["unlock"]), won(e["box"]), e["discovered"]]
            for e in sample if e["day"] <= 20]
    w("표본 한 번의 흐름 (seed 0):\n")
    w(table(["일차", "레벨", "잔액(마감)", "매출", "영업 순이익", "결제 손님", "평판", "해금 지출", "박스 지출", "발견 수"], rows))
    w("")
    return "\n".join(out)


def compare_day_old(content, seeds=40):
    """하루 지난 빵 규칙 두 가지를 같은 조건(레벨 3, 영업 중 추가 굽기 없음)에서 비교한다."""
    out = ["## 하루 지난 빵 규칙 비교 (레벨 3, 영업 중 추가 굽기 없음)\n"]
    rows = []
    for rule, label in (("probability", "구매 확률 ×0.6 (v1)"), ("discount", "표시가 30% 할인 (현재)")):
        RULES["day_old"] = rule
        for fill in [0.4, 0.7, 1.0, 1.3, 1.6]:
            s = average_days(content, 3, Policy(fill=fill, jit=False), seeds=seeds)
            rows.append([label, f"{fill:.1f}", f"{s['items']:.1f}", won(s["revenue"]), won(s["net"]), f"{s['missed']:.1f}",
                         f"{s['too_expensive']:.1f}", f"{s['left_no_stock'] + s['left_too_expensive']:.1f}", f"{s['waste_count']:.1f}", f"{s['rep']:+.0f}"])
    RULES["day_old"] = "discount"
    out.append(table(["규칙", "준비량", "판매 개수", "매출", "순이익", "놓친 수요", "안 산 횟수", "빈손 이탈", "폐기(개)", "평판"], rows))
    return "\n".join(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", help="결과를 저장할 마크다운 경로")
    ap.add_argument("--quick", action="store_true", help="표본 수를 줄여 빠르게")
    ap.add_argument("--compare-day-old", action="store_true", help="하루 지난 빵 규칙(확률 감소 / 할인)만 비교해 출력")
    args = ap.parse_args()
    content = Content(load_content())
    if args.compare_day_old:
        sys.stdout.write(compare_day_old(content, seeds=12 if args.quick else 40) + "\n")
        return
    report = build_report(content, quick=args.quick)
    if args.out:
        Path(args.out).write_text(report + "\n", encoding="utf-8")
        print(f"wrote {args.out}")
    else:
        sys.stdout.write(report + "\n")


if __name__ == "__main__":
    main()

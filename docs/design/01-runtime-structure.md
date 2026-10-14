# 01. 런타임 구조

게임은 **부트스트랩 하나가 모든 시스템을 만들고 연결**하는 구조다. 싱글톤과 전역 static 상태를 쓰지 않고, 규칙 로직은 순수 C# 모델에, 화면과 물리는 MonoBehaviour 뷰에 둔다.

## 씬 구성

| 씬 | 역할 |
| --- | --- |
| `Title` | 새 게임 / 이어하기 선택, 설정 |
| `Shop` | 게임 플레이 전체 (매장, 창고, 문 앞 배송 지점) |

`Title`은 선택 결과를 `LaunchRequest`(새 게임인지, 어떤 세이브 슬롯인지)로 남기고 `Shop`을 로드한다. 매장이 하나뿐이라 씬을 나누지 않는다.

`Shop` 씬 하이어라키:

```
Shop
├── [Bootstrap]          GameBootstrap
├── [Environment]        벽·바닥·조명, NavMeshSurface
├── [Furniture]          Oven ×2, Shelf ×4, CheckoutCounter, OrderPC, StorageArea
├── [Points]             DeliveryPoint, CustomerSpawn, CustomerExit
├── [Player]             PlayerRig (CharacterController, Camera, Interactor, Hands)
├── [Pools]              CustomerPool, BreadViewPool, BoxPool
└── [UI]                 HUD, OvenPanel, PCApp, CheckoutPanel, ReportPanel
```

## 부트스트랩 (Composition Root)

`GameBootstrap`은 씬에 하나만 있는 MonoBehaviour로, 게임의 시작점이다.

```mermaid
flowchart TB
    Boot["GameBootstrap.Awake()"] --> DB["1. ContentDatabase 로드<br/>(레시피·재료·상품·손님·루트테이블 SO)"]
    DB --> Core["2. 코어 생성<br/>EventBus, Rng, TimeService"]
    Core --> Models["3. 시스템 생성 (순수 C#)<br/>Wallet, Storage, Pricing, Ovens, Shelves,<br/>Orders, Loot, RecipeBook, Customers, Checkout, Economy, DayCycle"]
    Models --> Ctx["4. GameContext로 묶음"]
    Ctx --> State["5. 상태 채우기<br/>새 게임: 시작 데이터 / 이어하기: SaveService.Load"]
    State --> Views["6. 씬 뷰 초기화<br/>FindObjectsByType로 IContextConsumer를 찾아 Init(ctx)"]
    Views --> Start["7. DayCycle.Begin() → 영업 준비 단계"]
```

- `GameContext`는 생성된 시스템을 담는 단순한 묶음이다. 서비스 로케이터처럼 아무 데서나 꺼내 쓰지 않고, 뷰와 팩토리가 `Init(GameContext ctx)` 시점에 **필요한 것만 꺼내 필드로 보관**한다.
- 런타임에 생성되는 오브젝트(손님, 빵 뷰, 배송 박스)는 팩토리가 생성 직후 `Init`을 호출해 주입한다.
- 초기화 순서가 코드 한 곳에 순서대로 적혀 있어서, `Awake`/`Start` 실행 순서에 따른 null 참조 문제가 생기지 않는다.

이 결정의 이유는 [ADR 0003](../decisions/0003-composition-root.md)에 있다.

## 모델과 뷰의 분리

| 계층 | 형태 | 예 | 테스트 |
| --- | --- | --- | --- |
| 모델 | 순수 C# 클래스 | `OvenModel`, `ShelfModel`, `StorageInventory`, `CustomerBrain`, `CheckoutSession` | EditMode 단위 테스트 |
| 뷰 | MonoBehaviour | `OvenView`, `ShelfView`, `CustomerController`, `CheckoutCounterView` | 플레이 테스트 |
| 엔진 경계 | 인터페이스 | `ICustomerBody` (NavMeshAgent 감싸기) | 가짜 구현으로 대체 |

규칙:

1. **진실은 모델에 있다.** 선반에 빵이 몇 개 있는지는 `ShelfModel`이 알고, `ShelfView`는 그 숫자만큼 빵 오브젝트를 보여줄 뿐이다. 세이브는 모델만 저장한다.
2. **모델은 `UnityEngine.Object`를 참조하지 않는다.** 엔진 기능이 필요하면 인터페이스로 받는다. 손님 FSM은 `NavMeshAgent` 대신 `ICustomerBody.MoveTo()`를 호출하므로, 테스트에서는 즉시 도착하는 가짜 몸체로 돌릴 수 있다.
3. **ScriptableObject 정의는 읽기 전용이다.** 플레이 중에 SO 필드를 바꾸지 않는다 (에디터에서 플레이 모드를 끝내도 값이 남는 문제 방지).

자세한 이유는 [ADR 0004](../decisions/0004-model-view-split.md)에 있다.

## 시스템 목록

| 시스템 | 책임 | 주요 의존 |
| --- | --- | --- |
| `TimeService` | 시뮬레이션 시간, 영업 시계, 일시정지 | - |
| `DayCycle` | 하루 단계 전환 (준비 → 영업 → 마감 → 정산) | TimeService, EventBus |
| `Wallet` | 보유 금액 (원 단위 정수) | EventBus |
| `StorageInventory` | 창고 재료 수량 | EventBus |
| `PricingService` | 레시피별 판매가, 시세 대비 구매 확률 | ContentDatabase |
| `OvenSystem` | 오븐 모델들, 굽기 진행, 품질 판정 | TimeService, Storage, RecipeBook |
| `ShelfSystem` | 선반 모델들, 진열·집기 | EventBus |
| `OrderService` | 발주, 배송 대기열 | Wallet, TimeService |
| `LootService` | 미스터리 박스 개봉, 천장 | Rng, ContentDatabase |
| `RecipeBook` | 재료 조합 판정, 발견 기록 | ContentDatabase |
| `CustomerSystem` | 손님 생성 주기, 손님 두뇌(FSM) 갱신 | Shelves, Pricing, Checkout, Rng |
| `CheckoutService` | 계산 대기열, 결제 세션 | Wallet |
| `EconomyLedger` | 하루 매출·비용·폐기 집계, 일일 리포트 | EventBus |
| `SaveService` | 저장·불러오기 | 모든 모델의 스냅샷 |

## 갱신 순서 (Tick)

시스템마다 `Update()`를 따로 두지 않고, 부트스트랩이 정해진 순서로 한 번씩 호출한다.

```
GameBootstrap.Update()
  1. TimeService.Advance(Time.deltaTime)   → 이번 프레임의 simDelta 계산 (일시정지면 0)
  2. DayCycle.Tick()                         → 영업 종료 시각 확인, 단계 전환
  3. OrderService.Tick(simDelta)             → 배송 도착 처리
  4. OvenSystem.Tick(simDelta)               → 굽기 진행
  5. CustomerSystem.Tick(simDelta)           → 손님 생성, 각 손님 FSM 갱신
```

순서가 코드에 고정되어 있어 "오븐이 먼저 도는지 시간이 먼저 도는지"에 따라 결과가 바뀌는 문제가 없다. 뷰(MonoBehaviour)의 `Update`는 화면 표시만 한다.

## 이벤트 버스

타입별 구조체 이벤트를 쓰는 인스턴스 기반 버스다. 부트스트랩이 하나 만들어 시스템과 뷰에 전달한다.

```csharp
public sealed class EventBus
{
    public void Subscribe<T>(Action<T> handler) where T : struct;
    public void Unsubscribe<T>(Action<T> handler) where T : struct;
    public void Publish<T>(in T evt) where T : struct;
}
```

사용 원칙:

- **이산적인 변화는 이벤트(push), 연속적인 값은 직접 읽기(pull).** 판매 완료·돈 변화·단계 전환은 이벤트로 알리고, 오븐 진행 바나 시계처럼 매 프레임 바뀌는 값은 뷰가 모델을 읽는다. 매 프레임 이벤트를 쏘지 않는다.
- 뷰는 `OnEnable`에서 구독, `OnDisable`에서 해제한다. 해제를 빠뜨리면 풀에 돌아간 오브젝트가 계속 이벤트를 받는다.
- 개발 빌드에서는 발행되는 모든 이벤트를 로그로 남길 수 있는 옵션을 둔다. 트러블슈팅 때 "무슨 순서로 무엇이 일어났는지"를 확인하는 도구다.

### 이벤트 목록

| 이벤트 | 내용 | 발행 |
| --- | --- | --- |
| `DayPhaseChanged` | 이전·다음 단계, 날짜 | DayCycle |
| `MoneyChanged` | 잔액, 변화량, 사유(판매·발주·환불 등) | Wallet |
| `StorageChanged` | 재료 ID, 변화량 | StorageInventory |
| `OrderPlaced` / `OrderDelivered` | 주문 ID, 상품 ID | OrderService |
| `BoxOpened` | 박스 ID, 나온 재료 목록, 최고 희귀도 | LootService |
| `RecipeDiscovered` | 레시피 ID | RecipeBook |
| `BakingStarted` / `BakingCollected` | 오븐 ID, 레시피 ID, 개수, 품질 | OvenSystem |
| `ShelfChanged` | 선반 ID, 칸 번호 | ShelfSystem |
| `PriceChanged` | 레시피 ID, 가격 | PricingService |
| `CustomerEntered` / `CustomerLeft` | 손님 ID, 떠난 이유(구매 완료·원하는 빵 없음·너무 비쌈·대기 초과) | CustomerSystem |
| `DemandMissed` | 레시피 ID (손님이 찾았지만 선반에 없던 빵) | CustomerSystem |
| `SaleCompleted` | 손님 ID, 품목, 합계, 받은 돈, 거스름돈, 실수 여부 | CheckoutService |
| `BreadDiscarded` | 레시피 ID, 개수, 손실액 | ShelfSystem, OvenSystem |
| `DaySettled` | 일일 리포트 | EconomyLedger |

### 예: 빵 하나가 팔릴 때

```mermaid
sequenceDiagram
    participant P as 플레이어
    participant CV as CheckoutCounterView
    participant CS as CheckoutService
    participant W as Wallet
    participant Bus as EventBus
    participant L as EconomyLedger
    participant H as HUD
    participant FX as SoundFX

    P->>CV: 거스름돈 건네기 확정
    CV->>CS: ConfirmChange(amount)
    CS->>W: Add(total)
    W->>Bus: MoneyChanged
    CS->>Bus: SaleCompleted
    Bus-->>L: 매출 집계
    Bus-->>H: 잔액 갱신
    Bus-->>FX: 계산 완료음
```

`CheckoutService`는 HUD나 사운드가 있는지 모른다. 판매 연출을 추가해도 결제 코드는 바뀌지 않는다.

## 의존성 규칙

```mermaid
flowchart LR
    Views["뷰 / UI<br/>(MonoBehaviour)"] -->|명령 호출| Systems["시스템·모델<br/>(순수 C#)"]
    Systems -->|발행| Bus["EventBus"]
    Bus -->|구독| Views
    Systems -->|읽기| Content["ContentDatabase<br/>(ScriptableObject)"]
    Systems -->|인터페이스| Engine["ICustomerBody 등<br/>엔진 경계"]
```

- 화살표 반대 방향 참조는 금지한다. 시스템이 뷰를 직접 부르지 않는다.
- 시스템끼리는 생성자에서 받은 참조로 직접 호출해도 된다(예: `OvenSystem`이 `StorageInventory.TryConsume` 호출). 다만 결과를 여러 곳에 알려야 하면 이벤트를 쓴다.

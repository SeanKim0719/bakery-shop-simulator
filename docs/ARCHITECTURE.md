# 아키텍처

데이터·시스템·표현을 분리하고, 그 사이를 이벤트 버스로 잇는다. 시스템은 데이터를 읽기만 하고, 화면은 이벤트를 구독해서만 반응한다.

## 계층 구조

```mermaid
flowchart TB
    subgraph Data["데이터 (ScriptableObject)"]
        RecipeData["RecipeData<br/>굽는 시간·시세·유통기한"]
        ProductData["ProductData<br/>발주 단가·배송 시간"]
        CustomerProfile["CustomerProfile<br/>선호·예산·인내심"]
        FurnitureData["FurnitureData<br/>가격·크기·슬롯 수"]
        LootTable["LootTable<br/>박스 등급별 가중치"]
    end

    subgraph Systems["게임 시스템 (순수 C# 중심)"]
        TimeManager["TimeManager<br/>게임 시간·하루 이벤트"]
        OrderService["OrderService<br/>발주·배송 큐"]
        ProductionSystem["ProductionSystem<br/>오븐 슬롯·품질 판정"]
        Inventory["Inventory<br/>재고·신선도"]
        PriceService["PriceService<br/>시세·구매 확률"]
        CustomerAI["CustomerAI<br/>NavMesh·FSM·풀링"]
        CheckoutCounter["CheckoutCounter<br/>스캔·결제 흐름"]
        LootService["LootService<br/>가중치 랜덤·천장"]
        RecipeBook["RecipeBook<br/>조합 판정·도감"]
        EconomyManager["EconomyManager<br/>매출·비용 집계"]
        SaveSystem["SaveSystem<br/>배치·재고 JSON"]
    end

    Bus["GameEvents (이벤트 버스)<br/>OnTick · OnOrderArrived · OnBoxOpened · OnBreadSold · OnCustomerLeft · OnDayEnd"]

    subgraph Presentation["표현·입력 (MonoBehaviour·UI)"]
        PlayerController["PlayerController<br/>이동·시점·집기"]
        HUD["HUD<br/>돈·시간·상호작용 안내"]
        PCApp["PC 앱 UI<br/>발주·리포트·도감"]
        FX["사운드·이펙트<br/>개봉·판매·굽기 피드백"]
    end

    Data -->|읽기 전용 참조| Systems
    Systems <-->|발행·구독| Bus
    Bus -->|구독| Presentation
    Presentation -->|명령| Systems
```

## 설계 원칙

- **데이터 드리븐**: 밸런스 수치와 콘텐츠는 ScriptableObject 에셋에 둔다. 새 빵·상품·손님·가구는 에셋 추가만으로 들어간다.
- **순수 C# 우선**: Inventory, PriceService, LootService, 거스름돈 계산처럼 규칙이 핵심인 코드는 MonoBehaviour에 의존하지 않게 만들어 단위 테스트한다.
- **이벤트로 분리**: UI는 버튼 입력만 시스템에 명령으로 전달하고, 화면 갱신은 이벤트 구독으로만 한다. 예를 들어 빵 하나가 팔리면 `OnBreadSold` 하나에 EconomyManager, HUD, 사운드가 각자 반응한다.
- **시간은 한 곳에서**: 오븐 타이머·신선도·손님 인내심은 모두 TimeManager의 게임 시간을 기준으로 계산한다. 스크립트마다 `Time.deltaTime`을 직접 쓰지 않는다.
- **재현 가능한 랜덤**: 박스 개봉, 손님 생성 등 모든 난수는 시드 고정 RNG를 거친다.

## 주요 이벤트

| 이벤트 | 발행 | 주요 구독자 |
| --- | --- | --- |
| `OnTick` | TimeManager | ProductionSystem, Inventory(신선도), CustomerAI |
| `OnDayStart` / `OnDayEnd` | TimeManager | EconomyManager, SaveSystem, HUD |
| `OnOrderArrived` | OrderService | 배송 박스 스폰, HUD 알림 |
| `OnBoxOpened` | LootService | Inventory, 개봉 연출(FX) |
| `OnRecipeDiscovered` | RecipeBook | 도감 UI, FX |
| `OnBreadSold` | CheckoutCounter | EconomyManager, HUD, FX |
| `OnCustomerLeft` | CustomerAI | EconomyManager(평판), HUD |

## 폴더 구성

```
Assets/
  Scripts/
    Data/        ScriptableObject 정의 (Recipe, Product, Customer, Furniture, LootTable)
    Core/        TimeManager, GameEvents, SaveSystem, Rng
    Player/      PlayerController, IInteractable, HeldItem
    Systems/     Order, Production, Inventory, Price, Customer, Checkout, Loot, RecipeBook, Economy
    UI/          HUD, PC 앱 화면
  Data/          ScriptableObject 에셋 인스턴스
  Tests/
    EditMode/    Inventory, PriceService, LootService 분포, 거스름돈 계산 등 순수 C# 테스트
```

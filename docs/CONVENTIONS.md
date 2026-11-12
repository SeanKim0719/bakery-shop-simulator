# 코드 작성 규칙

[구현 계획](plan/README.md)의 모든 작업이 따르는 규칙이다. 설계 문서가 "무엇을 만드는가"라면 이 문서는 "어떤 모양으로 쓰는가"다. 규칙마다 이유를 함께 적는다.

## 폴더와 네임스페이스

폴더 하나가 네임스페이스 하나다. 파일 하나에는 public 타입 하나만 둔다 (작은 enum과 이벤트 구조체는 예외).

```
Assets/_Project/
  Data/
    content.json            모든 수치의 원본
    shop_layout.json        매장 배치의 원본
    Generated/              임포터가 만든 ScriptableObject (손으로 수치를 고치지 않는다)
  Scripts/                  BakeryShop.Runtime 어셈블리
    Core/                   BakeryShop.Core         GameBootstrap, GameContext, EventBus, Rng, PiecewiseCurve, TimeService, DayCycle
    Events/                 BakeryShop.Events       이벤트 구조체 (DayEvents.cs, EconomyEvents.cs …)
    Data/                   BakeryShop.Data         ScriptableObject 정의, ContentDatabase, enum, 작은 값 타입
    Interaction/            BakeryShop.Interaction  IInteractable, IHoldable, IHands
    Player/                 BakeryShop.Player       입력, 이동, Interactor, Hands
    Systems/
      Economy/              BakeryShop.Economy      Wallet, PricingService, PriceMath, ReputationService, ProgressionService, EconomyLedger
      Production/           BakeryShop.Production   StorageInventory, OrderService, BoxService, OvenSystem, BakeMath, ShelfSystem, TrayModel
      Customers/            BakeryShop.Customers    CustomerPlanner, PurchaseDecision, CustomerBrain, CustomerSpawner, CustomerSystem
      Checkout/             BakeryShop.Checkout     CheckoutQueue, CheckoutSession, CashMath, CheckoutService
      Growth/               BakeryShop.Growth       UnlockService, LootService, RecipeBook, ObjectiveTracker
      Save/                 BakeryShop.Save         SaveData, SaveService, ISaveStorage, ISaveMigration
    World/                  BakeryShop.World        씬에 놓이는 뷰: OvenView, ShelfView, StorageZone, DeliveryBox, CustomerController …
    UI/                     BakeryShop.UI           PanelHost, HUD, 패널
  Editor/                   BakeryShop.Editor       ContentImporter, ShopSceneBuilder, ShopUiBuilder
  Tests/EditMode/           BakeryShop.Tests        폴더 구조는 Scripts를 따라간다 (Tests/EditMode/Economy/WalletTests.cs)
```

- `Systems/` 아래에는 **순수 C#만** 둔다. `MonoBehaviour`가 필요하면 `World/`나 `UI/`에 둔다. 폴더만 봐도 테스트할 수 있는 코드와 아닌 코드가 나뉜다.
- 기존 `Assets/_Project/Tests/EditMode/`의 테스트는 M2의 첫 작업에서 하위 폴더로 옮긴다.

## 이름

| 대상 | 규칙 | 예 |
| --- | --- | --- |
| 클래스, 메서드, 프로퍼티 | PascalCase | `OvenSystem`, `TryStartBaking` |
| private 필드 | `_camelCase` | `_freeTrays` |
| 지역 변수, 매개변수 | camelCase | `recipeId` |
| 인터페이스 | `I` 접두사 | `ICustomerBody` |
| 이벤트 구조체 | 과거형 또는 일어난 일 | `SaleCompleted`, `OvenReady` |
| 실패할 수 있는 명령 | `Try` 접두사, `bool` 또는 결과 enum 반환 | `TrySpend`, `TryOpenShop` |
| 순수 계산 모음 | `~Math` static 클래스 | `PriceMath`, `BakeMath`, `CashMath` |
| 콘텐츠 정의 | `~Data`, `~Profile`, `~Table` | `RecipeData`, `CustomerProfile` |
| 뷰 | `~View`, `~Controller`, `~Panel` | `OvenView`, `OvenPanel` |

- 줄임말을 만들지 않는다 (`cust`, `mgr`, `ctx` 대신 `customer`, `context`). `Id`, `Xp`, `Ui`, `Rng`만 예외다.
- 금액은 `int` 원 단위, 시간은 `float` 초(누적 시각은 `double`), 확률은 0~1의 `float`다. 변수 이름에 단위가 필요하면 붙인다 (`bakeSeconds`, `openMinutes`).

## 모델 (순수 C#)

```csharp
namespace BakeryShop.Economy
{
    /// <summary>잔액. 지출이 잔액을 넘으면 실패한다 (임대료만 예외).</summary>
    public sealed class Wallet
    {
        private readonly EventBus _bus;

        public Wallet(EventBus bus, int startMoney) { … }

        public int Balance { get; private set; }

        public bool TrySpend(int amount, MoneyReason reason)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Balance) return false;

            Balance -= amount;
            _bus.Publish(new MoneyChanged(Balance, -amount, reason));
            return true;
        }
    }
}
```

1. **의존은 생성자로 받는다.** static 필드, 싱글톤, `FindObjectOfType`을 쓰지 않는다 ([ADR 0003](decisions/0003-composition-root.md)).
2. **`UnityEngine.Object`를 참조하지 않는다.** `Vector3`, `Mathf` 같은 값 타입과 수학 함수는 써도 된다. 콘텐츠 정의(ScriptableObject)는 읽기 전용으로만 참조한다.
3. **시간은 받아서 쓴다.** `Time.deltaTime`, `Time.time`, `DateTime.Now`를 쓰지 않고 `TimeService`나 `simDelta` 매개변수를 쓴다.
4. **난수는 `Rng`를 받아서 쓴다.** `UnityEngine.Random`, `System.Random`을 쓰지 않는다. 손님과 박스는 서로 다른 `Rng`를 받는다.
5. **상태가 바뀌면 이벤트를 발행한다.** 상태를 먼저 바꾸고 나서 발행한다. 구독자가 모델을 읽었을 때 이미 새 값이어야 한다.
6. **플레이어가 일으킬 수 있는 실패는 반환값으로, 코드의 잘못은 예외로 알린다.** 돈이 모자란 것은 `false`, 음수 금액을 넘긴 것은 예외다.
7. **`Restore~` 메서드는 이벤트를 발행하지 않는다.** 세이브 복원 중에 경험치 알림이나 효과음이 나오지 않게 한다.

## 뷰 (MonoBehaviour)

```csharp
namespace BakeryShop.World
{
    public sealed class OvenView : MonoBehaviour, IContextConsumer, IInteractable
    {
        [SerializeField] private string _ovenId = "oven.01";

        private OvenSystem _ovens;
        private EventBus _bus;

        public void Init(GameContext context)
        {
            _ovens = context.Ovens;
            _bus = context.Bus;
            _bus.Subscribe<OvenReady>(OnOvenReady);
        }

        private void OnDestroy() => _bus?.Unsubscribe<OvenReady>(OnOvenReady);

        private void Update()
        {
            // 연속 값은 모델을 읽어서 표시만 한다.
            _progressBar.Set(_ovens.Progress(_ovenId));
        }
    }
}
```

1. **뷰는 규칙을 갖지 않는다.** "구울 수 있는가"는 `OvenSystem`에 묻고, 뷰는 결과를 보여주고 명령을 전달한다. 뷰에 `if (재료가 충분하면)` 같은 판단이 생기면 모델로 옮긴다.
2. **의존은 `Init(GameContext)`에서 받는다.** `Awake`에서는 자기 컴포넌트만 찾고, 다른 시스템을 쓰는 코드는 `Init` 이후에만 실행한다. 풀에서 꺼내 쓰는 오브젝트는 팩토리가 `Init`을 부른다.
3. **구독하면 반드시 해제한다.** 씬에 고정된 뷰는 `Init`에서 구독하고 `OnDestroy`에서 해제한다. 풀을 쓰는 뷰는 꺼낼 때 구독하고 돌려놓을 때 해제한다.
4. **`Update`는 화면 표시만 한다.** 게임 상태를 바꾸는 갱신은 `GameBootstrap`이 정한 순서로 시스템의 `Tick`이 한다.
5. **인스펙터 필드는 `[SerializeField] private`**로 두고, `[Tooltip]`에 단위와 뜻을 적는다. 수치는 인스펙터가 아니라 `content.json`에 둔다 (손 위치, 색처럼 보이는 것만 인스펙터).
6. **`GameObject.Find`, 태그 문자열 비교를 쓰지 않는다.** 가구는 고정 ID로, 나머지는 직접 참조로 찾는다.

## 이벤트

```csharp
namespace BakeryShop.Events
{
    public readonly struct MoneyChanged
    {
        public readonly int Balance;
        public readonly int Delta;
        public readonly MoneyReason Reason;

        public MoneyChanged(int balance, int delta, MoneyReason reason) { … }
    }
}
```

- `readonly struct`이고 필드는 값이나 문자열 ID다. 모델 객체의 참조를 담지 않는다 (구독자가 모델을 바꾸는 길을 막는다). 목록이 필요하면 `IReadOnlyList<T>`로 담는다.
- 이산적인 변화만 이벤트로 알린다. 매 프레임 바뀌는 값은 뷰가 모델을 읽는다.
- 이벤트를 추가하면 [런타임 구조 문서의 이벤트 목록](design/01-runtime-structure.md#이벤트-목록)에도 추가한다.

## 상태 기계

손님, 결제 세션처럼 상태가 셋 이상이고 상태마다 하는 일이 다르면 상태 클래스로 나눈다.

```csharp
public interface IState<TContext>
{
    void Enter(TContext context);
    IState<TContext> Tick(TContext context, float simDelta);   // 다음 상태를 돌려준다. 그대로면 this
    void Exit(TContext context);
}
```

- 전환은 `Tick`의 반환값으로만 일어난다. 상태가 바깥의 `ChangeState`를 부르지 않는다.
- 상태 클래스는 필드를 갖지 않고 필요한 값은 컨텍스트에 둔다. 그래서 상태 객체 하나를 모든 손님이 함께 쓴다 (손님마다 새로 만들지 않는다).
- 하루 단계(`DayCycle`)처럼 전환이 명령으로만 일어나는 경우는 enum과 `Try~` 메서드로 충분하다.

## 테스트

- 파일: `대상Tests.cs`, 메서드: `상황_기대결과` (`Blocked_PullsInImmediately`, `NotEnoughMoney_ReturnsFalseAndKeepsBalance`).
- 테스트 하나는 한 가지만 확인한다. 준비-실행-확인 세 부분이 눈에 보이게 쓴다.
- 콘텐츠가 필요한 테스트는 `TestContent`가 만드는 작은 고정 데이터를 쓴다 (재료 4종, 레시피 3종). 실제 `content.json`을 쓰는 테스트는 콘텐츠 검증 테스트뿐이다. 밸런스 수치를 바꿔도 규칙 테스트가 깨지지 않게 하기 위해서다.
- 난수를 쓰는 테스트는 시드를 고정한다. 분포를 확인하는 테스트는 횟수와 허용 오차를 상수로 적는다.
- 버그를 고칠 때는 **먼저 그 버그를 재현하는 테스트를 추가**하고, 고친 뒤 통과하는 것을 확인한다. 테스트 이름이나 주석에 이슈 번호를 적는다.
- 뷰와 물리는 EditMode 테스트로 확인하지 않는다. 작업 명세의 "Unity에서 확인"을 사람이 직접 본다.

## 주석과 문서

- public 타입에는 `/// <summary>`로 **무엇을 책임지는지** 한두 문장을 쓴다.
- 코드 안의 주석은 **왜 이렇게 했는지**만 쓴다. 코드를 다시 설명하는 주석은 쓰지 않는다.
- 설계와 다르게 구현하게 되면 코드만 바꾸지 않고 설계 문서를 같은 PR에서 고친다. 결정이 바뀌는 수준이면 [설계 결정 기록](decisions/README.md)을 추가한다.

## 수치와 콘텐츠

- 게임 규칙에 쓰이는 숫자는 코드에 적지 않고 `content.json`에 둔다. 코드에는 `GameConfig`에서 읽은 값만 쓴다.
- 수치를 바꿀 때의 순서: `content.json` 수정 → Unity에서 **BakeryShop → 콘텐츠 가져오기** → `python3 Tools/balance/simulate.py --out Tools/balance/latest_report.md` → `python3 Tools/docs/content_tables.py` → 영향받는 설계 문서의 표 수정 → 한 커밋으로 올린다.
- 콘텐츠 ID는 한번 정하면 바꾸지 않는다. 세이브가 ID로 참조하기 때문이다.

## 작업 흐름

| 단계 | 누가 | 하는 일 |
| --- | --- | --- |
| 1 | Claude | `feature/M3-06-oven-system`처럼 작업 ID가 들어간 브랜치에서 코드와 EditMode 테스트를 작성하고 PR을 연다. PR 본문에 "Unity에서 확인할 것"을 체크리스트로 적는다 |
| 2 | 사용자 | 브랜치를 받아 Unity를 열고, Test Runner의 EditMode 테스트를 전부 실행한다 |
| 3 | 사용자 | 체크리스트를 직접 플레이하며 확인한다. 어색하거나 틀린 것은 트러블슈팅 템플릿으로 이슈를 남긴다 |
| 4 | 함께 | 이슈의 가설을 세우고 검증한다. 수정 커밋에 이슈 번호를 붙인다 |
| 5 | 사용자 | 체크리스트가 모두 통과하면 PR을 병합한다 |

- 커밋 메시지는 `타입: 내용 (#이슈번호)` 형식이다 ([개발 환경 셋업](SETUP.md#커밋-메시지)).
- PR 하나는 작업 하나다. 리뷰하고 확인하는 데 30분이 넘게 걸릴 크기면 작업을 나눈다.
- 씬 파일(`.unity`)과 프리팹은 Claude가 직접 고치지 않는다. 에디터 빌더 코드(`ShopSceneBuilder`, `ShopUiBuilder`)를 고치고, 사용자가 메뉴를 실행해 씬을 다시 만든다. 씬 파일의 병합 충돌을 피하고, 씬의 구성이 코드로 남는다.
- 마일스톤을 마치면 `v0.N-이름` 태그를 남긴다.

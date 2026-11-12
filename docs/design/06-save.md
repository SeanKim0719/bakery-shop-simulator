# 06. 세이브

**저장은 하루와 하루 사이에만 한다** ([ADR 0005](../decisions/0005-save-between-days.md)). 정산 단계에 들어가는 순간 하루 마무리 처리가 끝난 상태를 자동 저장하고, 불러오면 항상 다음 날 준비 단계에서 시작한다.

## 저장 시점에 비어 있는 것들

하루 마무리 처리([02 문서](02-day-cycle-and-time.md#하루-마무리-처리)) 덕분에 저장 시점에는 아래가 항상 비어 있다. 저장할 필요가 없으니 세이브 구조가 단순해진다.

| 대상 | 정산 진입 시 처리 | 세이브 |
| --- | --- | --- |
| 손님, 계산 줄, 결제 세션 | 마감 단계에서 모두 나감 | 저장 안 함 |
| 오븐 | 남은 빵 폐기 | 저장 안 함 |
| 배송 중인 주문 | 문 앞 박스로 도착 처리 | 저장 안 함 (박스는 월드 아이템으로 저장) |
| 손에 든 물건 | 발밑에 내려놓음 | 월드 아이템으로 저장 |
| 영업 시계 | 다음 날 09:00 | 저장 안 함 |

## 저장하는 것

```json
{
  "version": 1,
  "day": 3,
  "money": 152300,
  "reputation": 536,
  "xp": 131,
  "customerRngState": 2463534242,
  "lootRngState": 88172645,
  "nextEntityId": 1043,
  "unlocks": ["unlock.recipe.saltbutter", "unlock.tray.4"],
  "prices": [{ "recipeId": "bread.croissant", "price": 3300 }],
  "storage": [{ "ingredientId": "ing.butter", "count": 12 }],
  "knownRecipes": ["bread.white", "bread.redbean", "bread.croissant", "bread.saltbutter"],
  "foundKeyIngredients": ["ing.almond"],
  "experiments": [
    { "keyIngredientId": "ing.almond", "tried": ["ing.flour", "ing.milk"], "matched": 1 }
  ],
  "pity": [{ "lootTableId": "loot.basic", "count": 2 }],
  "soldCounts": [{ "recipeId": "bread.croissant", "count": 31 }],
  "shelves": [
    {
      "id": "shelf.01",
      "slots": [
        { "recipeId": "bread.croissant", "breads": [{ "quality": 2, "daysOld": 1 }] },
        { "recipeId": "", "breads": [] },
        { "recipeId": "", "breads": [] }
      ]
    }
  ],
  "worldItems": [
    { "entityId": 1040, "kind": "Box", "productId": "product.box.basic",
      "position": { "x": -4.2, "y": 0.25, "z": -7.3 }, "rotation": { "x": 0, "y": 0, "z": 0, "w": 1 } },
    { "entityId": 1041, "kind": "Tray", "tray": { "recipeId": "bread.white", "breads": [{ "quality": 2, "daysOld": 1 }] },
      "position": { "x": -2.6, "y": 0.95, "z": 5.5 }, "rotation": { "x": 0, "y": 0, "z": 0, "w": 1 } }
  ],
  "objectivesDone": 6,
  "demoGoalShown": false,
  "settings": { "showChange": true },
  "history": [
    { "day": 2, "revenue": 77200, "orderSpend": 21800, "rent": 10000, "net": 45400, "customersPaid": 16, "wasteCount": 1, "missedDemand": 2 }
  ]
}
```

| 항목 | 비고 |
| --- | --- |
| `xp` | 레벨은 저장하지 않고 경험치에서 계산한다 |
| `unlocks` | 구매한 해금 ID. 오븐·진열대의 잠금 상태와 전체 트레이 수는 여기서 계산한다 |
| `knownRecipes` | 라이선스를 샀거나 발견한 레시피 |
| `foundKeyIngredients`, `experiments` | 도감의 힌트 상태와 실험 기록 |
| `pity` | 확률표별 연속 꽝 횟수 |
| `customerRngState`, `lootRngState` | 난수 스트림 두 개의 상태 |
| `worldItems` | 바닥·작업대에 놓인 박스와 트레이. 빈 트레이 수 = 전체 트레이 − 여기 있는 트레이 수 |
| `objectivesDone` | 목표 리스트에서 완료한 개수 |
| `soldCounts` | 빵별 누적 판매 개수. 도감에 보인다 |
| `history` | 최근 7일의 일일 리포트. 예시는 줄였고, 실제로는 빵별 표까지 `DailyReport` 전체를 저장한다. PC의 리포트 탭에서 다시 본다 |

- 세이브 DTO(`SaveData`)는 런타임 모델과 별개의 단순 클래스다. 모델이 바뀌어도 세이브 형식은 버전을 올려서만 바꾼다.
- 마우스 감도와 소리 크기는 세이브가 아니라 `PlayerPrefs`에 저장한다 (세이브 슬롯과 무관한 기기 설정).

## 형식과 파일

- `JsonUtility`를 쓴다. 딕셔너리와 다형성을 지원하지 않으므로 DTO는 리스트와 단순 필드로만 만든다. 월드 아이템도 상속 대신 `kind` 필드 하나로 구분한다.
- 경로: `Application.persistentDataPath/save_{슬롯}.json`
- **임시 파일에 쓴 뒤 교체**한다. `save_1.json.tmp`에 다 쓰고 나서 기존 파일을 `.bak`으로 옮기고 임시 파일 이름을 바꾼다. 쓰는 도중 게임이 꺼져도 이전 세이브가 남는다.

## 불러오기 순서

```mermaid
flowchart TB
    Read["1. 파일 읽기, JSON 파싱"] --> Migrate["2. 버전 확인 → 필요하면 단계별 변환<br/>(v1 → v2 → … 현재)"]
    Migrate --> Models["3. 모델 복원 (순수 C#)<br/>지갑·평판·경험치·해금·창고·가격·도감·천장·난수·ID 발급기·진열대"]
    Models --> World["4. 월드 아이템 생성<br/>팩토리가 박스·트레이 프리팹 생성, entityId 부여"]
    World --> Views["5. 뷰 초기화와 동기화<br/>ShelfView가 모델을 읽어 빵 오브젝트 배치"]
```

- **모델을 먼저, 오브젝트를 나중에.** 오브젝트가 서로를 참조하는 상태를 직렬화하지 않고, ID만 저장해 두었다가 모델 복원 후 연결한다.
- 진열대는 씬에 배치된 고정 ID(`shelf.01` 등)로 찾는다. 세이브에 있는데 씬에 없는 ID는 경고 로그를 남기고 건너뛴다.
- 해금은 모델 복원에서 가장 먼저 적용한다. 잠긴 진열대에 빵을 채우는 순서 문제를 막기 위해서다.
- 콘텐츠에서 사라진 레시피·재료 ID도 경고 로그 후 건너뛴다. 개발 중 콘텐츠를 지우거나 이름을 바꿨을 때 세이브가 통째로 깨지지 않게 한다.

## 버전 변환

```csharp
public interface ISaveMigration
{
    int FromVersion { get; }
    void Apply(SaveData data);   // FromVersion → FromVersion + 1
}
```

세이브 구조를 바꿀 때마다 변환기를 하나씩 추가하고, 불러올 때 현재 버전까지 순서대로 적용한다.

## 테스트

| 테스트 | 확인하는 것 |
| --- | --- |
| 왕복 테스트 | 상태 생성 → 저장 → 불러오기 → 모든 모델이 같은지 |
| 데모 세이브 테스트 | 저장소에 넣어 둔 데모 세이브 파일이 현재 버전으로 불러와지는지 |
| 버전 변환 테스트 | v1 예시 JSON을 넣으면 현재 버전 형식으로 올라오는지 |
| 손상 대비 테스트 | 알 수 없는 ID가 섞인 세이브를 불러와도 나머지가 복원되는지 |

파일 대신 문자열로 저장·불러오기 할 수 있게 `ISaveStorage` 인터페이스를 두어, 테스트에서는 디스크를 건드리지 않는다.

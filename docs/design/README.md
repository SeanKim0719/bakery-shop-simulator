# 게임 구조 설계

기획서([GDD](../GDD.md))의 기능을 실제 코드 구조로 옮기기 위한 설계 문서다. 위에서부터 순서대로 읽으면 된다.

| 문서 | 내용 |
| --- | --- |
| [01. 런타임 구조](01-runtime-structure.md) | 씬, 부트스트랩, 시스템 목록, 갱신 순서, 이벤트 버스, 의존성 규칙 |
| [02. 하루 흐름과 시간](02-day-cycle-and-time.md) | 준비·영업·마감·정산 단계, 두 개의 시계, 하루 마무리 처리 |
| [03. 데이터 모델](03-data-model.md) | ScriptableObject 정의, 런타임 상태, 돈과 가격, ID 규칙, MVP 콘텐츠 |
| [04. 플레이어와 상호작용](04-player-interaction.md) | Interactor·Hands·IInteractable, 상호작용 표, 입력 모드 |
| [05. 손님과 계산](05-customer-and-checkout.md) | 손님 FSM, 구매 판단, 계산 줄, 결제 세션, 평판 |
| [06. 세이브](06-save.md) | 저장 시점, 세이브 형식, 불러오기 순서, 버전 변환 |

## 설계를 관통하는 원칙

1. **부트스트랩 하나가 모든 것을 만든다.** 싱글톤과 static 상태 없이, 초기화 순서를 한 곳에 적는다.
2. **규칙은 순수 C# 모델에, 화면은 MonoBehaviour 뷰에.** 진실은 모델에 있고 세이브도 모델만 한다.
3. **이산적인 변화는 이벤트로, 연속적인 값은 직접 읽기로.**
4. **시간은 `TimeService` 하나에서.** 게임 코드에서 `Time.deltaTime`을 직접 쓰지 않는다.
5. **범위를 줄이는 규칙을 먼저 정한다.** 저장은 하루 사이에만, 신선도는 날짜 단위로. 구현과 버그가 생길 자리를 설계 단계에서 줄인다.

## 관련 결정

| ADR | 결정 |
| --- | --- |
| [0003](../decisions/0003-composition-root.md) | 싱글톤 대신 부트스트랩(Composition Root)으로 연결 |
| [0004](../decisions/0004-model-view-split.md) | 순수 C# 모델과 MonoBehaviour 뷰 분리 |
| [0005](../decisions/0005-save-between-days.md) | 저장은 하루와 하루 사이에만 |
| [0006](../decisions/0006-day-based-freshness.md) | 신선도를 실시간 대신 날짜 단위로 |

## 구현 순서와의 연결

| 로드맵 | 먼저 만들 것 |
| --- | --- |
| 1~2주차 | 04 문서 전체 (`PlayerMotor`, `Interactor`, `Hands`, `IInteractable`), 01의 `GameBootstrap` 뼈대 |
| 3~4주차 | `TimeService`, `DayCycle`, `EventBus`, 오븐·선반·창고 모델, 발주, 단순 손님, 결제 세션 |
| 5~6주차 | 미스터리 박스·도감, 손님 FSM 완성과 평판, 구매 판단 곡선, 세이브 |

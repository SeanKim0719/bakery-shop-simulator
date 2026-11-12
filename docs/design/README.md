# 게임 구조 설계

기획서([GDD](../GDD.md))의 기능을 실제 코드 구조로 옮기기 위한 설계 문서다. 01~06은 구조, 08~12는 규칙과 수치다. 이 설계를 작업 단위로 나눈 것이 [구현 계획](../plan/README.md)이다.

| 문서 | 내용 |
| --- | --- |
| [01. 런타임 구조](01-runtime-structure.md) | 씬, 부트스트랩, 시스템 목록, 갱신 순서, 이벤트 버스, 의존성 규칙 |
| [02. 하루 흐름과 시간](02-day-cycle-and-time.md) | 준비·영업·마감·정산 단계, 두 개의 시계, 하루 마무리 처리 |
| [03. 데이터 모델](03-data-model.md) | `content.json` → ScriptableObject 흐름, 정의의 필드, 런타임 상태, ID 규칙 |
| [04. 플레이어와 상호작용](04-player-interaction.md) | Interactor·Hands·IInteractable, 상호작용 표, 입력 모드 |
| [05. 손님과 계산](05-customer-and-checkout.md) | 손님 FSM, 구매 판단, 계산 줄, 결제 세션, 평판 |
| [06. 세이브](06-save.md) | 저장 시점, 세이브 형식, 불러오기 순서, 버전 변환 |
| [07. 절도·보안·매장 어지럽힘](07-theft-and-disorder.md) | MVP 이후 확장: 절도 손님, 보안요원·설비, 진열 흐트러뜨리기 |
| [08. 성장과 경제](08-progression-and-economy.md) | 레벨과 경험치, 돈의 흐름, 가격과 구매 확률, 평판, 트레이와 빵 냄새, 시뮬레이션 결과 |
| [09. 미스터리 박스와 도감](09-mystery-box-and-recipe-book.md) | 희귀도, 뽑기 규칙과 천장, 개봉 연출, 실험 규칙, 도감 상태 |
| [10. 화면과 UI](10-ui.md) | HUD, 오븐·PC·계산 패널, 정산 리포트, 알림, 목표 리스트 |
| [11. 매장 배치](11-shop-layout.md) | 평면도, 가구와 지점의 좌표, 씬 구성, 길 찾기와 충돌 |
| [12. 콘텐츠 표](12-content-tables.md) | 재료·레시피·상품·확률표·손님·해금의 전체 수치 (`content.json`에서 자동 생성) |

## 설계를 관통하는 원칙

1. **부트스트랩 하나가 모든 것을 만든다.** 싱글톤과 static 상태 없이, 초기화 순서를 한 곳에 적는다.
2. **규칙은 순수 C# 모델에, 화면은 MonoBehaviour 뷰에.** 진실은 모델에 있고 세이브도 모델만 한다.
3. **이산적인 변화는 이벤트로, 연속적인 값은 직접 읽기로.**
4. **시간은 `TimeService` 하나에서.** 게임 코드에서 `Time.deltaTime`을 직접 쓰지 않는다.
5. **범위를 줄이는 규칙을 먼저 정한다.** 저장은 하루 사이에만, 신선도는 날짜 단위로. 구현과 버그가 생길 자리를 설계 단계에서 줄인다.
6. **수치는 `content.json` 한 곳에, 근거는 시뮬레이터로.** 규칙을 구현하기 전에 [밸런스 시뮬레이터](../../Tools/balance/simulate.py)로 돌려 보고, 결과가 의도와 다르면 규칙을 고친다.

## 관련 결정

| ADR | 결정 |
| --- | --- |
| [0003](../decisions/0003-composition-root.md) | 싱글톤 대신 부트스트랩(Composition Root)으로 연결 |
| [0004](../decisions/0004-model-view-split.md) | 순수 C# 모델과 MonoBehaviour 뷰 분리 |
| [0005](../decisions/0005-save-between-days.md) | 저장은 하루와 하루 사이에만 |
| [0006](../decisions/0006-day-based-freshness.md) | 신선도를 실시간 대신 날짜 단위로 |
| [0007](../decisions/0007-theft-security-after-mvp.md) | 절도·보안은 MVP 이후로, 진열장 파손 대신 흐트러뜨리기 |
| [0008](../decisions/0008-progression-and-locked-furniture.md) | 경험치·레벨로 성장하고, 가구는 "잠긴 가구"를 해금 |
| [0009](../decisions/0009-content-json-pipeline.md) | 수치는 `content.json` 한 파일에 두고 ScriptableObject로 가져오기 |
| [0010](../decisions/0010-trays-aroma-dayold-discount.md) | 트레이 수 제한, 빵 냄새, 하루 지난 빵 할인 |
| [0011](../decisions/0011-experiment-feedback.md) | 레시피 실험은 "맞은 개수"를 알려주고 핵심 재료를 소모하지 않음 |

## 구현 순서와의 연결

| 마일스톤 | 주로 참고하는 문서 |
| --- | --- |
| [M1. 조작과 상호작용](../plan/M1-player-interaction.md) | 04 |
| [M2. 코어와 매장 그레이박스](../plan/M2-core-and-greybox.md) | 01, 02, 03, 11 |
| [M3. 생산 루프](../plan/M3-production-loop.md) | 03, 04, 08, 10 |
| [M4. 영업 루프](../plan/M4-shop-loop.md) | 05, 08, 10 |
| [M5. 성장과 수집](../plan/M5-growth-and-collection.md) | 08, 09, 10 |
| [M6. 저장과 마감](../plan/M6-save-and-polish.md) | 06, 10 |

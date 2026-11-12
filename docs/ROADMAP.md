# 로드맵

MVP는 **레벨 1~5, 빵 10종(일반 6, 비밀 4), 오븐 2대, 진열대 4개, 손님 성향 3종, 미스터리 박스 2종, 실험과 도감, 하루 사이 자동 저장**이다. 범위의 근거는 [기획서의 MVP 범위](GDD.md)에 있다.

공기업 준비와 병행하므로 하루 1~2시간 기준으로 잡았다. 마일스톤 여섯 개, 작업 45개, 약 12.5주다. 마일스톤이 끝날 때마다 "이 상태로도 보여줄 수 있는" 빌드를 태그로 남겨, 시험 기간에 개발이 멈춰도 손실이 없게 한다.

작업마다의 파일, 공개 API, 규칙, 테스트, Unity에서 확인할 것은 [구현 계획](plan/README.md)에 있다. 작업은 모두 GitHub 이슈로 등록되어 있고, 마일스톤별 진행률은 [Milestones](https://github.com/SeanKim0719/bakery-shop-simulator/milestones)에서 막대로 볼 수 있다. 이 문서는 전체를 한 장으로 보는 체크리스트다. 이슈를 닫으면 여기도 체크한다.

## 진행 상황

| 마일스톤 | 끝났을 때 되는 것 | 기간 | 태그 | 이슈 | 상태 |
| --- | --- | --- | --- | --- | --- |
| [M1. 조작과 상호작용](plan/M1-player-interaction.md) | 박스를 들고, 쌓고, 내려놓는다 | 0.5주 | `v0.1-movement` | [진행률](https://github.com/SeanKim0719/bakery-shop-simulator/milestone/1) | 코드 병합 완료 ([PR #1](https://github.com/SeanKim0719/bakery-shop-simulator/pull/1)), Unity 확인 중 |
| [M2. 코어와 매장 그레이박스](plan/M2-core-and-greybox.md) | 실제 배치의 매장에서 개점하면 시계가 흐르고 마감된다 | 2주 | `v0.2-core` | [진행률](https://github.com/SeanKim0719/bakery-shop-simulator/milestone/2) | 시작 전 |
| [M3. 생산 루프](plan/M3-production-loop.md) | 발주 → 배송 → 굽기 → 진열 → 가격 | 2.5주 | `v0.3-production` | [진행률](https://github.com/SeanKim0719/bakery-shop-simulator/milestone/3) | 시작 전 |
| [M4. 영업 루프](plan/M4-shop-loop.md) | 손님, 계산, 정산. 하루가 처음부터 끝까지 돈다 | 3주 | `v0.4-day-loop` | [진행률](https://github.com/SeanKim0719/bakery-shop-simulator/milestone/4) | 시작 전 |
| [M5. 성장과 수집](plan/M5-growth-and-collection.md) | 해금, 미스터리 박스, 실험, 도감, 목표 리스트 | 2주 | `v0.5-growth` | [진행률](https://github.com/SeanKim0719/bakery-shop-simulator/milestone/5) | 시작 전 |
| [M6. 저장과 마감](plan/M6-save-and-polish.md) | 세이브, 타이틀, 사운드, 아트, 최적화, 빌드와 영상 | 2.5주 | `v1.0-mvp` | [진행률](https://github.com/SeanKim0719/bakery-shop-simulator/milestone/6) | 시작 전 |

## 이미 끝난 것

- [x] 기획서 v2, 설계 문서 01~12, 설계 결정 기록 0001~0011
- [x] Unity 프로젝트 생성, Git LFS·.gitignore 설정
- [x] 수치 원본 `content.json`과 밸런스 시뮬레이터 ([결과](../Tools/balance/latest_report.md))
- [x] 매장 배치 `shop_layout.json`과 [평면도](images/shop-layout.svg)
- [x] 구현 계획 (작업 45개의 명세), [코드 작성 규칙](CONVENTIONS.md)
- [x] 트러블슈팅 [TS-001](troubleshooting/TS-001-day-old-bread-blocks-shelf.md) (구현 전 시뮬레이션에서 찾은 규칙 문제)

## M1. 조작과 상호작용

- [x] 입력, 이동, 시점 (`PlayerInputReader`, `PlayerMotor`)
- [x] 상호작용 (`Interactor`, `IInteractable`, 안내 HUD)
- [x] 들기, 내려놓기, 쌓기 (`Hands`, `IHoldable`)
- [x] 플레이 테스트에서 나온 버그 2건 수정 (벽 파고듦, 몸에 겹쳐 내려놓기)
- [x] PR #1을 `main`에 병합 (2026-11-12)
- [ ] M1-01 병합한 조작 코드 확인 ([#4](https://github.com/SeanKim0719/bakery-shop-simulator/issues/4))
- [ ] M1-02 트러블슈팅 기록 2건 ([#5](https://github.com/SeanKim0719/bakery-shop-simulator/issues/5). 초안: [#2](https://github.com/SeanKim0719/bakery-shop-simulator/issues/2), [#3](https://github.com/SeanKim0719/bakery-shop-simulator/issues/3))
- [ ] M1-03 태그 `v0.1-movement` ([#6](https://github.com/SeanKim0719/bakery-shop-simulator/issues/6))

## M2. 코어와 매장 그레이박스

- [ ] M2-01 이벤트 버스 ([#7](https://github.com/SeanKim0719/bakery-shop-simulator/issues/7))
- [ ] M2-02 난수와 꺾은선 ([#8](https://github.com/SeanKim0719/bakery-shop-simulator/issues/8))
- [ ] M2-03 시간 ([#9](https://github.com/SeanKim0719/bakery-shop-simulator/issues/9))
- [ ] M2-04 하루 단계 ([#10](https://github.com/SeanKim0719/bakery-shop-simulator/issues/10))
- [ ] M2-05 콘텐츠 정의와 데이터베이스 ([#11](https://github.com/SeanKim0719/bakery-shop-simulator/issues/11))
- [ ] M2-06 `content.json` 임포터 ([#12](https://github.com/SeanKim0719/bakery-shop-simulator/issues/12))
- [ ] M2-07 돈·평판·경험치 상태 ([#13](https://github.com/SeanKim0719/bakery-shop-simulator/issues/13))
- [ ] M2-08 매장 그레이박스 씬 ([#14](https://github.com/SeanKim0719/bakery-shop-simulator/issues/14))
- [ ] M2-09 부트스트랩 확장, 팻말, HUD, 패널 호스트 ([#15](https://github.com/SeanKim0719/bakery-shop-simulator/issues/15))
- 완료 기준: 매장을 걸어 다니고, 팻말을 돌리면 시계가 09:00부터 흐르다가 18:00에 마감된다

## M3. 생산 루프

- [ ] M3-01 창고 ([#16](https://github.com/SeanKim0719/bakery-shop-simulator/issues/16))
- [ ] M3-02 발주와 배송 ([#17](https://github.com/SeanKim0719/bakery-shop-simulator/issues/17))
- [ ] M3-03 박스 열기 ([#18](https://github.com/SeanKim0719/bakery-shop-simulator/issues/18))
- [ ] M3-04 가격 ([#19](https://github.com/SeanKim0719/bakery-shop-simulator/issues/19))
- [ ] M3-05 오븐과 트레이 수 ([#20](https://github.com/SeanKim0719/bakery-shop-simulator/issues/20))
- [ ] M3-06 트레이와 진열대 ([#21](https://github.com/SeanKim0719/bakery-shop-simulator/issues/21))
- [ ] M3-07 오븐 뷰와 굽기 패널 ([#22](https://github.com/SeanKim0719/bakery-shop-simulator/issues/22))
- [ ] M3-08 진열대 뷰와 쓰레기통 ([#23](https://github.com/SeanKim0719/bakery-shop-simulator/issues/23))
- [ ] M3-09 PC 패널: 발주와 가격 ([#24](https://github.com/SeanKim0719/bakery-shop-simulator/issues/24))
- [ ] M3-10 하루 마무리 단계 (생산), HUD, 알림 ([#25](https://github.com/SeanKim0719/bakery-shop-simulator/issues/25))
- 완료 기준: 손님 없이 주문 → 배송 → 굽기 → 진열 → 가격 변경 → 다음 날까지 한 번에 된다

## M4. 영업 루프

- [ ] M4-01 방문 계획과 구매 판단 ([#26](https://github.com/SeanKim0719/bakery-shop-simulator/issues/26))
- [ ] M4-02 손님 두뇌(FSM)와 계산 줄 ([#27](https://github.com/SeanKim0719/bakery-shop-simulator/issues/27))
- [ ] M4-03 손님 생성과 빵 냄새 ([#28](https://github.com/SeanKim0719/bakery-shop-simulator/issues/28))
- [ ] M4-04 손님 몸체, 풀, 매장 지점 ([#29](https://github.com/SeanKim0719/bakery-shop-simulator/issues/29))
- [ ] M4-05 결제 세션과 현금 계산 ([#30](https://github.com/SeanKim0719/bakery-shop-simulator/issues/30))
- [ ] M4-06 계산대와 결제 패널 ([#31](https://github.com/SeanKim0719/bakery-shop-simulator/issues/31))
- [ ] M4-07 평판·경험치 규칙 연결 ([#32](https://github.com/SeanKim0719/bakery-shop-simulator/issues/32))
- [ ] M4-08 정산과 리포트 ([#33](https://github.com/SeanKim0719/bakery-shop-simulator/issues/33))
- [ ] M4-09 하루 통합 테스트와 수치 재확인 ([#34](https://github.com/SeanKim0719/bakery-shop-simulator/issues/34))
- 완료 기준: 1일차부터 3일차까지 이어서 플레이할 수 있다. 시뮬레이터의 가정값을 실제로 잰 값으로 바꿨다

## M5. 성장과 수집

- [ ] M5-01 해금과 잠긴 가구 ([#35](https://github.com/SeanKim0719/bakery-shop-simulator/issues/35))
- [ ] M5-02 미스터리 박스 뽑기 ([#36](https://github.com/SeanKim0719/bakery-shop-simulator/issues/36))
- [ ] M5-03 박스 개봉 연출 ([#37](https://github.com/SeanKim0719/bakery-shop-simulator/issues/37))
- [ ] M5-04 도감과 실험 ([#38](https://github.com/SeanKim0719/bakery-shop-simulator/issues/38))
- [ ] M5-05 실험 탭, 도감 탭, 재료 판매 ([#39](https://github.com/SeanKim0719/bakery-shop-simulator/issues/39))
- [ ] M5-06 목표 리스트, 알림, 데모 목표 ([#40](https://github.com/SeanKim0719/bakery-shop-simulator/issues/40))
- 완료 기준: 새 게임에서 레벨 5와 비밀 레시피 2종까지 갈 수 있고, 처음 하는 사람이 설명 없이 첫날을 마친다

## M6. 저장과 마감

- [ ] M6-01 세이브 데이터와 저장 ([#41](https://github.com/SeanKim0719/bakery-shop-simulator/issues/41))
- [ ] M6-02 불러오기와 월드 아이템 ([#42](https://github.com/SeanKim0719/bakery-shop-simulator/issues/42))
- [ ] M6-03 타이틀, 일시정지, 설정 ([#43](https://github.com/SeanKim0719/bakery-shop-simulator/issues/43))
- [ ] M6-04 데모 세이브 ([#44](https://github.com/SeanKim0719/bakery-shop-simulator/issues/44))
- [ ] M6-05 사운드 ([#45](https://github.com/SeanKim0719/bakery-shop-simulator/issues/45))
- [ ] M6-06 아트 패스 ([#46](https://github.com/SeanKim0719/bakery-shop-simulator/issues/46))
- [ ] M6-07 프로파일링과 스트레스 테스트 ([#47](https://github.com/SeanKim0719/bakery-shop-simulator/issues/47))
- [ ] M6-08 빌드, 영상, README ([#48](https://github.com/SeanKim0719/bakery-shop-simulator/issues/48))
- 완료 기준: Windows 빌드가 다른 PC에서 실행되고, README에 영상과 트러블슈팅 3~4건이 있다

## 일정이 밀리면

줄이는 순서는 [구현 계획](plan/README.md#일정이-밀릴-때-줄이는-순서)에 정해 두었다: 아트 패스 축소 → 레벨 4 콘텐츠 → 현금 결제 → 목표 리스트 → WebGL 빌드. 트러블슈팅 기록, 테스트, 세이브, 미스터리 박스와 실험은 줄이지 않는다.

## MVP 이후 선택 과제

- **첫 확장: 절도·보안·매장 어지럽힘** ([설계](design/07-theft-and-disorder.md))
    1. 절도 손님, 플레이어가 직접 붙잡기
    2. 보안요원 고용, CCTV·도난방지 게이트
    3. 진열을 흐트러뜨리는 손님, 정리하기
- 직원 고용 (계산원)
- 매장 확장과 가구 자유 배치 (런타임 NavMesh)
- 요일·날씨에 따른 수요 변동, 통계 그래프
- 서버 관심사를 살린 간단한 랭킹 서버 또는 클라우드 세이브

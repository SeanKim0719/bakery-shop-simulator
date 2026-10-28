# 개발 환경 셋업

## 개발 환경

| 항목 | 내용 |
| --- | --- |
| 엔진 | Unity 6.6 (`6000.6.7f1`) — 팀 전체가 이 버전으로 고정 |
| 템플릿 | Universal 3D (URP 17.6) |
| 주요 패키지 | Input System 1.20, AI Navigation 2.0, Test Framework (내장) |
| IDE | Visual Studio 2022 또는 JetBrains Rider |
| 버전 관리 | Git + Git LFS, Sourcetree |

에디터 버전을 올릴 때는 `ProjectSettings/ProjectVersion.txt`가 바뀌므로, 버전 변경만 따로 커밋하고 이 표도 함께 고친다.

## 프로젝트 받기 (새 PC에서, 또는 다시 받을 때)

1. Unity Hub → Installs에서 **6000.6.7f1** 설치 (모듈: Windows Build Support, WebGL Build Support)
2. Sourcetree → **Clone** → `https://github.com/SeanKim0719/bakery-shop-simulator.git`
   - Git LFS 설치 여부를 물으면 "예"
3. Unity Hub → **Add → Add project from disk** → 클론한 레포 폴더 (레포 폴더 자체가 Unity 프로젝트 폴더)
4. 처음 열 때 `Library`를 새로 만드느라 몇 분 걸린다
5. 확인
   - Console에 컴파일 에러가 없는지
   - Window → General → Test Runner → EditMode → Run All → `SetupSmokeTests` 2개 통과

### 그레이박스 확인 씬

메뉴 **BakeryShop → 그레이박스 씬 만들기 (1주차)**를 누르면 `Assets/_Project/Scenes/Greybox_Week1.unity`가 만들어진다. 바닥·벽·선반·테이블·턱, 배송 박스 5개, 플레이어, HUD가 연결까지 끝난 상태로 배치된다. 씬 연결을 손으로 하면 빠뜨리기 쉬워서 코드로 고정했다. 씬을 손으로 고친 뒤 다시 실행하면 덮어쓰니 주의한다.

| 키 | 동작 |
| --- | --- |
| WASD / 마우스 | 이동 / 시점 |
| E 또는 좌클릭 | 들기, 바라보는 윗면에 내려놓기 (박스 위면 쌓기) |
| G | 들고 있는 위치에서 떨어뜨리기 |
| Esc | 커서 풀기 / 다시 잠그기 |

### 주의: 레포 폴더 = Unity 프로젝트 폴더

`.gitignore`는 `/[Ll]ibrary/`처럼 **레포 맨 위 기준**으로 작성되어 있다. Unity 프로젝트를 레포 안의 하위 폴더에 만들면 그 폴더의 `Library/`가 제외되지 않아, 수백 MB의 캐시가 커밋되고 GitHub가 100MB 초과 파일로 푸시를 거절한다 (2026-10-22 실제로 겪은 문제). 커밋 전에 Sourcetree 파일 상태에 `Library`, `Temp`, `Logs`, `UserSettings`가 보이면 커밋하지 않는다.

## 폴더 구조

```
Assets/
  _Project/                   직접 만드는 모든 것 (밑줄로 항상 맨 위에 정렬)
    Art/                      Materials, Models, Textures
    Audio/
    Data/                     ScriptableObject 에셋 인스턴스 (레시피, 상품, 손님 프로필 등)
    Prefabs/
    Scenes/
    Scripts/                  BakeryShop.Runtime 어셈블리
      Core/                   GameBootstrap, EventBus, TimeService, DayCycle, Rng, SaveService
      Data/                   ScriptableObject 정의
      Interaction/            IInteractable, IHoldable, IHands, InteractionResolver, HoldableObject
      Player/                 PlayerInputReader, PlayerMotor, Interactor, Hands
      Systems/                창고, 오븐, 선반, 가격, 발주, 박스, 도감, 손님, 계산, 경제
      UI/                     HUD, PC 앱 화면
    Input/                    BakeryControls.inputactions
    Editor/                   BakeryShop.Editor 어셈블리 (그레이박스 씬 빌더 등 에디터 도구)
    Tests/
      EditMode/               BakeryShop.Tests.EditMode 어셈블리
  ThirdParty/                 에셋 스토어 등 외부 에셋 (직접 수정하지 않음)
  Settings/                   URP 템플릿이 만든 렌더 파이프라인 설정 (그대로 둠)
```

직접 만든 것과 외부 에셋을 폴더 단위로 분리해, 레포를 보는 사람이 어떤 코드가 내 것인지 바로 알 수 있게 한다.

## 어셈블리 정의 (asmdef)

| 어셈블리 | 위치 | 역할 |
| --- | --- | --- |
| `BakeryShop.Runtime` | `Assets/_Project/Scripts` | 게임 코드 전체 |
| `BakeryShop.Tests.EditMode` | `Assets/_Project/Tests/EditMode` | 순수 C# 로직 단위 테스트 |
| `BakeryShop.Editor` | `Assets/_Project/Editor` | 에디터 전용 도구. 빌드에 포함되지 않음 |

- 스크립트 수정 시 해당 어셈블리만 다시 컴파일되어 반복 속도가 빨라진다.
- 테스트 어셈블리는 `InternalsVisibleTo`로 Runtime의 internal 멤버에 접근할 수 있다. 규칙 로직을 public으로 열지 않고 테스트하기 위함이다.
- Runtime 어셈블리는 `Unity.InputSystem`, `Unity.AI.Navigation`, `UnityEngine.UI`를 참조한다. 다른 패키지 코드를 쓰기 시작하면 Runtime asmdef의 References에 추가한다.

## 작업 규칙

### 커밋 메시지

`타입: 내용 (#이슈번호)` 형식을 쓴다. 이슈 번호를 붙이면 트러블슈팅 기록과 해결 커밋이 자동으로 연결된다.

| 타입 | 용도 |
| --- | --- |
| `feat` | 기능 추가 |
| `fix` | 버그 수정 |
| `refactor` | 동작 변화 없는 구조 개선 |
| `perf` | 성능 개선 |
| `test` | 테스트 추가·수정 |
| `docs` | 문서 |
| `chore` | 설정, 패키지, 에셋 정리 |

예: `fix: 손에 든 박스가 선반을 뚫는 문제 수정 (#12)`

### 문제가 생겼을 때

1. GitHub Issues → New issue → **트러블슈팅** 템플릿으로 바로 기록
2. 가설과 검증 과정을 진행하면서 이슈에 계속 덧붙이기
3. 해결 커밋에 이슈 번호 연결, 전후 수치·GIF 첨부

자세한 기준은 [트러블슈팅 문서](troubleshooting/README.md)를 따른다.

### 단계별 빌드 태그

로드맵의 각 단계를 마치면 태그를 남긴다. 예: `git tag v0.1-movement && git push origin v0.1-movement`

## 참고: Git LFS 용량

GitHub 무료 계정의 LFS는 저장 공간과 월간 전송량에 한도가 있다. 큰 에셋 팩은 필요한 파일만 `ThirdParty/`에 가져오고, 사용하지 않는 에셋은 커밋하지 않는다.

# 개발 환경 셋업

## 개발 환경

| 항목 | 내용 |
| --- | --- |
| 엔진 | Unity 6.3 LTS (6000.3.x) |
| 템플릿 | Universal 3D (URP) |
| 주요 패키지 | Input System, AI Navigation, Test Framework (템플릿 기본 포함) |
| IDE | Visual Studio 2022 또는 JetBrains Rider |
| 버전 관리 | Git + Git LFS |

## 처음 한 번: Unity 프로젝트 생성

레포에는 스크립트 폴더와 문서만 있고, Unity가 만드는 `Packages/`, `ProjectSettings/`는 아직 없다. Unity Hub로 프로젝트를 만든 뒤 그 폴더에 레포를 붙이는 순서로 진행한다. Unity Hub는 비어 있지 않은 폴더에 새 프로젝트를 만들지 못하기 때문이다.

1. **Unity Hub에서 프로젝트 생성**
   - Installs에서 Unity 6.3 LTS 설치 (모듈: Windows Build Support, WebGL Build Support)
   - New project → **Universal 3D** 템플릿
   - Project name: `bakery-shop-simulator`, Location: 원하는 작업 폴더
   - 생성이 끝나면 Unity 에디터를 닫는다

2. **생성된 폴더에 레포 연결** (Git Bash 또는 터미널, 프로젝트 폴더 안에서)

   ```bash
   git lfs install
   git init -b main
   git remote add origin https://github.com/SeanKim0719/bakery-shop-simulator.git
   git pull origin main
   ```

   `git lfs install`은 반드시 첫 커밋 전에 실행한다. 늦으면 텍스처·모델이 일반 파일로 커밋되어 나중에 히스토리를 정리해야 한다.

3. **Unity로 다시 열어 확인**
   - Console에 컴파일 에러가 없는지 확인
   - Window → General → Test Runner → EditMode → Run All → `SetupSmokeTests` 2개 통과 확인
   - Edit → Project Settings → Player에서 Company Name, Product Name 설정
   - (선택) 템플릿 예제인 `Assets/TutorialInfo`, `Assets/Readme.asset` 삭제

4. **Unity가 생성한 파일 커밋**

   ```bash
   git add -A
   git status        # Library/, Temp/ 가 목록에 없는지 확인
   git commit -m "chore: Unity 6.3 프로젝트 생성 (URP 템플릿)"
   git push -u origin main
   ```

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
      Player/                 PlayerInputReader, PlayerMotor, Interactor, Hands, IInteractable
      Systems/                창고, 오븐, 선반, 가격, 발주, 박스, 도감, 손님, 계산, 경제
      UI/                     HUD, PC 앱 화면
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

- 스크립트 수정 시 해당 어셈블리만 다시 컴파일되어 반복 속도가 빨라진다.
- 테스트 어셈블리는 `InternalsVisibleTo`로 Runtime의 internal 멤버에 접근할 수 있다. 규칙 로직을 public으로 열지 않고 테스트하기 위함이다.
- 패키지 코드를 쓰기 시작하면 Runtime asmdef의 References에 추가한다. 예: PlayerController에서 Input System을 쓰면 `Unity.InputSystem`, NavMeshSurface를 쓰면 `Unity.AI.Navigation`.

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

using System.Collections.Generic;
using BakeryShop.Data;
using BakeryShop.Player;
using BakeryShop.UI;
using BakeryShop.World;
using UnityEngine;

namespace BakeryShop.Core
{
    /// <summary>
    /// 게임의 시작점 (Composition Root). 시스템을 만들고 씬의 컴포넌트에 주입한다.
    /// 초기화 순서를 이 클래스 한 곳에 순서대로 적어서 Awake 실행 순서에 따른 null 참조를 막는다.
    /// 시스템을 만드는 일 자체는 GameComposer가, 프레임마다의 갱신 순서는 GameLoop가 맡는다 (둘 다 씬 없이 테스트한다).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private ContentDatabase _content;
        [SerializeField] private PlayerInputReader _playerInput;

        [Tooltip("패널을 여닫는 곳. 패널이 없는 씬(1주차 그레이박스)에서는 비워 둔다.")]
        [SerializeField] private PanelHost _panels;

        [Tooltip("매장의 지점 좌표. 매장 씬에만 있다.")]
        [SerializeField] private ShopLayout _layout;

        [Tooltip("0이면 실행할 때마다 다른 난수를 쓴다. 버그를 재현할 때는 콘솔에 찍힌 시드를 여기에 넣는다.")]
        [SerializeField] private uint _fixedSeed;

        private GameContext _context;

        private void Awake()
        {
            if (_content == null || _playerInput == null)
            {
                Debug.LogError(
                    "[GameBootstrap] ContentDatabase 또는 PlayerInputReader가 연결되지 않았습니다. "
                    + "씬을 빌더 메뉴(BakeryShop > 매장 씬 만들기)로 다시 만들어 주세요.",
                    this);
                enabled = false;
                return;
            }

            ReportContentProblems();

            // 1. 시스템 생성
            uint seed = ChooseSeed();
            _context = GameComposer.Compose(_content, seed, GameComposer.LootSeedFor(seed));

            // 2. 처음 상태 (세이브가 생기면 여기서 새 게임과 불러오기가 갈린다: M6-01)
            GameComposer.StartNewGame(_context);

            // 3. 씬 참조
            _context.AttachScene(_playerInput, _panels, _layout);
            PhysicsLayers.ApplyCollisionRules();

            // 4. 씬의 컴포넌트에 주입. 여기서 각 뷰가 이벤트를 구독한다.
            InitializeSceneConsumers(_context);

            // 5. 모두 구독을 마친 뒤에 하루를 시작한다 (첫 DayPhaseChanged를 아무도 놓치지 않게).
            _context.Day.Begin();

            Debug.Log("[GameBootstrap] 시작. 난수 시드 " + seed + " (같은 상황을 다시 보려면 GameBootstrap의 Fixed Seed에 넣는다)", this);
        }

        private void OnEnable()
        {
            if (_playerInput != null)
            {
                _playerInput.PausePressed += HandleEscape;
            }
        }

        private void OnDisable()
        {
            if (_playerInput != null)
            {
                _playerInput.PausePressed -= HandleEscape;
            }
        }

        private void Update()
        {
            GameLoop.Tick(_context, Time.deltaTime);
        }

        // 열린 패널이 있으면 Esc는 패널을 닫는다.
        // TODO(M6-03 일시정지 메뉴): 패널이 없을 때는 메뉴를 연다. 지금은 커서를 풀고 다시 잠그기만 한다
        // (에디터에서 플레이 중에 커서를 되찾는 용도).
        private void HandleEscape()
        {
            if (_panels != null && _panels.HandleEscape())
            {
                return;
            }

            InputMode next = _playerInput.Mode == InputMode.Gameplay ? InputMode.UI : InputMode.Gameplay;
            _playerInput.SetMode(next);
        }

        private uint ChooseSeed()
        {
            if (_fixedSeed != 0u)
            {
                return _fixedSeed;
            }

            uint seed = (uint)System.Environment.TickCount;
            return seed != 0u ? seed : 1u;
        }

        // 콘텐츠의 오타는 가져올 때 한 번 걸러지지만, 에셋을 손으로 고쳤거나 가져오기를 잊은 경우를 시작할 때 다시 알려 준다.
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ReportContentProblems()
        {
            List<string> problems = ContentValidator.Validate(_content);
            foreach (string problem in problems)
            {
                Debug.LogError("[콘텐츠 검증] " + problem, _content);
            }
        }

        private void InitializeSceneConsumers(GameContext context)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (IContextConsumer consumer in root.GetComponentsInChildren<IContextConsumer>(includeInactive: true))
                {
                    consumer.Init(context);
                }
            }
        }
    }
}

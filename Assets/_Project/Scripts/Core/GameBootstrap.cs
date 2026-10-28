using BakeryShop.Player;
using UnityEngine;

namespace BakeryShop.Core
{
    /// <summary>
    /// 게임의 시작점 (Composition Root). 시스템을 만들고 씬의 컴포넌트에 주입한다.
    /// 초기화 순서를 이 클래스 한 곳에 순서대로 적어서 Awake 실행 순서에 따른 null 참조를 막는다.
    /// 지금은 1주차 뼈대이고, 시스템 생성과 갱신 순서(Tick)는 3주차부터 채운다 (docs/design/01).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _playerInput;

        private GameContext _context;

        private void Awake()
        {
            if (_playerInput == null)
            {
                Debug.LogError("[GameBootstrap] PlayerInputReader가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            // 1. 코어·시스템 생성 (3주차: EventBus, TimeService, DayCycle …)
            _context = new GameContext(_playerInput);

            // 2. 씬 컴포넌트에 주입
            InitializeSceneConsumers(_context);
        }

        private void OnEnable()
        {
            if (_playerInput != null)
            {
                _playerInput.PausePressed += TogglePause;
            }
        }

        private void OnDisable()
        {
            if (_playerInput != null)
            {
                _playerInput.PausePressed -= TogglePause;
            }
        }

        // TODO(일시정지 메뉴): 메뉴가 생기면 메뉴가 입력 모드를 바꾼다.
        // 지금은 Esc로 커서를 풀고 다시 잠그기만 한다 (에디터에서 플레이 중 커서를 되찾는 용도).
        private void TogglePause()
        {
            InputMode next = _playerInput.Mode == InputMode.Gameplay ? InputMode.UI : InputMode.Gameplay;
            _playerInput.SetMode(next);
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

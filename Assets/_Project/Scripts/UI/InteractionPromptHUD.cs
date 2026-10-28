using BakeryShop.Interaction;
using BakeryShop.Player;
using UnityEngine;
using UnityEngine.UI;

namespace BakeryShop.UI
{
    /// <summary>
    /// 화면 중앙 아래에 "[E] 배송 박스 들기" 같은 안내를 띄운다. Interactor가 알려줄 때만 갱신한다.
    /// </summary>
    /// <remarks>
    /// 그레이박스용 임시 HUD라 기본 UI Text와 운영체제 한글 글꼴(맑은 고딕 등)을 쓴다.
    /// TextMeshPro 한글 폰트 에셋은 UI 작업 단계(7~8주차)에서 교체한다.
    /// </remarks>
    public sealed class InteractionPromptHUD : MonoBehaviour
    {
        private static readonly string[] KoreanFontCandidates =
        {
            "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "NanumGothic",
        };

        [SerializeField] private Interactor _interactor;
        [SerializeField] private Text _label;
        [SerializeField] private string _keyHint = "[E]";

        [Tooltip("켜져 있으면 시작할 때 운영체제의 한글 글꼴로 바꾼다. 한글이 □로 나오는 것을 막는다.")]
        [SerializeField] private bool _useOsKoreanFont = true;

        private void Awake()
        {
            if (_label != null && _useOsKoreanFont)
            {
                _label.font = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, Mathf.Max(16, _label.fontSize));
            }
        }

        private void OnEnable()
        {
            if (_interactor == null)
            {
                return;
            }

            _interactor.OptionChanged += Show;
            Show(_interactor.Current);
        }

        private void OnDisable()
        {
            if (_interactor != null)
            {
                _interactor.OptionChanged -= Show;
            }
        }

        private void Show(InteractionOption option)
        {
            if (_label == null)
            {
                return;
            }

            bool visible = option.Kind != InteractionKind.None;
            _label.enabled = visible;
            if (visible)
            {
                _label.text = $"{_keyHint} {option.Prompt}";
            }
        }
    }
}

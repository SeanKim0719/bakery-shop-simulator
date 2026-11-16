using BakeryShop.Interaction;
using BakeryShop.Player;
using TMPro;
using UnityEngine;

namespace BakeryShop.UI
{
    /// <summary>
    /// 화면 중앙 아래에 "[E] 배송 박스 들기" 같은 안내를 띄운다. Interactor가 알려줄 때만 갱신한다.
    /// </summary>
    public sealed class InteractionPromptHUD : MonoBehaviour
    {
        [SerializeField] private Interactor _interactor;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private string _keyHint = "[E]";

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
                _label.text = _keyHint + " " + option.Prompt;
            }
        }
    }
}

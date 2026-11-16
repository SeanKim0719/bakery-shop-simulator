using TMPro;
using UnityEngine;

namespace BakeryShop.UI
{
    /// <summary>
    /// UI가 쓰는 글꼴 하나. 모든 텍스트가 이 에셋을 거쳐 글꼴을 받으므로, 글꼴을 바꿀 때 이 에셋의 연결만 바꾸면 된다.
    /// 한글을 그리려면 한글 글리프가 든 TMP 글꼴 에셋이 필요하다 (TMP의 기본 글꼴에는 한글이 없어 □로 나온다).
    /// </summary>
    public sealed class UiFont : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset _font;

        /// <summary>연결된 글꼴이 없으면 null (그러면 TMP의 기본 글꼴이 쓰인다).</summary>
        public TMP_FontAsset Font => _font;
    }
}

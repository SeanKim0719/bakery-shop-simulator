using UnityEngine;

namespace BakeryShop.Data
{
    /// <summary>
    /// 콘텐츠 정의의 공통 부분: ID와 표시 이름.
    /// 정의는 플레이 중에 바뀌지 않는다. 수치는 content.json에서 임포터가 Apply로 채우고,
    /// 아이콘·프리팹 같은 외형만 인스펙터에서 연결한다.
    /// </summary>
    public abstract class ContentDefinition : ScriptableObject
    {
        [SerializeField] private string _id = "";
        [SerializeField] private string _displayName = "";

        /// <summary>콘텐츠 ID (예: bread.croissant). 세이브가 이 문자열로 정의를 가리킨다.</summary>
        public string Id => _id;

        /// <summary>화면에 보이는 이름.</summary>
        public string DisplayName => _displayName;

        protected void ApplyIdentity(string id, string displayName)
        {
            _id = id ?? "";
            _displayName = displayName ?? "";
        }
    }
}

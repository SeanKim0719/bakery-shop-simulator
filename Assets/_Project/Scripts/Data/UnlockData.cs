using UnityEngine;

namespace BakeryShop.Data
{
    /// <summary>해금 정의. PC의 '성장' 탭에서 돈을 내고 산다.</summary>
    public sealed class UnlockData : ContentDefinition
    {
        [SerializeField] private UnlockKind _kind;
        [SerializeField] private string _targetId = "";
        [SerializeField] private int _requiredLevel = 1;
        [SerializeField] private int _cost;

        public UnlockKind Kind => _kind;

        /// <summary>RecipeLicense는 레시피 ID, Furniture는 가구의 고정 ID (예: oven.02). Tray는 빈 문자열.</summary>
        public string TargetId => _targetId;

        /// <summary>이 레벨부터 살 수 있다.</summary>
        public int RequiredLevel => _requiredLevel;

        /// <summary>가격(원).</summary>
        public int Cost => _cost;

        internal void Apply(UnlockJson json)
        {
            ApplyIdentity(json.Id, json.Name);
            _kind = ContentJson.ParseEnum<UnlockKind>(json.Kind, json.Id, "kind");
            _targetId = json.Target ?? "";
            _requiredLevel = json.RequiredLevel;
            _cost = json.Cost;
        }
    }
}

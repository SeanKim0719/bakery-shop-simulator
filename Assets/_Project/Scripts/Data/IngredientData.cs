using UnityEngine;

namespace BakeryShop.Data
{
    /// <summary>재료 정의.</summary>
    public sealed class IngredientData : ContentDefinition
    {
        [SerializeField] private Rarity _rarity;
        [SerializeField] private int _resellPrice;

        [Header("외형 (인스펙터에서 연결)")]
        [SerializeField] private Sprite _icon;

        public Rarity Rarity => _rarity;

        /// <summary>되팔 때 받는 금액(원).</summary>
        public int ResellPrice => _resellPrice;

        public Sprite Icon => _icon;

        /// <summary>수치만 덮어쓴다. Icon은 건드리지 않는다.</summary>
        internal void Apply(IngredientJson json)
        {
            ApplyIdentity(json.Id, json.Name);
            _rarity = ContentJson.ParseEnum<Rarity>(json.Rarity, json.Id, "rarity");
            _resellPrice = json.ResellPrice;
        }
    }
}

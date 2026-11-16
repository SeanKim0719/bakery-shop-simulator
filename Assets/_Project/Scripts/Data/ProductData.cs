using System.Collections.Generic;
using UnityEngine;

namespace BakeryShop.Data
{
    /// <summary>발주 상품 정의. PC에서 주문하면 박스로 배송된다.</summary>
    public sealed class ProductData : ContentDefinition
    {
        [SerializeField] private ProductKind _kind;
        [SerializeField] private int _cost;
        [SerializeField] private int _requiredLevel = 1;
        [SerializeField] private IngredientAmount[] _contents = new IngredientAmount[0];
        [SerializeField] private string _lootTableId = "";

        [Header("외형 (인스펙터에서 연결)")]
        [SerializeField] private GameObject _boxPrefab;

        public ProductKind Kind => _kind;

        /// <summary>구매가(원).</summary>
        public int Cost => _cost;

        /// <summary>이 레벨부터 주문할 수 있다.</summary>
        public int RequiredLevel => _requiredLevel;

        /// <summary>IngredientPack의 내용물. MysteryBox는 비어 있다.</summary>
        public IReadOnlyList<IngredientAmount> Contents => _contents;

        /// <summary>MysteryBox의 확률표 ID. IngredientPack은 빈 문자열.</summary>
        public string LootTableId => _lootTableId;

        public GameObject BoxPrefab => _boxPrefab;

        /// <summary>수치만 덮어쓴다. BoxPrefab은 건드리지 않는다.</summary>
        internal void Apply(ProductJson json)
        {
            ApplyIdentity(json.Id, json.Name);
            _kind = ContentJson.ParseEnum<ProductKind>(json.Kind, json.Id, "kind");
            _cost = json.Cost;
            _requiredLevel = json.RequiredLevel;
            _contents = ContentJson.ToAmounts(json.Contents);
            _lootTableId = json.LootTable ?? "";
        }
    }
}

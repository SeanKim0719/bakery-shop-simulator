using System;
using System.Collections.Generic;
using UnityEngine;

namespace BakeryShop.Data
{
    /// <summary>확률표의 항목 하나: 어떤 재료가, 어떤 비중으로, 한 번에 몇 개 나오는지.</summary>
    [Serializable]
    public struct LootEntry
    {
        public string IngredientId;
        public float Weight;
        public int Count;

        public LootEntry(string ingredientId, float weight, int count)
        {
            IngredientId = ingredientId;
            Weight = weight;
            Count = count;
        }
    }

    /// <summary>미스터리 박스의 확률표. 표시 이름은 없다 (이름은 상품이 가진다).</summary>
    public sealed class LootTable : ContentDefinition
    {
        [SerializeField] private int _rollsPerBox;
        [SerializeField] private LootEntry[] _entries = new LootEntry[0];
        [SerializeField] private int _pityThreshold;
        [SerializeField] private Rarity _pityMinRarity;

        /// <summary>박스 하나를 열 때의 뽑기 횟수.</summary>
        public int RollsPerBox => _rollsPerBox;

        public IReadOnlyList<LootEntry> Entries => _entries;

        /// <summary>이 횟수만큼 연속으로 꽝이면 다음 박스에서 PityMinRarity 이상을 하나 보장한다 (천장).</summary>
        public int PityThreshold => _pityThreshold;

        /// <summary>천장이 보장하는 최소 희귀도. 이보다 낮은 것만 나온 박스가 "꽝"이다.</summary>
        public Rarity PityMinRarity => _pityMinRarity;

        internal void Apply(LootTableJson json)
        {
            ApplyIdentity(json.Id, "");
            _rollsPerBox = json.RollsPerBox;
            _pityThreshold = json.PityThreshold;
            _pityMinRarity = ContentJson.ParseEnum<Rarity>(json.PityMinRarity, json.Id, "pityMinRarity");

            LootEntryJson[] source = json.Entries ?? new LootEntryJson[0];
            _entries = new LootEntry[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                _entries[i] = new LootEntry(source[i].Id ?? "", source[i].Weight, source[i].Count);
            }
        }
    }
}

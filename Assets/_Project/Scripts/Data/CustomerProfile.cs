using System;
using System.Collections.Generic;
using UnityEngine;

namespace BakeryShop.Data
{
    /// <summary>손님이 어떤 빵을 얼마나 좋아하는지.</summary>
    [Serializable]
    public struct RecipeWeight
    {
        public string RecipeId;
        public float Weight;

        public RecipeWeight(string recipeId, float weight)
        {
            RecipeId = recipeId;
            Weight = weight;
        }
    }

    /// <summary>손님 성향 정의 (학생, 직장인, 미식가 …).</summary>
    public sealed class CustomerProfile : ContentDefinition
    {
        [SerializeField] private float[] _spawnWeightByLevel = new float[0];
        [SerializeField] private int _budgetMin;
        [SerializeField] private int _budgetMax;
        [SerializeField] private int _maxItems;
        [SerializeField] private float _patienceSeconds;
        [SerializeField] private float _cashRatio;
        [SerializeField] private CurvePoint[] _priceSensitivity = new CurvePoint[0];
        [SerializeField] private RecipeWeight[] _preferences = new RecipeWeight[0];
        [SerializeField] private float _defaultPreference;
        [SerializeField] private float _theftChance;
        [SerializeField] private float _messiness;

        [Header("외형 (인스펙터에서 연결)")]
        [SerializeField] private GameObject[] _prefabs = new GameObject[0];

        /// <summary>레벨별 등장 비중. 인덱스 0이 레벨 1이다.</summary>
        public IReadOnlyList<float> SpawnWeightByLevel => _spawnWeightByLevel;

        /// <summary>한 번 방문의 예산(원). 이 범위에서 고르게 뽑는다.</summary>
        public int BudgetMin => _budgetMin;

        public int BudgetMax => _budgetMax;

        /// <summary>사려는 빵의 최대 개수.</summary>
        public int MaxItems => _maxItems;

        /// <summary>계산을 기다리는 한계(초).</summary>
        public float PatienceSeconds => _patienceSeconds;

        /// <summary>현금으로 결제할 확률 (0~1).</summary>
        public float CashRatio => _cashRatio;

        /// <summary>가격 민감도. x = 표시가 ÷ 시세, y = 구매 확률. PiecewiseCurve로 읽는다.</summary>
        public IReadOnlyList<CurvePoint> PriceSensitivity => _priceSensitivity;

        /// <summary>좋아하는 빵의 가중치. 목록에 없는 빵은 DefaultPreference를 쓴다.</summary>
        public IReadOnlyList<RecipeWeight> Preferences => _preferences;

        public float DefaultPreference => _defaultPreference;

        /// <summary>MVP에서는 0. 07 문서의 확장에서 쓴다.</summary>
        public float TheftChance => _theftChance;

        /// <summary>MVP에서는 0. 07 문서의 확장에서 쓴다.</summary>
        public float Messiness => _messiness;

        public IReadOnlyList<GameObject> Prefabs => _prefabs;

        /// <summary>수치만 덮어쓴다. Prefabs는 건드리지 않는다.</summary>
        internal void Apply(CustomerProfileJson json)
        {
            ApplyIdentity(json.Id, json.Name);
            _spawnWeightByLevel = ContentJson.Copy(json.SpawnWeightByLevel);
            _budgetMin = json.BudgetMin;
            _budgetMax = json.BudgetMax;
            _maxItems = json.MaxItems;
            _patienceSeconds = json.PatienceSeconds;
            _cashRatio = json.CashRatio;
            _priceSensitivity = ContentJson.Copy(json.PriceSensitivity);
            _defaultPreference = json.DefaultPreference;
            _theftChance = json.TheftChance;
            _messiness = json.Messiness;

            RecipeWeightJson[] source = json.Preferences ?? new RecipeWeightJson[0];
            _preferences = new RecipeWeight[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                _preferences[i] = new RecipeWeight(source[i].Id ?? "", source[i].Weight);
            }
        }
    }
}

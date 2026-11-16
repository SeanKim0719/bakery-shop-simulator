using System.Collections.Generic;
using UnityEngine;

namespace BakeryShop.Data
{
    /// <summary>레시피 정의. 한 판을 굽는 데 드는 재료와 시간, 나오는 빵의 수와 시세.</summary>
    public sealed class RecipeData : ContentDefinition
    {
        [SerializeField] private IngredientAmount[] _ingredients = new IngredientAmount[0];
        [SerializeField] private int _yield;
        [SerializeField] private int _marketPrice;
        [SerializeField] private float _bakeSeconds;
        [SerializeField] private float _perfectWindowSeconds;
        [SerializeField] private float _burnAfterSeconds;
        [SerializeField] private int _shelfLifeDays;
        [SerializeField] private bool _isSecret;
        [SerializeField] private string _keyIngredientId = "";
        [SerializeField] private string _hint = "";

        [Header("외형 (인스펙터에서 연결)")]
        [SerializeField] private Sprite _icon;
        [SerializeField] private GameObject _breadPrefab;

        /// <summary>한 판에 필요한 재료와 수량.</summary>
        public IReadOnlyList<IngredientAmount> Ingredients => _ingredients;

        /// <summary>한 판에 나오는 빵의 개수.</summary>
        public int Yield => _yield;

        /// <summary>시세(원). 손님이 "비싼지 싼지"를 판단하는 기준이다.</summary>
        public int MarketPrice => _marketPrice;

        /// <summary>다 익기까지 걸리는 시간(초, SimTime).</summary>
        public float BakeSeconds => _bakeSeconds;

        /// <summary>다 익은 뒤 이 시간 안에 꺼내면 완벽 판정.</summary>
        public float PerfectWindowSeconds => _perfectWindowSeconds;

        /// <summary>다 익은 뒤 이 시간을 넘기면 탄다.</summary>
        public float BurnAfterSeconds => _burnAfterSeconds;

        /// <summary>보관 일수. 구운 날을 0일로 세고, 이 일수를 넘기면 폐기된다.</summary>
        public int ShelfLifeDays => _shelfLifeDays;

        /// <summary>실험으로 발견해야 하는 레시피인지.</summary>
        public bool IsSecret => _isSecret;

        /// <summary>비밀 레시피의 핵심 재료. 비밀 레시피가 아니면 빈 문자열.</summary>
        public string KeyIngredientId => _keyIngredientId;

        /// <summary>도감에 보이는 힌트 문장. 없으면 빈 문자열.</summary>
        public string Hint => _hint;

        /// <summary>핵심 재료를 뺀 재료의 종류 수. 실험에서 채워야 하는 칸의 수다.</summary>
        public int OtherIngredientCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _ingredients.Length; i++)
                {
                    if (!_isSecret || _ingredients[i].IngredientId != _keyIngredientId)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public Sprite Icon => _icon;

        public GameObject BreadPrefab => _breadPrefab;

        /// <summary>수치만 덮어쓴다. Icon과 BreadPrefab은 건드리지 않는다.</summary>
        internal void Apply(RecipeJson json)
        {
            ApplyIdentity(json.Id, json.Name);
            _ingredients = ContentJson.ToAmounts(json.Ingredients);
            _yield = json.Yield;
            _marketPrice = json.MarketPrice;
            _bakeSeconds = json.BakeSeconds;
            _perfectWindowSeconds = json.PerfectWindowSeconds;
            _burnAfterSeconds = json.BurnAfterSeconds;
            _shelfLifeDays = json.ShelfLifeDays;
            _isSecret = json.Secret;
            _keyIngredientId = json.KeyIngredient ?? "";
            _hint = json.Hint ?? "";
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace BakeryShop.Data
{
    /// <summary>
    /// 모든 콘텐츠 정의를 들고 있는 한 곳. 시스템은 문자열 ID로 여기서만 정의를 찾는다.
    /// Get~은 ID가 없으면 예외를 던진다 (코드나 콘텐츠의 잘못).
    /// TryGet~은 세이브 복원처럼 "없을 수도 있는" 경우에 쓴다.
    /// </summary>
    public sealed class ContentDatabase : ScriptableObject
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private List<IngredientData> _ingredients = new List<IngredientData>();
        [SerializeField] private List<RecipeData> _recipes = new List<RecipeData>();
        [SerializeField] private List<ProductData> _products = new List<ProductData>();
        [SerializeField] private List<LootTable> _lootTables = new List<LootTable>();
        [SerializeField] private List<CustomerProfile> _customerProfiles = new List<CustomerProfile>();
        [SerializeField] private List<UnlockData> _unlocks = new List<UnlockData>();

        // 처음 조회할 때 만든다. 에셋에는 저장하지 않는다.
        [NonSerialized] private bool _isIndexed;
        [NonSerialized] private Dictionary<string, IngredientData> _ingredientById;
        [NonSerialized] private Dictionary<string, RecipeData> _recipeById;
        [NonSerialized] private Dictionary<string, ProductData> _productById;
        [NonSerialized] private Dictionary<string, LootTable> _lootTableById;
        [NonSerialized] private Dictionary<string, CustomerProfile> _customerProfileById;
        [NonSerialized] private Dictionary<string, UnlockData> _unlockById;
        [NonSerialized] private Dictionary<string, RecipeData> _secretRecipeByKeyIngredient;
        [NonSerialized] private List<IngredientData> _experimentChoices;

        public GameConfig Config => _config;
        public IReadOnlyList<IngredientData> Ingredients => _ingredients;
        public IReadOnlyList<RecipeData> Recipes => _recipes;
        public IReadOnlyList<ProductData> Products => _products;
        public IReadOnlyList<LootTable> LootTables => _lootTables;
        public IReadOnlyList<CustomerProfile> CustomerProfiles => _customerProfiles;
        public IReadOnlyList<UnlockData> Unlocks => _unlocks;

        /// <summary>
        /// 실험에서 고를 수 있는 재료: 발주 상품(재료 묶음)에 들어 있는 재료.
        /// 목록을 따로 적어 두지 않고 상품에서 계산하므로, 상품을 추가하면 실험의 선택지도 함께 늘어난다.
        /// 순서는 재료 목록의 순서를 따른다.
        /// </summary>
        public IReadOnlyList<IngredientData> ExperimentChoices
        {
            get
            {
                EnsureIndexed();
                return _experimentChoices;
            }
        }

        public IngredientData GetIngredient(string id)
        {
            EnsureIndexed();
            return Require(_ingredientById, id, "재료");
        }

        public bool TryGetIngredient(string id, out IngredientData ingredient)
        {
            EnsureIndexed();
            return TryFind(_ingredientById, id, out ingredient);
        }

        public RecipeData GetRecipe(string id)
        {
            EnsureIndexed();
            return Require(_recipeById, id, "레시피");
        }

        public bool TryGetRecipe(string id, out RecipeData recipe)
        {
            EnsureIndexed();
            return TryFind(_recipeById, id, out recipe);
        }

        public ProductData GetProduct(string id)
        {
            EnsureIndexed();
            return Require(_productById, id, "상품");
        }

        public bool TryGetProduct(string id, out ProductData product)
        {
            EnsureIndexed();
            return TryFind(_productById, id, out product);
        }

        public LootTable GetLootTable(string id)
        {
            EnsureIndexed();
            return Require(_lootTableById, id, "확률표");
        }

        public bool TryGetLootTable(string id, out LootTable lootTable)
        {
            EnsureIndexed();
            return TryFind(_lootTableById, id, out lootTable);
        }

        public CustomerProfile GetCustomerProfile(string id)
        {
            EnsureIndexed();
            return Require(_customerProfileById, id, "손님 성향");
        }

        public bool TryGetCustomerProfile(string id, out CustomerProfile profile)
        {
            EnsureIndexed();
            return TryFind(_customerProfileById, id, out profile);
        }

        public UnlockData GetUnlock(string id)
        {
            EnsureIndexed();
            return Require(_unlockById, id, "해금");
        }

        public bool TryGetUnlock(string id, out UnlockData unlock)
        {
            EnsureIndexed();
            return TryFind(_unlockById, id, out unlock);
        }

        /// <summary>이 재료를 핵심 재료로 쓰는 비밀 레시피. 없으면 null.</summary>
        public RecipeData SecretRecipeFor(string keyIngredientId)
        {
            EnsureIndexed();
            RecipeData recipe;
            return TryFind(_secretRecipeByKeyIngredient, keyIngredientId, out recipe) ? recipe : null;
        }

        /// <summary>임포터와 테스트가 내용을 채운다. 조회용 색인은 다음 조회 때 다시 만든다.</summary>
        internal void SetContent(
            GameConfig config,
            IEnumerable<IngredientData> ingredients,
            IEnumerable<RecipeData> recipes,
            IEnumerable<ProductData> products,
            IEnumerable<LootTable> lootTables,
            IEnumerable<CustomerProfile> customerProfiles,
            IEnumerable<UnlockData> unlocks)
        {
            _config = config;
            _ingredients = new List<IngredientData>(ingredients);
            _recipes = new List<RecipeData>(recipes);
            _products = new List<ProductData>(products);
            _lootTables = new List<LootTable>(lootTables);
            _customerProfiles = new List<CustomerProfile>(customerProfiles);
            _unlocks = new List<UnlockData>(unlocks);
            _isIndexed = false;
        }

        /// <summary>
        /// 에셋 파일 없이 메모리에만 데이터베이스를 만든다. 테스트(TestContent)와 임포터의 검증이 쓴다.
        /// </summary>
        internal static ContentDatabase CreateInMemory(ContentJson json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var config = CreateInstance<GameConfig>();
            config.name = "GameConfig";
            config.Apply(json.Config);

            var ingredients = new List<IngredientData>();
            foreach (IngredientJson item in json.Ingredients ?? new IngredientJson[0])
            {
                var definition = CreateInstance<IngredientData>();
                definition.name = item.Id;
                definition.Apply(item);
                ingredients.Add(definition);
            }

            var recipes = new List<RecipeData>();
            foreach (RecipeJson item in json.Recipes ?? new RecipeJson[0])
            {
                var definition = CreateInstance<RecipeData>();
                definition.name = item.Id;
                definition.Apply(item);
                recipes.Add(definition);
            }

            var products = new List<ProductData>();
            foreach (ProductJson item in json.Products ?? new ProductJson[0])
            {
                var definition = CreateInstance<ProductData>();
                definition.name = item.Id;
                definition.Apply(item);
                products.Add(definition);
            }

            var lootTables = new List<LootTable>();
            foreach (LootTableJson item in json.LootTables ?? new LootTableJson[0])
            {
                var definition = CreateInstance<LootTable>();
                definition.name = item.Id;
                definition.Apply(item);
                lootTables.Add(definition);
            }

            var customerProfiles = new List<CustomerProfile>();
            foreach (CustomerProfileJson item in json.CustomerProfiles ?? new CustomerProfileJson[0])
            {
                var definition = CreateInstance<CustomerProfile>();
                definition.name = item.Id;
                definition.Apply(item);
                customerProfiles.Add(definition);
            }

            var unlocks = new List<UnlockData>();
            foreach (UnlockJson item in json.Unlocks ?? new UnlockJson[0])
            {
                var definition = CreateInstance<UnlockData>();
                definition.name = item.Id;
                definition.Apply(item);
                unlocks.Add(definition);
            }

            var database = CreateInstance<ContentDatabase>();
            database.name = "ContentDatabase";
            database.SetContent(config, ingredients, recipes, products, lootTables, customerProfiles, unlocks);
            return database;
        }

        // 에셋이 다시 로드되면(임포트, 도메인 리로드) 색인을 다시 만들게 한다.
        private void OnEnable()
        {
            _isIndexed = false;
        }

        private void EnsureIndexed()
        {
            if (_isIndexed)
            {
                return;
            }

            _ingredientById = BuildIndex(_ingredients);
            _recipeById = BuildIndex(_recipes);
            _productById = BuildIndex(_products);
            _lootTableById = BuildIndex(_lootTables);
            _customerProfileById = BuildIndex(_customerProfiles);
            _unlockById = BuildIndex(_unlocks);

            _secretRecipeByKeyIngredient = new Dictionary<string, RecipeData>();
            foreach (RecipeData recipe in _recipes)
            {
                if (recipe == null || !recipe.IsSecret || string.IsNullOrEmpty(recipe.KeyIngredientId))
                {
                    continue;
                }

                if (!_secretRecipeByKeyIngredient.ContainsKey(recipe.KeyIngredientId))
                {
                    _secretRecipeByKeyIngredient.Add(recipe.KeyIngredientId, recipe);
                }
            }

            var orderable = new HashSet<string>();
            foreach (ProductData product in _products)
            {
                if (product == null || product.Kind != ProductKind.IngredientPack)
                {
                    continue;
                }

                for (int i = 0; i < product.Contents.Count; i++)
                {
                    orderable.Add(product.Contents[i].IngredientId);
                }
            }

            _experimentChoices = new List<IngredientData>();
            foreach (IngredientData ingredient in _ingredients)
            {
                if (ingredient != null && orderable.Contains(ingredient.Id))
                {
                    _experimentChoices.Add(ingredient);
                }
            }

            _isIndexed = true;
        }

        // ID가 비었거나 중복된 항목은 색인에 넣지 않는다 (먼저 나온 것이 남는다). 그런 문제는 ContentValidator가 알려 준다.
        private static Dictionary<string, T> BuildIndex<T>(List<T> items) where T : ContentDefinition
        {
            var index = new Dictionary<string, T>(items.Count);
            foreach (T item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.Id) || index.ContainsKey(item.Id))
                {
                    continue;
                }

                index.Add(item.Id, item);
            }

            return index;
        }

        private static bool TryFind<T>(Dictionary<string, T> index, string id, out T value) where T : class
        {
            if (string.IsNullOrEmpty(id))
            {
                value = null;
                return false;
            }

            return index.TryGetValue(id, out value);
        }

        private static T Require<T>(Dictionary<string, T> index, string id, string kindName) where T : class
        {
            T value;
            if (TryFind(index, id, out value))
            {
                return value;
            }

            throw new KeyNotFoundException(kindName + " ID '" + id + "'가 콘텐츠에 없습니다.");
        }
    }
}

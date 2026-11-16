using System;
using BakeryShop.Data;

namespace BakeryShop.Tests
{
    /// <summary>
    /// 테스트용 작은 콘텐츠. 규칙 테스트 전체가 함께 쓴다.
    /// 실제 content.json을 쓰지 않는 이유: 밸런스 수치를 바꿀 때마다 규칙 테스트가 깨지면 안 된다.
    /// 여기의 수치는 계산하기 쉬운 값이고, 테스트가 기대값의 근거로 삼으므로 함부로 바꾸지 않는다.
    ///
    /// 재료   ing.a, ing.b, ing.c (Common, 발주 가능) / ing.key (Rare, 박스에서만)
    /// 레시피 bread.plain  a2 b1 → 4개, 시세 1,000, 굽기 10초, 완벽 5초, 타기까지 20초, 보관 1일
    ///        bread.long   a1 c1 → 2개, 시세 3,000, 보관 2일
    ///        bread.secret a1 b1 key1 → 2개, 시세 5,000, 비밀 (핵심 재료 ing.key)
    /// 상품   product.a / product.b / product.c (각 10개, 1,000원), product.box (2,000원, 레벨 2)
    /// 확률표 loot.test (뽑기 3회, a 90 / key 10, 천장 2)
    /// 손님   customer.test (예산 2,000~4,000, 최대 2개, 인내심 30초, 현금 50%)
    /// 해금   unlock.recipe.long (레벨 2, 5,000원), unlock.tray.3 (레벨 2, 1,000원), unlock.shelf.02 (레벨 3, 3,000원)
    /// 설정   레벨 3단계 [0, 10, 30], 시작 트레이 2개, 진열대 칸 3개 × 4개
    /// </summary>
    internal static class TestContent
    {
        public const string IngredientA = "ing.a";
        public const string IngredientB = "ing.b";
        public const string IngredientC = "ing.c";
        public const string IngredientKey = "ing.key";

        public const string RecipePlain = "bread.plain";
        public const string RecipeLong = "bread.long";
        public const string RecipeSecret = "bread.secret";

        public const string ProductA = "product.a";
        public const string ProductB = "product.b";
        public const string ProductC = "product.c";
        public const string ProductBox = "product.box";

        public const string LootTest = "loot.test";
        public const string CustomerTest = "customer.test";

        public const string UnlockRecipeLong = "unlock.recipe.long";
        public const string UnlockTray3 = "unlock.tray.3";
        public const string UnlockShelf02 = "unlock.shelf.02";

        /// <summary>올바른 테스트 콘텐츠.</summary>
        public static ContentDatabase Create()
        {
            return ContentDatabase.CreateInMemory(CreateJson());
        }

        /// <summary>테스트 콘텐츠를 만들기 전에 일부를 고친다 (수치를 바꾸거나 일부러 깨뜨릴 때).</summary>
        public static ContentDatabase Create(Action<ContentJson> modify)
        {
            ContentJson json = CreateJson();
            modify(json);
            return ContentDatabase.CreateInMemory(json);
        }

        public static ContentJson CreateJson()
        {
            var json = new ContentJson();

            json.Ingredients = new[]
            {
                Ingredient(IngredientA, "재료 A", "Common", 100),
                Ingredient(IngredientB, "재료 B", "Common", 100),
                Ingredient(IngredientC, "재료 C", "Common", 100),
                Ingredient(IngredientKey, "핵심 재료", "Rare", 1000),
            };

            json.Recipes = new[]
            {
                new RecipeJson
                {
                    Id = RecipePlain,
                    Name = "기본 빵",
                    Ingredients = new[] { new AmountJson(IngredientA, 2), new AmountJson(IngredientB, 1) },
                    Yield = 4,
                    MarketPrice = 1000,
                    BakeSeconds = 10f,
                    PerfectWindowSeconds = 5f,
                    BurnAfterSeconds = 20f,
                    ShelfLifeDays = 1,
                },
                new RecipeJson
                {
                    Id = RecipeLong,
                    Name = "오래가는 빵",
                    Ingredients = new[] { new AmountJson(IngredientA, 1), new AmountJson(IngredientC, 1) },
                    Yield = 2,
                    MarketPrice = 3000,
                    BakeSeconds = 10f,
                    PerfectWindowSeconds = 5f,
                    BurnAfterSeconds = 20f,
                    ShelfLifeDays = 2,
                },
                new RecipeJson
                {
                    Id = RecipeSecret,
                    Name = "비밀 빵",
                    Secret = true,
                    KeyIngredient = IngredientKey,
                    Hint = "기본 빵에 귀한 무언가를 더하면…",
                    Ingredients = new[]
                    {
                        new AmountJson(IngredientA, 1),
                        new AmountJson(IngredientB, 1),
                        new AmountJson(IngredientKey, 1),
                    },
                    Yield = 2,
                    MarketPrice = 5000,
                    BakeSeconds = 10f,
                    PerfectWindowSeconds = 5f,
                    BurnAfterSeconds = 20f,
                    ShelfLifeDays = 1,
                },
            };

            json.Products = new[]
            {
                Pack(ProductA, "재료 A 10개", IngredientA),
                Pack(ProductB, "재료 B 10개", IngredientB),
                Pack(ProductC, "재료 C 10개", IngredientC),
                new ProductJson
                {
                    Id = ProductBox,
                    Name = "테스트 박스",
                    Kind = "MysteryBox",
                    Cost = 2000,
                    RequiredLevel = 2,
                    LootTable = LootTest,
                },
            };

            json.LootTables = new[]
            {
                new LootTableJson
                {
                    Id = LootTest,
                    RollsPerBox = 3,
                    PityThreshold = 2,
                    PityMinRarity = "Rare",
                    Entries = new[]
                    {
                        new LootEntryJson(IngredientA, 90f, 2),
                        new LootEntryJson(IngredientKey, 10f, 1),
                    },
                },
            };

            json.CustomerProfiles = new[]
            {
                new CustomerProfileJson
                {
                    Id = CustomerTest,
                    Name = "테스트 손님",
                    SpawnWeightByLevel = new[] { 1f, 1f, 1f },
                    BudgetMin = 2000,
                    BudgetMax = 4000,
                    MaxItems = 2,
                    PatienceSeconds = 30f,
                    CashRatio = 0.5f,
                    PriceSensitivity = new[]
                    {
                        new CurvePoint(0.5f, 1f),
                        new CurvePoint(1f, 0.9f),
                        new CurvePoint(1.5f, 0.1f),
                        new CurvePoint(2f, 0f),
                    },
                    DefaultPreference = 1f,
                    Preferences = new[] { new RecipeWeightJson(RecipePlain, 3f) },
                },
            };

            json.Unlocks = new[]
            {
                new UnlockJson { Id = UnlockRecipeLong, Name = "오래가는 빵 레시피", Kind = "RecipeLicense", Target = RecipeLong, RequiredLevel = 2, Cost = 5000 },
                new UnlockJson { Id = UnlockTray3, Name = "트레이 추가 (3개째)", Kind = "Tray", Target = "", RequiredLevel = 2, Cost = 1000 },
                new UnlockJson { Id = UnlockShelf02, Name = "진열대 2", Kind = "Furniture", Target = "shelf.02", RequiredLevel = 3, Cost = 3000 },
            };

            json.Config = new ConfigJson
            {
                Day = new DayJson
                {
                    OpenMinutes = 540,
                    CloseMinutes = 1080,
                    OpenRealSeconds = 360f,
                    SpawnCurve = new[] { new CurvePoint(540f, 1f), new CurvePoint(1080f, 1f) },
                },
                Economy = new EconomyJson
                {
                    StartMoney = 10000,
                    MoneyFloor = 5000,
                    RentByLevel = new[] { 1000, 2000, 3000 },
                    QualityPriceMultiplier = new QualityMultiplierJson { Undercooked = 0.7f, Normal = 1f, Perfect = 1.2f },
                    PriceStep = 100,
                    PriceMinRatio = 0.5f,
                    PriceMaxRatio = 2.5f,
                    StartTrays = 2,
                    StartRecipes = new[] { RecipePlain },
                    StartStorage = new[] { new AmountJson(IngredientA, 10), new AmountJson(IngredientB, 5) },
                },
                Oven = new OvenJson { MinCollectProgress = 0.5f },
                Shelf = new ShelfJson { SlotsPerShelf = 3, SlotCapacity = 4 },
                Customers = new CustomersJson
                {
                    BaseCountByLevel = new[] { 4, 6, 8 },
                    MaxConcurrent = 4,
                    QueueSlots = 3,
                    ReputationSpawnMin = 0.7f,
                    ReputationSpawnMax = 1.3f,
                    DayOldPriceMultiplier = 0.7f,
                    UndercookedMultiplier = 0.6f,
                    BrowseSecondsPerItem = 1f,
                    AromaSeconds = 10f,
                    AromaSpawnMultiplier = 1.4f,
                },
                Reputation = new ReputationJson
                {
                    Start = 500,
                    Min = 0,
                    Max = 1000,
                    SaleCompleted = 1,
                    LeftNoStock = -4,
                    LeftTooExpensive = -2,
                    LeftQueueTimeout = -10,
                    PaymentComplaint = -5,
                    RecipeDiscovered = 20,
                },
                Progression = new ProgressionJson
                {
                    XpThresholds = new[] { 0, 10, 30 },
                    XpPerBread = 1,
                    XpPerfectBonus = 1,
                    XpSecretBonus = 2,
                    XpDiscovery = 20,
                    XpDayComplete = 5,
                    DemoGoalLevel = 3,
                    DemoGoalSecretRecipes = 1,
                },
                Delivery = new DeliveryJson { Seconds = 5f },
            };

            return json;
        }

        /// <summary>배열 끝에 항목 하나를 붙인 새 배열. 콘텐츠를 고치는 테스트가 쓴다.</summary>
        public static T[] Append<T>(T[] source, T item)
        {
            var result = new T[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[source.Length] = item;
            return result;
        }

        public static IngredientJson Ingredient(string id, string name, string rarity, int resellPrice)
        {
            return new IngredientJson { Id = id, Name = name, Rarity = rarity, ResellPrice = resellPrice };
        }

        private static ProductJson Pack(string id, string name, string ingredientId)
        {
            return new ProductJson
            {
                Id = id,
                Name = name,
                Kind = "IngredientPack",
                Cost = 1000,
                RequiredLevel = 1,
                Contents = new[] { new AmountJson(ingredientId, 10) },
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using BakeryShop.Data;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    /// <summary>
    /// 올바른 TestContent에서 한 군데만 깨뜨리고, 문제가 정확히 하나 나오는지 확인한다.
    /// "정확히 하나"를 요구하는 이유: 원인 하나에 문제가 여러 줄 나오면 콘솔에서 진짜 원인을 찾기 어렵다.
    /// </summary>
    public class ContentValidatorTests
    {
        private static void AssertSingleProblem(Action<ContentJson> breakIt, params string[] expectedFragments)
        {
            ContentDatabase database = TestContent.Create(breakIt);

            List<string> problems = ContentValidator.Validate(database);

            Assert.AreEqual(1, problems.Count, "문제 목록:\n" + string.Join("\n", problems.ToArray()));
            foreach (string fragment in expectedFragments)
            {
                StringAssert.Contains(fragment, problems[0]);
            }
        }

        private static RecipeJson Recipe(ContentJson json, string id)
        {
            return json.Recipes.First(r => r.Id == id);
        }

        private static ProductJson Product(ContentJson json, string id)
        {
            return json.Products.First(p => p.Id == id);
        }

        private static UnlockJson Unlock(ContentJson json, string id)
        {
            return json.Unlocks.First(u => u.Id == id);
        }

        [Test]
        public void ValidContent_HasNoProblems()
        {
            List<string> problems = ContentValidator.Validate(TestContent.Create());

            Assert.AreEqual(0, problems.Count, "문제 목록:\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void NullDatabase_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ContentValidator.Validate(null));
        }

        // ---- 전체: ID ----

        [Test]
        public void EmptyId_IsReported()
        {
            AssertSingleProblem(
                json => json.Unlocks = TestContent.Append(json.Unlocks, new UnlockJson { Id = "", Kind = "Tray", RequiredLevel = 1, Cost = 100 }),
                "해금", "4번째", "ID가 비어");
        }

        [Test]
        public void DuplicateId_IsReportedOnce()
        {
            AssertSingleProblem(
                json =>
                {
                    json.Ingredients = TestContent.Append(json.Ingredients, TestContent.Ingredient(TestContent.IngredientA, "중복 1", "Common", 1));
                    json.Ingredients = TestContent.Append(json.Ingredients, TestContent.Ingredient(TestContent.IngredientA, "중복 2", "Common", 1));
                },
                "재료", TestContent.IngredientA, "중복");
        }

        // ---- 레시피 ----

        [Test]
        public void Recipe_UnknownIngredient_IsReported()
        {
            AssertSingleProblem(
                json => Recipe(json, TestContent.RecipePlain).Ingredients[0].Id = "ing.nope",
                TestContent.RecipePlain, "ing.nope");
        }

        [Test]
        public void Recipe_ZeroIngredientCount_IsReported()
        {
            AssertSingleProblem(
                json => Recipe(json, TestContent.RecipePlain).Ingredients[1].Count = 0,
                TestContent.RecipePlain, TestContent.IngredientB, "수량");
        }

        [Test]
        public void Recipe_NoIngredients_IsReported()
        {
            AssertSingleProblem(
                json => Recipe(json, TestContent.RecipeLong).Ingredients = new AmountJson[0],
                TestContent.RecipeLong, "재료가 없습니다");
        }

        [Test]
        public void Recipe_ZeroYield_IsReported()
        {
            AssertSingleProblem(json => Recipe(json, TestContent.RecipePlain).Yield = 0, TestContent.RecipePlain, "yield");
        }

        [Test]
        public void Recipe_ZeroMarketPrice_IsReported()
        {
            AssertSingleProblem(json => Recipe(json, TestContent.RecipePlain).MarketPrice = 0, TestContent.RecipePlain, "marketPrice");
        }

        [Test]
        public void Recipe_ZeroBakeSeconds_IsReported()
        {
            AssertSingleProblem(json => Recipe(json, TestContent.RecipePlain).BakeSeconds = 0f, TestContent.RecipePlain, "bakeSeconds");
        }

        [Test]
        public void Recipe_BurnsBeforePerfectWindowEnds_IsReported()
        {
            AssertSingleProblem(
                json => Recipe(json, TestContent.RecipePlain).BurnAfterSeconds = 5f,
                TestContent.RecipePlain, "burnAfterSeconds");
        }

        [Test]
        public void Recipe_ZeroShelfLife_IsReported()
        {
            AssertSingleProblem(json => Recipe(json, TestContent.RecipePlain).ShelfLifeDays = 0, TestContent.RecipePlain, "shelfLifeDays");
        }

        // ---- 비밀 레시피 ----

        [Test]
        public void SecretRecipe_WithoutKeyIngredient_IsReported()
        {
            AssertSingleProblem(
                json => Recipe(json, TestContent.RecipeSecret).KeyIngredient = "",
                TestContent.RecipeSecret, "keyIngredient");
        }

        [Test]
        public void SecretRecipe_UnknownKeyIngredient_IsReported()
        {
            AssertSingleProblem(
                json => Recipe(json, TestContent.RecipeSecret).KeyIngredient = "ing.nope",
                TestContent.RecipeSecret, "ing.nope");
        }

        [Test]
        public void SecretRecipe_KeyIngredientNotInIngredients_IsReported()
        {
            AssertSingleProblem(
                json =>
                {
                    RecipeJson secret = Recipe(json, TestContent.RecipeSecret);
                    secret.Ingredients = secret.Ingredients.Where(a => a.Id != TestContent.IngredientKey).ToArray();
                },
                TestContent.RecipeSecret, TestContent.IngredientKey, "들어 있지 않습니다");
        }

        [Test]
        public void SecretRecipes_SharingAKeyIngredient_AreReported()
        {
            AssertSingleProblem(
                json => json.Recipes = TestContent.Append(json.Recipes, new RecipeJson
                {
                    Id = "bread.secret2",
                    Name = "두 번째 비밀 빵",
                    Secret = true,
                    KeyIngredient = TestContent.IngredientKey,
                    Ingredients = new[] { new AmountJson(TestContent.IngredientC, 1), new AmountJson(TestContent.IngredientKey, 1) },
                    Yield = 1,
                    MarketPrice = 100,
                    BakeSeconds = 1f,
                    PerfectWindowSeconds = 1f,
                    BurnAfterSeconds = 2f,
                    ShelfLifeDays = 1,
                }),
                "bread.secret2", TestContent.RecipeSecret, TestContent.IngredientKey);
        }

        [Test]
        public void SecretRecipe_NeedingAnotherBoxOnlyIngredient_IsReported()
        {
            // 실험에서는 발주할 수 있는 재료만 고른다. 핵심 재료 말고 박스 전용 재료가 더 필요하면 발견할 길이 없다.
            AssertSingleProblem(
                json =>
                {
                    json.Ingredients = TestContent.Append(json.Ingredients, TestContent.Ingredient("ing.rare2", "다른 귀한 재료", "Rare", 500));
                    RecipeJson secret = Recipe(json, TestContent.RecipeSecret);
                    secret.Ingredients = TestContent.Append(secret.Ingredients, new AmountJson("ing.rare2", 1));
                },
                TestContent.RecipeSecret, "ing.rare2", "발주");
        }

        // ---- 상품 ----

        [Test]
        public void Product_UnknownContent_IsReported()
        {
            AssertSingleProblem(
                json =>
                {
                    // 재료 C의 묶음이 사라지지 않게, 새 상품을 추가해서 깨뜨린다.
                    json.Products = TestContent.Append(json.Products, new ProductJson
                    {
                        Id = "product.broken",
                        Kind = "IngredientPack",
                        Cost = 100,
                        RequiredLevel = 1,
                        Contents = new[] { new AmountJson("ing.nope", 1) },
                    });
                },
                "product.broken", "ing.nope");
        }

        [Test]
        public void Product_PackWithoutContents_IsReported()
        {
            AssertSingleProblem(
                json => json.Products = TestContent.Append(json.Products, new ProductJson { Id = "product.empty", Kind = "IngredientPack", Cost = 100, RequiredLevel = 1 }),
                "product.empty", "contents");
        }

        [Test]
        public void Product_UnknownLootTable_IsReported()
        {
            AssertSingleProblem(
                json => Product(json, TestContent.ProductBox).LootTable = "loot.nope",
                TestContent.ProductBox, "loot.nope");
        }

        [Test]
        public void Product_BoxWithoutLootTable_IsReported()
        {
            AssertSingleProblem(
                json => Product(json, TestContent.ProductBox).LootTable = "",
                TestContent.ProductBox, "lootTable");
        }

        [Test]
        public void Product_ZeroCost_IsReported()
        {
            AssertSingleProblem(json => Product(json, TestContent.ProductA).Cost = 0, TestContent.ProductA, "cost");
        }

        [TestCase(0)]
        [TestCase(4)]
        public void Product_RequiredLevelOutOfRange_IsReported(int level)
        {
            AssertSingleProblem(json => Product(json, TestContent.ProductBox).RequiredLevel = level, TestContent.ProductBox, "requiredLevel");
        }

        // ---- 확률표 ----

        [Test]
        public void LootTable_UnknownIngredient_IsReported()
        {
            AssertSingleProblem(
                json => json.LootTables[0].Entries[0].Id = "ing.nope",
                TestContent.LootTest, "ing.nope");
        }

        [Test]
        public void LootTable_ZeroTotalWeight_IsReported()
        {
            AssertSingleProblem(
                json =>
                {
                    json.LootTables[0].Entries[0].Weight = 0f;
                    json.LootTables[0].Entries[1].Weight = 0f;
                },
                TestContent.LootTest, "가중치의 합");
        }

        [Test]
        public void LootTable_NothingForPityToGuarantee_IsReported()
        {
            // 천장은 Rare 이상을 보장하는데, 표에 Rare 이상이 나올 길이 없다.
            AssertSingleProblem(
                json => json.LootTables[0].Entries[1].Weight = 0f,
                TestContent.LootTest, "천장", "Rare");
        }

        [Test]
        public void LootTable_NegativeWeight_IsReported()
        {
            AssertSingleProblem(json => json.LootTables[0].Entries[0].Weight = -1f, TestContent.LootTest, TestContent.IngredientA, "음수");
        }

        [Test]
        public void LootTable_ZeroRolls_IsReported()
        {
            AssertSingleProblem(json => json.LootTables[0].RollsPerBox = 0, TestContent.LootTest, "rollsPerBox");
        }

        [Test]
        public void LootTable_ZeroPityThreshold_IsReported()
        {
            AssertSingleProblem(json => json.LootTables[0].PityThreshold = 0, TestContent.LootTest, "pityThreshold");
        }

        // ---- 손님 ----

        [Test]
        public void Customer_UnknownPreferredRecipe_IsReported()
        {
            AssertSingleProblem(
                json => json.CustomerProfiles[0].Preferences[0].Id = "bread.nope",
                TestContent.CustomerTest, "bread.nope");
        }

        [Test]
        public void Customer_SpawnWeightLengthMismatch_IsReported()
        {
            AssertSingleProblem(
                json => json.CustomerProfiles[0].SpawnWeightByLevel = new[] { 1f, 1f },
                TestContent.CustomerTest, "spawnWeightByLevel");
        }

        [Test]
        public void Customer_PriceSensitivityNotAscending_IsReported()
        {
            AssertSingleProblem(
                json => json.CustomerProfiles[0].PriceSensitivity[2] = new CurvePoint(0.8f, 0.1f),
                TestContent.CustomerTest, "priceSensitivity");
        }

        [Test]
        public void Customer_EmptyPriceSensitivity_IsReported()
        {
            AssertSingleProblem(
                json => json.CustomerProfiles[0].PriceSensitivity = new CurvePoint[0],
                TestContent.CustomerTest, "priceSensitivity");
        }

        [Test]
        public void Customer_BudgetMinAboveMax_IsReported()
        {
            AssertSingleProblem(json => json.CustomerProfiles[0].BudgetMin = 5000, TestContent.CustomerTest, "budgetMin");
        }

        [Test]
        public void Customer_CashRatioOutOfRange_IsReported()
        {
            AssertSingleProblem(json => json.CustomerProfiles[0].CashRatio = 1.5f, TestContent.CustomerTest, "cashRatio");
        }

        // ---- 해금 ----

        [Test]
        public void Unlock_UnknownRecipe_IsReported()
        {
            AssertSingleProblem(
                json => Unlock(json, TestContent.UnlockRecipeLong).Target = "bread.nope",
                TestContent.UnlockRecipeLong, "bread.nope");
        }

        [Test]
        public void Unlock_LicenseForSecretRecipe_IsReported()
        {
            AssertSingleProblem(
                json => Unlock(json, TestContent.UnlockRecipeLong).Target = TestContent.RecipeSecret,
                TestContent.UnlockRecipeLong, TestContent.RecipeSecret, "비밀");
        }

        [Test]
        public void Unlock_FurnitureWithoutTarget_IsReported()
        {
            AssertSingleProblem(json => Unlock(json, TestContent.UnlockShelf02).Target = "", TestContent.UnlockShelf02, "target");
        }

        [Test]
        public void Unlock_ZeroCost_IsReported()
        {
            AssertSingleProblem(json => Unlock(json, TestContent.UnlockTray3).Cost = 0, TestContent.UnlockTray3, "cost");
        }

        [Test]
        public void Unlock_RequiredLevelOutOfRange_IsReported()
        {
            AssertSingleProblem(json => Unlock(json, TestContent.UnlockTray3).RequiredLevel = 9, TestContent.UnlockTray3, "requiredLevel");
        }

        // ---- 설정 ----

        [Test]
        public void Config_RentLengthMismatch_IsReported()
        {
            AssertSingleProblem(json => json.Config.Economy.RentByLevel = new[] { 1000, 2000 }, "rentByLevel");
        }

        [Test]
        public void Config_BaseCountLengthMismatch_IsReported()
        {
            AssertSingleProblem(json => json.Config.Customers.BaseCountByLevel = new[] { 4, 6, 8, 10 }, "baseCountByLevel");
        }

        [Test]
        public void Config_XpThresholdsNotAscending_IsReported()
        {
            AssertSingleProblem(json => json.Config.Progression.XpThresholds = new[] { 0, 30, 10 }, "xpThresholds");
        }

        [Test]
        public void Config_XpThresholdsNotStartingAtZero_IsReported()
        {
            AssertSingleProblem(json => json.Config.Progression.XpThresholds = new[] { 5, 10, 30 }, "xpThresholds", "0");
        }

        [Test]
        public void Config_UnknownStartRecipe_IsReported()
        {
            AssertSingleProblem(json => json.Config.Economy.StartRecipes = new[] { "bread.nope" }, "startRecipes", "bread.nope");
        }

        [Test]
        public void Config_SecretStartRecipe_IsReported()
        {
            AssertSingleProblem(
                json => json.Config.Economy.StartRecipes = new[] { TestContent.RecipePlain, TestContent.RecipeSecret },
                "startRecipes", TestContent.RecipeSecret);
        }

        [Test]
        public void Config_UnknownStartIngredient_IsReported()
        {
            AssertSingleProblem(json => json.Config.Economy.StartStorage[0].Id = "ing.nope", "startStorage", "ing.nope");
        }

        [Test]
        public void Config_CloseNotAfterOpen_IsReported()
        {
            AssertSingleProblem(json => json.Config.Day.CloseMinutes = 540, "closeMinutes");
        }

        [Test]
        public void Config_SpawnCurveNotAscending_IsReported()
        {
            AssertSingleProblem(
                json => json.Config.Day.SpawnCurve = new[] { new CurvePoint(1080f, 1f), new CurvePoint(540f, 1f) },
                "spawnCurve");
        }

        [Test]
        public void Config_PriceRatiosReversed_IsReported()
        {
            AssertSingleProblem(json => json.Config.Economy.PriceMinRatio = 3f, "priceMinRatio");
        }

        [Test]
        public void Config_ZeroPriceStep_IsReported()
        {
            AssertSingleProblem(json => json.Config.Economy.PriceStep = 0, "priceStep");
        }

        [Test]
        public void Config_ReputationStartOutOfRange_IsReported()
        {
            AssertSingleProblem(json => json.Config.Reputation.Start = 2000, "reputation");
        }

        [Test]
        public void SeveralBreaks_AreAllReported()
        {
            ContentDatabase database = TestContent.Create(json =>
            {
                Recipe(json, TestContent.RecipePlain).Yield = 0;
                Product(json, TestContent.ProductBox).LootTable = "loot.nope";
                json.Config.Economy.PriceStep = 0;
            });

            Assert.AreEqual(3, ContentValidator.Validate(database).Count);
        }
    }
}

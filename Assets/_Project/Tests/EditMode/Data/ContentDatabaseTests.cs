using System;
using System.Collections.Generic;
using System.Linq;
using BakeryShop.Core;
using BakeryShop.Data;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class ContentDatabaseTests
    {
        private ContentDatabase _database;

        [SetUp]
        public void SetUp()
        {
            _database = TestContent.Create();
        }

        [Test]
        public void Lists_HoldEverythingInJsonOrder()
        {
            CollectionAssert.AreEqual(
                new[] { TestContent.IngredientA, TestContent.IngredientB, TestContent.IngredientC, TestContent.IngredientKey },
                _database.Ingredients.Select(i => i.Id).ToArray());
            CollectionAssert.AreEqual(
                new[] { TestContent.RecipePlain, TestContent.RecipeLong, TestContent.RecipeSecret },
                _database.Recipes.Select(r => r.Id).ToArray());
            Assert.AreEqual(4, _database.Products.Count);
            Assert.AreEqual(1, _database.LootTables.Count);
            Assert.AreEqual(1, _database.CustomerProfiles.Count);
            Assert.AreEqual(3, _database.Unlocks.Count);
            Assert.IsNotNull(_database.Config);
        }

        [Test]
        public void GetRecipe_ReturnsTheDefinition_WithItsNumbers()
        {
            RecipeData recipe = _database.GetRecipe(TestContent.RecipePlain);

            Assert.AreEqual(TestContent.RecipePlain, recipe.Id);
            Assert.AreEqual("기본 빵", recipe.DisplayName);
            Assert.AreEqual(4, recipe.Yield);
            Assert.AreEqual(1000, recipe.MarketPrice);
            Assert.AreEqual(10f, recipe.BakeSeconds, 0.001f);
            Assert.AreEqual(5f, recipe.PerfectWindowSeconds, 0.001f);
            Assert.AreEqual(20f, recipe.BurnAfterSeconds, 0.001f);
            Assert.AreEqual(1, recipe.ShelfLifeDays);
            Assert.IsFalse(recipe.IsSecret);
            Assert.AreEqual("", recipe.KeyIngredientId);
            Assert.AreEqual(2, recipe.Ingredients.Count);
            Assert.AreEqual(TestContent.IngredientA, recipe.Ingredients[0].IngredientId);
            Assert.AreEqual(2, recipe.Ingredients[0].Count);
        }

        [Test]
        public void GetRecipe_UnknownId_ThrowsWithTheId()
        {
            var exception = Assert.Throws<KeyNotFoundException>(() => _database.GetRecipe("bread.nope"));

            StringAssert.Contains("bread.nope", exception.Message);
        }

        [TestCase(null)]
        [TestCase("")]
        public void GetRecipe_EmptyId_Throws(string id)
        {
            Assert.Throws<KeyNotFoundException>(() => _database.GetRecipe(id));
        }

        [Test]
        public void TryGetRecipe_KnownId_ReturnsTrue()
        {
            RecipeData recipe;

            Assert.IsTrue(_database.TryGetRecipe(TestContent.RecipeLong, out recipe));
            Assert.AreEqual(TestContent.RecipeLong, recipe.Id);
        }

        [TestCase("bread.nope")]
        [TestCase("")]
        [TestCase(null)]
        public void TryGetRecipe_UnknownId_ReturnsFalse(string id)
        {
            RecipeData recipe;

            Assert.IsFalse(_database.TryGetRecipe(id, out recipe));
            Assert.IsNull(recipe);
        }

        [Test]
        public void EveryKind_CanBeFoundById()
        {
            Assert.AreEqual(Rarity.Rare, _database.GetIngredient(TestContent.IngredientKey).Rarity);
            Assert.AreEqual(1000, _database.GetIngredient(TestContent.IngredientKey).ResellPrice);

            ProductData box = _database.GetProduct(TestContent.ProductBox);
            Assert.AreEqual(ProductKind.MysteryBox, box.Kind);
            Assert.AreEqual(2000, box.Cost);
            Assert.AreEqual(2, box.RequiredLevel);
            Assert.AreEqual(TestContent.LootTest, box.LootTableId);
            Assert.AreEqual(0, box.Contents.Count);

            ProductData pack = _database.GetProduct(TestContent.ProductA);
            Assert.AreEqual(ProductKind.IngredientPack, pack.Kind);
            Assert.AreEqual(10, pack.Contents[0].Count);
            Assert.AreEqual("", pack.LootTableId);

            LootTable loot = _database.GetLootTable(TestContent.LootTest);
            Assert.AreEqual(3, loot.RollsPerBox);
            Assert.AreEqual(2, loot.PityThreshold);
            Assert.AreEqual(Rarity.Rare, loot.PityMinRarity);
            Assert.AreEqual(2, loot.Entries.Count);
            Assert.AreEqual(90f, loot.Entries[0].Weight, 0.001f);

            CustomerProfile customer = _database.GetCustomerProfile(TestContent.CustomerTest);
            Assert.AreEqual(2000, customer.BudgetMin);
            Assert.AreEqual(4000, customer.BudgetMax);
            Assert.AreEqual(2, customer.MaxItems);
            Assert.AreEqual(30f, customer.PatienceSeconds, 0.001f);
            Assert.AreEqual(0.5f, customer.CashRatio, 0.001f);
            Assert.AreEqual(TestContent.RecipePlain, customer.Preferences[0].RecipeId);
            Assert.AreEqual(3, customer.SpawnWeightByLevel.Count);

            UnlockData unlock = _database.GetUnlock(TestContent.UnlockShelf02);
            Assert.AreEqual(UnlockKind.Furniture, unlock.Kind);
            Assert.AreEqual("shelf.02", unlock.TargetId);
            Assert.AreEqual(3, unlock.RequiredLevel);
            Assert.AreEqual(3000, unlock.Cost);
        }

        [Test]
        public void UnknownIds_OfEveryKind_Throw()
        {
            Assert.Throws<KeyNotFoundException>(() => _database.GetIngredient("x"));
            Assert.Throws<KeyNotFoundException>(() => _database.GetProduct("x"));
            Assert.Throws<KeyNotFoundException>(() => _database.GetLootTable("x"));
            Assert.Throws<KeyNotFoundException>(() => _database.GetCustomerProfile("x"));
            Assert.Throws<KeyNotFoundException>(() => _database.GetUnlock("x"));
        }

        [Test]
        public void TryGet_OfEveryKind_ReturnsFalseForUnknownIds()
        {
            IngredientData ingredient;
            ProductData product;
            LootTable lootTable;
            CustomerProfile profile;
            UnlockData unlock;

            Assert.IsFalse(_database.TryGetIngredient("x", out ingredient));
            Assert.IsFalse(_database.TryGetProduct("x", out product));
            Assert.IsFalse(_database.TryGetLootTable("x", out lootTable));
            Assert.IsFalse(_database.TryGetCustomerProfile("x", out profile));
            Assert.IsFalse(_database.TryGetUnlock("x", out unlock));

            Assert.IsTrue(_database.TryGetIngredient(TestContent.IngredientA, out ingredient));
            Assert.IsTrue(_database.TryGetProduct(TestContent.ProductBox, out product));
            Assert.IsTrue(_database.TryGetLootTable(TestContent.LootTest, out lootTable));
            Assert.IsTrue(_database.TryGetCustomerProfile(TestContent.CustomerTest, out profile));
            Assert.IsTrue(_database.TryGetUnlock(TestContent.UnlockTray3, out unlock));
        }

        [Test]
        public void SecretRecipeFor_KeyIngredient_ReturnsTheRecipe()
        {
            RecipeData recipe = _database.SecretRecipeFor(TestContent.IngredientKey);

            Assert.IsNotNull(recipe);
            Assert.AreEqual(TestContent.RecipeSecret, recipe.Id);
            Assert.IsTrue(recipe.IsSecret);
            Assert.AreEqual(TestContent.IngredientKey, recipe.KeyIngredientId);
        }

        [TestCase("ing.a")]
        [TestCase("ing.nope")]
        [TestCase("")]
        [TestCase(null)]
        public void SecretRecipeFor_OtherIngredient_ReturnsNull(string ingredientId)
        {
            Assert.IsNull(_database.SecretRecipeFor(ingredientId));
        }

        [Test]
        public void ExperimentChoices_AreTheIngredientsSoldInPacks()
        {
            // ing.key는 박스에서만 나오므로 실험에서 고를 수 없다.
            CollectionAssert.AreEqual(
                new[] { TestContent.IngredientA, TestContent.IngredientB, TestContent.IngredientC },
                _database.ExperimentChoices.Select(i => i.Id).ToArray());
        }

        [Test]
        public void ExperimentChoices_FollowTheProducts()
        {
            // 목록을 코드에 적어 두지 않았으므로, 상품이 빠지면 선택지에서도 빠진다.
            ContentDatabase database = TestContent.Create(json =>
            {
                json.Products = json.Products.Where(p => p.Id != TestContent.ProductC).ToArray();
            });

            CollectionAssert.AreEqual(
                new[] { TestContent.IngredientA, TestContent.IngredientB },
                database.ExperimentChoices.Select(i => i.Id).ToArray());
        }

        [Test]
        public void OtherIngredientCount_ExcludesTheKeyIngredient()
        {
            Assert.AreEqual(2, _database.GetRecipe(TestContent.RecipeSecret).OtherIngredientCount);
        }

        [Test]
        public void OtherIngredientCount_OfNormalRecipe_IsAllIngredients()
        {
            Assert.AreEqual(2, _database.GetRecipe(TestContent.RecipePlain).OtherIngredientCount);
        }

        [Test]
        public void Config_HoldsEveryGroup()
        {
            GameConfig config = _database.Config;

            Assert.AreEqual(3, config.LevelCount);

            Assert.AreEqual(540, config.Day.OpenMinutes);
            Assert.AreEqual(1080, config.Day.CloseMinutes);
            Assert.AreEqual(360f, config.Day.OpenRealSeconds, 0.001f);
            Assert.AreEqual(2, config.Day.SpawnCurve.Count);

            Assert.AreEqual(10000, config.Economy.StartMoney);
            Assert.AreEqual(5000, config.Economy.MoneyFloor);
            CollectionAssert.AreEqual(new[] { 1000, 2000, 3000 }, config.Economy.RentByLevel.ToArray());
            Assert.AreEqual(100, config.Economy.PriceStep);
            Assert.AreEqual(0.5f, config.Economy.PriceMinRatio, 0.001f);
            Assert.AreEqual(2.5f, config.Economy.PriceMaxRatio, 0.001f);
            Assert.AreEqual(2, config.Economy.StartTrays);
            CollectionAssert.AreEqual(new[] { TestContent.RecipePlain }, config.Economy.StartRecipes.ToArray());
            Assert.AreEqual(2, config.Economy.StartStorage.Count);
            Assert.AreEqual(10, config.Economy.StartStorage[0].Count);

            Assert.AreEqual(0.5f, config.Oven.MinCollectProgress, 0.001f);
            Assert.AreEqual(3, config.Shelf.SlotsPerShelf);
            Assert.AreEqual(4, config.Shelf.SlotCapacity);

            CollectionAssert.AreEqual(new[] { 4, 6, 8 }, config.Customers.BaseCountByLevel.ToArray());
            Assert.AreEqual(4, config.Customers.MaxConcurrent);
            Assert.AreEqual(3, config.Customers.QueueSlots);
            Assert.AreEqual(0.7f, config.Customers.DayOldPriceMultiplier, 0.001f);

            Assert.AreEqual(500, config.Reputation.Start);
            Assert.AreEqual(-10, config.Reputation.LeftQueueTimeout);

            CollectionAssert.AreEqual(new[] { 0, 10, 30 }, config.Progression.XpThresholds.ToArray());
            Assert.AreEqual(5, config.Progression.XpDayComplete);
            Assert.AreEqual(3, config.Progression.DemoGoalLevel);

            Assert.AreEqual(5f, config.Delivery.Seconds, 0.001f);
        }

        [TestCase(BreadQuality.Undercooked, 0.7f)]
        [TestCase(BreadQuality.Normal, 1f)]
        [TestCase(BreadQuality.Perfect, 1.2f)]
        [TestCase(BreadQuality.Burnt, 0f)]
        public void QualityPriceMultiplier_ByQuality(BreadQuality quality, float expected)
        {
            Assert.AreEqual(expected, _database.Config.Economy.QualityPriceMultiplier(quality), 0.001f);
        }

        [Test]
        public void PriceSensitivity_CanBeReadWithPiecewiseCurve()
        {
            // 정의의 점 목록을 M2-02의 PiecewiseCurve가 그대로 받는다: 0.9와 0.1의 중간.
            CustomerProfile customer = _database.GetCustomerProfile(TestContent.CustomerTest);

            Assert.AreEqual(0.5f, PiecewiseCurve.Evaluate(customer.PriceSensitivity, 1.25f), 0.001f);
        }

        [Test]
        public void DuplicateId_FirstOneWins_InLookups()
        {
            // 중복은 ContentValidator가 알려 준다. 조회는 예외 없이 먼저 나온 것을 돌려준다.
            ContentDatabase database = TestContent.Create(json =>
            {
                json.Ingredients = TestContent.Append(
                    json.Ingredients,
                    TestContent.Ingredient(TestContent.IngredientA, "중복", "Legendary", 9999));
            });

            Assert.AreEqual(Rarity.Common, database.GetIngredient(TestContent.IngredientA).Rarity);
        }

        [Test]
        public void UnknownEnumText_ThrowsWithOwnerAndField()
        {
            var exception = Assert.Throws<FormatException>(() => TestContent.Create(json =>
            {
                json.Ingredients[0].Rarity = "Epic";
            }));

            StringAssert.Contains(TestContent.IngredientA, exception.Message);
            StringAssert.Contains("rarity", exception.Message);
            StringAssert.Contains("Epic", exception.Message);
        }

        [Test]
        public void Definitions_DoNotShareArraysWithTheJson()
        {
            // 정의를 만든 뒤에 JSON 객체를 고쳐도 정의가 따라 바뀌지 않는다.
            ContentJson json = TestContent.CreateJson();
            ContentDatabase database = ContentDatabase.CreateInMemory(json);

            json.Config.Progression.XpThresholds[1] = 999;
            json.CustomerProfiles[0].SpawnWeightByLevel[0] = 999f;

            Assert.AreEqual(10, database.Config.Progression.XpThresholds[1]);
            Assert.AreEqual(1f, database.GetCustomerProfile(TestContent.CustomerTest).SpawnWeightByLevel[0], 0.001f);
        }

        [Test]
        public void CreateInMemory_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ContentDatabase.CreateInMemory(null));
        }
    }
}

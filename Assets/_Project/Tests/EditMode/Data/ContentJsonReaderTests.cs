using System;
using BakeryShop.Data;
using BakeryShop.EditorTools;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class ContentJsonReaderTests
    {
        [Test]
        public void PointLists_BecomeCurvePoints()
        {
            ContentJson content = ContentJsonReader.Read(@"{
                ""config"": { ""day"": { ""openMinutes"": 540, ""closeMinutes"": 1080, ""openRealSeconds"": 360,
                    ""spawnCurve"": [[540, 0.6], [600, 0.8], [1080, 1.5]] } }
            }");

            CurvePoint[] curve = content.Config.Day.SpawnCurve;
            Assert.AreEqual(3, curve.Length);
            Assert.AreEqual(540f, curve[0].X, 0.001f);
            Assert.AreEqual(0.6f, curve[0].Y, 0.001f);
            Assert.AreEqual(1080f, curve[2].X, 0.001f);
            Assert.AreEqual(1.5f, curve[2].Y, 0.001f);
            Assert.AreEqual(540, content.Config.Day.OpenMinutes);
            Assert.AreEqual(360f, content.Config.Day.OpenRealSeconds, 0.001f);
        }

        [Test]
        public void QualityMultiplierDictionary_IsRead()
        {
            ContentJson content = ContentJsonReader.Read(@"{
                ""config"": { ""economy"": { ""qualityPriceMultiplier"": { ""Undercooked"": 0.7, ""Normal"": 1.0, ""Perfect"": 1.2 } } }
            }");

            QualityMultiplierJson quality = content.Config.Economy.QualityPriceMultiplier;
            Assert.AreEqual(0.7f, quality.Undercooked, 0.001f);
            Assert.AreEqual(1.0f, quality.Normal, 0.001f);
            Assert.AreEqual(1.2f, quality.Perfect, 0.001f);
        }

        [Test]
        public void Recipe_IsRead_WithNestedIngredients()
        {
            ContentJson content = ContentJsonReader.Read(@"{
                ""recipes"": [ { ""id"": ""bread.white"", ""name"": ""식빵"", ""secret"": false,
                    ""ingredients"": [ { ""id"": ""ing.flour"", ""count"": 3 }, { ""id"": ""ing.milk"", ""count"": 1 } ],
                    ""yield"": 4, ""marketPrice"": 3500, ""bakeSeconds"": 50, ""perfectWindowSeconds"": 8,
                    ""burnAfterSeconds"": 30, ""shelfLifeDays"": 2 } ]
            }");

            RecipeJson recipe = content.Recipes[0];
            Assert.AreEqual("bread.white", recipe.Id);
            Assert.AreEqual("식빵", recipe.Name);
            Assert.AreEqual(2, recipe.Ingredients.Length);
            Assert.AreEqual("ing.flour", recipe.Ingredients[0].Id);
            Assert.AreEqual(3, recipe.Ingredients[0].Count);
            Assert.AreEqual(4, recipe.Yield);
            Assert.AreEqual(3500, recipe.MarketPrice);
            Assert.AreEqual(50f, recipe.BakeSeconds, 0.001f);
            Assert.AreEqual(2, recipe.ShelfLifeDays);
        }

        [Test]
        public void OptionalFields_CanBeMissing()
        {
            // keyIngredient, hint는 비밀 레시피에만, lootTable은 미스터리 박스에만, contents는 재료 묶음에만 있다.
            ContentJson content = ContentJsonReader.Read(@"{
                ""recipes"": [ { ""id"": ""bread.white"", ""name"": ""식빵"" } ],
                ""products"": [
                    { ""id"": ""product.flour"", ""name"": ""밀가루"", ""kind"": ""IngredientPack"", ""cost"": 6000, ""requiredLevel"": 1,
                      ""contents"": [ { ""id"": ""ing.flour"", ""count"": 10 } ] },
                    { ""id"": ""product.box"", ""name"": ""박스"", ""kind"": ""MysteryBox"", ""cost"": 12000, ""requiredLevel"": 2,
                      ""lootTable"": ""loot.basic"" } ]
            }");

            Assert.AreEqual("", content.Recipes[0].KeyIngredient);
            Assert.AreEqual("", content.Recipes[0].Hint);
            Assert.IsFalse(content.Recipes[0].Secret);
            Assert.AreEqual("", content.Products[0].LootTable);
            Assert.AreEqual(0, content.Products[1].Contents.Length);
            Assert.AreEqual("loot.basic", content.Products[1].LootTable);
        }

        [Test]
        public void MissingSections_BecomeEmpty_NotNull()
        {
            ContentJson content = ContentJsonReader.Read(@"{ ""version"": 1 }");

            Assert.AreEqual(1, content.Version);
            Assert.IsNotNull(content.Config);
            Assert.IsNotNull(content.Config.Economy);
            Assert.AreEqual(0, content.Ingredients.Length);
            Assert.AreEqual(0, content.Unlocks.Length);
        }

        [Test]
        public void SecretRecipe_KeepsKeyIngredientAndHint()
        {
            ContentJson content = ContentJsonReader.Read(@"{
                ""recipes"": [ { ""id"": ""bread.almond_croissant"", ""secret"": true,
                    ""keyIngredient"": ""ing.almond"", ""hint"": ""고소한 무언가"" } ]
            }");

            Assert.IsTrue(content.Recipes[0].Secret);
            Assert.AreEqual("ing.almond", content.Recipes[0].KeyIngredient);
            Assert.AreEqual("고소한 무언가", content.Recipes[0].Hint);
        }

        [Test]
        public void CustomerProfile_IsRead_WithCurveAndPreferences()
        {
            ContentJson content = ContentJsonReader.Read(@"{
                ""customerProfiles"": [ { ""id"": ""customer.student"", ""name"": ""학생"",
                    ""spawnWeightByLevel"": [5, 5, 4], ""budgetMin"": 3000, ""budgetMax"": 8000, ""maxItems"": 2,
                    ""patienceSeconds"": 70, ""cashRatio"": 0.6,
                    ""priceSensitivity"": [[0.7, 1.0], [1.0, 0.95]],
                    ""defaultPreference"": 1,
                    ""preferences"": [ { ""id"": ""bread.redbean"", ""weight"": 5 } ] } ]
            }");

            CustomerProfileJson customer = content.CustomerProfiles[0];
            Assert.AreEqual(3, customer.SpawnWeightByLevel.Length);
            Assert.AreEqual(4f, customer.SpawnWeightByLevel[2], 0.001f);
            Assert.AreEqual(2, customer.PriceSensitivity.Length);
            Assert.AreEqual(0.95f, customer.PriceSensitivity[1].Y, 0.001f);
            Assert.AreEqual("bread.redbean", customer.Preferences[0].Id);
            Assert.AreEqual(5f, customer.Preferences[0].Weight, 0.001f);
            Assert.AreEqual(0f, customer.TheftChance, 0.001f);
        }

        [Test]
        public void UnknownKey_IsAnError_NamingTheKey()
        {
            // marketPrice의 오타. 조용히 무시되면 시세가 0인 빵이 만들어진다.
            var exception = Assert.Throws<FormatException>(() => ContentJsonReader.Read(@"{
                ""recipes"": [ { ""id"": ""bread.white"", ""marketPirce"": 3500 } ]
            }"));

            StringAssert.Contains("marketPirce", exception.Message);
        }

        [Test]
        public void PointWithWrongShape_IsAnError()
        {
            var exception = Assert.Throws<FormatException>(() => ContentJsonReader.Read(@"{
                ""config"": { ""day"": { ""spawnCurve"": [[540, 0.6, 1.0]] } }
            }"));

            StringAssert.Contains("[x, y]", exception.Message);
        }

        [Test]
        public void BrokenJson_IsAnError()
        {
            Assert.Throws<FormatException>(() => ContentJsonReader.Read(@"{ ""recipes"": [ { ""id"": ""bread.white"" "));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void EmptyText_IsAnError(string text)
        {
            Assert.Throws<FormatException>(() => ContentJsonReader.Read(text));
        }

        [Test]
        public void ReadResult_CanBuildADatabase()
        {
            // 읽은 결과가 M2-05의 CreateInMemory로 그대로 이어진다.
            ContentJson content = ContentJsonReader.Read(@"{
                ""ingredients"": [ { ""id"": ""ing.flour"", ""name"": ""밀가루"", ""rarity"": ""Common"", ""resellPrice"": 300 } ],
                ""unlocks"": [ { ""id"": ""unlock.tray.4"", ""name"": ""트레이"", ""kind"": ""Tray"", ""target"": """", ""requiredLevel"": 2, ""cost"": 15000 } ]
            }");

            ContentDatabase database = ContentDatabase.CreateInMemory(content);

            Assert.AreEqual(Rarity.Common, database.GetIngredient("ing.flour").Rarity);
            Assert.AreEqual(300, database.GetIngredient("ing.flour").ResellPrice);
            Assert.AreEqual(UnlockKind.Tray, database.GetUnlock("unlock.tray.4").Kind);
            Assert.AreEqual(15000, database.GetUnlock("unlock.tray.4").Cost);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using BakeryShop.Data;
using BakeryShop.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace BakeryShop.Tests
{
    /// <summary>
    /// 실제 content.json을 확인하는 유일한 테스트. 규칙 테스트는 TestContent를 쓴다.
    /// 밸런스 수치를 바꾸면 여기서 걸릴 수 있고, 그것이 의도다: 숫자를 바꾼 사람이 문서와 에셋을 함께 맞추게 한다.
    /// </summary>
    public class RealContentTests
    {
        private ContentDatabase _fromJson;

        [SetUp]
        public void SetUp()
        {
            _fromJson = ContentImporter.BuildInMemory(File.ReadAllText(ContentImporter.JsonPath));
        }

        [TearDown]
        public void TearDown()
        {
            ContentImporter.DestroyInMemory(_fromJson);
        }

        [Test]
        public void ContentJson_PassesTheValidator()
        {
            List<string> problems = ContentValidator.Validate(_fromJson);

            Assert.AreEqual(0, problems.Count, "content.json의 문제:\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void GeneratedAssets_MatchContentJson()
        {
            // content.json을 고치고 '콘텐츠 가져오기'를 잊으면 이 테스트가 실패한다.
            var asset = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentImporter.DatabasePath);
            Assert.IsNotNull(
                asset,
                ContentImporter.DatabasePath + "이 없습니다. 메뉴 BakeryShop > 콘텐츠 가져오기를 실행하세요.");

            List<string> differences = ContentImporter.Diff(_fromJson, asset);

            Assert.AreEqual(
                0,
                differences.Count,
                "에셋이 content.json과 다릅니다. 메뉴 BakeryShop > 콘텐츠 가져오기를 실행하세요.\n"
                + string.Join("\n", differences.ToArray()));
        }

        [Test]
        public void GeneratedAssets_PassTheValidator()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentImporter.DatabasePath);
            Assert.IsNotNull(
                asset,
                ContentImporter.DatabasePath + "이 없습니다. 메뉴 BakeryShop > 콘텐츠 가져오기를 실행하세요.");

            List<string> problems = ContentValidator.Validate(asset);

            Assert.AreEqual(0, problems.Count, "에셋의 문제:\n" + string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void EveryRecipe_EarnsMoreThanItsIngredientsCost()
        {
            // 재료 하나의 값 = 그 재료만 든 묶음 상품의 가격 ÷ 개수. 핵심 재료는 살 수 없으므로 뺀다.
            var unitCost = new Dictionary<string, float>();
            foreach (ProductData product in _fromJson.Products)
            {
                if (product.Kind != ProductKind.IngredientPack || product.Contents.Count != 1)
                {
                    continue;
                }

                IngredientAmount content = product.Contents[0];
                float cost = product.Cost / (float)content.Count;
                float known;
                if (!unitCost.TryGetValue(content.IngredientId, out known) || cost < known)
                {
                    unitCost[content.IngredientId] = cost;
                }
            }

            foreach (RecipeData recipe in _fromJson.Recipes)
            {
                float ingredientCost = 0f;
                foreach (IngredientAmount amount in recipe.Ingredients)
                {
                    if (recipe.IsSecret && amount.IngredientId == recipe.KeyIngredientId)
                    {
                        continue;
                    }

                    Assert.IsTrue(
                        unitCost.ContainsKey(amount.IngredientId),
                        recipe.Id + "의 재료 " + amount.IngredientId + "를 살 수 있는 묶음 상품이 없습니다.");
                    ingredientCost += unitCost[amount.IngredientId] * amount.Count;
                }

                int batchRevenue = recipe.MarketPrice * recipe.Yield;
                Assert.Greater(
                    (float)batchRevenue,
                    ingredientCost,
                    recipe.Id + ": 한 판 매출(" + batchRevenue + ")이 재료비(" + ingredientCost + ")보다 커야 합니다.");
            }
        }

        [Test]
        public void UnlockCosts_AddUpToTheDocumentedTotal()
        {
            // 08 문서(성장과 경제)의 "모든 해금을 사는 데 드는 돈"과 같은 값이다. 수치를 바꾸면 문서도 함께 고친다.
            Assert.AreEqual(630000, _fromJson.Unlocks.Sum(unlock => unlock.Cost));
        }

        [Test]
        public void Diff_OfTheSameContent_IsEmpty()
        {
            ContentDatabase second = ContentImporter.BuildInMemory(File.ReadAllText(ContentImporter.JsonPath));
            try
            {
                List<string> differences = ContentImporter.Diff(_fromJson, second);

                Assert.AreEqual(0, differences.Count, string.Join("\n", differences.ToArray()));
            }
            finally
            {
                ContentImporter.DestroyInMemory(second);
            }
        }

        [Test]
        public void Diff_ReportsAChangedNumber_WithTheId()
        {
            // 크루아상의 시세만 바꾼 콘텐츠와 비교하면, 그 한 줄만 다르다고 나와야 한다.
            ContentJson changedJson = ContentJsonReader.Read(File.ReadAllText(ContentImporter.JsonPath));
            RecipeJson croissant = changedJson.Recipes.First(recipe => recipe.Id == "bread.croissant");
            croissant.MarketPrice += 100;
            ContentDatabase changed = ContentDatabase.CreateInMemory(changedJson);

            try
            {
                List<string> differences = ContentImporter.Diff(_fromJson, changed);

                Assert.AreEqual(1, differences.Count, string.Join("\n", differences.ToArray()));
                StringAssert.Contains("bread.croissant", differences[0]);
                StringAssert.Contains("_marketPrice", differences[0]);
            }
            finally
            {
                ContentImporter.DestroyInMemory(changed);
            }
        }

        [Test]
        public void Diff_ReportsAMissingDefinition()
        {
            ContentJson shorterJson = ContentJsonReader.Read(File.ReadAllText(ContentImporter.JsonPath));
            shorterJson.Unlocks = shorterJson.Unlocks.Skip(1).ToArray();
            ContentDatabase shorter = ContentDatabase.CreateInMemory(shorterJson);

            try
            {
                List<string> differences = ContentImporter.Diff(_fromJson, shorter);

                Assert.AreEqual(1, differences.Count, string.Join("\n", differences.ToArray()));
                StringAssert.Contains(_fromJson.Unlocks[0].Id, differences[0]);
            }
            finally
            {
                ContentImporter.DestroyInMemory(shorter);
            }
        }
    }
}

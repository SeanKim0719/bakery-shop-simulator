using System;
using System.Collections.Generic;

namespace BakeryShop.Data
{
    /// <summary>
    /// 콘텐츠가 서로 맞물리는지 확인한다 (가리키는 ID가 있는지, 수치가 말이 되는지).
    /// 임포터가 가져올 때마다 실행하고, 테스트가 실제 content.json에 대해 실행한다.
    /// 오타 하나로 "이 빵만 안 구워져요" 같은 버그가 게임 중에 나오는 것을 가져오는 시점에 막는다.
    /// </summary>
    public static class ContentValidator
    {
        /// <summary>문제를 사람이 읽을 문장으로 돌려준다. 비어 있으면 통과.</summary>
        public static List<string> Validate(ContentDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            var problems = new List<string>();

            CheckIds(database.Ingredients, "재료", problems);
            CheckIds(database.Recipes, "레시피", problems);
            CheckIds(database.Products, "상품", problems);
            CheckIds(database.LootTables, "확률표", problems);
            CheckIds(database.CustomerProfiles, "손님", problems);
            CheckIds(database.Unlocks, "해금", problems);

            GameConfig config = database.Config;
            if (config == null)
            {
                problems.Add("설정(GameConfig)이 없습니다.");
                return problems;
            }

            CheckConfig(database, config, problems);
            CheckRecipes(database, problems);
            CheckProducts(database, config.LevelCount, problems);
            CheckLootTables(database, problems);
            CheckCustomers(database, config.LevelCount, problems);
            CheckUnlocks(database, config.LevelCount, problems);

            return problems;
        }

        private static void CheckIds<T>(IReadOnlyList<T> items, string kindName, List<string> problems)
            where T : ContentDefinition
        {
            var seen = new HashSet<string>();
            var reported = new HashSet<string>();

            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (item == null)
                {
                    problems.Add(kindName + " 목록의 " + (i + 1) + "번째 항목이 비어 있습니다.");
                    continue;
                }

                if (string.IsNullOrEmpty(item.Id))
                {
                    problems.Add(kindName + " 목록의 " + (i + 1) + "번째 항목: ID가 비어 있습니다.");
                    continue;
                }

                if (!seen.Add(item.Id) && reported.Add(item.Id))
                {
                    problems.Add(kindName + " ID '" + item.Id + "'가 중복됩니다.");
                }
            }
        }

        private static void CheckConfig(ContentDatabase database, GameConfig config, List<string> problems)
        {
            DayConfig day = config.Day;
            if (day.CloseMinutes <= day.OpenMinutes)
            {
                problems.Add("설정 day: closeMinutes(" + day.CloseMinutes + ")는 openMinutes(" + day.OpenMinutes + ")보다 커야 합니다.");
            }

            if (!(day.OpenRealSeconds > 0f))
            {
                problems.Add("설정 day: openRealSeconds는 0보다 커야 합니다.");
            }

            CheckCurve(day.SpawnCurve, "설정 day: spawnCurve", problems);

            ProgressionConfig progression = config.Progression;
            int levelCount = progression.XpThresholds.Count;
            if (levelCount == 0)
            {
                problems.Add("설정 progression: xpThresholds가 비어 있습니다.");
            }
            else
            {
                if (progression.XpThresholds[0] != 0)
                {
                    problems.Add("설정 progression: xpThresholds의 첫 값은 0이어야 합니다 (레벨 1).");
                }

                for (int i = 1; i < levelCount; i++)
                {
                    if (progression.XpThresholds[i] <= progression.XpThresholds[i - 1])
                    {
                        problems.Add("설정 progression: xpThresholds는 커지는 순서여야 합니다.");
                        break;
                    }
                }
            }

            EconomyConfig economy = config.Economy;
            if (economy.RentByLevel.Count != levelCount)
            {
                problems.Add("설정 economy: rentByLevel의 길이(" + economy.RentByLevel.Count + ")가 레벨 수(" + levelCount + ")와 다릅니다.");
            }

            if (config.Customers.BaseCountByLevel.Count != levelCount)
            {
                problems.Add("설정 customers: baseCountByLevel의 길이(" + config.Customers.BaseCountByLevel.Count + ")가 레벨 수(" + levelCount + ")와 다릅니다.");
            }

            if (economy.PriceStep <= 0)
            {
                problems.Add("설정 economy: priceStep은 0보다 커야 합니다.");
            }

            if (!(economy.PriceMinRatio > 0f) || !(economy.PriceMaxRatio > economy.PriceMinRatio))
            {
                problems.Add("설정 economy: 0 < priceMinRatio < priceMaxRatio여야 합니다.");
            }

            if (economy.StartTrays < 1)
            {
                problems.Add("설정 economy: startTrays는 1 이상이어야 합니다.");
            }

            for (int i = 0; i < economy.StartRecipes.Count; i++)
            {
                string recipeId = economy.StartRecipes[i];
                RecipeData recipe;
                if (!database.TryGetRecipe(recipeId, out recipe))
                {
                    problems.Add("설정 economy: startRecipes의 '" + recipeId + "'가 레시피 목록에 없습니다.");
                }
                else if (recipe.IsSecret)
                {
                    problems.Add("설정 economy: startRecipes의 '" + recipeId + "'는 비밀 레시피입니다. 처음부터 알고 있을 수 없습니다.");
                }
            }

            for (int i = 0; i < economy.StartStorage.Count; i++)
            {
                IngredientAmount amount = economy.StartStorage[i];
                IngredientData ingredient;
                if (!database.TryGetIngredient(amount.IngredientId, out ingredient))
                {
                    problems.Add("설정 economy: startStorage의 '" + amount.IngredientId + "'가 재료 목록에 없습니다.");
                }
                else if (amount.Count <= 0)
                {
                    problems.Add("설정 economy: startStorage의 '" + amount.IngredientId + "' 수량은 0보다 커야 합니다.");
                }
            }

            if (config.Shelf.SlotsPerShelf <= 0 || config.Shelf.SlotCapacity <= 0)
            {
                problems.Add("설정 shelf: slotsPerShelf와 slotCapacity는 0보다 커야 합니다.");
            }

            if (config.Oven.MinCollectProgress < 0f || config.Oven.MinCollectProgress > 1f)
            {
                problems.Add("설정 oven: minCollectProgress는 0과 1 사이여야 합니다.");
            }

            if (config.Customers.MaxConcurrent <= 0 || config.Customers.QueueSlots <= 0)
            {
                problems.Add("설정 customers: maxConcurrent와 queueSlots는 0보다 커야 합니다.");
            }

            if (config.Customers.ReputationSpawnMin > config.Customers.ReputationSpawnMax)
            {
                problems.Add("설정 customers: reputationSpawnMin이 reputationSpawnMax보다 큽니다.");
            }

            ReputationConfig reputation = config.Reputation;
            if (reputation.Min >= reputation.Max || reputation.Start < reputation.Min || reputation.Start > reputation.Max)
            {
                problems.Add("설정 reputation: min < max이고 start가 그 사이에 있어야 합니다.");
            }

            if (config.Delivery.Seconds < 0f)
            {
                problems.Add("설정 delivery: seconds는 0 이상이어야 합니다.");
            }
        }

        private static void CheckRecipes(ContentDatabase database, List<string> problems)
        {
            var experimentChoices = new HashSet<string>();
            foreach (IngredientData choice in database.ExperimentChoices)
            {
                experimentChoices.Add(choice.Id);
            }

            // 핵심 재료 ID → 그 재료를 쓰는 첫 비밀 레시피의 ID
            var keyOwners = new Dictionary<string, string>();

            foreach (RecipeData recipe in database.Recipes)
            {
                if (recipe == null || string.IsNullOrEmpty(recipe.Id))
                {
                    continue;
                }

                string label = "레시피 '" + recipe.Id + "'";

                if (recipe.Ingredients.Count == 0)
                {
                    problems.Add(label + ": 재료가 없습니다.");
                }

                // 핵심 재료 자체가 잘못되었으면 "나머지 재료" 검사는 의미가 없으므로 건너뛴다 (같은 원인으로 문제가 두 번 나오지 않게).
                IngredientData keyIngredient = null;
                bool hasValidKey = recipe.IsSecret && database.TryGetIngredient(recipe.KeyIngredientId, out keyIngredient);

                bool containsKey = false;
                for (int i = 0; i < recipe.Ingredients.Count; i++)
                {
                    IngredientAmount amount = recipe.Ingredients[i];
                    IngredientData ingredient;
                    bool exists = database.TryGetIngredient(amount.IngredientId, out ingredient);
                    if (!exists)
                    {
                        problems.Add(label + ": 재료 '" + amount.IngredientId + "'가 재료 목록에 없습니다.");
                    }

                    if (amount.Count <= 0)
                    {
                        problems.Add(label + ": 재료 '" + amount.IngredientId + "'의 수량은 0보다 커야 합니다.");
                    }

                    if (recipe.IsSecret && amount.IngredientId == recipe.KeyIngredientId)
                    {
                        containsKey = true;
                    }
                    else if (hasValidKey && exists && !experimentChoices.Contains(amount.IngredientId))
                    {
                        // 실험에서는 발주할 수 있는 재료만 고를 수 있다. 핵심 재료 말고 다른 박스 전용 재료가 필요하면 발견할 길이 없다.
                        problems.Add(label + ": 재료 '" + amount.IngredientId + "'는 발주할 수 없어서 실험에서 고를 수 없습니다 (핵심 재료가 아닌 재료는 발주 상품에 있어야 합니다).");
                    }
                }

                if (recipe.Yield <= 0)
                {
                    problems.Add(label + ": yield는 0보다 커야 합니다.");
                }

                if (recipe.MarketPrice <= 0)
                {
                    problems.Add(label + ": marketPrice는 0보다 커야 합니다.");
                }

                if (!(recipe.BakeSeconds > 0f))
                {
                    problems.Add(label + ": bakeSeconds는 0보다 커야 합니다.");
                }

                if (recipe.PerfectWindowSeconds < 0f || !(recipe.BurnAfterSeconds > recipe.PerfectWindowSeconds))
                {
                    problems.Add(label + ": 0 ≤ perfectWindowSeconds < burnAfterSeconds여야 합니다.");
                }

                if (recipe.ShelfLifeDays < 1)
                {
                    problems.Add(label + ": shelfLifeDays는 1 이상이어야 합니다.");
                }

                if (!recipe.IsSecret)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(recipe.KeyIngredientId))
                {
                    problems.Add(label + ": 비밀 레시피인데 keyIngredient가 없습니다.");
                }
                else if (!hasValidKey)
                {
                    problems.Add(label + ": 핵심 재료 '" + recipe.KeyIngredientId + "'가 재료 목록에 없습니다.");
                }
                else
                {
                    if (!containsKey)
                    {
                        problems.Add(label + ": 핵심 재료 '" + recipe.KeyIngredientId + "'가 이 레시피의 재료에 들어 있지 않습니다.");
                    }

                    string owner;
                    if (keyOwners.TryGetValue(recipe.KeyIngredientId, out owner))
                    {
                        problems.Add(label + ": 핵심 재료 '" + recipe.KeyIngredientId + "'를 레시피 '" + owner + "'도 쓰고 있습니다. 핵심 재료 하나는 레시피 하나에만 씁니다.");
                    }
                    else
                    {
                        keyOwners.Add(recipe.KeyIngredientId, recipe.Id);
                    }
                }
            }
        }

        private static void CheckProducts(ContentDatabase database, int levelCount, List<string> problems)
        {
            foreach (ProductData product in database.Products)
            {
                if (product == null || string.IsNullOrEmpty(product.Id))
                {
                    continue;
                }

                string label = "상품 '" + product.Id + "'";

                if (product.Cost <= 0)
                {
                    problems.Add(label + ": cost는 0보다 커야 합니다.");
                }

                CheckRequiredLevel(product.RequiredLevel, levelCount, label, problems);

                if (product.Kind == ProductKind.IngredientPack)
                {
                    if (product.Contents.Count == 0)
                    {
                        problems.Add(label + ": 재료 묶음인데 contents가 비어 있습니다.");
                    }

                    for (int i = 0; i < product.Contents.Count; i++)
                    {
                        IngredientAmount amount = product.Contents[i];
                        IngredientData ingredient;
                        if (!database.TryGetIngredient(amount.IngredientId, out ingredient))
                        {
                            problems.Add(label + ": 내용물 '" + amount.IngredientId + "'가 재료 목록에 없습니다.");
                        }
                        else if (amount.Count <= 0)
                        {
                            problems.Add(label + ": 내용물 '" + amount.IngredientId + "'의 수량은 0보다 커야 합니다.");
                        }
                    }
                }
                else
                {
                    LootTable lootTable;
                    if (string.IsNullOrEmpty(product.LootTableId))
                    {
                        problems.Add(label + ": 미스터리 박스인데 lootTable이 없습니다.");
                    }
                    else if (!database.TryGetLootTable(product.LootTableId, out lootTable))
                    {
                        problems.Add(label + ": 확률표 '" + product.LootTableId + "'가 확률표 목록에 없습니다.");
                    }
                }
            }
        }

        private static void CheckLootTables(ContentDatabase database, List<string> problems)
        {
            foreach (LootTable table in database.LootTables)
            {
                if (table == null || string.IsNullOrEmpty(table.Id))
                {
                    continue;
                }

                string label = "확률표 '" + table.Id + "'";

                if (table.RollsPerBox <= 0)
                {
                    problems.Add(label + ": rollsPerBox는 0보다 커야 합니다.");
                }

                if (table.PityThreshold <= 0)
                {
                    problems.Add(label + ": pityThreshold는 0보다 커야 합니다.");
                }

                float totalWeight = 0f;
                bool hasGuaranteedEntry = false;
                bool allIngredientsExist = true;

                for (int i = 0; i < table.Entries.Count; i++)
                {
                    LootEntry entry = table.Entries[i];
                    IngredientData ingredient;
                    if (!database.TryGetIngredient(entry.IngredientId, out ingredient))
                    {
                        problems.Add(label + ": 재료 '" + entry.IngredientId + "'가 재료 목록에 없습니다.");
                        allIngredientsExist = false;
                        continue;
                    }

                    if (entry.Weight < 0f)
                    {
                        problems.Add(label + ": 재료 '" + entry.IngredientId + "'의 weight가 음수입니다.");
                        continue;
                    }

                    if (entry.Count <= 0)
                    {
                        problems.Add(label + ": 재료 '" + entry.IngredientId + "'의 count는 0보다 커야 합니다.");
                    }

                    totalWeight += entry.Weight;
                    if (entry.Weight > 0f && ingredient.Rarity >= table.PityMinRarity)
                    {
                        hasGuaranteedEntry = true;
                    }
                }

                if (!(totalWeight > 0f))
                {
                    problems.Add(label + ": 가중치의 합이 0보다 커야 합니다.");
                }
                else if (!hasGuaranteedEntry && allIngredientsExist)
                {
                    problems.Add(label + ": 천장이 보장할 항목이 없습니다 (" + table.PityMinRarity + " 이상의 재료가 하나는 있어야 합니다).");
                }
            }
        }

        private static void CheckCustomers(ContentDatabase database, int levelCount, List<string> problems)
        {
            foreach (CustomerProfile profile in database.CustomerProfiles)
            {
                if (profile == null || string.IsNullOrEmpty(profile.Id))
                {
                    continue;
                }

                string label = "손님 '" + profile.Id + "'";

                if (profile.SpawnWeightByLevel.Count != levelCount)
                {
                    problems.Add(label + ": spawnWeightByLevel의 길이(" + profile.SpawnWeightByLevel.Count + ")가 레벨 수(" + levelCount + ")와 다릅니다.");
                }

                if (profile.BudgetMin > profile.BudgetMax)
                {
                    problems.Add(label + ": budgetMin(" + profile.BudgetMin + ")이 budgetMax(" + profile.BudgetMax + ")보다 큽니다.");
                }

                if (profile.MaxItems <= 0)
                {
                    problems.Add(label + ": maxItems는 0보다 커야 합니다.");
                }

                if (!(profile.PatienceSeconds > 0f))
                {
                    problems.Add(label + ": patienceSeconds는 0보다 커야 합니다.");
                }

                if (profile.CashRatio < 0f || profile.CashRatio > 1f)
                {
                    problems.Add(label + ": cashRatio는 0과 1 사이여야 합니다.");
                }

                CheckCurve(profile.PriceSensitivity, label + ": priceSensitivity", problems);

                for (int i = 0; i < profile.Preferences.Count; i++)
                {
                    RecipeData recipe;
                    if (!database.TryGetRecipe(profile.Preferences[i].RecipeId, out recipe))
                    {
                        problems.Add(label + ": 선호 목록의 '" + profile.Preferences[i].RecipeId + "'가 레시피 목록에 없습니다.");
                    }
                }
            }
        }

        private static void CheckUnlocks(ContentDatabase database, int levelCount, List<string> problems)
        {
            foreach (UnlockData unlock in database.Unlocks)
            {
                if (unlock == null || string.IsNullOrEmpty(unlock.Id))
                {
                    continue;
                }

                string label = "해금 '" + unlock.Id + "'";

                if (unlock.Cost <= 0)
                {
                    problems.Add(label + ": cost는 0보다 커야 합니다.");
                }

                CheckRequiredLevel(unlock.RequiredLevel, levelCount, label, problems);

                if (unlock.Kind == UnlockKind.RecipeLicense)
                {
                    RecipeData recipe;
                    if (!database.TryGetRecipe(unlock.TargetId, out recipe))
                    {
                        problems.Add(label + ": 대상 레시피 '" + unlock.TargetId + "'가 레시피 목록에 없습니다.");
                    }
                    else if (recipe.IsSecret)
                    {
                        problems.Add(label + ": 대상 '" + unlock.TargetId + "'는 비밀 레시피입니다. 비밀 레시피는 실험으로만 얻습니다.");
                    }
                }
                else if (unlock.Kind == UnlockKind.Furniture && string.IsNullOrEmpty(unlock.TargetId))
                {
                    problems.Add(label + ": 가구 해금인데 target이 비어 있습니다.");
                }
            }
        }

        private static void CheckRequiredLevel(int requiredLevel, int levelCount, string label, List<string> problems)
        {
            if (requiredLevel < 1 || requiredLevel > levelCount)
            {
                problems.Add(label + ": requiredLevel(" + requiredLevel + ")이 1 ~ " + levelCount + " 범위를 벗어납니다.");
            }
        }

        // PiecewiseCurve는 점이 X가 커지는 순서로 들어 있다고 가정한다.
        private static void CheckCurve(IReadOnlyList<CurvePoint> points, string label, List<string> problems)
        {
            if (points.Count == 0)
            {
                problems.Add(label + "에 점이 없습니다.");
                return;
            }

            for (int i = 1; i < points.Count; i++)
            {
                if (points[i].X <= points[i - 1].X)
                {
                    problems.Add(label + "의 x가 커지는 순서가 아닙니다 (" + (i + 1) + "번째 점).");
                    return;
                }
            }
        }
    }
}

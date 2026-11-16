using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using BakeryShop.Data;
using UnityEditor;
using UnityEngine;

namespace BakeryShop.EditorTools
{
    /// <summary>
    /// content.json을 읽어 Data/Generated의 ScriptableObject를 만들거나 갱신한다.
    /// 메뉴: BakeryShop > 콘텐츠 가져오기
    /// ID가 같은 에셋은 수치만 덮어쓰고, 인스펙터에서 연결한 아이콘·프리팹은 그대로 둔다.
    /// 이유는 ADR 0009: 수치의 원본을 JSON 하나로 두면 밸런스 시뮬레이터와 게임이 같은 숫자를 쓴다.
    /// </summary>
    public static class ContentImporter
    {
        public const string JsonPath = "Assets/_Project/Data/content.json";
        public const string OutputFolder = "Assets/_Project/Data/Generated";
        public const string DatabasePath = OutputFolder + "/ContentDatabase.asset";
        public const string ConfigPath = OutputFolder + "/GameConfig.asset";

        // ID를 파일 이름으로 쓴다 (bread.croissant.asset). 파일 이름에 쓸 수 없는 글자를 미리 막는다.
        private static readonly Regex IdPattern = new Regex("^[a-z0-9_.]+$");

        private sealed class Counter
        {
            public int Created;
            public int Updated;
            public int Unchanged;
        }

        [MenuItem("BakeryShop/콘텐츠 가져오기")]
        public static void Import()
        {
            ContentJson json;
            try
            {
                json = ContentJsonReader.Read(File.ReadAllText(JsonPath));

                // 에셋을 건드리기 전에 메모리에서 한 번 만들어 본다.
                // "rarity": "Epic" 같은 값은 Apply에서 걸리는데, 그때 에셋이 절반만 바뀐 상태로 남지 않게 한다.
                DestroyInMemory(ContentDatabase.CreateInMemory(json));
            }
            catch (Exception exception) when (exception is FormatException || exception is IOException)
            {
                Debug.LogError("[콘텐츠 가져오기] 중단: " + exception.Message);
                return;
            }

            if (!CheckIds(json))
            {
                return;
            }

            EnsureFolder(OutputFolder);
            var counter = new Counter();

            GameConfig config = LoadOrCreate<GameConfig>(ConfigPath, counter, asset => asset.Apply(json.Config));
            List<IngredientData> ingredients = ImportList<IngredientData, IngredientJson>(
                json.Ingredients, "Ingredients", item => item.Id, (asset, item) => asset.Apply(item), counter);
            List<RecipeData> recipes = ImportList<RecipeData, RecipeJson>(
                json.Recipes, "Recipes", item => item.Id, (asset, item) => asset.Apply(item), counter);
            List<ProductData> products = ImportList<ProductData, ProductJson>(
                json.Products, "Products", item => item.Id, (asset, item) => asset.Apply(item), counter);
            List<LootTable> lootTables = ImportList<LootTable, LootTableJson>(
                json.LootTables, "LootTables", item => item.Id, (asset, item) => asset.Apply(item), counter);
            List<CustomerProfile> customerProfiles = ImportList<CustomerProfile, CustomerProfileJson>(
                json.CustomerProfiles, "CustomerProfiles", item => item.Id, (asset, item) => asset.Apply(item), counter);
            List<UnlockData> unlocks = ImportList<UnlockData, UnlockJson>(
                json.Unlocks, "Unlocks", item => item.Id, (asset, item) => asset.Apply(item), counter);

            ContentDatabase database = LoadOrCreate<ContentDatabase>(
                DatabasePath,
                counter,
                asset => asset.SetContent(config, ingredients, recipes, products, lootTables, customerProfiles, unlocks));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            List<string> problems = ContentValidator.Validate(database);
            foreach (string problem in problems)
            {
                Debug.LogError("[콘텐츠 검증] " + problem, database);
            }

            Debug.Log(
                "[콘텐츠 가져오기] 완료: 만든 에셋 " + counter.Created + ", 바꾼 에셋 " + counter.Updated
                + ", 그대로인 에셋 " + counter.Unchanged + ", 검증 문제 " + problems.Count + "개",
                database);
        }

        /// <summary>에셋 파일을 만들지 않고 메모리에만 데이터베이스를 만든다 (테스트용). 다 쓰면 DestroyInMemory로 지운다.</summary>
        public static ContentDatabase BuildInMemory(string jsonText)
        {
            return ContentDatabase.CreateInMemory(ContentJsonReader.Read(jsonText));
        }

        /// <summary>BuildInMemory로 만든 데이터베이스와 그 안의 정의를 지운다. 에셋 파일에서 불러온 것은 건드리지 않는다.</summary>
        public static void DestroyInMemory(ContentDatabase database)
        {
            if (database == null)
            {
                return;
            }

            DestroyIfInMemory(database.Config);
            DestroyAllInMemory(database.Ingredients);
            DestroyAllInMemory(database.Recipes);
            DestroyAllInMemory(database.Products);
            DestroyAllInMemory(database.LootTables);
            DestroyAllInMemory(database.CustomerProfiles);
            DestroyAllInMemory(database.Unlocks);
            DestroyIfInMemory(database);
        }

        /// <summary>
        /// 두 데이터베이스의 수치가 다른 곳을 문장으로 돌려준다. 비어 있으면 같다.
        /// 아이콘·프리팹 같은 외형 필드는 비교하지 않는다.
        /// </summary>
        public static List<string> Diff(ContentDatabase expected, ContentDatabase actual)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            if (actual == null)
            {
                throw new ArgumentNullException(nameof(actual));
            }

            var differences = new List<string>();

            if (expected.Config == null || actual.Config == null)
            {
                if (expected.Config != actual.Config)
                {
                    differences.Add("설정(GameConfig)이 한쪽에만 있습니다.");
                }
            }
            else
            {
                DiffObject("설정", expected.Config, actual.Config, differences);
            }

            DiffList("재료", expected.Ingredients, actual.Ingredients, differences);
            DiffList("레시피", expected.Recipes, actual.Recipes, differences);
            DiffList("상품", expected.Products, actual.Products, differences);
            DiffList("확률표", expected.LootTables, actual.LootTables, differences);
            DiffList("손님", expected.CustomerProfiles, actual.CustomerProfiles, differences);
            DiffList("해금", expected.Unlocks, actual.Unlocks, differences);

            return differences;
        }

        private static bool CheckIds(ContentJson json)
        {
            var problems = new List<string>();
            CheckIdList("재료", json.Ingredients, item => item.Id, problems);
            CheckIdList("레시피", json.Recipes, item => item.Id, problems);
            CheckIdList("상품", json.Products, item => item.Id, problems);
            CheckIdList("확률표", json.LootTables, item => item.Id, problems);
            CheckIdList("손님", json.CustomerProfiles, item => item.Id, problems);
            CheckIdList("해금", json.Unlocks, item => item.Id, problems);

            foreach (string problem in problems)
            {
                Debug.LogError("[콘텐츠 가져오기] 중단: " + problem);
            }

            return problems.Count == 0;
        }

        private static void CheckIdList<T>(string kindName, T[] items, Func<T, string> idOf, List<string> problems)
        {
            var seen = new HashSet<string>();
            foreach (T item in items ?? new T[0])
            {
                string id = idOf(item);
                if (string.IsNullOrEmpty(id) || !IdPattern.IsMatch(id))
                {
                    problems.Add(kindName + " ID '" + id + "'는 쓸 수 없습니다. 영어 소문자, 숫자, 밑줄, 점만 씁니다 (ID가 에셋 파일 이름이 됩니다).");
                }
                else if (!seen.Add(id))
                {
                    problems.Add(kindName + " ID '" + id + "'가 중복됩니다.");
                }
            }
        }

        private static List<TAsset> ImportList<TAsset, TJson>(
            TJson[] items,
            string folderName,
            Func<TJson, string> idOf,
            Action<TAsset, TJson> apply,
            Counter counter)
            where TAsset : ContentDefinition
        {
            string folder = OutputFolder + "/" + folderName;
            EnsureFolder(folder);

            var assets = new List<TAsset>();
            var ids = new HashSet<string>();
            foreach (TJson item in items ?? new TJson[0])
            {
                TJson current = item;
                string id = idOf(current);
                ids.Add(id);
                assets.Add(LoadOrCreate<TAsset>(folder + "/" + id + ".asset", counter, asset => apply(asset, current)));
            }

            // JSON에서 사라진 ID의 에셋은 지우지 않는다 (연결해 둔 아이콘·프리팹이 함께 사라지므로). 알려만 준다.
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(TAsset).Name, new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!ids.Contains(Path.GetFileNameWithoutExtension(path)))
                {
                    Debug.LogWarning(
                        "[콘텐츠 가져오기] content.json에 없는 에셋이 남아 있습니다: " + path + " (필요 없으면 직접 지우세요)",
                        AssetDatabase.LoadMainAssetAtPath(path));
                }
            }

            return assets;
        }

        private static T LoadOrCreate<T>(string path, Counter counter, Action<T> apply) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                apply(asset);
                AssetDatabase.CreateAsset(asset, path);
                counter.Created++;
                return asset;
            }

            // 바뀐 에셋만 저장 대상으로 표시한다. 수치가 그대로면 파일도 그대로여서 Git에 변경이 생기지 않는다.
            string before = EditorJsonUtility.ToJson(asset);
            apply(asset);
            string after = EditorJsonUtility.ToJson(asset);

            if (before == after)
            {
                counter.Unchanged++;
            }
            else
            {
                EditorUtility.SetDirty(asset);
                counter.Updated++;
            }

            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static void DestroyAllInMemory<T>(IReadOnlyList<T> items) where T : UnityEngine.Object
        {
            for (int i = 0; i < items.Count; i++)
            {
                DestroyIfInMemory(items[i]);
            }
        }

        private static void DestroyIfInMemory(UnityEngine.Object target)
        {
            if (target != null && !EditorUtility.IsPersistent(target))
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void DiffList<T>(string kindName, IReadOnlyList<T> expected, IReadOnlyList<T> actual, List<string> differences)
            where T : ContentDefinition
        {
            var actualById = new Dictionary<string, T>();
            foreach (T item in actual)
            {
                if (item != null && !actualById.ContainsKey(item.Id))
                {
                    actualById.Add(item.Id, item);
                }
            }

            bool sameMembers = true;
            var expectedIds = new HashSet<string>();
            foreach (T item in expected)
            {
                expectedIds.Add(item.Id);

                T other;
                if (!actualById.TryGetValue(item.Id, out other))
                {
                    differences.Add(kindName + " '" + item.Id + "': 에셋이 없습니다.");
                    sameMembers = false;
                    continue;
                }

                DiffObject(kindName + " '" + item.Id + "'", item, other, differences);
            }

            foreach (T item in actual)
            {
                if (item == null)
                {
                    differences.Add(kindName + " 목록에 빈 항목이 있습니다 (에셋이 지워졌습니다).");
                    sameMembers = false;
                }
                else if (!expectedIds.Contains(item.Id))
                {
                    differences.Add(kindName + " '" + item.Id + "': content.json에 없는 정의가 데이터베이스에 들어 있습니다.");
                    sameMembers = false;
                }
            }

            if (!sameMembers || expected.Count != actual.Count)
            {
                return;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                if (expected[i].Id != actual[i].Id)
                {
                    differences.Add(kindName + " 목록의 순서가 content.json과 다릅니다.");
                    return;
                }
            }
        }

        private static void DiffObject(string label, UnityEngine.Object expected, UnityEngine.Object actual, List<string> differences)
        {
            Dictionary<string, string> expectedValues = ReadValues(expected);
            Dictionary<string, string> actualValues = ReadValues(actual);

            foreach (KeyValuePair<string, string> pair in expectedValues)
            {
                string actualValue;
                if (!actualValues.TryGetValue(pair.Key, out actualValue))
                {
                    differences.Add(label + ": " + pair.Key + "가 에셋에 없습니다 (content.json: " + pair.Value + ").");
                }
                else if (actualValue != pair.Value)
                {
                    differences.Add(label + ": " + pair.Key + "가 다릅니다 (content.json: " + pair.Value + ", 에셋: " + actualValue + ").");
                }
            }

            foreach (KeyValuePair<string, string> pair in actualValues)
            {
                if (!expectedValues.ContainsKey(pair.Key))
                {
                    differences.Add(label + ": " + pair.Key + "가 content.json 쪽에 없습니다 (에셋: " + pair.Value + ").");
                }
            }
        }

        /// <summary>
        /// 직렬화된 수치 필드를 "경로 → 값"으로 모은다. 필드를 하나씩 코드에 적지 않으므로,
        /// 정의에 필드를 추가해도 비교가 자동으로 따라온다.
        /// </summary>
        private static Dictionary<string, string> ReadValues(UnityEngine.Object target)
        {
            var values = new Dictionary<string, string>();
            using (var serialized = new SerializedObject(target))
            {
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = false;
                    string path = property.propertyPath;

                    // m_Script, m_Name 같은 Unity의 기본 필드.
                    if (path.StartsWith("m_", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    switch (property.propertyType)
                    {
                        case SerializedPropertyType.Integer:
                            values[path] = property.longValue.ToString(CultureInfo.InvariantCulture);
                            break;
                        case SerializedPropertyType.ArraySize:
                            values[path] = property.intValue.ToString(CultureInfo.InvariantCulture);
                            break;
                        case SerializedPropertyType.Boolean:
                            values[path] = property.boolValue ? "true" : "false";
                            break;
                        case SerializedPropertyType.Float:
                            values[path] = property.floatValue.ToString("R", CultureInfo.InvariantCulture);
                            break;
                        case SerializedPropertyType.String:
                            values[path] = "\"" + property.stringValue + "\"";
                            break;
                        case SerializedPropertyType.Enum:
                            values[path] = property.intValue.ToString(CultureInfo.InvariantCulture);
                            break;
                        case SerializedPropertyType.ObjectReference:
                            // 외형 필드 (아이콘, 프리팹). 비교하지 않는다.
                            break;
                        case SerializedPropertyType.Generic:
                            // 구조체, 묶음 클래스, 배열. 외형의 배열(프리팹 목록)은 들어가지 않는다.
                            enterChildren = !IsObjectReferenceArray(property);
                            break;
                        default:
                            enterChildren = property.hasChildren;
                            break;
                    }
                }
            }

            return values;
        }

        private static bool IsObjectReferenceArray(SerializedProperty property)
        {
            return property.isArray && property.arrayElementType.StartsWith("PPtr<", StringComparison.Ordinal);
        }
    }
}

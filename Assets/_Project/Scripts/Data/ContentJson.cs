using System;

namespace BakeryShop.Data
{
    // content.json의 모양을 그대로 옮긴 단순 클래스들이다.
    // 에디터의 임포터가 JSON을 이 타입으로 읽고, 각 정의의 Apply가 여기서 값을 가져간다.
    // 테스트는 JSON 문자열 없이 이 객체를 직접 만들어 작은 콘텐츠를 구성한다 (TestContent).
    // 런타임 어셈블리에 있지만 게임 실행 중에는 쓰지 않는다.
    // 필드 이름은 JSON의 키와 같다 (대소문자는 읽을 때 구분하지 않는다).

    public sealed class ContentJson
    {
        public int Version = 1;
        public ConfigJson Config = new ConfigJson();
        public IngredientJson[] Ingredients = new IngredientJson[0];
        public RecipeJson[] Recipes = new RecipeJson[0];
        public ProductJson[] Products = new ProductJson[0];
        public LootTableJson[] LootTables = new LootTableJson[0];
        public CustomerProfileJson[] CustomerProfiles = new CustomerProfileJson[0];
        public UnlockJson[] Unlocks = new UnlockJson[0];

        /// <summary>"Rare" 같은 문자열을 enum으로 바꾼다. 모르는 값이면 어느 정의의 어느 필드인지 알려 준다.</summary>
        internal static T ParseEnum<T>(string value, string ownerId, string fieldName) where T : struct
        {
            T parsed;
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, false, out parsed) && Enum.IsDefined(typeof(T), parsed))
            {
                return parsed;
            }

            throw new FormatException(
                "'" + ownerId + "'의 " + fieldName + " 값 '" + value + "'을(를) 읽을 수 없습니다. 쓸 수 있는 값: "
                + string.Join(", ", Enum.GetNames(typeof(T))));
        }

        internal static IngredientAmount[] ToAmounts(AmountJson[] source)
        {
            if (source == null)
            {
                return new IngredientAmount[0];
            }

            var result = new IngredientAmount[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = new IngredientAmount(source[i].Id ?? "", source[i].Count);
            }

            return result;
        }

        /// <summary>배열을 복사한다 (null이면 빈 배열). 정의가 JSON 객체의 배열을 그대로 붙잡지 않게 한다.</summary>
        internal static T[] Copy<T>(T[] source)
        {
            if (source == null)
            {
                return new T[0];
            }

            var result = new T[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }
    }

    public sealed class AmountJson
    {
        public string Id = "";
        public int Count;

        public AmountJson()
        {
        }

        public AmountJson(string id, int count)
        {
            Id = id;
            Count = count;
        }
    }

    public sealed class IngredientJson
    {
        public string Id = "";
        public string Name = "";
        public string Rarity = "Common";
        public int ResellPrice;
    }

    public sealed class RecipeJson
    {
        public string Id = "";
        public string Name = "";
        public bool Secret;
        public string KeyIngredient = "";
        public string Hint = "";
        public AmountJson[] Ingredients = new AmountJson[0];
        public int Yield;
        public int MarketPrice;
        public float BakeSeconds;
        public float PerfectWindowSeconds;
        public float BurnAfterSeconds;
        public int ShelfLifeDays;
    }

    public sealed class ProductJson
    {
        public string Id = "";
        public string Name = "";
        public string Kind = "IngredientPack";
        public int Cost;
        public int RequiredLevel = 1;
        public AmountJson[] Contents = new AmountJson[0];
        public string LootTable = "";
    }

    public sealed class LootEntryJson
    {
        public string Id = "";
        public float Weight;
        public int Count = 1;

        public LootEntryJson()
        {
        }

        public LootEntryJson(string id, float weight, int count)
        {
            Id = id;
            Weight = weight;
            Count = count;
        }
    }

    public sealed class LootTableJson
    {
        public string Id = "";
        public int RollsPerBox;
        public int PityThreshold;
        public string PityMinRarity = "Rare";
        public LootEntryJson[] Entries = new LootEntryJson[0];
    }

    public sealed class RecipeWeightJson
    {
        public string Id = "";
        public float Weight;

        public RecipeWeightJson()
        {
        }

        public RecipeWeightJson(string id, float weight)
        {
            Id = id;
            Weight = weight;
        }
    }

    public sealed class CustomerProfileJson
    {
        public string Id = "";
        public string Name = "";
        public float[] SpawnWeightByLevel = new float[0];
        public int BudgetMin;
        public int BudgetMax;
        public int MaxItems;
        public float PatienceSeconds;
        public float CashRatio;
        public CurvePoint[] PriceSensitivity = new CurvePoint[0];
        public float DefaultPreference = 1f;
        public RecipeWeightJson[] Preferences = new RecipeWeightJson[0];
        public float TheftChance;
        public float Messiness;
    }

    public sealed class UnlockJson
    {
        public string Id = "";
        public string Name = "";
        public string Kind = "Tray";
        public string Target = "";
        public int RequiredLevel = 1;
        public int Cost;
    }

    public sealed class ConfigJson
    {
        public DayJson Day = new DayJson();
        public EconomyJson Economy = new EconomyJson();
        public OvenJson Oven = new OvenJson();
        public ShelfJson Shelf = new ShelfJson();
        public CustomersJson Customers = new CustomersJson();
        public ReputationJson Reputation = new ReputationJson();
        public ProgressionJson Progression = new ProgressionJson();
        public DeliveryJson Delivery = new DeliveryJson();
    }

    public sealed class DayJson
    {
        public int OpenMinutes;
        public int CloseMinutes;
        public float OpenRealSeconds;
        public CurvePoint[] SpawnCurve = new CurvePoint[0];
    }

    public sealed class QualityMultiplierJson
    {
        public float Undercooked;
        public float Normal;
        public float Perfect;
    }

    public sealed class EconomyJson
    {
        public int StartMoney;
        public int MoneyFloor;
        public int[] RentByLevel = new int[0];
        public QualityMultiplierJson QualityPriceMultiplier = new QualityMultiplierJson();
        public int PriceStep;
        public float PriceMinRatio;
        public float PriceMaxRatio;
        public int StartTrays;
        public string[] StartRecipes = new string[0];
        public AmountJson[] StartStorage = new AmountJson[0];
    }

    public sealed class OvenJson
    {
        public float MinCollectProgress;
    }

    public sealed class ShelfJson
    {
        public int SlotsPerShelf;
        public int SlotCapacity;
    }

    public sealed class CustomersJson
    {
        public int[] BaseCountByLevel = new int[0];
        public int MaxConcurrent;
        public int QueueSlots;
        public float ReputationSpawnMin;
        public float ReputationSpawnMax;
        public float DayOldPriceMultiplier;
        public float UndercookedMultiplier;
        public float BrowseSecondsPerItem;
        public float AromaSeconds;
        public float AromaSpawnMultiplier;
    }

    public sealed class ReputationJson
    {
        public int Start;
        public int Min;
        public int Max;
        public int SaleCompleted;
        public int LeftNoStock;
        public int LeftTooExpensive;
        public int LeftQueueTimeout;
        public int PaymentComplaint;
        public int RecipeDiscovered;
    }

    public sealed class ProgressionJson
    {
        public int[] XpThresholds = new int[0];
        public int XpPerBread;
        public int XpPerfectBonus;
        public int XpSecretBonus;
        public int XpDiscovery;
        public int XpDayComplete;
        public int DemoGoalLevel;
        public int DemoGoalSecretRecipes;
    }

    public sealed class DeliveryJson
    {
        public float Seconds;
    }
}

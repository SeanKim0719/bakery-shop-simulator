using System;
using System.Collections.Generic;
using UnityEngine;

namespace BakeryShop.Data
{
    // content.json의 config 전체. 묶음의 이름과 필드는 JSON과 같다.
    // 묶음 클래스들은 GameConfig의 일부라서 한 파일에 둔다.

    /// <summary>게임 규칙에 쓰이는 숫자 전부. 코드에는 숫자를 적지 않고 여기서 읽는다.</summary>
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField] private DayConfig _day = new DayConfig();
        [SerializeField] private EconomyConfig _economy = new EconomyConfig();
        [SerializeField] private OvenConfig _oven = new OvenConfig();
        [SerializeField] private ShelfConfig _shelf = new ShelfConfig();
        [SerializeField] private CustomerConfig _customers = new CustomerConfig();
        [SerializeField] private ReputationConfig _reputation = new ReputationConfig();
        [SerializeField] private ProgressionConfig _progression = new ProgressionConfig();
        [SerializeField] private DeliveryConfig _delivery = new DeliveryConfig();

        public DayConfig Day => _day;
        public EconomyConfig Economy => _economy;
        public OvenConfig Oven => _oven;
        public ShelfConfig Shelf => _shelf;
        public CustomerConfig Customers => _customers;
        public ReputationConfig Reputation => _reputation;
        public ProgressionConfig Progression => _progression;
        public DeliveryConfig Delivery => _delivery;

        /// <summary>레벨의 수. 경험치 기준표의 길이로 정해진다.</summary>
        public int LevelCount => _progression.XpThresholds.Count;

        internal void Apply(ConfigJson json)
        {
            json = json ?? new ConfigJson();
            _day.Apply(json.Day ?? new DayJson());
            _economy.Apply(json.Economy ?? new EconomyJson());
            _oven.Apply(json.Oven ?? new OvenJson());
            _shelf.Apply(json.Shelf ?? new ShelfJson());
            _customers.Apply(json.Customers ?? new CustomersJson());
            _reputation.Apply(json.Reputation ?? new ReputationJson());
            _progression.Apply(json.Progression ?? new ProgressionJson());
            _delivery.Apply(json.Delivery ?? new DeliveryJson());
        }
    }

    /// <summary>영업 시간.</summary>
    [Serializable]
    public sealed class DayConfig
    {
        [SerializeField] private int _openMinutes;
        [SerializeField] private int _closeMinutes;
        [SerializeField] private float _openRealSeconds;
        [SerializeField] private CurvePoint[] _spawnCurve = new CurvePoint[0];

        /// <summary>개점 시각. 자정부터의 분 (09:00 = 540).</summary>
        public int OpenMinutes => _openMinutes;

        /// <summary>폐점 시각. 자정부터의 분 (18:00 = 1080).</summary>
        public int CloseMinutes => _closeMinutes;

        /// <summary>개점부터 폐점까지 걸리는 실제 시간(초).</summary>
        public float OpenRealSeconds => _openRealSeconds;

        /// <summary>시간대별 손님 수의 배율. x = 영업 시계(분), y = 배율.</summary>
        public IReadOnlyList<CurvePoint> SpawnCurve => _spawnCurve;

        internal void Apply(DayJson json)
        {
            _openMinutes = json.OpenMinutes;
            _closeMinutes = json.CloseMinutes;
            _openRealSeconds = json.OpenRealSeconds;
            _spawnCurve = ContentJson.Copy(json.SpawnCurve);
        }
    }

    /// <summary>돈, 가격, 시작 상태.</summary>
    [Serializable]
    public sealed class EconomyConfig
    {
        [SerializeField] private int _startMoney;
        [SerializeField] private int _moneyFloor;
        [SerializeField] private int[] _rentByLevel = new int[0];
        [SerializeField] private float _undercookedPriceMultiplier;
        [SerializeField] private float _normalPriceMultiplier;
        [SerializeField] private float _perfectPriceMultiplier;
        [SerializeField] private int _priceStep;
        [SerializeField] private float _priceMinRatio;
        [SerializeField] private float _priceMaxRatio;
        [SerializeField] private int _startTrays;
        [SerializeField] private string[] _startRecipes = new string[0];
        [SerializeField] private IngredientAmount[] _startStorage = new IngredientAmount[0];

        public int StartMoney => _startMoney;

        /// <summary>하루를 마칠 때 잔액이 이보다 적으면 지원금이 이 금액까지 채운다.</summary>
        public int MoneyFloor => _moneyFloor;

        /// <summary>레벨별 하루 임대료. 인덱스 0이 레벨 1이다.</summary>
        public IReadOnlyList<int> RentByLevel => _rentByLevel;

        /// <summary>판매가를 올리고 내리는 단위(원).</summary>
        public int PriceStep => _priceStep;

        /// <summary>판매가의 하한. 시세에 곱한다.</summary>
        public float PriceMinRatio => _priceMinRatio;

        /// <summary>판매가의 상한. 시세에 곱한다.</summary>
        public float PriceMaxRatio => _priceMaxRatio;

        public int StartTrays => _startTrays;

        /// <summary>처음부터 알고 있는 레시피의 ID.</summary>
        public IReadOnlyList<string> StartRecipes => _startRecipes;

        /// <summary>처음에 창고에 들어 있는 재료.</summary>
        public IReadOnlyList<IngredientAmount> StartStorage => _startStorage;

        /// <summary>품질에 따른 결제 금액의 배율. 탄 빵은 팔 수 없으므로 0이다.</summary>
        public float QualityPriceMultiplier(BreadQuality quality)
        {
            switch (quality)
            {
                case BreadQuality.Undercooked:
                    return _undercookedPriceMultiplier;
                case BreadQuality.Normal:
                    return _normalPriceMultiplier;
                case BreadQuality.Perfect:
                    return _perfectPriceMultiplier;
                default:
                    return 0f;
            }
        }

        internal void Apply(EconomyJson json)
        {
            QualityMultiplierJson quality = json.QualityPriceMultiplier ?? new QualityMultiplierJson();
            _startMoney = json.StartMoney;
            _moneyFloor = json.MoneyFloor;
            _rentByLevel = ContentJson.Copy(json.RentByLevel);
            _undercookedPriceMultiplier = quality.Undercooked;
            _normalPriceMultiplier = quality.Normal;
            _perfectPriceMultiplier = quality.Perfect;
            _priceStep = json.PriceStep;
            _priceMinRatio = json.PriceMinRatio;
            _priceMaxRatio = json.PriceMaxRatio;
            _startTrays = json.StartTrays;
            _startRecipes = ContentJson.Copy(json.StartRecipes);
            _startStorage = ContentJson.ToAmounts(json.StartStorage);
        }
    }

    [Serializable]
    public sealed class OvenConfig
    {
        [SerializeField] private float _minCollectProgress;

        /// <summary>굽기 진행도가 이 값(0~1)을 넘어야 꺼낼 수 있다.</summary>
        public float MinCollectProgress => _minCollectProgress;

        internal void Apply(OvenJson json)
        {
            _minCollectProgress = json.MinCollectProgress;
        }
    }

    [Serializable]
    public sealed class ShelfConfig
    {
        [SerializeField] private int _slotsPerShelf;
        [SerializeField] private int _slotCapacity;

        /// <summary>진열대 하나의 칸 수.</summary>
        public int SlotsPerShelf => _slotsPerShelf;

        /// <summary>칸 하나에 놓을 수 있는 빵의 수.</summary>
        public int SlotCapacity => _slotCapacity;

        internal void Apply(ShelfJson json)
        {
            _slotsPerShelf = json.SlotsPerShelf;
            _slotCapacity = json.SlotCapacity;
        }
    }

    /// <summary>손님 수와 손님의 판단에 쓰는 값.</summary>
    [Serializable]
    public sealed class CustomerConfig
    {
        [SerializeField] private int[] _baseCountByLevel = new int[0];
        [SerializeField] private int _maxConcurrent;
        [SerializeField] private int _queueSlots;
        [SerializeField] private float _reputationSpawnMin;
        [SerializeField] private float _reputationSpawnMax;
        [SerializeField] private float _dayOldPriceMultiplier;
        [SerializeField] private float _undercookedMultiplier;
        [SerializeField] private float _browseSecondsPerItem;
        [SerializeField] private float _aromaSeconds;
        [SerializeField] private float _aromaSpawnMultiplier;

        /// <summary>레벨별 하루 기본 손님 수. 인덱스 0이 레벨 1이다.</summary>
        public IReadOnlyList<int> BaseCountByLevel => _baseCountByLevel;

        /// <summary>매장에 동시에 있을 수 있는 손님 수.</summary>
        public int MaxConcurrent => _maxConcurrent;

        /// <summary>계산대 줄의 자리 수.</summary>
        public int QueueSlots => _queueSlots;

        /// <summary>평판이 최저일 때 손님 수의 배율.</summary>
        public float ReputationSpawnMin => _reputationSpawnMin;

        /// <summary>평판이 최고일 때 손님 수의 배율.</summary>
        public float ReputationSpawnMax => _reputationSpawnMax;

        /// <summary>하루 지난 빵의 가격 배율.</summary>
        public float DayOldPriceMultiplier => _dayOldPriceMultiplier;

        /// <summary>덜 익은 빵을 살 확률에 곱하는 값.</summary>
        public float UndercookedMultiplier => _undercookedMultiplier;

        /// <summary>빵 하나를 고르는 데 걸리는 시간(초).</summary>
        public float BrowseSecondsPerItem => _browseSecondsPerItem;

        /// <summary>갓 구운 빵 냄새가 이어지는 시간(초).</summary>
        public float AromaSeconds => _aromaSeconds;

        /// <summary>빵 냄새가 나는 동안 손님 수의 배율.</summary>
        public float AromaSpawnMultiplier => _aromaSpawnMultiplier;

        internal void Apply(CustomersJson json)
        {
            _baseCountByLevel = ContentJson.Copy(json.BaseCountByLevel);
            _maxConcurrent = json.MaxConcurrent;
            _queueSlots = json.QueueSlots;
            _reputationSpawnMin = json.ReputationSpawnMin;
            _reputationSpawnMax = json.ReputationSpawnMax;
            _dayOldPriceMultiplier = json.DayOldPriceMultiplier;
            _undercookedMultiplier = json.UndercookedMultiplier;
            _browseSecondsPerItem = json.BrowseSecondsPerItem;
            _aromaSeconds = json.AromaSeconds;
            _aromaSpawnMultiplier = json.AromaSpawnMultiplier;
        }
    }

    /// <summary>평판의 범위와, 사건마다 오르내리는 양.</summary>
    [Serializable]
    public sealed class ReputationConfig
    {
        [SerializeField] private int _start;
        [SerializeField] private int _min;
        [SerializeField] private int _max;
        [SerializeField] private int _saleCompleted;
        [SerializeField] private int _leftNoStock;
        [SerializeField] private int _leftTooExpensive;
        [SerializeField] private int _leftQueueTimeout;
        [SerializeField] private int _paymentComplaint;
        [SerializeField] private int _recipeDiscovered;

        public int Start => _start;
        public int Min => _min;
        public int Max => _max;
        public int SaleCompleted => _saleCompleted;
        public int LeftNoStock => _leftNoStock;
        public int LeftTooExpensive => _leftTooExpensive;
        public int LeftQueueTimeout => _leftQueueTimeout;
        public int PaymentComplaint => _paymentComplaint;
        public int RecipeDiscovered => _recipeDiscovered;

        internal void Apply(ReputationJson json)
        {
            _start = json.Start;
            _min = json.Min;
            _max = json.Max;
            _saleCompleted = json.SaleCompleted;
            _leftNoStock = json.LeftNoStock;
            _leftTooExpensive = json.LeftTooExpensive;
            _leftQueueTimeout = json.LeftQueueTimeout;
            _paymentComplaint = json.PaymentComplaint;
            _recipeDiscovered = json.RecipeDiscovered;
        }
    }

    /// <summary>경험치와 레벨, 데모의 목표.</summary>
    [Serializable]
    public sealed class ProgressionConfig
    {
        [SerializeField] private int[] _xpThresholds = new int[0];
        [SerializeField] private int _xpPerBread;
        [SerializeField] private int _xpPerfectBonus;
        [SerializeField] private int _xpSecretBonus;
        [SerializeField] private int _xpDiscovery;
        [SerializeField] private int _xpDayComplete;
        [SerializeField] private int _demoGoalLevel;
        [SerializeField] private int _demoGoalSecretRecipes;

        /// <summary>레벨별 누적 경험치 기준. 인덱스 0이 레벨 1이고 값은 0이다.</summary>
        public IReadOnlyList<int> XpThresholds => _xpThresholds;

        public int XpPerBread => _xpPerBread;
        public int XpPerfectBonus => _xpPerfectBonus;
        public int XpSecretBonus => _xpSecretBonus;
        public int XpDiscovery => _xpDiscovery;
        public int XpDayComplete => _xpDayComplete;
        public int DemoGoalLevel => _demoGoalLevel;
        public int DemoGoalSecretRecipes => _demoGoalSecretRecipes;

        internal void Apply(ProgressionJson json)
        {
            _xpThresholds = ContentJson.Copy(json.XpThresholds);
            _xpPerBread = json.XpPerBread;
            _xpPerfectBonus = json.XpPerfectBonus;
            _xpSecretBonus = json.XpSecretBonus;
            _xpDiscovery = json.XpDiscovery;
            _xpDayComplete = json.XpDayComplete;
            _demoGoalLevel = json.DemoGoalLevel;
            _demoGoalSecretRecipes = json.DemoGoalSecretRecipes;
        }
    }

    [Serializable]
    public sealed class DeliveryConfig
    {
        [SerializeField] private float _seconds;

        /// <summary>주문부터 박스가 도착하기까지의 시간(초).</summary>
        public float Seconds => _seconds;

        internal void Apply(DeliveryJson json)
        {
            _seconds = json.Seconds;
        }
    }
}

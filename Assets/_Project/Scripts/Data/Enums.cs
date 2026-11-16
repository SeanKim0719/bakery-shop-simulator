namespace BakeryShop.Data
{
    /// <summary>재료의 희귀도. 뒤로 갈수록 귀하다 (값의 크기로 비교한다).</summary>
    public enum Rarity
    {
        Common,
        Fine,
        Rare,
        Legendary,
    }

    /// <summary>빵의 품질. 오븐에서 꺼내는 순간 정해진다.</summary>
    public enum BreadQuality
    {
        Undercooked,
        Normal,
        Perfect,
        Burnt,
    }

    /// <summary>발주 상품의 종류.</summary>
    public enum ProductKind
    {
        /// <summary>내용물이 정해진 재료 묶음.</summary>
        IngredientPack,

        /// <summary>확률표에서 뽑는 미스터리 박스.</summary>
        MysteryBox,
    }

    /// <summary>해금의 종류.</summary>
    public enum UnlockKind
    {
        RecipeLicense,
        Furniture,
        Tray,
    }
}

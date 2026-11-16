using System;

namespace BakeryShop.Data
{
    /// <summary>재료와 수량 한 쌍. 레시피의 재료, 상품의 내용물, 시작 창고에 쓴다.</summary>
    [Serializable]
    public struct IngredientAmount
    {
        public string IngredientId;
        public int Count;

        public IngredientAmount(string ingredientId, int count)
        {
            IngredientId = ingredientId;
            Count = count;
        }
    }
}

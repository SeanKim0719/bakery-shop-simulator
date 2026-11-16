using System.Globalization;

namespace BakeryShop.Economy
{
    /// <summary>
    /// 금액을 화면에 보이는 글자로 바꾼다. HUD, 가격표, 리포트가 모두 여기를 거쳐서 표기가 한 가지로 유지된다.
    /// 컴퓨터의 지역 설정과 상관없이 천 단위에 쉼표를 쓴다 (독일어 Windows에서 "152.300원"이 되지 않게).
    /// </summary>
    public static class MoneyFormat
    {
        /// <summary>152300 → "152,300원", -500 → "-500원".</summary>
        public static string Won(int amount)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture) + "원";
        }

        /// <summary>변화량. 3600 → "+3,600", -500 → "-500", 0 → "0".</summary>
        public static string Signed(int amount)
        {
            string text = amount.ToString("N0", CultureInfo.InvariantCulture);
            return amount > 0 ? "+" + text : text;
        }
    }
}

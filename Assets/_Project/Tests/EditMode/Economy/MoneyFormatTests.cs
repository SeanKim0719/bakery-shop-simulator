using System.Globalization;
using System.Threading;
using BakeryShop.Economy;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class MoneyFormatTests
    {
        [TestCase(0, "0원")]
        [TestCase(999, "999원")]
        [TestCase(1000, "1,000원")]
        [TestCase(152300, "152,300원")]
        [TestCase(1234567, "1,234,567원")]
        [TestCase(-500, "-500원")]
        [TestCase(-15000, "-15,000원")]
        public void Won_FormatsWithThousandsSeparators(int amount, string expected)
        {
            Assert.AreEqual(expected, MoneyFormat.Won(amount));
        }

        [TestCase(3600, "+3,600")]
        [TestCase(1, "+1")]
        [TestCase(0, "0")]
        [TestCase(-500, "-500")]
        [TestCase(-15000, "-15,000")]
        public void Signed_ShowsThePlusSign(int amount, string expected)
        {
            Assert.AreEqual(expected, MoneyFormat.Signed(amount));
        }

        [Test]
        public void Format_DoesNotDependOnTheComputersRegion()
        {
            // 독일어 설정에서는 천 단위 구분이 점이다. 지역 설정을 따르면 "152.300원"이 된다.
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                Assert.AreEqual("152,300원", MoneyFormat.Won(152300));
                Assert.AreEqual("+3,600", MoneyFormat.Signed(3600));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}

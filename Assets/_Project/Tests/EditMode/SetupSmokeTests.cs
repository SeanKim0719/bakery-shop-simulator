using NUnit.Framework;

namespace BakeryShop.Tests
{
    /// <summary>
    /// 테스트 환경이 제대로 구성됐는지 확인하는 테스트.
    /// Window > General > Test Runner > EditMode에서 실행해 초록색이 뜨면 셋업 완료.
    /// 실제 테스트가 생기면 지워도 된다.
    /// </summary>
    public class SetupSmokeTests
    {
        [Test]
        public void TestRunner_IsConfigured()
        {
            Assert.Pass();
        }

        [Test]
        public void RuntimeAssembly_IsReferenced()
        {
            var assembly = System.Reflection.Assembly.Load("BakeryShop.Runtime");
            Assert.IsNotNull(assembly);
        }
    }
}

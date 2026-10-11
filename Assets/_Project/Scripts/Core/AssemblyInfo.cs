using System.Runtime.CompilerServices;

// 테스트 어셈블리가 internal 멤버에 접근할 수 있게 한다.
// 규칙 로직(재고, 가격, 확률 등)을 public으로 열지 않고도 단위 테스트하기 위함.
[assembly: InternalsVisibleTo("BakeryShop.Tests.EditMode")]

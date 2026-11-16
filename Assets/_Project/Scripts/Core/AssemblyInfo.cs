using System.Runtime.CompilerServices;

// 테스트 어셈블리가 internal 멤버에 접근할 수 있게 한다.
// 규칙 로직(재고, 가격, 확률 등)을 public으로 열지 않고도 단위 테스트하기 위함.
[assembly: InternalsVisibleTo("BakeryShop.Tests.EditMode")]

// 에디터의 콘텐츠 임포터가 정의의 internal Apply로 수치를 채울 수 있게 한다.
// 정의에 값을 쓰는 길을 임포터와 테스트로만 제한하기 위함.
[assembly: InternalsVisibleTo("BakeryShop.Editor")]

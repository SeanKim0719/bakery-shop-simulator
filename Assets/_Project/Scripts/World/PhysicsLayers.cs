using UnityEngine;

namespace BakeryShop.World
{
    /// <summary>게임이 쓰는 물리 레이어의 이름과 충돌 규칙.</summary>
    public static class PhysicsLayers
    {
        public const string Player = "Player";
        public const string Customer = "Customer";

        /// <summary>박스, 트레이처럼 들고 옮기는 물건.</summary>
        public const string Item = "Item";

        /// <summary>
        /// 플레이어와 손님이 서로 부딪히지 않게 한다. 좁은 통로에서 손님에게 막혀 갇히는 문제를 처음부터 없앤다.
        /// 씬 빌더가 프로젝트 설정에도 같은 규칙을 넣지만, 설정이 어긋나 있어도 게임이 같게 동작하도록 시작할 때 한 번 더 적용한다.
        /// </summary>
        public static void ApplyCollisionRules()
        {
            int player = LayerMask.NameToLayer(Player);
            int customer = LayerMask.NameToLayer(Customer);
            if (player < 0 || customer < 0)
            {
                Debug.LogWarning("[PhysicsLayers] 레이어 'Player' 또는 'Customer'가 없습니다. 메뉴 BakeryShop > 매장 씬 만들기를 실행하면 추가됩니다.");
                return;
            }

            Physics.IgnoreLayerCollision(player, customer, true);
        }
    }
}

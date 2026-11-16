using BakeryShop.Player;
using NUnit.Framework;
using UnityEngine;

namespace BakeryShop.Tests
{
    public class PlacementMathTests
    {
        // 그레이박스 씬의 플레이어: 높이 1.8, 반지름 0.35, 스킨 0.08 (CharacterController 기본값), 발이 원점.
        private const float Height = 1.8f;
        private const float Radius = 0.35f;
        private const float Skin = 0.08f;
        private static readonly Vector3 CapsuleCenter = new Vector3(0f, 0.9f, 0f);

        // 배송 박스: 0.5m 정육면체, 바닥에 놓인 상태.
        private static readonly Vector3 BoxHalf = Vector3.one * 0.25f;

        private static bool Overlaps(Vector3 boxCenter, float yaw = 0f, float radius = Radius + Skin) =>
            PlacementMath.BoxOverlapsUprightCapsule(boxCenter, yaw, BoxHalf, CapsuleCenter, Height, radius);

        [Test]
        public void BoxFarAway_DoesNotOverlap()
        {
            Assert.IsFalse(Overlaps(new Vector3(0f, 0.25f, 2f)));
        }

        [Test]
        public void BoxAtFeet_Overlaps()
        {
            Assert.IsTrue(Overlaps(new Vector3(0f, 0.25f, 0f)));
        }

        [Test]
        public void BoxInsideSkinOnly_IsBlocked()
        {
            // 회귀 테스트: 박스 앞면이 몸 중심에서 0.37m (반지름 0.35 바깥, 스킨 0.43 안쪽).
            // 반지름만 보는 검사로는 통과했던 위치라 플레이어가 박스 위로 올라탔다.
            var box = new Vector3(0f, 0.25f, 0.62f);

            Assert.IsFalse(Overlaps(box, radius: Radius), "반지름만 보면 겹치지 않는다");
            Assert.IsTrue(Overlaps(box), "스킨까지 보면 겹친다");
        }

        [Test]
        public void BoxJustOutsideSkin_IsAllowed()
        {
            // 박스 앞면이 0.45m: 스킨(0.43) 바깥이라 놓을 수 있어야 한다.
            Assert.IsFalse(Overlaps(new Vector3(0f, 0.25f, 0.70f)));
        }

        [Test]
        public void BoxAboveHead_DoesNotOverlap()
        {
            Assert.IsFalse(Overlaps(new Vector3(0f, 2.5f, 0f)));
        }

        [Test]
        public void BoxNearRoundedBottom_UsesHemisphereNotCylinder()
        {
            // 아래 반구 중심은 높이 0.43. 박스 윗면이 0.05, 수평으로 0.35 떨어져 있으면
            // 실제 거리는 sqrt(0.35² + 0.38²) ≈ 0.52 > 0.43 이라 겹치지 않는다 (원기둥으로 보면 겹침).
            var flatBox = new Vector3(0.6f, 0f, 0f);
            bool overlaps = PlacementMath.BoxOverlapsUprightCapsule(
                flatBox, 0f, new Vector3(0.25f, 0.05f, 0.25f), CapsuleCenter, Height, Radius + Skin);

            Assert.IsFalse(overlaps);
        }

        [Test]
        public void Rotation_IsTakenIntoAccount()
        {
            // 길쭉한 박스(가로 1m, 깊이 0.2m)를 90도 돌리면 긴 쪽이 앞뒤(z) 방향이 된다.
            var longHalf = new Vector3(0.5f, 0.25f, 0.1f);
            var capsuleAhead = new Vector3(0f, 0.9f, 0.45f);

            bool notRotated = PlacementMath.BoxOverlapsUprightCapsule(Vector3.zero, 0f, longHalf, capsuleAhead, Height, 0.1f);
            bool rotated = PlacementMath.BoxOverlapsUprightCapsule(Vector3.zero, 90f, longHalf, capsuleAhead, Height, 0.1f);

            Assert.IsFalse(notRotated);
            Assert.IsTrue(rotated);
        }
    }
}

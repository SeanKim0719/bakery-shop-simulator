using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BakeryShop.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace BakeryShop.Tests
{
    /// <summary>
    /// 실제 shop_layout.json으로 만든 배치가 말이 되는지 확인한다.
    /// 씬을 열어 걸어 보기 전에 잡을 수 있는 실수들이다: 문이 막혔다, 가구가 겹친다, 손님이 서는 자리가 가구 안이다.
    /// 배치를 바꾸면(JSON을 고치면) 이 테스트가 먼저 알려 준다.
    /// </summary>
    public class ShopBlueprintTests
    {
        private const float Epsilon = 0.001f;
        private const float PlayerRadius = 0.35f;
        private const float CustomerRadius = 0.3f;

        private ShopLayoutJson _layout;
        private ShopBlueprint _blueprint;

        [SetUp]
        public void SetUp()
        {
            _layout = LoadLayout();
            _blueprint = ShopBlueprint.Create(_layout);
        }

        private static ShopLayoutJson LoadLayout()
        {
            return ShopLayoutJson.Read(File.ReadAllText(ShopSceneBuilder.LayoutPath));
        }

        private FurnitureBlueprint Furniture(string id)
        {
            return _blueprint.Furniture.First(item => item.Id == id);
        }

        private IEnumerable<BlueprintBlock> SouthWallBlocks()
        {
            return _blueprint.Environment.Where(block => block.Kind == BlockKind.Wall && block.ZMax <= _blueprint.RoomZMin + Epsilon);
        }

        /// <summary>점에서 가구의 바닥 자리(x, z)까지의 거리. 안에 있으면 0.</summary>
        private static float DistanceToFootprint(Vector3 point, FurnitureBlueprint furniture)
        {
            float dx = Mathf.Max(furniture.XMin - point.x, 0f, point.x - furniture.XMax);
            float dz = Mathf.Max(furniture.ZMin - point.z, 0f, point.z - furniture.ZMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void AssertClearOfFurniture(Vector3 point, float radius, string pointName)
        {
            foreach (FurnitureBlueprint furniture in _blueprint.Furniture)
            {
                float distance = DistanceToFootprint(point, furniture);
                Assert.GreaterOrEqual(
                    distance,
                    radius - Epsilon,
                    pointName + " (" + point.x + ", " + point.z + ")가 가구 '" + furniture.Id + "'에 너무 가깝습니다 (거리 " + distance + ", 필요 " + radius + ").");
            }
        }

        private static bool Contains(BlueprintBlock zone, Vector3 point)
        {
            return point.x > zone.XMin + Epsilon && point.x < zone.XMax - Epsilon
                && point.z > zone.ZMin + Epsilon && point.z < zone.ZMax - Epsilon;
        }

        // ---- 건물 ----

        [Test]
        public void Door_IsOpen_BelowDoorHeight()
        {
            foreach (BlueprintBlock wall in SouthWallBlocks())
            {
                bool overDoor = wall.XMax > _blueprint.DoorXMin + Epsilon && wall.XMin < _blueprint.DoorXMax - Epsilon;
                if (overDoor)
                {
                    Assert.GreaterOrEqual(wall.YMin, ShopBlueprint.DoorHeight - Epsilon, wall.Name + "이 문 자리를 막고 있습니다.");
                }
            }
        }

        [Test]
        public void SouthWall_IsClosed_OutsideTheDoor()
        {
            for (float x = _blueprint.RoomXMin + 0.05f; x < _blueprint.RoomXMax; x += 0.1f)
            {
                if (x > _blueprint.DoorXMin - Epsilon && x < _blueprint.DoorXMax + Epsilon)
                {
                    continue;
                }

                bool covered = SouthWallBlocks().Any(wall => wall.XMin <= x && wall.XMax >= x && wall.YMin <= Epsilon);
                Assert.IsTrue(covered, "남쪽 벽이 x = " + x + "에서 뚫려 있습니다.");
            }
        }

        [Test]
        public void Walls_SurroundTheRoom_OnTheOutside()
        {
            // 벽이 실내 범위를 침범하지 않아야 실내가 정확히 room의 크기가 된다.
            foreach (BlueprintBlock wall in _blueprint.Environment.Where(block => block.Kind == BlockKind.Wall))
            {
                bool inside = wall.XMax > _blueprint.RoomXMin + Epsilon && wall.XMin < _blueprint.RoomXMax - Epsilon
                    && wall.ZMax > _blueprint.RoomZMin + Epsilon && wall.ZMin < _blueprint.RoomZMax - Epsilon;
                Assert.IsFalse(inside, wall.Name + "이 실내 범위 안으로 들어와 있습니다.");
            }

            Assert.IsTrue(_blueprint.Environment.Any(block => block.Name == "Wall_North"));
            Assert.IsTrue(_blueprint.Environment.Any(block => block.Name == "Wall_East"));
            Assert.IsTrue(_blueprint.Environment.Any(block => block.Name == "Wall_West"));
        }

        [Test]
        public void Floor_CoversTheRoom_AndItsTopIsAtZero()
        {
            BlueprintBlock floor = _blueprint.Environment.First(block => block.Kind == BlockKind.Floor);

            Assert.AreEqual(0f, floor.YMax, Epsilon);
            Assert.LessOrEqual(floor.XMin, _blueprint.RoomXMin + Epsilon);
            Assert.GreaterOrEqual(floor.XMax, _blueprint.RoomXMax - Epsilon);
            Assert.LessOrEqual(floor.ZMin, _blueprint.RoomZMin + Epsilon);
            Assert.GreaterOrEqual(floor.ZMax, _blueprint.RoomZMax - Epsilon);
        }

        [Test]
        public void FloorAndSidewalk_Meet_WithoutOverlapping()
        {
            // 같은 높이의 두 바닥이 겹치면 겹친 자리가 깜빡인다 (z-fighting).
            BlueprintBlock floor = _blueprint.Environment.First(block => block.Kind == BlockKind.Floor);
            BlueprintBlock sidewalk = _blueprint.Environment.First(block => block.Kind == BlockKind.Sidewalk);

            Assert.AreEqual(0f, sidewalk.YMax, Epsilon);
            Assert.AreEqual(sidewalk.ZMax, floor.ZMin, Epsilon);
        }

        [Test]
        public void Boundaries_CloseTheSidewalk_ButNotTheDoorway()
        {
            BlueprintBlock sidewalk = _blueprint.Environment.First(block => block.Kind == BlockKind.Sidewalk);

            Assert.IsTrue(_blueprint.Boundaries.Any(block => block.ZMax <= sidewalk.ZMin + Epsilon), "인도의 남쪽이 열려 있습니다.");
            Assert.IsTrue(_blueprint.Boundaries.Any(block => block.XMax <= sidewalk.XMin + Epsilon), "인도의 서쪽이 열려 있습니다.");
            Assert.IsTrue(_blueprint.Boundaries.Any(block => block.XMin >= sidewalk.XMax - Epsilon), "인도의 동쪽이 열려 있습니다.");

            // 문 앞(인도에서 문턱까지)을 막는 벽이 없어야 한다.
            foreach (BlueprintBlock boundary in _blueprint.Boundaries)
            {
                bool blocksDoorway = boundary.XMax > _blueprint.DoorXMin + Epsilon && boundary.XMin < _blueprint.DoorXMax - Epsilon
                    && boundary.ZMax > sidewalk.ZMin + Epsilon && boundary.ZMin < _blueprint.RoomZMin + Epsilon;
                Assert.IsFalse(blocksDoorway, boundary.Name + "이 문 앞을 막고 있습니다.");
            }
        }

        // ---- 가구 ----

        [Test]
        public void Furniture_IsInsideTheRoom()
        {
            foreach (FurnitureBlueprint furniture in _blueprint.Furniture)
            {
                Assert.GreaterOrEqual(furniture.XMin, _blueprint.RoomXMin - Epsilon, furniture.Id);
                Assert.LessOrEqual(furniture.XMax, _blueprint.RoomXMax + Epsilon, furniture.Id);
                Assert.GreaterOrEqual(furniture.ZMin, _blueprint.RoomZMin - Epsilon, furniture.Id);
                Assert.LessOrEqual(furniture.ZMax, _blueprint.RoomZMax + Epsilon, furniture.Id);
            }
        }

        [Test]
        public void Furniture_DoesNotOverlap()
        {
            // 맞닿는 것은 괜찮다 (진열대 3과 4는 등을 맞대고 있다).
            for (int i = 0; i < _blueprint.Furniture.Count; i++)
            {
                for (int j = i + 1; j < _blueprint.Furniture.Count; j++)
                {
                    FurnitureBlueprint a = _blueprint.Furniture[i];
                    FurnitureBlueprint b = _blueprint.Furniture[j];
                    bool overlaps = a.XMax > b.XMin + Epsilon && a.XMin < b.XMax - Epsilon
                        && a.ZMax > b.ZMin + Epsilon && a.ZMin < b.ZMax - Epsilon;
                    Assert.IsFalse(overlaps, "가구 '" + a.Id + "'와 '" + b.Id + "'가 겹칩니다.");
                }
            }
        }

        [Test]
        public void EveryFurniture_HasBlocks_StandingOnTheFloor()
        {
            foreach (FurnitureBlueprint furniture in _blueprint.Furniture)
            {
                Assert.Greater(furniture.Blocks.Count, 0, furniture.Id);
                Assert.AreEqual(0f, furniture.Blocks.Min(block => block.YMin), Epsilon, furniture.Id + "가 바닥에서 떠 있거나 묻혀 있습니다.");
                foreach (BlueprintBlock block in furniture.Blocks)
                {
                    Assert.GreaterOrEqual(block.XMin, furniture.XMin - Epsilon, furniture.Id);
                    Assert.LessOrEqual(block.XMax, furniture.XMax + Epsilon, furniture.Id);
                    Assert.GreaterOrEqual(block.ZMin, furniture.ZMin - Epsilon, furniture.Id);
                    Assert.LessOrEqual(block.ZMax, furniture.ZMax + Epsilon, furniture.Id);
                }
            }
        }

        [Test]
        public void LockedFurniture_MatchesTheDesign()
        {
            // 08 문서의 해금: 진열대 3, 진열대 4, 오븐 2.
            CollectionAssert.AreEquivalent(
                new[] { "shelf.03", "shelf.04", "oven.02" },
                _blueprint.Furniture.Where(item => item.IsLocked).Select(item => item.Id).ToArray());
        }

        [Test]
        public void StaffPassages_AreWideEnoughForThePlayer()
        {
            // 계산 구역과 주방은 동쪽 끝으로 드나든다. 그 틈이 플레이어의 지름보다 넉넉해야 한다.
            float needed = PlayerRadius * 2f + 0.1f;

            Assert.GreaterOrEqual(_blueprint.RoomXMax - Furniture("counter.01").XMax, needed, "계산 구역 입구가 좁습니다.");
            Assert.GreaterOrEqual(_blueprint.RoomXMax - Furniture("divider").XMax, needed, "주방 입구가 좁습니다.");
            Assert.GreaterOrEqual(_blueprint.DoorXMax - _blueprint.DoorXMin, needed, "문이 좁습니다.");
        }

        // ---- 진열대 ----

        [Test]
        public void EveryShelfFurniture_HasSlots()
        {
            string[] shelfFurniture = _blueprint.Furniture.Where(item => item.Kind == BlockKind.Shelf).Select(item => item.Id).ToArray();

            CollectionAssert.AreEquivalent(shelfFurniture, _blueprint.Shelves.Select(shelf => shelf.ShelfId).ToArray());
            foreach (ShelfBlueprint shelf in _blueprint.Shelves)
            {
                Assert.AreEqual(3, shelf.SlotPositions.Length, shelf.ShelfId);
                Assert.AreEqual(shelf.SlotPositions.Length, shelf.StandPoints.Length, shelf.ShelfId);
            }
        }

        [Test]
        public void Slots_AreOnTheShelfSurface()
        {
            foreach (ShelfBlueprint shelf in _blueprint.Shelves)
            {
                FurnitureBlueprint furniture = Furniture(shelf.ShelfId);
                foreach (Vector3 slot in shelf.SlotPositions)
                {
                    Assert.AreEqual(furniture.Position.x, slot.x, Epsilon, shelf.ShelfId);
                    Assert.AreEqual(ShopBlueprint.ShelfSurfaceHeight, slot.y, Epsilon, shelf.ShelfId);
                    Assert.Greater(slot.z, furniture.ZMin, shelf.ShelfId + "의 칸이 진열대 밖에 있습니다.");
                    Assert.Less(slot.z, furniture.ZMax, shelf.ShelfId + "의 칸이 진열대 밖에 있습니다.");
                }

                BlueprintBlock body = furniture.Blocks.First(block => block.Name == "Body");
                Assert.AreEqual(ShopBlueprint.ShelfSurfaceHeight, body.YMax, Epsilon, shelf.ShelfId + "의 몸통 윗면이 칸의 높이와 다릅니다.");
            }
        }

        [Test]
        public void StandPoints_AreOnTheFacingSide_AndTheBackPanelIsOnTheOtherSide()
        {
            foreach (LayoutShelfJson shelfJson in _layout.Shelves)
            {
                FurnitureBlueprint furniture = Furniture(shelfJson.Id);
                ShelfBlueprint shelf = _blueprint.Shelves.First(item => item.ShelfId == shelfJson.Id);
                BlueprintBlock back = furniture.Blocks.First(block => block.Name == "Back");

                foreach (Vector3 stand in shelf.StandPoints)
                {
                    if (shelfJson.Facing == "east")
                    {
                        Assert.Greater(stand.x, furniture.XMax, shelfJson.Id + ": 손님이 서는 자리가 동쪽이 아닙니다.");
                    }
                    else
                    {
                        Assert.Less(stand.x, furniture.XMin, shelfJson.Id + ": 손님이 서는 자리가 서쪽이 아닙니다.");
                    }
                }

                // 뒤판은 손님 반대쪽 끝에 있다.
                if (shelfJson.Facing == "east")
                {
                    Assert.AreEqual(furniture.XMin, back.XMin, Epsilon, shelfJson.Id);
                }
                else
                {
                    Assert.AreEqual(furniture.XMax, back.XMax, Epsilon, shelfJson.Id);
                }
            }
        }

        // ---- 지점 ----

        [Test]
        public void PlayerStart_IsInsideTheRoom_AndClearOfFurniture()
        {
            Vector3 start = _blueprint.PlayerStart;

            Assert.Greater(start.x, _blueprint.RoomXMin + PlayerRadius);
            Assert.Less(start.x, _blueprint.RoomXMax - PlayerRadius);
            Assert.Greater(start.z, _blueprint.RoomZMin + PlayerRadius);
            Assert.Less(start.z, _blueprint.RoomZMax - PlayerRadius);
            AssertClearOfFurniture(start, PlayerRadius, "플레이어 시작");
        }

        [Test]
        public void CustomerPoints_AreClearOfFurniture()
        {
            AssertClearOfFurniture(_blueprint.EntryPoint, CustomerRadius, "손님 입장 지점");
            for (int i = 0; i < _blueprint.QueuePoints.Length; i++)
            {
                AssertClearOfFurniture(_blueprint.QueuePoints[i], CustomerRadius, "줄 " + i);
            }

            foreach (ShelfBlueprint shelf in _blueprint.Shelves)
            {
                for (int i = 0; i < shelf.StandPoints.Length; i++)
                {
                    AssertClearOfFurniture(shelf.StandPoints[i], CustomerRadius, shelf.ShelfId + " 칸 " + i + " 앞");
                }
            }
        }

        [Test]
        public void CustomerPoints_AreOutsideTheStaffZones()
        {
            // 주방과 계산 구역은 손님의 길 찾기에서 빠진다. 손님이 가야 하는 자리가 그 안에 있으면 영영 도착하지 못한다.
            Assert.AreEqual(2, _blueprint.NoWalkZones.Count);

            var points = new List<Vector3> { _blueprint.EntryPoint };
            points.AddRange(_blueprint.QueuePoints);
            foreach (ShelfBlueprint shelf in _blueprint.Shelves)
            {
                points.AddRange(shelf.StandPoints);
            }

            foreach (Vector3 point in points)
            {
                foreach (BlueprintBlock zone in _blueprint.NoWalkZones)
                {
                    Assert.IsFalse(Contains(zone, point), "손님의 지점 (" + point.x + ", " + point.z + ")가 " + zone.Name + " 안에 있습니다.");
                }
            }
        }

        [Test]
        public void Queue_HasACheckoutSpotAndFourWaitingSpots()
        {
            Assert.AreEqual(5, _blueprint.QueuePoints.Length);

            // 줄은 계산대에서 멀어지는 쪽(북쪽)으로 뻗는다. 줄이 길어져도 문을 막지 않는다.
            for (int i = 1; i < _blueprint.QueuePoints.Length; i++)
            {
                Assert.Greater(_blueprint.QueuePoints[i].z, _blueprint.QueuePoints[i - 1].z, "줄 " + i);
            }
        }

        [Test]
        public void OutdoorPoints_AreOnTheSidewalk()
        {
            BlueprintBlock sidewalk = _blueprint.Environment.First(block => block.Kind == BlockKind.Sidewalk);
            var outdoor = new List<Vector3> { _blueprint.DeliveryPoint };
            outdoor.AddRange(_blueprint.SpawnPoints);

            foreach (Vector3 point in outdoor)
            {
                Assert.IsTrue(Contains(sidewalk, point), "실외 지점 (" + point.x + ", " + point.z + ")가 인도 밖에 있습니다.");
            }

            Assert.AreEqual(2, _blueprint.SpawnPoints.Length);
            Assert.Less(_blueprint.SpawnPoints[0].x, 0f, "0번이 서쪽이어야 합니다.");
            Assert.Greater(_blueprint.SpawnPoints[1].x, 0f, "1번이 동쪽이어야 합니다.");
        }

        [Test]
        public void Sign_IsOnTheSouthWall_BesideTheDoor()
        {
            Vector3 sign = _blueprint.SignPoint;

            Assert.AreEqual(_blueprint.RoomZMin, sign.z, 0.1f, "팻말이 남쪽 벽에 붙어 있지 않습니다.");
            bool inDoorway = sign.x > _blueprint.DoorXMin && sign.x < _blueprint.DoorXMax;
            Assert.IsFalse(inDoorway, "팻말이 문 자리에 있습니다.");
        }

        // ---- 잘못된 배치 ----

        [Test]
        public void UnknownFurnitureKind_IsAnError()
        {
            _layout.Furniture[0].Kind = "sofa";

            var exception = Assert.Throws<FormatException>(() => ShopBlueprint.Create(_layout));
            StringAssert.Contains("sofa", exception.Message);
        }

        [Test]
        public void MissingPoint_IsAnError()
        {
            _layout.Points = _layout.Points.Where(point => point.Id != "delivery").ToArray();

            var exception = Assert.Throws<FormatException>(() => ShopBlueprint.Create(_layout));
            StringAssert.Contains("delivery", exception.Message);
        }

        [Test]
        public void ShelfWithoutSlotInfo_IsAnError()
        {
            _layout.Shelves = _layout.Shelves.Where(shelf => shelf.Id != "shelf.02").ToArray();

            var exception = Assert.Throws<FormatException>(() => ShopBlueprint.Create(_layout));
            StringAssert.Contains("shelf.02", exception.Message);
        }

        [Test]
        public void DuplicateFurnitureId_IsAnError()
        {
            _layout.Furniture[1].Id = _layout.Furniture[0].Id;

            Assert.Throws<FormatException>(() => ShopBlueprint.Create(_layout));
        }

        [Test]
        public void DoorOutsideTheWall_IsAnError()
        {
            _layout.Door.XMin = _layout.Room.XMin - 1f;

            Assert.Throws<FormatException>(() => ShopBlueprint.Create(_layout));
        }

        [Test]
        public void UnknownFacing_IsAnError()
        {
            _layout.Shelves[0].Facing = "north";

            var exception = Assert.Throws<FormatException>(() => ShopBlueprint.Create(_layout));
            StringAssert.Contains("north", exception.Message);
        }
    }
}

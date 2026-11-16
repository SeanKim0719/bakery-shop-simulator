using System;
using System.Collections.Generic;
using UnityEngine;

namespace BakeryShop.EditorTools
{
    /// <summary>그레이박스 상자의 종류. 색을 정한다.</summary>
    public enum BlockKind
    {
        Floor,
        Sidewalk,
        Wall,
        Partition,
        Shelf,
        Oven,
        Storage,
        Furniture,
    }

    /// <summary>상자 하나. 중심과 크기는 월드 좌표(미터)다.</summary>
    public readonly struct BlueprintBlock
    {
        public readonly string Name;
        public readonly Vector3 Center;
        public readonly Vector3 Size;
        public readonly BlockKind Kind;

        public BlueprintBlock(string name, Vector3 center, Vector3 size, BlockKind kind)
        {
            Name = name;
            Center = center;
            Size = size;
            Kind = kind;
        }

        public float XMin => Center.x - Size.x * 0.5f;
        public float XMax => Center.x + Size.x * 0.5f;
        public float YMin => Center.y - Size.y * 0.5f;
        public float YMax => Center.y + Size.y * 0.5f;
        public float ZMin => Center.z - Size.z * 0.5f;
        public float ZMax => Center.z + Size.z * 0.5f;
    }

    /// <summary>가구 하나. 상자 한두 개로 이루어진다.</summary>
    public sealed class FurnitureBlueprint
    {
        public string Id;
        public string DisplayName;
        public BlockKind Kind;

        /// <summary>잠긴 채 시작하는 가구의 해금 ID. 아니면 빈 문자열.</summary>
        public string UnlockId;

        /// <summary>가구의 바닥 중심.</summary>
        public Vector3 Position;

        /// <summary>바닥에 차지하는 자리 (x, z).</summary>
        public float XMin;
        public float XMax;
        public float ZMin;
        public float ZMax;

        public List<BlueprintBlock> Blocks = new List<BlueprintBlock>();

        public bool IsLocked => !string.IsNullOrEmpty(UnlockId);
    }

    /// <summary>진열대의 칸 위치와 손님이 서는 자리.</summary>
    public sealed class ShelfBlueprint
    {
        public string ShelfId;
        public Vector3[] SlotPositions;
        public Vector3[] StandPoints;
    }

    /// <summary>
    /// shop_layout.json의 좌표에서 "씬에 무엇을 어디에 놓을지"를 계산한 결과.
    /// Unity의 씬 기능을 쓰지 않는 순수한 계산이라 테스트할 수 있다:
    /// 문 자리가 뚫려 있는지, 가구가 겹치지 않는지, 지점이 가구 안에 있지 않은지를 씬을 열지 않고 확인한다.
    /// 실제로 오브젝트를 만드는 일은 ShopSceneBuilder가 한다.
    /// </summary>
    public sealed class ShopBlueprint
    {
        /// <summary>문 위쪽 벽이 시작되는 높이.</summary>
        public const float DoorHeight = 2.2f;

        /// <summary>진열대에서 빵이 놓이는 면의 높이. 그 위는 뒤판이다.</summary>
        public const float ShelfSurfaceHeight = 0.9f;

        private const float ShelfBackThickness = 0.1f;
        private const float SlabThickness = 0.1f;
        private const float BoundaryHeight = 3f;
        private const float BoundaryThickness = 0.2f;

        public float RoomXMin;
        public float RoomXMax;
        public float RoomZMin;
        public float RoomZMax;
        public float DoorXMin;
        public float DoorXMax;

        /// <summary>바닥, 인도, 벽.</summary>
        public List<BlueprintBlock> Environment = new List<BlueprintBlock>();

        /// <summary>인도 둘레의 보이지 않는 벽. 플레이어가 인도 밖으로 떨어지지 않게 한다.</summary>
        public List<BlueprintBlock> Boundaries = new List<BlueprintBlock>();

        public List<FurnitureBlueprint> Furniture = new List<FurnitureBlueprint>();

        /// <summary>손님이 들어갈 수 없는 구역 (주방, 계산 구역).</summary>
        public List<BlueprintBlock> NoWalkZones = new List<BlueprintBlock>();

        public List<ShelfBlueprint> Shelves = new List<ShelfBlueprint>();

        public Vector3 PlayerStart;
        public Vector3 DeliveryPoint;
        public Vector3 EntryPoint;
        public Vector3 SignPoint;
        public Vector3[] SpawnPoints;
        public Vector3[] QueuePoints;

        public static ShopBlueprint Create(ShopLayoutJson layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var blueprint = new ShopBlueprint
            {
                RoomXMin = layout.Room.XMin,
                RoomXMax = layout.Room.XMax,
                RoomZMin = layout.Room.ZMin,
                RoomZMax = layout.Room.ZMax,
                DoorXMin = layout.Door.XMin,
                DoorXMax = layout.Door.XMax,
            };

            AddEnvironment(blueprint, layout);
            AddBoundaries(blueprint, layout);
            AddFurniture(blueprint, layout);
            AddZones(blueprint, layout);
            AddPoints(blueprint, layout);
            AddShelves(blueprint, layout);
            return blueprint;
        }

        // 벽은 실내 범위의 바깥쪽에 붙인다. 그래야 실내가 정확히 room의 범위가 된다.
        private static void AddEnvironment(ShopBlueprint blueprint, ShopLayoutJson layout)
        {
            LayoutRoomJson room = layout.Room;
            LayoutRectJson sidewalk = layout.Sidewalk;
            LayoutDoorJson door = layout.Door;
            float thickness = room.WallThickness;
            float height = room.WallHeight;
            float outerXMin = room.XMin - thickness;
            float outerXMax = room.XMax + thickness;

            if (door.XMin <= room.XMin || door.XMax >= room.XMax || door.XMax <= door.XMin)
            {
                throw new FormatException("shop_layout.json: door는 남쪽 벽의 범위 안에 있어야 합니다.");
            }

            // 실내 바닥: 남쪽은 실내의 끝(문턱)까지만 깐다. 그 바깥은 인도가 덮는다 (두 바닥이 겹쳐 깜빡이지 않게).
            blueprint.Environment.Add(Slab("Floor", outerXMin, outerXMax, room.ZMin, room.ZMax + thickness, BlockKind.Floor));
            blueprint.Environment.Add(Slab("Sidewalk", sidewalk.XMin, sidewalk.XMax, sidewalk.ZMin, sidewalk.ZMax, BlockKind.Sidewalk));

            blueprint.Environment.Add(Box("Wall_North", outerXMin, outerXMax, 0f, height, room.ZMax, room.ZMax + thickness, BlockKind.Wall));
            blueprint.Environment.Add(Box("Wall_East", room.XMax, outerXMax, 0f, height, room.ZMin - thickness, room.ZMax, BlockKind.Wall));
            blueprint.Environment.Add(Box("Wall_West", outerXMin, room.XMin, 0f, height, room.ZMin - thickness, room.ZMax, BlockKind.Wall));

            // 남쪽 벽은 문 자리를 비우고 두 조각으로 만든다.
            blueprint.Environment.Add(Box("Wall_South_West", room.XMin, door.XMin, 0f, height, room.ZMin - thickness, room.ZMin, BlockKind.Wall));
            blueprint.Environment.Add(Box("Wall_South_East", door.XMax, room.XMax, 0f, height, room.ZMin - thickness, room.ZMin, BlockKind.Wall));
            if (height > DoorHeight)
            {
                blueprint.Environment.Add(Box("Wall_South_AboveDoor", door.XMin, door.XMax, DoorHeight, height, room.ZMin - thickness, room.ZMin, BlockKind.Wall));
            }
        }

        private static void AddBoundaries(ShopBlueprint blueprint, ShopLayoutJson layout)
        {
            LayoutRoomJson room = layout.Room;
            LayoutRectJson sidewalk = layout.Sidewalk;
            float t = BoundaryThickness;
            float h = BoundaryHeight;
            float outerXMin = room.XMin - room.WallThickness;
            float outerXMax = room.XMax + room.WallThickness;

            blueprint.Boundaries.Add(Box("Boundary_South", sidewalk.XMin - t, sidewalk.XMax + t, 0f, h, sidewalk.ZMin - t, sidewalk.ZMin, BlockKind.Wall));
            blueprint.Boundaries.Add(Box("Boundary_West", sidewalk.XMin - t, sidewalk.XMin, 0f, h, sidewalk.ZMin, sidewalk.ZMax, BlockKind.Wall));
            blueprint.Boundaries.Add(Box("Boundary_East", sidewalk.XMax, sidewalk.XMax + t, 0f, h, sidewalk.ZMin, sidewalk.ZMax, BlockKind.Wall));

            // 건물 양옆으로 인도가 더 뻗어 있으면, 그 북쪽 끝을 막는다.
            if (sidewalk.XMin < outerXMin)
            {
                blueprint.Boundaries.Add(Box("Boundary_North_West", sidewalk.XMin - t, outerXMin, 0f, h, sidewalk.ZMax, sidewalk.ZMax + t, BlockKind.Wall));
            }

            if (sidewalk.XMax > outerXMax)
            {
                blueprint.Boundaries.Add(Box("Boundary_North_East", outerXMax, sidewalk.XMax + t, 0f, h, sidewalk.ZMax, sidewalk.ZMax + t, BlockKind.Wall));
            }
        }

        private static void AddFurniture(ShopBlueprint blueprint, ShopLayoutJson layout)
        {
            var facingByShelf = new Dictionary<string, string>();
            foreach (LayoutShelfJson shelf in layout.Shelves)
            {
                facingByShelf[shelf.Id] = shelf.Facing;
            }

            var seen = new HashSet<string>();
            foreach (LayoutFurnitureJson item in layout.Furniture)
            {
                if (string.IsNullOrEmpty(item.Id) || !seen.Add(item.Id))
                {
                    throw new FormatException("shop_layout.json: 가구 ID '" + item.Id + "'가 비어 있거나 중복됩니다.");
                }

                if (item.SizeX <= 0f || item.SizeZ <= 0f || item.Height <= 0f)
                {
                    throw new FormatException("shop_layout.json: 가구 '" + item.Id + "'의 크기는 0보다 커야 합니다.");
                }

                var furniture = new FurnitureBlueprint
                {
                    Id = item.Id,
                    DisplayName = item.Name,
                    Kind = ParseKind(item),
                    UnlockId = item.UnlockId ?? "",
                    Position = new Vector3(item.X, 0f, item.Z),
                    XMin = item.X - item.SizeX * 0.5f,
                    XMax = item.X + item.SizeX * 0.5f,
                    ZMin = item.Z - item.SizeZ * 0.5f,
                    ZMax = item.Z + item.SizeZ * 0.5f,
                };

                if (furniture.Kind == BlockKind.Shelf)
                {
                    string facing;
                    if (!facingByShelf.TryGetValue(item.Id, out facing))
                    {
                        throw new FormatException("shop_layout.json: 진열대 '" + item.Id + "'의 칸 정보(shelves)가 없습니다.");
                    }

                    AddShelfBlocks(furniture, item, FacingSign(facing, item.Id));
                }
                else
                {
                    furniture.Blocks.Add(Box("Body", furniture.XMin, furniture.XMax, 0f, item.Height, furniture.ZMin, furniture.ZMax, furniture.Kind));
                }

                blueprint.Furniture.Add(furniture);
            }
        }

        // 진열대 = 빵을 놓는 낮은 몸통 + 손님 반대쪽의 뒤판.
        private static void AddShelfBlocks(FurnitureBlueprint furniture, LayoutFurnitureJson item, float facingSign)
        {
            float surface = Mathf.Min(ShelfSurfaceHeight, item.Height);
            furniture.Blocks.Add(Box("Body", furniture.XMin, furniture.XMax, 0f, surface, furniture.ZMin, furniture.ZMax, BlockKind.Shelf));

            if (item.Height <= surface)
            {
                return;
            }

            // facingSign이 +1(동쪽)이면 손님이 동쪽에 서므로 뒤판은 서쪽 끝에 붙는다.
            float backXMin = facingSign > 0f ? furniture.XMin : furniture.XMax - ShelfBackThickness;
            furniture.Blocks.Add(Box("Back", backXMin, backXMin + ShelfBackThickness, surface, item.Height, furniture.ZMin, furniture.ZMax, BlockKind.Shelf));
        }

        private static void AddZones(ShopBlueprint blueprint, ShopLayoutJson layout)
        {
            foreach (LayoutZoneJson zone in layout.Zones)
            {
                if (zone.CustomersCanWalk)
                {
                    continue;
                }

                blueprint.NoWalkZones.Add(Box("NoWalk_" + zone.Id, zone.XMin, zone.XMax, 0f, layout.Room.WallHeight, zone.ZMin, zone.ZMax, BlockKind.Floor));
            }
        }

        private static void AddPoints(ShopBlueprint blueprint, ShopLayoutJson layout)
        {
            var points = new Dictionary<string, Vector3>();
            foreach (LayoutPointJson point in layout.Points)
            {
                points[point.Id] = new Vector3(point.X, 0f, point.Z);
            }

            blueprint.PlayerStart = Require(points, "player.start");
            blueprint.DeliveryPoint = Require(points, "delivery");
            blueprint.EntryPoint = Require(points, "customer.entry");
            blueprint.SignPoint = Require(points, "sign");
            blueprint.SpawnPoints = new[] { Require(points, "customer.spawn.west"), Require(points, "customer.spawn.east") };

            // queue.0, queue.1, … 을 빠진 번호가 나올 때까지 모은다.
            var queue = new List<Vector3>();
            Vector3 queuePoint;
            while (points.TryGetValue("queue." + queue.Count, out queuePoint))
            {
                queue.Add(queuePoint);
            }

            if (queue.Count == 0)
            {
                throw new FormatException("shop_layout.json: 지점 'queue.0'(손님 계산 위치)이 없습니다.");
            }

            blueprint.QueuePoints = queue.ToArray();
        }

        private static void AddShelves(ShopBlueprint blueprint, ShopLayoutJson layout)
        {
            foreach (LayoutShelfJson shelf in layout.Shelves)
            {
                FurnitureBlueprint furniture = blueprint.Furniture.Find(item => item.Id == shelf.Id);
                if (furniture == null || furniture.Kind != BlockKind.Shelf)
                {
                    throw new FormatException("shop_layout.json: shelves의 '" + shelf.Id + "'가 진열대 가구 목록에 없습니다.");
                }

                FacingSign(shelf.Facing, shelf.Id);

                float[] slotZ = shelf.SlotZ ?? new float[0];
                var slots = new Vector3[slotZ.Length];
                var stands = new Vector3[slotZ.Length];
                for (int i = 0; i < slotZ.Length; i++)
                {
                    slots[i] = new Vector3(furniture.Position.x, ShelfSurfaceHeight, slotZ[i]);
                    stands[i] = new Vector3(shelf.StandX, 0f, slotZ[i]);
                }

                blueprint.Shelves.Add(new ShelfBlueprint { ShelfId = shelf.Id, SlotPositions = slots, StandPoints = stands });
            }
        }

        private static BlockKind ParseKind(LayoutFurnitureJson item)
        {
            switch (item.Kind)
            {
                case "furniture":
                    return BlockKind.Furniture;
                case "wall":
                    return BlockKind.Partition;
                case "shelf":
                    return BlockKind.Shelf;
                case "oven":
                    return BlockKind.Oven;
                case "storage":
                    return BlockKind.Storage;
                default:
                    throw new FormatException("shop_layout.json: 가구 '" + item.Id + "'의 kind '" + item.Kind + "'를 모릅니다 (furniture, wall, shelf, oven, storage).");
            }
        }

        /// <summary>손님이 서는 쪽. east면 +1, west면 −1.</summary>
        private static float FacingSign(string facing, string shelfId)
        {
            switch (facing)
            {
                case "east":
                    return 1f;
                case "west":
                    return -1f;
                default:
                    throw new FormatException("shop_layout.json: 진열대 '" + shelfId + "'의 facing '" + facing + "'를 모릅니다 (east, west).");
            }
        }

        private static Vector3 Require(Dictionary<string, Vector3> points, string id)
        {
            Vector3 point;
            if (points.TryGetValue(id, out point))
            {
                return point;
            }

            throw new FormatException("shop_layout.json: 지점 '" + id + "'가 없습니다.");
        }

        /// <summary>윗면이 y = 0인 얇은 바닥판.</summary>
        private static BlueprintBlock Slab(string name, float xMin, float xMax, float zMin, float zMax, BlockKind kind)
        {
            return Box(name, xMin, xMax, -SlabThickness, 0f, zMin, zMax, kind);
        }

        private static BlueprintBlock Box(string name, float xMin, float xMax, float yMin, float yMax, float zMin, float zMax, BlockKind kind)
        {
            var center = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, (zMin + zMax) * 0.5f);
            var size = new Vector3(xMax - xMin, yMax - yMin, zMax - zMin);
            return new BlueprintBlock(name, center, size, kind);
        }
    }
}

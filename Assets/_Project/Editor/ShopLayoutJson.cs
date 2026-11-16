using System;
using Newtonsoft.Json;

namespace BakeryShop.EditorTools
{
    // shop_layout.json의 모양을 그대로 옮긴 클래스들이다.
    // 같은 파일을 평면도 스크립트(Tools/docs/shop_layout.py)도 읽는다.
    // 이름에 Layout을 붙인 이유: 콘텐츠 쪽의 ShelfJson(진열대 설정)과 겹치지 않게.

    public sealed class ShopLayoutJson
    {
        public int Version = 1;
        public LayoutRoomJson Room;
        public LayoutRectJson Sidewalk;
        public LayoutDoorJson Door;
        public LayoutZoneJson[] Zones = new LayoutZoneJson[0];
        public LayoutFurnitureJson[] Furniture = new LayoutFurnitureJson[0];
        public LayoutShelfJson[] Shelves = new LayoutShelfJson[0];
        public LayoutPointJson[] Points = new LayoutPointJson[0];
        public LayoutStandJson[] PlayerStands = new LayoutStandJson[0];

        /// <summary>평면도에 그리는 플레이어 동선. 씬에서는 쓰지 않는다.</summary>
        public float[][] StaffPath = new float[0][];

        /// <summary>읽지 못하면 FormatException. 모르는 키도 오류로 본다 (오타 방지).</summary>
        public static ShopLayoutJson Read(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                throw new FormatException("shop_layout.json이 비어 있습니다.");
            }

            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error,
            };

            ShopLayoutJson layout;
            try
            {
                layout = JsonConvert.DeserializeObject<ShopLayoutJson>(jsonText, settings);
            }
            catch (JsonException exception)
            {
                throw new FormatException("shop_layout.json을 읽을 수 없습니다: " + exception.Message, exception);
            }

            if (layout == null || layout.Room == null || layout.Sidewalk == null || layout.Door == null)
            {
                throw new FormatException("shop_layout.json에 room, sidewalk, door가 모두 있어야 합니다.");
            }

            return layout;
        }
    }

    public sealed class LayoutRoomJson
    {
        public float XMin;
        public float ZMin;
        public float XMax;
        public float ZMax;
        public float WallHeight;
        public float WallThickness;
    }

    public sealed class LayoutRectJson
    {
        public float XMin;
        public float ZMin;
        public float XMax;
        public float ZMax;
    }

    public sealed class LayoutDoorJson
    {
        public float XMin;
        public float XMax;
    }

    public sealed class LayoutZoneJson
    {
        public string Id = "";
        public string Name = "";
        public float XMin;
        public float ZMin;
        public float XMax;
        public float ZMax;
        public bool CustomersCanWalk = true;
    }

    public sealed class LayoutFurnitureJson
    {
        public string Id = "";
        public string Name = "";

        /// <summary>furniture, wall, shelf, oven, storage. 그레이박스의 색을 정한다.</summary>
        public string Kind = "furniture";

        public float X;
        public float Z;
        public float SizeX;
        public float SizeZ;
        public float Height;

        /// <summary>잠긴 채 시작하는 가구의 해금 ID. 처음부터 쓸 수 있으면 빈 문자열.</summary>
        public string UnlockId = "";
    }

    public sealed class LayoutShelfJson
    {
        public string Id = "";

        /// <summary>east 또는 west. 손님이 서는 쪽이다.</summary>
        public string Facing = "east";

        public float[] SlotZ = new float[0];
        public float StandX;
    }

    public sealed class LayoutPointJson
    {
        public string Id = "";
        public string Name = "";
        public string Kind = "";
        public float X;
        public float Z;
    }

    public sealed class LayoutStandJson
    {
        public string Target = "";
        public float X;
        public float Z;
    }
}

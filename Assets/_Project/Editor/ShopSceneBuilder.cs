using System;
using System.Collections.Generic;
using System.IO;
using BakeryShop.Core;
using BakeryShop.Interaction;
using BakeryShop.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BakeryShop.EditorTools
{
    /// <summary>
    /// shop_layout.json의 좌표대로 매장 씬(Scenes/Shop.unity)을 만든다.
    /// 메뉴: BakeryShop > 매장 씬 만들기
    /// 실행할 때마다 씬을 처음부터 다시 만든다. 그레이박스 단계에서는 씬을 손으로 고치지 않고 이 빌더(와 JSON)를 고친다.
    /// 무엇을 어디에 놓을지는 ShopBlueprint가 계산하고, 여기서는 그 결과를 오브젝트로 옮기기만 한다.
    /// </summary>
    public static class ShopSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Shop.unity";
        public const string LayoutPath = "Assets/_Project/Data/shop_layout.json";

        private const string InputActionsPath = "Assets/_Project/Input/BakeryControls.inputactions";
        private const string DialogTitle = "매장 씬";

        // 내비게이션의 기본 영역 번호. 1번이 "걸을 수 없음"이다.
        private const int NotWalkableArea = 1;

        // 잠긴 가구의 투명도.
        private const float LockedAlpha = 0.35f;

        private static readonly Vector3 BoxSize = new Vector3(0.5f, 0.5f, 0.5f);

        [MenuItem("BakeryShop/매장 씬 만들기")]
        public static void Build()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog(DialogTitle, "이미 매장 씬이 있습니다. 처음부터 다시 만들까요?\n(씬에서 손으로 고친 내용은 사라집니다)", "다시 만들기", "취소"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string problem = BuildWithoutPrompts();
            if (problem != null)
            {
                EditorUtility.DisplayDialog(DialogTitle, problem, "확인");
            }
        }

        /// <summary>
        /// 확인 창 없이 씬을 만든다 (자동화용). 열려 있는 씬의 저장하지 않은 변경은 버려진다.
        /// 성공하면 null, 만들지 못했으면 이유를 돌려준다.
        /// </summary>
        public static string BuildWithoutPrompts()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
            {
                return "입력 에셋을 찾을 수 없습니다.\n" + InputActionsPath;
            }

            ShopBlueprint blueprint;
            try
            {
                blueprint = ShopBlueprint.Create(ShopLayoutJson.Read(File.ReadAllText(LayoutPath)));
            }
            catch (Exception exception) when (exception is FormatException || exception is IOException)
            {
                return "매장 배치를 읽을 수 없습니다.\n" + exception.Message;
            }

            int playerLayer = SceneBuildUtility.EnsureLayer(PhysicsLayers.Player);
            int customerLayer = SceneBuildUtility.EnsureLayer(PhysicsLayers.Customer);
            int itemLayer = SceneBuildUtility.EnsureLayer(PhysicsLayers.Item);
            Physics.IgnoreLayerCollision(playerLayer, customerLayer, true);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment(blueprint, playerLayer, customerLayer, itemLayer);
            BuildFurniture(blueprint);
            BuildPoints(blueprint);
            BuildTestBoxes(blueprint, itemLayer);

            PlayerRig player = PlayerRigBuilder.Build("[Player]", actions, blueprint.PlayerStart, 0f);
            SceneBuildUtility.SetLayerRecursively(player.Root, playerLayer);

            // HUD는 M2-09에서 만든다. 그때까지는 M1의 조준점과 안내만 띄운다.
            GreyboxSceneBuilder.BuildHud(player.Interactor);

            var bootstrap = new GameObject("[Bootstrap]").AddComponent<GameBootstrap>();
            SceneBuildUtility.SetReference(bootstrap, "_playerInput", player.Input);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildUtility.AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[매장 씬] " + ScenePath + " 생성 완료: 가구 " + blueprint.Furniture.Count + "개, 진열대 " + blueprint.Shelves.Count
                + "개, 줄 자리 " + blueprint.QueuePoints.Length + "개. 레이어 Player=" + playerLayer + ", Customer=" + customerLayer + ", Item=" + itemLayer);
            return null;
        }

        private static void BuildEnvironment(ShopBlueprint blueprint, int playerLayer, int customerLayer, int itemLayer)
        {
            var environment = new GameObject("[Environment]");

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.transform.SetParent(environment.transform, false);
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            foreach (BlueprintBlock block in blueprint.Environment)
            {
                SceneBuildUtility.CreateBlock(block.Name, environment.transform, block.Center, block.Size, MaterialFor(block.Kind, false));
            }

            Transform boundaries = new GameObject("Boundaries").transform;
            boundaries.SetParent(environment.transform, false);
            foreach (BlueprintBlock block in blueprint.Boundaries)
            {
                SceneBuildUtility.CreateInvisibleWall(block.Name, boundaries, block.Center, block.Size);
            }

            // 손님의 길 찾기. 여기서는 굽지 않는다: 씬이 시작될 때 한 번 만든다 (M4-04).
            // 구운 데이터를 에셋으로 두지 않으므로 씬을 다시 만들어도 어긋날 것이 없다.
            var surface = environment.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~((1 << playerLayer) | (1 << customerLayer) | (1 << itemLayer));

            Transform zones = new GameObject("NavZones").transform;
            zones.SetParent(environment.transform, false);
            foreach (BlueprintBlock zone in blueprint.NoWalkZones)
            {
                var volumeObject = new GameObject(zone.Name);
                volumeObject.transform.SetParent(zones, false);
                volumeObject.transform.localPosition = zone.Center;

                var volume = volumeObject.AddComponent<NavMeshModifierVolume>();
                volume.center = Vector3.zero;
                volume.size = zone.Size;
                volume.area = NotWalkableArea;
            }
        }

        private static void BuildFurniture(ShopBlueprint blueprint)
        {
            Transform parent = new GameObject("[Furniture]").transform;
            foreach (FurnitureBlueprint furniture in blueprint.Furniture)
            {
                var root = new GameObject(furniture.Id);
                root.transform.SetParent(parent, false);
                root.transform.localPosition = furniture.Position;
                root.isStatic = true;
                root.AddComponent<FurnitureId>().SetId(furniture.Id);

                Material material = MaterialFor(furniture.Kind, furniture.IsLocked);
                foreach (BlueprintBlock block in furniture.Blocks)
                {
                    // 상자의 좌표는 월드 기준이므로 가구의 위치를 빼서 자식의 로컬 좌표로 바꾼다.
                    SceneBuildUtility.CreateBlock(block.Name, root.transform, block.Center - furniture.Position, block.Size, material);
                }
            }
        }

        private static void BuildPoints(ShopBlueprint blueprint)
        {
            var layout = new GameObject("[Points]").AddComponent<ShopLayout>();

            var shelves = new List<ShopLayout.ShelfPoints>();
            foreach (ShelfBlueprint shelf in blueprint.Shelves)
            {
                shelves.Add(new ShopLayout.ShelfPoints
                {
                    ShelfId = shelf.ShelfId,
                    SlotPositions = shelf.SlotPositions,
                    StandPoints = shelf.StandPoints,
                });
            }

            layout.Configure(
                blueprint.PlayerStart,
                blueprint.DeliveryPoint,
                blueprint.EntryPoint,
                blueprint.SpawnPoints,
                blueprint.QueuePoints,
                shelves.ToArray());
            EditorUtility.SetDirty(layout);
        }

        // 배송 지점에 박스 두 개를 놓아 둔다. 박스를 들고 매장을 가로질러 창고까지 가 보는 용도다.
        // 발주와 배송이 생기면(M3-03) 이 박스는 빌더에서 뺀다.
        private static void BuildTestBoxes(ShopBlueprint blueprint, int itemLayer)
        {
            Material material = SceneBuildUtility.GetOrCreateMaterial("Greybox_Box", new Color(0.82f, 0.62f, 0.38f));
            Transform parent = new GameObject("[Items]").transform;

            for (int i = 0; i < 2; i++)
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "DeliveryBox_Test_" + (i + 1);
                box.layer = itemLayer;
                box.transform.SetParent(parent, false);
                box.transform.localPosition = blueprint.DeliveryPoint + new Vector3(i * 0.7f, BoxSize.y * 0.5f, 0f);
                box.transform.localScale = BoxSize;
                box.GetComponent<MeshRenderer>().sharedMaterial = material;

                var holdable = box.AddComponent<HoldableObject>(); // Rigidbody는 RequireComponent로 함께 붙는다.
                SceneBuildUtility.SetString(holdable, "_displayName", "배송 박스");

                var body = box.GetComponent<Rigidbody>();
                body.mass = 2f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }

        // 색은 평면도(docs/images/shop-layout.svg)와 맞춘다. 잠긴 가구는 같은 색의 반투명이다.
        private static Material MaterialFor(BlockKind kind, bool locked)
        {
            string name;
            Color color;
            switch (kind)
            {
                case BlockKind.Floor:
                    name = "Shop_Floor";
                    color = new Color(0.87f, 0.82f, 0.72f);
                    break;
                case BlockKind.Sidewalk:
                    name = "Shop_Sidewalk";
                    color = new Color(0.76f, 0.76f, 0.76f);
                    break;
                case BlockKind.Wall:
                    name = "Shop_Wall";
                    color = new Color(0.92f, 0.91f, 0.88f);
                    break;
                case BlockKind.Partition:
                    name = "Shop_Partition";
                    color = new Color(0.7f, 0.7f, 0.72f);
                    break;
                case BlockKind.Shelf:
                    name = "Shop_Shelf";
                    color = new Color(0.93f, 0.6f, 0.25f);
                    break;
                case BlockKind.Oven:
                    name = "Shop_Oven";
                    color = new Color(0.8f, 0.3f, 0.25f);
                    break;
                case BlockKind.Storage:
                    name = "Shop_Storage";
                    color = new Color(0.3f, 0.5f, 0.8f);
                    break;
                default:
                    name = "Shop_Furniture";
                    color = new Color(0.55f, 0.55f, 0.58f);
                    break;
            }

            if (!locked)
            {
                return SceneBuildUtility.GetOrCreateMaterial(name, color);
            }

            color.a = LockedAlpha;
            return SceneBuildUtility.GetOrCreateMaterial(name + "_Locked", color, transparent: true);
        }
    }
}

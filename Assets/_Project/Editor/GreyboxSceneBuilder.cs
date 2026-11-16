using System.IO;
using BakeryShop.Core;
using BakeryShop.Data;
using BakeryShop.Interaction;
using BakeryShop.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BakeryShop.EditorTools
{
    /// <summary>
    /// 1주차 조작감 확인용 그레이박스 씬을 메뉴 한 번으로 만든다.
    /// 메뉴: BakeryShop > 그레이박스 씬 만들기 (1주차)
    /// 씬을 손으로 배치하면 컴포넌트 연결을 빠뜨리기 쉬워서, 연결까지 코드로 고정해 둔다.
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Greybox_Week1.unity";
        private const string InputActionsPath = "Assets/_Project/Input/BakeryControls.inputactions";

        private static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, -3f);

        [MenuItem("BakeryShop/그레이박스 씬 만들기 (1주차)")]
        public static void Build()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
            {
                EditorUtility.DisplayDialog("그레이박스 씬", $"입력 에셋을 찾을 수 없습니다.\n{InputActionsPath}", "확인");
                return;
            }

            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("그레이박스 씬", "이미 씬이 있습니다. 새로 만들어 덮어쓸까요?", "덮어쓰기", "취소"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string uiProblem = ShopUiBuilder.CheckPrerequisites();
            if (uiProblem != null)
            {
                EditorUtility.DisplayDialog("그레이박스 씬", uiProblem, "확인");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            BuildEnvironment();
            BuildBoxes();
            PlayerRig player = PlayerRigBuilder.Build("Player", actions, PlayerSpawn, 0f);
            ShopUiBuilder.BuildPromptOnly(player.Interactor);
            BuildBootstrap(player.Input);

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildUtility.AddToBuildSettings(ScenePath);
            Debug.Log($"[GreyboxSceneBuilder] {ScenePath} 생성 완료. 플레이해서 조작감을 확인하세요.");
        }

        private static void CreateLight()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void BuildEnvironment()
        {
            Material floor = SceneBuildUtility.GetOrCreateMaterial("Greybox_Floor", new Color(0.55f, 0.55f, 0.55f));
            Material wall = SceneBuildUtility.GetOrCreateMaterial("Greybox_Wall", new Color(0.8f, 0.8f, 0.78f));
            Material furniture = SceneBuildUtility.GetOrCreateMaterial("Greybox_Furniture", new Color(0.45f, 0.6f, 0.75f));

            Transform env = new GameObject("[Environment]").transform;
            SceneBuildUtility.CreateBlock("Floor", env, new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 12f), floor);
            SceneBuildUtility.CreateBlock("Wall_North", env, new Vector3(0f, 1.5f, 6f), new Vector3(12f, 3f, 0.2f), wall);
            SceneBuildUtility.CreateBlock("Wall_South", env, new Vector3(0f, 1.5f, -6f), new Vector3(12f, 3f, 0.2f), wall);
            SceneBuildUtility.CreateBlock("Wall_East", env, new Vector3(6f, 1.5f, 0f), new Vector3(0.2f, 3f, 12f), wall);
            SceneBuildUtility.CreateBlock("Wall_West", env, new Vector3(-6f, 1.5f, 0f), new Vector3(0.2f, 3f, 12f), wall);

            // 확인용 가구: 윗면에 박스 올리기, 벽·선반에 대고 내려놓기, 턱 넘기.
            Transform furn = new GameObject("[Furniture]").transform;
            SceneBuildUtility.CreateBlock("Shelf_01", furn, new Vector3(-3f, 0.6f, 5f), new Vector3(2f, 1.2f, 0.6f), furniture);
            SceneBuildUtility.CreateBlock("Shelf_02", furn, new Vector3(3f, 0.6f, 5f), new Vector3(2f, 1.2f, 0.6f), furniture);
            SceneBuildUtility.CreateBlock("Table", furn, new Vector3(3.5f, 0.4f, 0f), new Vector3(1.5f, 0.8f, 0.8f), furniture);
            SceneBuildUtility.CreateBlock("Step_25cm", furn, new Vector3(-3.5f, 0.125f, 0f), new Vector3(2f, 0.25f, 2f), furniture);
        }

        private static void BuildBoxes()
        {
            Material boxMat = SceneBuildUtility.GetOrCreateMaterial("Greybox_Box", new Color(0.82f, 0.62f, 0.38f));
            Transform items = new GameObject("[Items]").transform;

            Vector3[] positions =
            {
                new Vector3(-1f, 0.25f, 0f),
                new Vector3(0f, 0.25f, 0f),
                new Vector3(1f, 0.25f, 0f),
                new Vector3(0f, 0.75f, 0f), // 쌓여 있는 박스
                new Vector3(3.5f, 1.05f, 0f), // 테이블 위
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"DeliveryBox_{i + 1:00}";
                box.transform.SetParent(items, false);
                box.transform.localPosition = positions[i];
                box.transform.localScale = Vector3.one * 0.5f;
                box.GetComponent<MeshRenderer>().sharedMaterial = boxMat;

                var holdable = box.AddComponent<HoldableObject>(); // Rigidbody는 RequireComponent로 함께 붙는다.
                SceneBuildUtility.SetString(holdable, "_displayName", "배송 박스");

                var body = box.GetComponent<Rigidbody>();
                body.mass = 2f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                // kinematic과 일반 상태를 오가므로 양쪽 모두 지원하는 Speculative 방식을 쓴다.
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }

        private static void BuildBootstrap(PlayerInputReader input)
        {
            var bootstrap = new GameObject("[Bootstrap]").AddComponent<GameBootstrap>();
            SceneBuildUtility.SetReference(bootstrap, "_playerInput", input);

            // 부트스트랩은 콘텐츠가 있어야 시스템을 만든다 (M2-09부터). 이 씬에는 매장 배치와 패널이 없으므로 나머지는 비워 둔다.
            var content = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentImporter.DatabasePath);
            if (content == null)
            {
                Debug.LogWarning("[GreyboxSceneBuilder] 콘텐츠 에셋이 없습니다. 메뉴 BakeryShop > 콘텐츠 가져오기를 실행한 뒤 씬을 다시 만드세요.");
            }

            SceneBuildUtility.SetReference(bootstrap, "_content", content);
        }
    }
}

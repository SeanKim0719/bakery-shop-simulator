using System.IO;
using System.Linq;
using BakeryShop.Core;
using BakeryShop.Interaction;
using BakeryShop.Player;
using BakeryShop.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        private const string MaterialFolder = "Assets/_Project/Art/Materials";

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

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            BuildEnvironment();
            BuildBoxes();
            PlayerRig player = BuildPlayer(actions);
            BuildHud(player.Interactor);
            BuildBootstrap(player.Input);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[GreyboxSceneBuilder] {ScenePath} 생성 완료. 플레이해서 조작감을 확인하세요.");
        }

        private readonly struct PlayerRig
        {
            public readonly PlayerInputReader Input;
            public readonly Interactor Interactor;

            public PlayerRig(PlayerInputReader input, Interactor interactor)
            {
                Input = input;
                Interactor = interactor;
            }
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
            Material floor = GetOrCreateMaterial("Greybox_Floor", new Color(0.55f, 0.55f, 0.55f));
            Material wall = GetOrCreateMaterial("Greybox_Wall", new Color(0.8f, 0.8f, 0.78f));
            Material furniture = GetOrCreateMaterial("Greybox_Furniture", new Color(0.45f, 0.6f, 0.75f));

            Transform env = new GameObject("[Environment]").transform;
            CreateBlock("Floor", env, new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 12f), floor);
            CreateBlock("Wall_North", env, new Vector3(0f, 1.5f, 6f), new Vector3(12f, 3f, 0.2f), wall);
            CreateBlock("Wall_South", env, new Vector3(0f, 1.5f, -6f), new Vector3(12f, 3f, 0.2f), wall);
            CreateBlock("Wall_East", env, new Vector3(6f, 1.5f, 0f), new Vector3(0.2f, 3f, 12f), wall);
            CreateBlock("Wall_West", env, new Vector3(-6f, 1.5f, 0f), new Vector3(0.2f, 3f, 12f), wall);

            // 확인용 가구: 윗면에 박스 올리기, 벽·선반에 대고 내려놓기, 턱 넘기.
            Transform furn = new GameObject("[Furniture]").transform;
            CreateBlock("Shelf_01", furn, new Vector3(-3f, 0.6f, 5f), new Vector3(2f, 1.2f, 0.6f), furniture);
            CreateBlock("Shelf_02", furn, new Vector3(3f, 0.6f, 5f), new Vector3(2f, 1.2f, 0.6f), furniture);
            CreateBlock("Table", furn, new Vector3(3.5f, 0.4f, 0f), new Vector3(1.5f, 0.8f, 0.8f), furniture);
            CreateBlock("Step_25cm", furn, new Vector3(-3.5f, 0.125f, 0f), new Vector3(2f, 0.25f, 2f), furniture);
        }

        private static void BuildBoxes()
        {
            Material boxMat = GetOrCreateMaterial("Greybox_Box", new Color(0.82f, 0.62f, 0.38f));
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
                SetString(holdable, "_displayName", "배송 박스");

                var body = box.GetComponent<Rigidbody>();
                body.mass = 2f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                // kinematic과 일반 상태를 오가므로 양쪽 모두 지원하는 Speculative 방식을 쓴다.
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }

        private static PlayerRig BuildPlayer(InputActionAsset actions)
        {
            var player = new GameObject("Player");
            player.transform.position = PlayerSpawn;

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;

            var input = player.AddComponent<PlayerInputReader>();
            var motor = player.AddComponent<PlayerMotor>();
            var hands = player.AddComponent<Hands>();
            var interactor = player.AddComponent<Interactor>();

            Transform pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(pivot, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 70f;

            Transform holdAnchor = new GameObject("HoldAnchor").transform;
            holdAnchor.SetParent(cameraObject.transform, false);
            holdAnchor.localPosition = new Vector3(0f, -0.3f, 0.9f);

            SetReference(input, "_actions", actions);
            SetReference(motor, "_input", input);
            SetReference(motor, "_cameraPivot", pivot);
            SetReference(hands, "_holdAnchor", holdAnchor);
            SetReference(interactor, "_input", input);
            SetReference(interactor, "_hands", hands);
            SetReference(interactor, "_motor", motor);
            SetReference(interactor, "_viewOrigin", cameraObject.transform);

            return new PlayerRig(input, interactor);
        }

        private static void BuildHud(Interactor interactor)
        {
            var canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var crosshair = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            crosshair.transform.SetParent(canvasObject.transform, false);
            var crosshairRect = (RectTransform)crosshair.transform;
            crosshairRect.sizeDelta = new Vector2(6f, 6f);
            crosshair.GetComponent<Image>().raycastTarget = false;

            var promptObject = new GameObject("InteractionPrompt", typeof(RectTransform), typeof(Text), typeof(Shadow));
            promptObject.transform.SetParent(canvasObject.transform, false);
            var promptRect = (RectTransform)promptObject.transform;
            promptRect.sizeDelta = new Vector2(800f, 60f);
            promptRect.anchoredPosition = new Vector2(0f, -80f);
            var label = promptObject.GetComponent<Text>();
            label.fontSize = 30;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = string.Empty;

            var hud = canvasObject.AddComponent<InteractionPromptHUD>();
            SetReference(hud, "_interactor", interactor);
            SetReference(hud, "_label", label);
        }

        private static void BuildBootstrap(PlayerInputReader input)
        {
            var bootstrap = new GameObject("[Bootstrap]").AddComponent<GameBootstrap>();
            SetReference(bootstrap, "_playerInput", input);
        }

        private static GameObject CreateBlock(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            block.isStatic = true;
            return block;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"[GreyboxSceneBuilder] {target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.", target);
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(UnityEngine.Object target, string fieldName, string value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"[GreyboxSceneBuilder] {target.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.", target);
                return;
            }

            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == scenePath))
            {
                return;
            }

            EditorBuildSettings.scenes = scenes.Append(new EditorBuildSettingsScene(scenePath, true)).ToArray();
        }
    }
}

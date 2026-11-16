using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BakeryShop.EditorTools
{
    /// <summary>씬 빌더들이 함께 쓰는 도구: 상자 만들기, 재질, 컴포넌트 연결, 레이어.</summary>
    internal static class SceneBuildUtility
    {
        public const string MaterialFolder = "Assets/_Project/Art/Materials";

        // 0~5번은 Unity가 쓰는 레이어다.
        private const int FirstUserLayer = 6;

        /// <summary>크기 1의 큐브를 늘려 만든 상자. 콜라이더가 함께 붙는다.</summary>
        public static GameObject CreateBlock(string name, Transform parent, Vector3 position, Vector3 size, Material material)
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

        /// <summary>보이지 않는 벽. 콜라이더만 있다.</summary>
        public static GameObject CreateInvisibleWall(string name, Transform parent, Vector3 position, Vector3 size)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            wall.AddComponent<BoxCollider>().size = size;
            wall.isStatic = true;
            return wall;
        }

        /// <summary>
        /// 재질을 찾거나 만든다. 이미 있으면 색만 다시 맞춘다 (빌더의 색을 고치고 다시 실행하면 반영되게).
        /// </summary>
        public static Material GetOrCreateMaterial(string name, Color color, bool transparent = false)
        {
            EnsureFolder(MaterialFolder);
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (transparent)
            {
                MakeTransparent(material);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>인스펙터에서 연결하던 참조를 코드로 연결한다. 필드 이름이 틀리면 오류를 남긴다.</summary>
        public static void SetReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[씬 빌더] " + target.GetType().Name + "." + fieldName + " 필드를 찾을 수 없습니다.", target);
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(UnityEngine.Object target, string fieldName, string value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[씬 빌더] " + target.GetType().Name + "." + fieldName + " 필드를 찾을 수 없습니다.", target);
                return;
            }

            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void AddToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Any(scene => scene.path == scenePath))
            {
                return;
            }

            EditorBuildSettings.scenes = scenes.Append(new EditorBuildSettingsScene(scenePath, true)).ToArray();
        }

        /// <summary>이름의 레이어 번호를 돌려준다. 없으면 빈 칸에 추가한다 (Project Settings > Tags and Layers).</summary>
        public static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
            {
                return existing;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                throw new InvalidOperationException("ProjectSettings/TagManager.asset을 읽을 수 없습니다.");
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = FirstUserLayer; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }

            throw new InvalidOperationException("레이어 '" + layerName + "'를 넣을 빈 칸이 없습니다.");
        }

        /// <summary>오브젝트와 그 아래 전부의 레이어를 바꾼다.</summary>
        public static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // URP Lit 재질을 반투명으로 바꾼다 (인스펙터에서 Surface Type을 Transparent로 고르는 것과 같은 설정).
        private static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}

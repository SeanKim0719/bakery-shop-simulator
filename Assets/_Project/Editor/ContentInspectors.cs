using BakeryShop.Data;
using UnityEditor;
using UnityEngine;

namespace BakeryShop.EditorTools
{
    /// <summary>
    /// 가져온 콘텐츠 에셋의 인스펙터. 수치는 회색(읽기 전용)으로 보여 준다.
    /// 수치의 원본은 content.json이라서, 인스펙터에서 고쳐도 다음 가져오기 때 덮어쓰인다.
    /// 그 사실을 모르고 인스펙터에서 밸런스를 맞추다가 작업을 날리는 일을 막는다.
    /// </summary>
    public abstract class GeneratedContentEditor : Editor
    {
        /// <summary>아이콘·프리팹 같은 외형 필드를 고칠 수 있게 둘지.</summary>
        protected abstract bool AllowAppearanceEditing { get; }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                AllowAppearanceEditing
                    ? "수치는 content.json에서 가져옵니다. 여기서는 외형(아이콘, 프리팹)만 연결하세요.\n수치를 바꾸려면 content.json을 고치고 메뉴 BakeryShop > 콘텐츠 가져오기를 실행합니다."
                    : "content.json에서 가져온 에셋입니다. 직접 고치지 않습니다.\n바꾸려면 content.json을 고치고 메뉴 BakeryShop > 콘텐츠 가져오기를 실행합니다.",
                MessageType.Info);

            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                bool editable = AllowAppearanceEditing && IsAppearance(property);
                using (new EditorGUI.DisabledScope(!editable))
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsAppearance(SerializedProperty property)
        {
            if (property.propertyPath == "m_Script")
            {
                return false;
            }

            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                return true;
            }

            return property.isArray
                && property.propertyType != SerializedPropertyType.String
                && property.arrayElementType.StartsWith("PPtr<", System.StringComparison.Ordinal);
        }
    }

    [CustomEditor(typeof(ContentDefinition), true)]
    [CanEditMultipleObjects]
    public sealed class ContentDefinitionEditor : GeneratedContentEditor
    {
        protected override bool AllowAppearanceEditing => true;
    }

    [CustomEditor(typeof(GameConfig))]
    public sealed class GameConfigEditor : GeneratedContentEditor
    {
        protected override bool AllowAppearanceEditing => false;
    }

    [CustomEditor(typeof(ContentDatabase))]
    public sealed class ContentDatabaseEditor : GeneratedContentEditor
    {
        protected override bool AllowAppearanceEditing => false;
    }
}

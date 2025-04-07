using UnityEditor;
using UnityEditor.UI;

namespace XiaoZhi.Unity
{
    [CustomEditor(typeof(XButton))]
    public class XButtonEditor : ButtonEditor
    {
        private SerializedProperty _reactModifiers;

        protected override void OnEnable()
        {
            base.OnEnable();
            _reactModifiers = serializedObject.FindProperty("_reactModifiers");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.PropertyField(_reactModifiers);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
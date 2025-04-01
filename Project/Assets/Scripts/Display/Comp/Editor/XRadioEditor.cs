using UnityEngine;
using UnityEditor;
using UnityEditor.UI;

namespace XiaoZhi.Unity
{
    [CustomEditor(typeof(XRadio))]
    public class XRadioEditor : ToggleEditor
    {
        private SerializedProperty _off;
        private SerializedProperty _on;
        private SerializedProperty _bg;
        private SerializedProperty _circle;
        private SerializedProperty _offGraphic;
        private SerializedProperty _onGraphic;

        protected override void OnEnable()
        {
            base.OnEnable();
            _off = serializedObject.FindProperty("_off");
            _on = serializedObject.FindProperty("_on");
            _bg = serializedObject.FindProperty("_bg");
            _circle = serializedObject.FindProperty("_circle");
            _offGraphic = serializedObject.FindProperty("_offGraphic");
            _onGraphic = serializedObject.FindProperty("_onGraphic");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Off State Colors", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_off.FindPropertyRelative("Background"), new GUIContent("Background"));
                EditorGUILayout.PropertyField(_off.FindPropertyRelative("Circle"), new GUIContent("Circle"));
                EditorGUILayout.PropertyField(_off.FindPropertyRelative("CircleHover"), new GUIContent("Circle Hover"));
                EditorGUILayout.PropertyField(_off.FindPropertyRelative("CircleSelected"), new GUIContent("Circle Selected"));
                EditorGUILayout.PropertyField(_off.FindPropertyRelative("CirclePressed"), new GUIContent("Circle Pressed"));
                EditorGUILayout.PropertyField(_off.FindPropertyRelative("CircleDisabled"), new GUIContent("Circle Disabled"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("On State Colors", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_on.FindPropertyRelative("Background"), new GUIContent("Background"));
                EditorGUILayout.PropertyField(_on.FindPropertyRelative("Circle"), new GUIContent("Circle"));
                EditorGUILayout.PropertyField(_on.FindPropertyRelative("CircleHover"), new GUIContent("Circle Hover"));
                EditorGUILayout.PropertyField(_on.FindPropertyRelative("CircleSelected"), new GUIContent("Circle Selected"));
                EditorGUILayout.PropertyField(_on.FindPropertyRelative("CirclePressed"), new GUIContent("Circle Pressed"));
                EditorGUILayout.PropertyField(_on.FindPropertyRelative("CircleDisabled"), new GUIContent("Circle Disabled"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Graphics", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_bg);
                EditorGUILayout.PropertyField(_circle);
                EditorGUILayout.PropertyField(_offGraphic);
                EditorGUILayout.PropertyField(_onGraphic);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
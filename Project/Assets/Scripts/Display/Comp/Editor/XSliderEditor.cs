using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace XiaoZhi.Unity
{
    [CustomEditor(typeof(XSlider))]
    public class XSliderEditor : SliderEditor
    {
        private SerializedProperty _value;
        private SerializedProperty _from;
        private SerializedProperty _to;
        private SerializedProperty _reactModifiers;

        protected override void OnEnable()
        {
            base.OnEnable();
            _value = serializedObject.FindProperty("_value");
            _from = serializedObject.FindProperty("_from");
            _to = serializedObject.FindProperty("_to");
            _reactModifiers = serializedObject.FindProperty("_reactModifiers");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Display Settings", EditorStyles.boldLabel);
            
            // Draw text display settings
            EditorGUILayout.PropertyField(_value, new GUIContent("Value Text"));
            EditorGUILayout.PropertyField(_from, new GUIContent("From Text"));
            EditorGUILayout.PropertyField(_to, new GUIContent("To Text"));
            EditorGUILayout.PropertyField(_reactModifiers);
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
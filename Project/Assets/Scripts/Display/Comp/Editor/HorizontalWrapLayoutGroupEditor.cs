using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

[CustomEditor(typeof(HorizontalWrapLayoutGroup))]
[CanEditMultipleObjects]
public class HorizontalWrapLayoutGroupEditor : HorizontalOrVerticalLayoutGroupEditor
{
    SerializedProperty _fixedRowHeight;

    protected override void OnEnable()
    {
        base.OnEnable();
        _fixedRowHeight = serializedObject.FindProperty("_fixedRowHeight");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.PropertyField(_fixedRowHeight, new GUIContent("Fixed Row Height"));
        EditorGUILayout.Space();
        serializedObject.ApplyModifiedProperties();
    }
}
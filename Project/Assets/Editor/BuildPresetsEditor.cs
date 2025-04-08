using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BuildPresets))]
public class BuildPresetsEditor : Editor
{
    private BuildPresets _buildPresets;
    private BuildTarget _buildTarget = BuildTarget.StandaloneWindows64;

    private void OnEnable()
    {
        _buildPresets = (BuildPresets)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("Debug"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("OutputPath"));

        if (_buildTarget == BuildTarget.Android)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Android Settings", EditorStyles.boldLabel);
            var androidPreset = serializedObject.FindProperty("AndroidPreset");
            EditorGUILayout.PropertyField(androidPreset.FindPropertyRelative("KeystorePath"));
            EditorGUILayout.PropertyField(androidPreset.FindPropertyRelative("KeystorePassword"));
            EditorGUILayout.PropertyField(androidPreset.FindPropertyRelative("KeyAliasName"));
            EditorGUILayout.PropertyField(androidPreset.FindPropertyRelative("KeyAliasPassword"));
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        _buildTarget = (BuildTarget)EditorGUILayout.EnumPopup("Build Target", _buildTarget);
        if (GUILayout.Button("Build", GUILayout.Width(200))) Builder.Build(_buildPresets, _buildTarget);
        EditorGUILayout.EndHorizontal();
    }
}
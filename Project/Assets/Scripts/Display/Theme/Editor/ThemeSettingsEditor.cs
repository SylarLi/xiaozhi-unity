using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace XiaoZhi.Unity
{
    [CustomEditor(typeof(ThemeSettings))]
    public class ThemeSettingsEditor : Editor
    {
        private SerializedProperty spotSettingsProperty;
        private SerializedProperty actionSettingsProperty;
        private SerializedProperty atlasSettingsProperty;
        private SerializedProperty defaultThemeProperty;
        private SerializedProperty defaultFillProperty;

        private Color _color1;
        private Color _color2;
        private int _baselineHeight = 100;

        private void OnEnable()
        {
            spotSettingsProperty = serializedObject.FindProperty("SpotSettings");
            actionSettingsProperty = serializedObject.FindProperty("ActionSettings");
            atlasSettingsProperty = serializedObject.FindProperty("AtlasSettings");
            defaultThemeProperty = serializedObject.FindProperty("DefaultTheme");
            defaultFillProperty = serializedObject.FindProperty("DefaultFill");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Spot Settings", EditorStyles.boldLabel);
            for (int i = 0; i < spotSettingsProperty.arraySize; i++)
            {
                SerializedProperty element = spotSettingsProperty.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(element.FindPropertyRelative("Theme"), GUIContent.none);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("Background"), GUIContent.none);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("Color"), GUIContent.none);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    spotSettingsProperty.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Spot Setting"))
            {
                spotSettingsProperty.arraySize++;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Action Settings", EditorStyles.boldLabel);
            for (int i = 0; i < actionSettingsProperty.arraySize; i++)
            {
                SerializedProperty element = actionSettingsProperty.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(element.FindPropertyRelative("Theme"), GUIContent.none);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("Action"), GUIContent.none);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("Color"), GUIContent.none);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    actionSettingsProperty.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Action Setting"))
            {
                actionSettingsProperty.arraySize++;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Default Theme", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(defaultThemeProperty);
            
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            _color1 = EditorGUILayout.ColorField(_color1);
            _color2 = EditorGUILayout.ColorField(_color2);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.ColorField("Color Div:", ThemeManager.ColorDiv(_color1, _color2));
            EditorGUILayout.ColorField("Color Minus:", _color1 - _color2);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            _baselineHeight = EditorGUILayout.IntField("Base line height", _baselineHeight);
            if (GUILayout.Button("Apply"))
            {
                var comps = AssetDatabase.FindAssets("t:prefab", new[] { "Assets/Resources/UI/Comp" });
                foreach (var comp in comps)
                {
                    var path = AssetDatabase.GUIDToAssetPath(comp);
                    var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var rt = obj.GetComponent<RectTransform>();
                    var nm = obj.name;
                    if (nm.StartsWith("Button_Icon"))
                    {
                        rt.sizeDelta = new Vector2(_baselineHeight, _baselineHeight) * 1.0f;
                    }
                    else if (nm.StartsWith("Button_Text"))
                    {
                        rt.sizeDelta = new Vector2(rt.sizeDelta.x, _baselineHeight * 1.0f);
                    }
                    else if (nm.StartsWith("Radio"))
                    {
                        var h = _baselineHeight * 0.72f;
                        rt.sizeDelta = new Vector2(h * 5 / 3, h);
                    }
                    else if (nm.StartsWith("Toggle_Icon"))
                    {
                        rt.sizeDelta = new Vector2(_baselineHeight, _baselineHeight) * 0.72f;
                    }
                    else if (nm.StartsWith("Toggle_Text"))
                    {
                        rt.sizeDelta = new Vector2(rt.sizeDelta.x, _baselineHeight * 0.72f);
                    }

                    PrefabUtility.SavePrefabAsset(obj);
                }
            }

            EditorGUILayout.EndHorizontal();

            var modified = serializedObject.hasModifiedProperties;
            serializedObject.ApplyModifiedProperties();
            if (modified)
            {
                ThemeManager.ReloadSettings();
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
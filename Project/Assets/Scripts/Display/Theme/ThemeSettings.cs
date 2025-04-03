using System;
using UnityEngine;
using UnityEngine.U2D;

namespace XiaoZhi.Unity
{
    public class ThemeSettings : ScriptableObject
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Assets/Create/ThemeSettings")]
        public static ThemeSettings Get(UnityEditor.MenuCommand command)
        {
            const string path = "Assets/Resources/ThemeSettings.asset";
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeSettings>(path);
            if (!asset)
            {
                asset = CreateInstance<ThemeSettings>();
                UnityEditor.AssetDatabase.CreateAsset(asset, path);
            }
            
            return asset;
        }
#endif

        public enum Theme
        {
            Light,
            Dark
        }

        public enum Action
        {
            Default,
            Hover,
            Selected,
            Pressed,
            Disabled,
        }

        public enum Background
        {
            Default,
            Graphic,
            Stateful,
            SpotThin,
            SpotStrong,
            Neutral,
            Title
        }

        [Serializable]
        public struct SpotSetting
        {
            public Theme Theme;

            public Background Background;

            public Color Color;
        }

        [Serializable]
        public struct ActionSetting
        {
            public Theme Theme;

            public Action Action;

            public Color Color;
        }
        
        public SpotSetting[] SpotSettings;

        public ActionSetting[] ActionSettings;
        
        public Theme DefaultTheme;
    }
}
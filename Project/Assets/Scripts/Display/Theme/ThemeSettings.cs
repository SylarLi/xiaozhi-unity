using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace XiaoZhi.Unity
{
    public class ThemeSettings : ScriptableObject
    {
        [MenuItem("Assets/Create/ThemeSettings")]
        public static ThemeSettings Get(MenuCommand command)
        {
            const string path = "Assets/Resources/ThemeSettings.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ThemeSettings>(path);
            if (!asset)
            {
                asset = CreateInstance<ThemeSettings>();
                AssetDatabase.CreateAsset(asset, path);
            }
            
            return asset;
        }

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

        [Serializable]
        public struct AtlasSetting
        {
            public bool Fill;
            
            public SpriteAtlas Atlas;
        }

        public SpotSetting[] SpotSettings;

        public ActionSetting[] ActionSettings;
        
        public AtlasSetting[] AtlasSettings;

        public Theme DefaultTheme;

        public bool DefaultFill;
    }
}
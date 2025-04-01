using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public static class ThemeManager
    {
        private static ThemeSettings _settings;
        private static Dictionary<ThemeSettings.Theme, Dictionary<ThemeSettings.Background, Color>> _background;
        private static Dictionary<ThemeSettings.Theme, Dictionary<ThemeSettings.Action, Color>> _action;
        private static Dictionary<bool, SpriteAtlas> _atlas;
        private static bool _fill;
        public static bool Fill => _fill;
        private static ThemeSettings.Theme _theme = ThemeSettings.Theme.Light;
        public static ThemeSettings.Theme Theme => _theme;

        public static UnityEvent<bool> OnFillChanged = new();
        public static UnityEvent<ThemeSettings.Theme> OnThemeChanged = new();

        private static Settings _prefSettings = new("theme");

        static ThemeManager()
        {
            ReloadSettings();
        }

        public static void ReloadSettings()
        {
#if UNITY_EDITOR
            _settings =
                UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeSettings>("Assets/Resources/ThemeSettings.asset");
#else
            _settings = Resources.Load<ThemeSettings>("ThemeSettings");
#endif
            if (!_settings) throw new NullReferenceException("Can not load Theme Settings.");
            _background = new Dictionary<ThemeSettings.Theme, Dictionary<ThemeSettings.Background, Color>>();
            if (_settings.SpotSettings != null)
            {
                foreach (var i in _settings.SpotSettings)
                {
                    if (!_background.ContainsKey(i.Theme))
                        _background.Add(i.Theme, new Dictionary<ThemeSettings.Background, Color>());
                    _background[i.Theme].Add(i.Background, i.Color);
                }
            }

            _action = new Dictionary<ThemeSettings.Theme, Dictionary<ThemeSettings.Action, Color>>();
            if (_settings.ActionSettings != null)
            {
                foreach (var i in _settings.ActionSettings)
                {
                    if (!_action.ContainsKey(i.Theme))
                        _action.Add(i.Theme, new Dictionary<ThemeSettings.Action, Color>());
                    _action[i.Theme].Add(i.Action, i.Color);
                }
            }

            _atlas = new Dictionary<bool, SpriteAtlas>();
            if (_settings.AtlasSettings != null)
            {
                foreach (var i in _settings.AtlasSettings)
                {
                    if (!_atlas.ContainsKey(i.Fill))
                        _atlas.Add(i.Fill, i.Atlas);
                }
            }

            _theme = (ThemeSettings.Theme)_prefSettings.GetInt("theme", (int)_settings.DefaultTheme);
            _fill = _prefSettings.GetInt("fill", _settings.DefaultFill ? 1 : 0) == 1;
        }

        public static void SetTheme(ThemeSettings.Theme theme)
        {
            if (_theme != theme)
            {
                _theme = theme;
                _prefSettings.SetInt("theme", (int)_theme);
                _prefSettings.Save();
                OnThemeChanged?.Invoke(theme);
                Canvas.ForceUpdateCanvases();
            }
        }

        public static void SetFill(bool fill)
        {
            if (_fill != fill)
            {
                _fill = fill;
                _prefSettings.SetInt("fill", _fill ? 1 : 0);
                _prefSettings.Save();
                OnFillChanged?.Invoke(fill);
            }
        }

        public static Sprite FetchSprite(string spriteName, bool fill)
        {
            // var atlas = _atlas[fill];
            // return atlas?.GetSprite(spriteName);
            return null;
        }

        public static Color FetchColor(ThemeSettings.Theme theme,
            ThemeSettings.Background background = ThemeSettings.Background.Default,
            ThemeSettings.Action action = ThemeSettings.Action.Default)
        {
            var backgroundColor = _background[theme][background];
            var overlayColor = _action[theme][action];
            switch (theme)
            {
                case ThemeSettings.Theme.Light:
                    return backgroundColor * overlayColor;
                case ThemeSettings.Theme.Dark:
                    var color = backgroundColor + overlayColor;
                    color.a = backgroundColor.a * overlayColor.a;
                    return color;
            }

            throw new NotImplementedException();
        }

        public static Color ColorDiv(Color color, Color divisor)
        {
            return new Color(color.r / divisor.r, color.g / divisor.g, color.b / divisor.b, color.a);
        }
    }
}
using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class SettingsUI : BaseUI
    {
        private TMP_InputField _inputWebSocketUrl;
        private TMP_InputField _inputWebSocketAccessToken;
        private TMP_InputField _inputCustomMacAddress;
        private Transform _listDisplayMode;
        private GameObject _goCharacter;
        private Transform _listCharacter;
        private Transform _listBreakMode;
        private GameObject _goKeywords;
        private TMP_InputField _inputKeywords;
        private XSlider _sliderVolume;
        private XButton _btnTheme;
        private XRadio _radioAutoHide;
        private XSpriteChanger _iconVolume;
        private XSpriteChanger _iconTheme;
        private Transform _listLang;
        private XButton _btnRestart;

        public override string GetResourcePath()
        {
            return "SettingsUI/SettingsUI";
        }

        protected override void OnInit()
        {
            var content = Tr.Find("Viewport/Content");
            _inputWebSocketUrl = GetComponent<TMP_InputField>(content, "WebSocketUrl/InputField");
            _inputWebSocketUrl.onDeselect.AddListener(OnChangeWebSocketUrl);
            _inputWebSocketAccessToken = GetComponent<TMP_InputField>(content, "WebSocketAccessToken/InputField");
            _inputWebSocketAccessToken.onDeselect.AddListener(OnChangeWebSocketAccessToken);
            _inputCustomMacAddress = GetComponent<TMP_InputField>(content, "CustomMacAddress/InputField");
            _inputCustomMacAddress.onDeselect.AddListener(OnChangeCustomMacAddress);
            _listDisplayMode = content.Find("DisplayMode/List");
            _goCharacter = content.Find("Character").gameObject;
            _listCharacter = content.Find("Character/List");
            _listBreakMode = content.Find("BreakMode/List");
            _inputKeywords = GetComponent<TMP_InputField>(content, "Keywords/InputField");
            _inputKeywords.onDeselect.AddListener(OnChangeKeywords);
            _sliderVolume = GetComponent<XSlider>(content, "Volume/Slider");
            _iconVolume = GetComponent<XSpriteChanger>(content, "Volume/Title/Icon");
            _sliderVolume.onValueChanged.AddListener(value =>
            {
                AppSettings.Instance.SetOutputVolume((int)value);
                UpdateIconVolume();
            });
            _radioAutoHide = GetComponent<XRadio>(content, "AutoHide/Radio");
            _radioAutoHide.onValueChanged.AddListener(value => { AppSettings.Instance.SetAutoHideUI(value); });
            _btnTheme = GetComponent<XButton>(content, "Theme/Button");
            _btnTheme.onClick.AddListener(() =>
            {
                ThemeManager.SetTheme(ThemeManager.Theme == ThemeSettings.Theme.Dark
                    ? ThemeSettings.Theme.Light
                    : ThemeSettings.Theme.Dark);
                UpdateIconTheme();
            });
            _iconTheme = GetComponent<XSpriteChanger>(content, "Theme/Button/Icon");
            _listLang = content.Find("Lang/List");
            _btnRestart = GetComponent<XButton>(content, "Restart/Button");
            _btnRestart.onClick.AddListener(() => { Context.Restart().Forget(); });

            GetComponent<XButton>(content, "Top/BtnClose").onClick.AddListener(() => Close().Forget());
            SetLang(content, "Title/Text", "SettingsUI_Title");
            SetLang(content, "OTAUrl/Title/Text", "SettingsUI_OTAUrl");
            SetLang(content, "OTAUrl/InputField/Text Area/Placeholder", "SettingsUI_Placeholder",
                Lang.Strings.Get("SettingsUI_OTAUrl"));
            SetLang(content, "WebSocketUrl/Title/Text", "SettingsUI_WebSocketUrl");
            SetLang(content, "WebSocketUrl/InputField/Text Area/Placeholder", "SettingsUI_Placeholder",
                Lang.Strings.Get("SettingsUI_WebSocketUrl"));
            SetLang(content, "WebSocketAccessToken/Title/Text", "SettingsUI_WebSocketAccessToken");
            SetLang(content, "WebSocketAccessToken/InputField/Text Area/Placeholder", "SettingsUI_Placeholder",
                Lang.Strings.Get("SettingsUI_WebSocketAccessToken"));
            SetLang(content, "CustomMacAddress/Title/Text", "SettingsUI_CustomMacAddress");
            SetLang(content, "CustomMacAddress/InputField/Text Area/Placeholder", "SettingsUI_Placeholder",
                Lang.Strings.Get("SettingsUI_CustomMacAddress"));
            SetLang(content, "DisplayMode/Title/Text", "SettingsUI_DisplayMode");
            SetLang(content, "Character/Title/Text", "SettingsUI_Character");
            SetLang(content, "BreakMode/Title/Text", "SettingsUI_BreakMode");
            SetLang(content, "Keywords/Title/Text", "SettingsUI_Keywords");
            SetLang(content, "Keywords/InputField/Text Area/Placeholder", "SettingsUI_Placeholder",
                Lang.Strings.Get("SettingsUI_Keywords"));
            SetLang(content, "Keywords/Tips_Help/Text", "SettingsUI_Keywords_Help");
            SetLang(content, "Volume/Title/Text", "SettingsUI_Volume");
            SetLang(content, "AutoHide/Title/Text", "SettingsUI_AutoHide");
            SetLang(content, "Theme/Title/Text", "SettingsUI_Theme");
            SetLang(content, "Lang/Title/Text", "SettingsUI_Lang");
            SetLang(content, "Restart/Button/Text", "SettingsUI_Restart");
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            UpdateWebSocketUrl();
            UpdateWebSocketAccessToken();
            UpdateCustomMacAddress();
            UpdateDisplayMode();
            UpdateCharacter();
            UpdateBreakMode();
            UpdateKeywords();
            UpdateVolume();
            UpdateIconVolume();
            UpdateAutoHide();
            UpdateIconTheme();
            UpdateLangList();

            Tr.DOKill();
            await Tr.SetAnchorPosX(Tr.rect.width + 16).DOAnchorPosX(0, AnimationDuration).SetEase(Ease.InOutSine);
        }

        protected override async UniTask OnHide()
        {
            Tr.DOKill();
            await Tr.DOAnchorPosX(Tr.rect.width + 16, AnimationDuration).SetEase(Ease.InOutSine);
        }

        private void UpdateDisplayMode()
        {
            var values = Enum.GetValues(typeof(DisplayMode));
            Tools.EnsureChildren(_listDisplayMode, values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                var go = _listDisplayMode.GetChild(i).gameObject;
                go.SetActive(true);
                GetComponent<TextMeshProUGUI>(go.transform, "Text").text =
                    Lang.Strings.Get($"SettingsUI_DisplayMode_{Enum.GetName(typeof(DisplayMode), i)}");
                var toggle = go.GetComponent<XToggle>();
                RemoveUniqueListener(toggle);
                toggle.isOn = (DisplayMode)values.GetValue(i) == AppSettings.Instance.GetDisplayMode();
                AddUniqueListener(toggle, i, OnToggleDisplayMode);
            }
        }

        private void OnToggleDisplayMode(Toggle toggle, int index, bool isOn)
        {
            if (isOn)
            {
                var displayMode = (DisplayMode)Enum.GetValues(typeof(DisplayMode)).GetValue(index);
                if (AppSettings.Instance.GetDisplayMode() == displayMode) return;
                AppSettings.Instance.SetDisplayMode(displayMode);
                ShowNotificationUI(Lang.Strings.Get("SettingsUI_Modify_Tips")).Forget();
                UpdateCharacter();
            }
        }

        private void UpdateCharacter()
        {
            var displayMode = AppSettings.Instance.GetDisplayMode();
            var showCharacter = displayMode == DisplayMode.VRM;
            _goCharacter.SetActive(showCharacter);
            if (!showCharacter) return;
            switch (displayMode)
            {
                case DisplayMode.VRM:
                    var values = Config.Instance.VRMCharacterModels;
                    var names = Config.Instance.VRMCharacterNames;
                    Tools.EnsureChildren(_listCharacter, values.Length);
                    for (var i = 0; i < values.Length; i++)
                    {
                        var go = _listCharacter.GetChild(i).gameObject;
                        go.SetActive(true);
                        GetComponent<TextMeshProUGUI>(go.transform, "Text").text = names[i];
                        var toggle = go.GetComponent<XToggle>();
                        RemoveUniqueListener(toggle);
                        toggle.isOn = i == AppSettings.Instance.GetVRMModel();
                        AddUniqueListener(toggle, i, OnToggleCharacter);
                    }

                    break;
            }
        }

        private void OnToggleCharacter(Toggle toggle, int index, bool isOn)
        {
            if (isOn)
            {
                var displayMode = AppSettings.Instance.GetDisplayMode();
                switch (displayMode)
                {
                    case DisplayMode.VRM:
                        if (AppSettings.Instance.GetVRMModel() == index) return;
                        AppSettings.Instance.SetVRMModel(index);
                        ShowNotificationUI(Lang.Strings.Get("SettingsUI_Modify_Tips")).Forget();
                        break;
                }
            }
        }

        private void UpdateBreakMode()
        {
            var values = Enum.GetValues(typeof(BreakMode));
            Tools.EnsureChildren(_listBreakMode, values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                var go = _listBreakMode.GetChild(i).gameObject;
                go.SetActive(true);
                GetComponent<TextMeshProUGUI>(go.transform, "Text").text =
                    Lang.Strings.Get($"SettingsUI_BreakMode_{Enum.GetName(typeof(BreakMode), i)}");
                var toggle = go.GetComponent<XToggle>();
                RemoveUniqueListener(toggle);
                toggle.isOn = (BreakMode)values.GetValue(i) == AppSettings.Instance.GetBreakMode();
                AddUniqueListener(toggle, i, OnToggleBreakMode);
            }
        }

        private void OnToggleBreakMode(Toggle toggle, int index, bool isOn)
        {
            if (isOn) AppSettings.Instance.SetBreakMode((BreakMode)Enum.GetValues(typeof(BreakMode)).GetValue(index));
        }

        private void UpdateKeywords()
        {
            _inputKeywords.text = AppSettings.Instance.GetKeywords();
        }

        private void OnChangeKeywords(string text)
        {
            if (AppSettings.Instance.GetKeywords().Equals(text)) return;
            AppSettings.Instance.SetKeywords(text);
            ShowNotificationUI(Lang.Strings.Get("SettingsUI_Modify_Tips")).Forget();
        }

        private void UpdateVolume()
        {
            _sliderVolume.value = AppSettings.Instance.GetOutputVolume();
        }

        private void UpdateIconVolume()
        {
            var volume = AppSettings.Instance.GetOutputVolume();
            var index = volume switch
            {
                0 => 0,
                < 50 => 1,
                _ => 2
            };
            _iconVolume.ChangeTo(index);
        }

        private void UpdateAutoHide()
        {
            _radioAutoHide.isOn = AppSettings.Instance.IsAutoHideUI();
        }

        private void UpdateIconTheme()
        {
            _iconTheme.ChangeTo(ThemeManager.Theme == ThemeSettings.Theme.Dark ? 0 : 1);
        }

        private void UpdateLangList()
        {
            var codes = Enum.GetValues(typeof(Lang.Code));
            Tools.EnsureChildren(_listLang, codes.Length);
            for (var i = 0; i < codes.Length; i++)
            {
                var go = _listLang.GetChild(i).gameObject;
                go.SetActive(true);
                var code = (Lang.Code)codes.GetValue(i);
                go.transform.Find("Text").GetComponent<TextMeshProUGUI>().text = Lang.GetLangName(code);
                var toggle = go.GetComponent<XToggle>();
                RemoveUniqueListener(toggle);
                toggle.isOn = code == AppSettings.Instance.GetLangCode();
                AddUniqueListener(toggle, i, OnToggleLang);
            }
        }

        private void OnToggleLang(Toggle toggle, int index, bool isOn)
        {
            if (isOn)
            {
                AppSettings.Instance.SetLangCode((Lang.Code)index);
                ShowNotificationUI(Lang.Strings.Get("SettingsUI_Modify_Tips")).Forget();
            }
        }

        private void UpdateCustomMacAddress()
        {
            _inputCustomMacAddress.text = AppSettings.Instance.GetMacAddress();
        }

        private void OnChangeCustomMacAddress(string value)
        {
            if (!Tools.IsValidMacAddress(value))
            {
                ShowNotificationUI(Lang.Strings.Get("SettingsUI_Invalid_MacAddress_Tips")).Forget();
                UpdateCustomMacAddress();
                return;
            }

            AppSettings.Instance.SetMacAddress(value);
        }

        private void UpdateWebSocketAccessToken()
        {
            _inputWebSocketAccessToken.text = AppSettings.Instance.GetWebSocketAccessToken();
        }

        private void OnChangeWebSocketAccessToken(string value)
        {
            AppSettings.Instance.SetWebSocketAccessToken(value);
        }

        private void UpdateWebSocketUrl()
        {
            _inputWebSocketUrl.text = AppSettings.Instance.GetWebSocketUrl();
        }

        private void OnChangeWebSocketUrl(string value)
        {
            if (!Tools.IsValidUrl(value))
            {
                ShowNotificationUI(Lang.Strings.Get("SettingsUI_Invalid_Url_Tips")).Forget();
                UpdateWebSocketUrl();
                return;
            }

            AppSettings.Instance.SetWebSocketUrl(value);
        }
    }
}
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
        private XSlider _sliderVolume;
        private XButton _btnTheme;
        private XRadio _radioFill;
        private XSpriteChanger _iconVolume;
        private XSpriteChanger _iconTheme;
        private Transform _listLang;

        public override string GetResourcePath()
        {
            return "SettingsUI/SettingsUI";
        }

        protected override void OnInit()
        {
            var content = Tr.Find("Viewport/Content");
            _sliderVolume = GetComponent<XSlider>(content, "Volume/Slider");
            _iconVolume = GetComponent<XSpriteChanger>(content, "Volume/Title/Icon");
            _sliderVolume.onValueChanged.AddListener(value =>
            {
                Context.App.GetCodec().SetOutputVolume((int)value);
                UpdateIconVolume();
            });
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

            GetComponent<XButton>(content, "Top/BtnClose").onClick.AddListener(() => Close().Forget());
            SetLang(content, "Title/Text", "SettingsUI_Title");
            SetLang(content, "Volume/Title/Text", "SettingsUI_Volume");
            SetLang(content, "Theme/Title/Text", "SettingsUI_Theme");
            SetLang(content, "Lang/Title/Text", "SettingsUI_Lang");
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            UpdateIconVolume();
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

        private void UpdateIconVolume()
        {
            var volume = Context.App.GetCodec().OutputVolume;
            var index = volume switch
            {
                0 => 0,
                < 50 => 1,
                _ => 2
            };
            _iconVolume.ChangeTo(index);
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
                toggle.isOn = code == Lang.GetLangCode();
                AddUniqueListener(toggle, i, OnToggleLang);
            }
        }

        private void OnToggleLang(Toggle toggle, int index, bool isOn)
        {
            if (isOn)
            {
                Lang.SetLangCode((Lang.Code)index);
                ShowNotificationUI(Lang.Strings.Get("SettingsUI_Lang_Change_Tips")).Forget();
            }
        }
    }
}
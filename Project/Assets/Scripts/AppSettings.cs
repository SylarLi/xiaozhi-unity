using System;

namespace XiaoZhi.Unity
{
    public class AppSettings : Settings
    {
        private static AppSettings _instance;

        public static AppSettings Instance => _instance;

        public static void Load()
        {
            _instance = new AppSettings();
        }
        
        private DisplayMode _displayMode;

        private int _vrmModel;

        private BreakMode _breakMode;

        private bool _autoHideUI;

        private string _keywords;

        private int _outputVolume;

        private Lang.Code _langCode;
        
        private string _webSocketUrl;

        private string _webSocketAccessToken;

        private string _customMacAddress;

        public event Action<bool> OnAutoHideUIUpdate;
        public event Action<int> OnOutputVolumeUpdate;

        private AppSettings() : base("app")
        {
            _displayMode = (DisplayMode)GetInt("display_mode");
            _breakMode = (BreakMode)GetInt("break_mode", (int)BreakMode.Keyword);
            _autoHideUI = GetInt("auto_hide_ui") == 1;
            _outputVolume = GetInt("output_volume", 50);
            _langCode = Enum.Parse<Lang.Code>(GetString("lang_code", Enum.GetName(typeof(Lang.Code), Lang.Code.CN)));
            _vrmModel = GetInt("vrm_model");
        }
        
        public DisplayMode GetDisplayMode() => _displayMode;

        public void SetDisplayMode(DisplayMode displayMode)
        {
            if (_displayMode == displayMode) return;
            _displayMode = displayMode;
            SetInt("display_mode", (int)displayMode);
            Save();
        }
        
        public int GetVRMModel() => _vrmModel;
        
        public void SetVRMModel(int vrmModel)
        {
            if (_vrmModel == vrmModel) return;
            _vrmModel = vrmModel;
            SetInt("vrm_model", _vrmModel);
            Save();
        }

        public BreakMode GetBreakMode() => _breakMode;

        public void SetBreakMode(BreakMode breakMode)
        {
            if (_breakMode == breakMode) return;
            _breakMode = breakMode;
            SetInt("break_mode", (int)breakMode);
            Save();
        }

        public string GetKeywords()
        {
            _keywords ??=
                FileUtility.ReadAllText(FileUtility.FileType.DataPath, Config.Instance.KeyWordSpotterKeyWordsFile);
            return _keywords;
        }

        public void SetKeywords(string keywords)
        {
            if (_keywords.Equals(keywords)) return;
            _keywords = keywords;
            FileUtility.WriteAllText(Config.Instance.KeyWordSpotterKeyWordsFile, _keywords);
        }

        public bool IsAutoHideUI()
        {
            return _autoHideUI;
        }

        public void SetAutoHideUI(bool autoHideUI)
        {
            if (_autoHideUI == autoHideUI) return;
            _autoHideUI = autoHideUI;
            SetInt("auto_hide_ui", _autoHideUI ? 1 : 0);
            Save();
            OnAutoHideUIUpdate?.Invoke(_autoHideUI);
        }

        public int GetOutputVolume()
        {
            return _outputVolume;
        }

        public void SetOutputVolume(int outputVolume)
        {
            if (_outputVolume == outputVolume) return;
            _outputVolume = outputVolume;
            SetInt("output_volume", _outputVolume);
            Save();
            OnOutputVolumeUpdate?.Invoke(_outputVolume);
        }

        public Lang.Code GetLangCode() => _langCode;

        public void SetLangCode(Lang.Code code)
        {
            if (_langCode == code) return;
            _langCode = code;
            SetString("lang_code", Enum.GetName(typeof(Lang.Code), _langCode));
            Save();
        }

        public bool IsFirstEnter()
        {
            return !HasKey("i_have_played_with_xiaozhi");
        }

        public void MarkAsNotFirstEnter()
        {
            SetInt("i_have_played_with_xiaozhi", 1);
            Save();
        }

        public string GetWebSocketUrl()
        {
            _webSocketUrl ??= GetString("web_socket_url", Config.Instance.WebSocketUrl);
            return _webSocketUrl;
        }

        public void SetWebSocketUrl(string url)
        {
            if (_webSocketUrl.Equals(url)) return;
            _webSocketUrl = url;
            SetString("web_socket_url", _webSocketUrl);
            Save();
        }

        public string GetWebSocketAccessToken()
        {
            _webSocketAccessToken ??= GetString("web_socket_access_token", Config.Instance.WebSocketAccessToken);
            return _webSocketAccessToken;
        }

        public void SetWebSocketAccessToken(string accessToken)
        {
            if (_webSocketAccessToken.Equals(accessToken)) return;
            _webSocketAccessToken = accessToken;
            SetString("web_socket_access_token", _webSocketAccessToken);
            Save();
        }

        public string GetMacAddress()
        {
            _customMacAddress ??= GetString("custom_mac_address", Config.GetMacAddress());
            return _customMacAddress;
        }

        public void SetMacAddress(string macAddress)
        {
            if (_customMacAddress.Equals(macAddress)) return;
            _customMacAddress = macAddress;
            SetString("custom_mac_address", _customMacAddress);
            Save();
        }
    }
}
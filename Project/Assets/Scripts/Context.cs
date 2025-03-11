using System;
using System.Linq;
using System.Net;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class Context
    {
        public static Context Instance { get; } = new();

        public string Uuid { get; private set; }

        public Display Display { get; }

        public AudioCodec AudioCodec { get; }

        private string _macAddress;

        private Context()
        {
            Uuid = Guid.NewGuid().ToString("d");
            Display = new UIDisplay();
            AudioCodec = new UnityAudioCodec(Config.Instance.AudioInputSampleRate,
                Config.Instance.AudioOutputSampleRate);
        }

        public string GetMacAddress()
        {
            _macAddress ??= BuildMacAddress();
            return _macAddress;
        }

        private string BuildMacAddress()
        {
            if (!string.IsNullOrEmpty(Config.Instance.CustomMacAddress))
                return Config.Instance.CustomMacAddress;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var contentResolver = currentActivity.Call<AndroidJavaObject>("getContentResolver"))
            using (var settingsSecure = new AndroidJavaClass("android.provider.Settings$Secure"))
            {
                var androidId = settingsSecure.CallStatic<string>("getString", contentResolver, "android_id");
                var formattedId = string.Join(":", Enumerable.Range(2, 6).Select(i => androidId.Substring(i * 2, 2)));
                return formattedId;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            var vendorId = UnityEngine.iOS.Device.vendorIdentifier;
            if (!string.IsNullOrEmpty(vendorId))
            {
                vendorId = vendorId.Replace("-", "").Substring(vendorId.Length - 12, 12);
                return string.Join(":", Enumerable.Range(0, 6)
                    .Select(i => vendorId.Substring(i * 2, 2)));
            }
#else
            var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (var adapter in nics)
            {
                if (adapter.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                    (adapter.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet ||
                     adapter.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211))
                {
                    var bytes = adapter.GetPhysicalAddress().GetAddressBytes();
                    return string.Join(":", bytes.Select(b => b.ToString("x2")));
                }
            }
#endif
            return string.Empty;
        }

        public string GetBoardName()
        {
            return Application.productName;
        }

        public string GetVersion()
        {
            return Application.version;
        }
    }
}
using System;
using System.Linq;

public class Context
{
    public static Context Instance { get; } = new();
    
    public string Uuid { get; private set; }
    
    public Display Display { get; }
    
    public AudioCodec AudioCodec { get; }
    
    public Context()
    {
        Uuid = Guid.NewGuid().ToString("N");
        Display = new UIDisplay();
        AudioCodec = new UnityAudioCodec(Config.Instance.AudioInputSampleRate, Config.Instance.AudioOutputSampleRate, true);
    }
    
    public string GetMacAddress()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var unityPlayer = new UnityEngine.AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<UnityEngine.AndroidJavaObject>("currentActivity"))
        using (var wifiManager = activity.Call<UnityEngine.AndroidJavaObject>("getSystemService", "wifi"))
        {
            var wifiInfo = wifiManager.Call<UnityEngine.AndroidJavaObject>("getConnectionInfo");
            var macAddress = wifiInfo.Call<string>("getMacAddress");
            
            // 确保格式正确
            if (!string.IsNullOrEmpty(macAddress))
            {
                return macAddress.ToLower();
            }
        }
#elif UNITY_IOS && !UNITY_EDITOR
        var idfv = UnityEngine.iOS.Device.vendorIdentifier;
        if (!string.IsNullOrEmpty(idfv))
        {
            return string.Join(":", Enumerable.Range(0, 6)
                .Select(i => idfv.Substring(i * 2, 2)));
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
}
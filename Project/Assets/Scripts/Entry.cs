using System;
using UnityEngine;
using XiaoZhi.Unity;

public class Entry : MonoBehaviour
{
    private void Start()
    {
        App.Instance.Start();
    }
    
    private void OnGUI()
    {
        if (GUILayout.Button("Boot", GUILayout.Width(300), GUILayout.Height(300)))
        {
            _ = App.Instance.ToggleChatState();
        }
        GUILayout.Label(Enum.GetName(typeof(DeviceState), App.Instance.GetDeviceState()), new GUIStyle() { fontSize = 50 });
    }
}
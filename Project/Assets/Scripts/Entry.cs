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
        if (GUILayout.Button("Boot", GUILayout.Width(100), GUILayout.Height(50)))
        {
            _ = App.Instance.ToggleChatState();
        }
    }

    private void Update()
    {
        App.Instance.Update();
    }
}
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
        if (GUILayout.Button("Start", GUILayout.Width(100), GUILayout.Height(50)))
        {
            App.Instance.Start();
        }
        
        if (GUILayout.Button("Boot", GUILayout.Width(100), GUILayout.Height(50)))
        {
            _ = App.Instance.StartListening();
        }
    }

    private void Update()
    {
        App.Instance.Update();
    }
}
using System;
using UnityEngine;
using XiaoZhi.Unity;

public class Entry : MonoBehaviour
{
    private void Start()
    {
        Context.Instance.App.Start().Forget();
    }

    private void OnApplicationQuit()
    {
        Context.Instance.Dispose();
    }
}
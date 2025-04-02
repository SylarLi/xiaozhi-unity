using System;
using UnityEngine;
using XiaoZhi.Unity;

public class Entry : MonoBehaviour
{
    private Context _context;
    
    private void Start()
    {
        _context = new Context();
        _context.Init();
        _context.App.Start().Forget();
    }

    private void OnApplicationQuit()
    {
        _context.Dispose();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        
    }
}
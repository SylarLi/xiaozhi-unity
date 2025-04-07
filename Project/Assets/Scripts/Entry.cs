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
        _context.Start();
    }

    private void OnApplicationQuit()
    {
        _context.Dispose();
    }
}
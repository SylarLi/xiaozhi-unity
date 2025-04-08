using UnityEngine;
using UnityEditor;
using System;
using System.IO;

[Serializable]
public class AndroidBuildPreset
{
    public string KeystorePath = "user.keystore";
    public string KeystorePassword = "";
    public string KeyAliasName = "";
    public string KeyAliasPassword = "";
}

[Serializable]
[CreateAssetMenu(menuName = "Build Presets")]
public class BuildPresets : ScriptableObject
{
    public bool Debug = true;
    public string OutputPath = "Builds";
    public AndroidBuildPreset AndroidPreset = new();
}
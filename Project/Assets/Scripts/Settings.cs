using UnityEngine;

public class Settings
{
    private string _namespace;

    public Settings(string ns)
    {
        _namespace = ns;
    }

    private string GetFullKey(string key)
    {
        return $"{_namespace}_{key}";
    }

    public string GetString(string key, string defaultValue = "")
    {
        return PlayerPrefs.GetString(GetFullKey(key), defaultValue);
    }

    public void SetString(string key, string value)
    {
        PlayerPrefs.SetString(GetFullKey(key), value);
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(GetFullKey(key), defaultValue);
    }

    public void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(GetFullKey(key), value);
    }

    public void EraseKey(string key)
    {
        PlayerPrefs.DeleteKey(GetFullKey(key));
    }

    public void Save()
    {
        PlayerPrefs.Save();
    }
}
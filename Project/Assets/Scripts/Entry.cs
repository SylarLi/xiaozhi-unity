using UnityEngine;

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
            _ = App.Instance.StartListening();
        }
    }
}
using UnityEngine;
using XiaoZhi.Unity;

public class Entry : MonoBehaviour
{
    private void Start()
    {
        Context.Instance.App.Start().Forget();
    }
}
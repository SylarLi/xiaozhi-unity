using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class XSpriteChanger : MonoBehaviour
{
    [SerializeField] private Sprite[] _sprites;
    private Image _image;
    private int _currentIndex = -1;

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    /// <summary>
    /// 切换到指定索引的Sprite
    /// </summary>
    /// <param name="index">目标索引</param>
    /// <returns>是否切换成功</returns>
    public bool ChangeTo(int index)
    {
        if (_sprites == null || index < 0 || index >= _sprites.Length) return false;
        if (_currentIndex == index) return true;
        _image.sprite = _sprites[index];
        _currentIndex = index;
        return true;
    }

    /// <summary>
    /// 获取当前显示的Sprite索引
    /// </summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>
    /// 获取Sprite数量
    /// </summary>
    public int Count => _sprites?.Length ?? 0;
}
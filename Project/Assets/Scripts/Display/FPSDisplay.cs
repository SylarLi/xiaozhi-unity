using UnityEngine;

namespace XiaoZhi.Unity
{
    public class FPSDisplay : MonoBehaviour
    {
        private float _updateInterval = 0.5f; // 更新FPS的时间间隔
        private float _accum; // FPS累积
        private int _frames; // 帧数
        private float _timeleft; // 剩余时间
        private float _currentFPS; // 当前FPS
        
        private GUIStyle _style;
        private Rect _rect;
        
        private void Start()
        {
            _timeleft = _updateInterval;
            
            // 初始化GUI样式
            _style = new GUIStyle
            {
                fontSize = 20,
                normal = { textColor = Color.green },
                fontStyle = FontStyle.Bold
            };
            
            // 设置显示位置（右下角）
            _rect = new Rect(Screen.width - 150, Screen.height - 40, 140, 30);
        }

        private void Update()
        {
            _timeleft -= Time.deltaTime;
            _accum += Time.timeScale / Time.deltaTime;
            _frames++;

            if (_timeleft <= 0.0f)
            {
                _currentFPS = _accum / _frames;
                _timeleft = _updateInterval;
                _accum = 0.0f;
                _frames = 0;
            }
        }

        private void OnGUI()
        {
            // 使用GUILayout可能会影响性能，所以这里直接使用GUI.Label
            GUI.Label(_rect, $"FPS: {_currentFPS:F1}", _style);
        }
    }
}
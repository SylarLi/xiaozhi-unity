using UnityEngine;

namespace XiaoZhi.Unity
{
    public class FPSDisplay : MonoBehaviour
    {
        private const float UpdateInterval = 0.5f; // 更新FPS的时间间隔
        private float _accum; // FPS累积
        private int _frames; // 帧数
        private float _timeLeft; // 剩余时间
        private float _currentFPS; // 当前FPS
        
        private GUIStyle _style;
        private Rect _rect;
        
        private void Start()
        {
            _timeLeft = UpdateInterval;
            _style = new GUIStyle
            {
                fontSize = 20,
                normal = { textColor = Color.green },
                fontStyle = FontStyle.Bold
            };
         }

        private void Update()
        {
            _timeLeft -= Time.deltaTime;
            _accum += Time.timeScale / Time.deltaTime;
            _frames++;

            if (_timeLeft <= 0.0f)
            {
                _currentFPS = _accum / _frames;
                _timeLeft = UpdateInterval;
                _accum = 0.0f;
                _frames = 0;
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(Screen.width - 150, Screen.height - 40, 140, 30), $"FPS: {_currentFPS:F1}", _style);
        }
    }
}
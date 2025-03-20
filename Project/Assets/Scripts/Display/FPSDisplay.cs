using UnityEngine;

namespace XiaoZhi.Display
{
    public class FPSDisplay : MonoBehaviour
    {
        private float updateInterval = 0.5f; // 更新FPS的时间间隔
        private float accum = 0.0f; // FPS累积
        private int frames = 0; // 帧数
        private float timeleft; // 剩余时间
        private float currentFPS = 0.0f; // 当前FPS
        
        private GUIStyle style;
        private Rect rect;
        
        private void Start()
        {
            timeleft = updateInterval;
            
            // 初始化GUI样式
            style = new GUIStyle
            {
                fontSize = 20,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold
            };
            
            // 设置显示位置（右下角）
            rect = new Rect(Screen.width - 150, Screen.height - 40, 140, 30);
        }

        private void Update()
        {
            timeleft -= Time.deltaTime;
            accum += Time.timeScale / Time.deltaTime;
            frames++;

            if (timeleft <= 0.0f)
            {
                currentFPS = accum / frames;
                timeleft = updateInterval;
                accum = 0.0f;
                frames = 0;
            }
        }

        private void OnGUI()
        {
            // 使用GUILayout可能会影响性能，所以这里直接使用GUI.Label
            GUI.Label(rect, $"FPS: {currentFPS:F1}", style);
        }
    }
}
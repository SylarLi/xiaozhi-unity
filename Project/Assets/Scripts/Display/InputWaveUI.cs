using UnityEngine;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public class InputWaveUI : Graphic
    {
        private const float SpectrumUpdateInterval = 0.05f;
        private const float SpectrumGap = 0.5f;
        private const float BaseHeight = 0.1f; // 基础高度占总高度的比例
        private const int SpectrumSize = 32; // 目标频段数量

        private float _lastUpdateTime;
        private readonly float[] _normalizeddBData = new float[SpectrumSize];

        private void Update()
        {
            if (!Application.isPlaying || Time.time - _lastUpdateTime < SpectrumUpdateInterval) return;
            _lastUpdateTime = Time.time;
            if (UpdateSpectrumData()) SetVerticesDirty();
        }

        private bool UpdateSpectrumData()
        {
            if (!Context.Instance.AudioCodec.GetInputSpectrum(out var spectrumData))
                return false;
            var sourceSpectrumSize = spectrumData.Length;
            for (var i = 0; i < _normalizeddBData.Length; i++)
            {
                var startIndex = (int)(i * (float)sourceSpectrumSize / SpectrumSize);
                var endIndex = (int)((i + 1) * (float)sourceSpectrumSize / SpectrumSize);
                var sum = 0f;
                var count = 0;
                for (var j = startIndex; j < endIndex; j++)
                {
                    sum += spectrumData[j];
                    count++;
                }

                _normalizeddBData[i] = count > 0 ? Linear2dB(sum / count) : 0;
            }

            return true;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            // 获取RectTransform的尺寸
            var rect = GetPixelAdjustedRect();
            var width = rect.width;
            var height = rect.height;
            var centerY = height / 2;
            var dBSize = _normalizeddBData.Length;

            // 计算柱体宽度
            var barWidth = width / dBSize / (1 + SpectrumGap);

            // 绘制频谱柱状图
            for (var i = 0; i < dBSize; i++)
            {
                var dBValue = _normalizeddBData[i];
                var barHeight = (BaseHeight + dBValue * (1 - BaseHeight)) * height;

                // 计算柱体位置
                var xCenter = ((i / (float)dBSize) - 0.5f) * width;
                var x1 = xCenter - barWidth / 2;
                var x2 = xCenter + barWidth / 2;
                var y1 = centerY;
                var y2 = centerY + barHeight;

                // 创建矩形顶点
                var vertex1 = UIVertex.simpleVert;
                var vertex2 = UIVertex.simpleVert;
                var vertex3 = UIVertex.simpleVert;
                var vertex4 = UIVertex.simpleVert;

                // 设置顶点位置
                vertex1.position = new Vector3(x1, y1, 0);
                vertex2.position = new Vector3(x2, y1, 0);
                vertex3.position = new Vector3(x2, y2, 0);
                vertex4.position = new Vector3(x1, y2, 0);

                // 设置顶点颜色
                vertex1.color = color;
                vertex2.color = color;
                vertex3.color = color;
                vertex4.color = color;

                // 添加顶点
                var vertexIndex = i * 4;
                vh.AddVert(vertex1);
                vh.AddVert(vertex2);
                vh.AddVert(vertex3);
                vh.AddVert(vertex4);

                // 添加三角形
                vh.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
                vh.AddTriangle(vertexIndex, vertexIndex + 2, vertexIndex + 3);
            }
        }

        private float Linear2dB(float linear)
        {
            return (Mathf.Clamp(Mathf.Log10(linear) * 20.0f, -80.0f, 0.0f) + 80) / 80;
        }
    }
}
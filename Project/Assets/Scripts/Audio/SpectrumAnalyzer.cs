using System;
using System.Numerics;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class SpectrumAnalyzer
    {
        private readonly float[] _hanningWindow;
        private readonly Complex[] _fftBuffer;
        private readonly float[] _spectrum;

        public SpectrumAnalyzer(int windowSize)
        {
            _hanningWindow = new float[windowSize];
            _fftBuffer = new Complex[windowSize];
            _spectrum = new float[windowSize / 2];

            // 初始化汉宁窗
            for (var i = 0; i < windowSize; i++)
            {
                _hanningWindow[i] = 0.5f * (1 - MathF.Cos(2 * MathF.PI * i / (windowSize - 1)));
            }
        }

        public bool Analyze(ReadOnlySpan<short> pcmData, out ReadOnlySpan<float> spectrum)
        {
            spectrum = default;
            var windowSize = _hanningWindow.Length;
            if (pcmData.Length < windowSize)
            {
                Debug.LogError("PCM数据长度必须大于等于窗口大小");
                return false;
            }

            // 应用汉宁窗并转换为复数
            for (var i = 0; i < windowSize; i++)
            {
                var value = pcmData[i] * _hanningWindow[i] / short.MaxValue;
                _fftBuffer[i] = new Complex(value, 0);
            }

            // FFT变换
            FFT(_fftBuffer);

            // 计算频谱
            var spectrumLength = windowSize / 2;
            for (var i = 0; i < spectrumLength; i++)
            {
                _spectrum[i] = (float)_fftBuffer[i].Magnitude / windowSize;
            }

            spectrum = _spectrum;
            return true;
        }

        private static void FFT(Complex[] buffer)
        {
            var n = buffer.Length;
            if (n <= 1) return;

            // 分解
            var even = new Complex[n / 2];
            var odd = new Complex[n / 2];
            for (var i = 0; i < n / 2; i++)
            {
                even[i] = buffer[2 * i];
                odd[i] = buffer[2 * i + 1];
            }

            // 递归
            FFT(even);
            FFT(odd);

            // 合并
            for (var k = 0; k < n / 2; k++)
            {
                var t = odd[k] * Complex.FromPolarCoordinates(1, -2 * Math.PI * k / n);
                buffer[k] = even[k] + t;
                buffer[k + n / 2] = even[k] - t;
            }
        }
    }
}
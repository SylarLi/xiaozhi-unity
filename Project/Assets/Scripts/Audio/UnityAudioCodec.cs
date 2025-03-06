using UnityEngine;
using System;
using Object = UnityEngine.Object;

public class UnityAudioCodec : AudioCodec
{
    private readonly AudioSource _audioSource;
    private AudioClip _recordingClip;
    private readonly float[] _recordingBuffer;
    private int _recordingPosition;
    private readonly float[] _playbackBuffer;
    private int _playbackPosition;
    private readonly int _playbackBufferSize;
    private bool _isPlaying;

    public UnityAudioCodec(int inputSampleRate, int outputSampleRate, bool inputReference)
    {
        this.inputSampleRate = inputSampleRate;
        this.outputSampleRate = outputSampleRate;
        this.inputReference = inputReference;
        inputChannels = inputReference ? 2 : 1;
        duplex = true;
        _audioSource = new GameObject(GetType().Name).AddComponent<AudioSource>();
        Object.DontDestroyOnLoad(_audioSource.gameObject);
        _recordingBuffer = new float[inputSampleRate * 2];
        
        // 初始化播放缓冲区
        const int buffLengthSeconds = 30;
        _playbackBufferSize = outputSampleRate * buffLengthSeconds;
        _playbackBuffer = new float[_playbackBufferSize];
        _playbackPosition = 0;
        _isPlaying = false;

        // 创建用于流式播放的AudioClip
        var playbackClip = AudioClip.Create("StreamPlayback", _playbackBufferSize, outputChannels, outputSampleRate, true, OnAudioRead);
        _audioSource.clip = playbackClip;
        _audioSource.loop = true;
    }

    private void OnAudioRead(float[] data)
    {
        var readCount = Math.Min(data.Length, _playbackBufferSize);
        var position = _playbackPosition;

        // 从缓冲区读取数据
        for (var i = 0; i < readCount; i++)
        {
            data[i] = _playbackBuffer[(position + i) % _playbackBufferSize];
        }
    }

    protected override int Write(ReadOnlySpan<short> data)
    {
        if (!outputEnabled)
            return 0;

        var samples = data.Length;
        var position = _playbackPosition;

        // 将PCM数据转换为float并写入缓冲区
        for (var i = 0; i < samples; i++)
        {
            _playbackBuffer[(position + i) % _playbackBufferSize] = data[i] / (float)short.MaxValue;
        }

        // 更新缓冲区位置
        _playbackPosition = (position + samples) % _playbackBufferSize;

        // 如果尚未开始播放，则开始播放
        if (!_isPlaying)
        {
            _audioSource.volume = outputVolume / 100f;
            _audioSource.Play();
            _isPlaying = true;
        }

        return samples;
    }

    public override void EnableOutput(bool enable)
    {
        if (outputEnabled == enable) return;
        if (!enable)
        {
            _audioSource.Stop();
            _isPlaying = false;
        }
        base.EnableOutput(enable);
    }

    protected override int Read(Span<short> dest)
    {
        if (!inputEnabled || !Microphone.IsRecording(null))
            return 0;

        // 获取录音数据
        var position = Microphone.GetPosition(null);
        if (position <= 0) return 0;

        // 计算需要读取的样本数
        var samplesToRead = Math.Min(dest.Length, position - _recordingPosition);
        if (samplesToRead <= 0) return 0;

        // 从录音缓冲区读取数据
        _recordingClip.GetData(_recordingBuffer, _recordingPosition);

        // 将float数据转换为short并写入目标缓冲区
        for (var i = 0; i < samplesToRead; i++)
        {
            dest[i] = (short)(_recordingBuffer[i] * short.MaxValue);
        }

        _recordingPosition = (_recordingPosition + samplesToRead) % _recordingBuffer.Length;
        return samplesToRead;
    }

    public override void EnableInput(bool enable)
    {
        if (inputEnabled == enable) return;
        var deviceName = Microphone.devices[0];
        if (enable)
        {
            if (!Microphone.IsRecording(deviceName))
            {
                _recordingClip = Microphone.Start(deviceName, false, 10, inputSampleRate);
                _recordingPosition = 0;
            }
        }
        else
        {
            if (Microphone.IsRecording(deviceName))
            {
                Microphone.End(null);
                _recordingPosition = 0;
            }
        }

        base.EnableInput(enable);
    }
}
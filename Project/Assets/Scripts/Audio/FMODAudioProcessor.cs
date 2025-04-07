using System;

public class FMODAudioProcessor : IDisposable
{
    private IntPtr _handle;
    private IntPtr _inputStream;
    private IntPtr _outputStream;

    public FMODAudioProcessor(int inputSampleRate, int inputChannels, int outputSampleRate, int outputChannels)
    {
        _inputStream = WebRTCAPMWrapper.WebRTC_APM_CreateStreamConfig(inputSampleRate, inputChannels);
        if (_inputStream == IntPtr.Zero) throw new InvalidOperationException("Failed to create stream config");
        _outputStream = WebRTCAPMWrapper.WebRTC_APM_CreateStreamConfig(outputSampleRate, outputChannels);
        if (_outputStream == IntPtr.Zero) throw new InvalidOperationException("Failed to create stream config");
        _handle = WebRTCAPMWrapper.WebRTC_APM_Create();
        if (_handle == IntPtr.Zero) throw new InvalidOperationException("Failed to create audio processor");
        var config = WebRTCAPMWrapper.Config.Build();
        config.Echo.Enabled = true;
        config.Echo.MobileMode = true;
        config.NoiseSuppress.Enabled = true;
        config.NoiseSuppress.NoiseLevel = WebRTCAPMWrapper.Config.NoiseSuppression.Level.VeryHigh;
        config.HighPass.Enabled = true;
        config.HighPass.ApplyInFullBand = true;
        WebRTCAPMWrapper.WebRTC_APM_ApplyConfig(_handle, ref config);
    }

    public void SetStreamDelayMs(int delayMs)
    {
        WebRTCAPMWrapper.WebRTC_APM_SetStreamDelayMs(_handle, delayMs);
    }

    public int ProcessReverseStream(Span<short> src, Span<short> dest)
    {
        return WebRTCAPMWrapper.WebRTC_APM_ProcessReverseStream(_handle, ref src.GetPinnableReference(), _outputStream,
            _outputStream, ref dest.GetPinnableReference());
    }

    public int ProcessStream(Span<short> src, Span<short> dest)
    {
        return WebRTCAPMWrapper.WebRTC_APM_ProcessStream(_handle, ref src.GetPinnableReference(), _inputStream,
            _inputStream, ref dest.GetPinnableReference());
    }
    
    public void Dispose()
    {
        InternalDispose();
        GC.SuppressFinalize(this);
    }
        
    private void InternalDispose()
    {
        if (_handle != IntPtr.Zero)
        {
            WebRTCAPMWrapper.WebRTC_APM_Destroy(_handle);
            _handle = IntPtr.Zero;
        }

        if (_inputStream != IntPtr.Zero)
        {
            WebRTCAPMWrapper.WebRTC_APM_DestroyStreamConfig(_inputStream);
            _inputStream = IntPtr.Zero;
        }

        if (_outputStream != IntPtr.Zero)
        {
            WebRTCAPMWrapper.WebRTC_APM_DestroyStreamConfig(_outputStream);
            _outputStream = IntPtr.Zero;
        }
    }

    ~FMODAudioProcessor()
    {
        InternalDispose();
    }
}
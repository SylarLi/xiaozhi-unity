using System;
using System.Runtime.InteropServices;

public static class OpusWrapper
{
    public const int OPUS_APPLICATION_VOIP = 2048;
    public const int OPUS_RESET_STATE = 4028;
    public const int OPUS_SET_DTX_REQUEST = 4016;
    public const int OPUS_SET_COMPLEXITY_REQUEST = 4010;
    
    // 加载库文件
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const string LIBRARY_NAME = "libopus";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    private const string LIBRARY_NAME = "libopus";
#elif UNITY_ANDROID
    private const string LIBRARY_NAME = "libopus";
#elif UNITY_IOS
    private const string LIBRARY_NAME = "__Internal";
#endif
    
    // 定义 Opus 函数
    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr opus_decoder_create(int Fs, int channels, out int error);

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int opus_decode_float(IntPtr st, char* data, int len, float* pcm, int frame_size, int decode_fec);
    
    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int opus_decode(IntPtr st, char* data, int len, short* pcm, int frame_size, int decode_fec);

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void opus_decoder_destroy(IntPtr st);
    
    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void opus_decoder_ctl(IntPtr st, int request);

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr opus_encoder_create(int Fs, int channels, int application, out int error);

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int opus_encode(IntPtr st, short* pcm, int analysisFrameSize, char* data, int outDataBytes);

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void opus_encoder_ctl(IntPtr st, int request);
    
    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void opus_encoder_ctl(IntPtr st, int request, int value);

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void opus_encoder_destroy(IntPtr st);

}
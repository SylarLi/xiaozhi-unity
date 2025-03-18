using System;
using System.Runtime.InteropServices;
using UnityEngine;
using FMODUnity;
using FMOD;

public class SpeakerOutputCapture: IDisposable
{
    private FMOD.DSP captureDSP;
    private float[] outputBuffer;
    private const int BUFFER_SIZE = 1024;
    
    public void Start()
    {
        InitializeCapture();
    }

    private void InitializeCapture()
    {
        var system = RuntimeManager.CoreSystem;
        outputBuffer = new float[BUFFER_SIZE];

        // 创建自定义DSP用于捕获音频
        var dspDesc = new DSP_DESCRIPTION();
        dspDesc.numoutputbuffers = 1;
        dspDesc.read = DSPCallback;
        
        system.createDSP(ref dspDesc, out captureDSP);
        
        // 将DSP添加到主混音器
        ChannelGroup masterGroup;
        system.getMasterChannelGroup(out masterGroup);
        masterGroup.addDSP(CHANNELCONTROL_DSP_INDEX.HEAD, captureDSP);
    }

    private RESULT DSPCallback(ref DSP_STATE dspState, IntPtr inBuffer, IntPtr outBuffer, uint length, int inChannels, ref int outChannels)
    {
        if (length <= BUFFER_SIZE)
        {
            Marshal.Copy(inBuffer, outputBuffer, 0, (int)length);
        }
        Marshal.Copy(inBuffer, outBuffer, 0, (int)length * sizeof(float));
        return RESULT.OK;
    }

    public float[] GetCurrentOutput()
    {
        if (captureDSP.hasHandle())
        {
            return outputBuffer;
        }
        return null;
    }

    public void Dispose()
    {
        if (captureDSP.hasHandle())
        {
            captureDSP.release();
            captureDSP.clearHandle();
        }
    }
}
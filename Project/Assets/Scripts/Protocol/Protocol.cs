using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;

public abstract class Protocol
{
    public delegate void OnAudioDataReceived(byte[] data);

    public delegate void OnJsonMessageReceived(JObject message);

    public delegate void OnAudioChannelClosed();

    public delegate void OnAudioChannelOpened();

    public delegate void OnNetworkErrorOccurred(string message);

    public event OnAudioDataReceived OnAudioData;
    public event OnJsonMessageReceived OnJsonMessage;
    public event OnAudioChannelClosed OnChannelClosed;
    public event OnAudioChannelOpened OnChannelOpened;
    public event OnNetworkErrorOccurred OnNetworkError;

    public int ServerSampleRate { get; protected set; }
    public int SessionId { get; protected set; }

    public abstract Task<bool> OpenAudioChannel();
    public abstract Task CloseAudioChannel();
    public abstract bool IsAudioChannelOpened();
    public abstract Task SendAudio(byte[] data);
    
    public virtual async Task SendAbortSpeaking(AbortReason reason)
    {
        if (reason == AbortReason.WakeWordDetected)
        {
            await SendJson(new
            {
                session_id = SessionId,
                type = "abort",
                reason = "wake_word_detected"
            });
        }
        else
        {
            await SendJson(new
            {
                session_id = SessionId,
                type = "abort"
            });
        }
    }

    public virtual async Task SendWakeWordDetected(string wakeWord)
    {
        await SendJson(new
        {
            session_id = SessionId,
            type = "listen",
            state = "detect",
            text = wakeWord
        });
    }

    public virtual async Task SendStartListening(ListenMode mode)
    {
        await SendJson(new
        {
            ession_id = SessionId,
            type = "listen",
            state = "start",
            mode = mode.ToString().ToLower()
        });
    }

    public virtual async Task SendStopListening()
    {
        await SendJson(new
        {
            session_id = SessionId,
            type = "listen",
            state = "stop"
        });
    }

    public virtual async Task SendIotDescriptors(string descriptors)
    {
        await SendJson(new
        {
            session_id = SessionId,
            type = "iot",
            descriptors
        });
    }

    public virtual async Task SendIotStates(string states)
    {
        await SendJson(new
        {
            session_id = SessionId,
            type = "iot",
            states
        });
    }

    protected abstract Task SendJson(object data);

    protected void InvokeOnAudioData(byte[] data)
    {
        OnAudioData?.Invoke(data);
    }

    protected void InvokeOnJsonMessage(JObject message)
    {
        OnJsonMessage?.Invoke(message);
    }

    protected void InvokeOnChannelClosed()
    {
        OnChannelClosed?.Invoke();
    }

    protected void InvokeOnChannelOpened()
    {
        OnChannelOpened?.Invoke();
    }

    protected void InvokeOnNetworkError(string message)
    {
        OnNetworkError?.Invoke(message);
    }
}
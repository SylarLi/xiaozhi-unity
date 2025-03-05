public enum MessageType
{
    Hello,
    Listen,
    Abort,
    WakeWordDetected,
    IoT,
    STT,
    LLM,
    TTS
}

public enum ListenState
{
    Start,
    Stop,
    Detect
}

public enum ListenMode
{
    Auto,
    Manual,
    Realtime
}

public enum TTSState
{
    Start,
    Stop,
    SentenceStart
}

public enum AbortReason
{
    None,
    WakeWordDetected
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public enum AudioState
{
    Idle,
    Listening,
    Speaking
}
using UnityEngine;
using System;
using System.Text;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XiaoZhi.Unity
{
    public class WebSocketProtocol : Protocol
    {
        private ClientWebSocket _webSocket;
        private bool _isConnected;
        private bool _isAudioChannelOpen;
        private bool _errorOccurred;
        private CancellationTokenSource _cancellationTokenSource;
        private TaskCompletionSource<bool> _helloTaskCompletionSource;
        private DateTime _lastIncomingTime;
        private Memory<byte> _buffer;

        public override void Start()
        {
            _buffer = new byte[8192];
        }

        public override async Task<bool> OpenAudioChannel()
        {
            var url = Config.Instance.WebSocketUrl;
            var token = Config.Instance.WebSocketAccessToken;
            var deviceId = Context.Instance.GetMacAddress();
            var clientId = Context.Instance.Uuid;
            Debug.Log($"url: {url}");
            Debug.Log($"token: {token}");
            Debug.Log($"deviceId: {deviceId}");
            Debug.Log($"clientId: {clientId}");
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token) ||
                string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(clientId))
            {
                SetError("连接失败: 请检查配置");
                return false;
            }

            await CloseWebSocket();

            try
            {
                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();
                _helloTaskCompletionSource = new TaskCompletionSource<bool>();

                // 设置请求头
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
                _webSocket.Options.SetRequestHeader("Protocol-Version", "1");
                _webSocket.Options.SetRequestHeader("Device-Id", deviceId);
                _webSocket.Options.SetRequestHeader("Client-Id", clientId);

                // 异步连接
                await _webSocket.ConnectAsync(new Uri(url), _cancellationTokenSource.Token);
                _isConnected = true;
                Debug.Log("WebSocket连接已打开");
                _ = StartReceiving();
                var helloMessage = new
                {
                    type = "hello",
                    version = 1,
                    transport = "websocket",
                    audio_params = new
                    {
                        format = "opus",
                        sample_rate = 16000,
                        channels = 1,
                        frame_duration = Config.Instance.OpusFrameDurationMs
                    }
                };
                await SendJson(helloMessage);
                await Task.WhenAny(_helloTaskCompletionSource.Task, Task.Delay(10000));
                if (_helloTaskCompletionSource.Task.IsCompletedSuccessfully) return true;
                SetError("连接失败: 连接超时");
                return false;
            }
            catch (Exception ex)
            {
                InvokeOnNetworkError($"连接失败: {ex.Message}");
                return false;
            }
        }

        private async Task StartReceiving()
        {
            try
            {
                while (_webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(
                        _buffer,
                        _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleWebSocketClose();
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var messageData = _buffer.Slice(0, result.Count).ToArray();
                        InvokeOnAudioData(messageData);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var messageText = Encoding.UTF8.GetString(_buffer.Span.Slice(0, result.Count));
                        Debug.Log($"Incoming json: {messageText}");
                        HandleJsonMessage(messageText);
                        _lastIncomingTime = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    InvokeOnNetworkError($"接收消息错误: {ex.Message}");
                }
            }
        }

        private async Task HandleWebSocketClose()
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client initiated close",
                    _cancellationTokenSource.Token);
            }

            _isConnected = false;
            _isAudioChannelOpen = false;
            Debug.Log("WebSocket连接已关闭");
            InvokeOnChannelClosed();
        }

        private void HandleJsonMessage(string jsonStr)
        {
            try
            {
                var message = JObject.Parse(jsonStr);
                var messageType = message["type"]?.ToString();
                if (messageType == "hello")
                {
                    HandleServerHello(message);
                }

                InvokeOnJsonMessage(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"解析JSON消息失败: {e.Message}");
            }
        }

        private void HandleServerHello(JObject message)
        {
            if (message["transport"]?.ToString() != "websocket")
            {
                InvokeOnNetworkError("不支持的传输类型");
                return;
            }

            var audioParams = message["audio_params"];
            if (audioParams != null)
            {
                ServerSampleRate = audioParams["sample_rate"]?.Value<int>() ?? 16000;
            }

            _isAudioChannelOpen = true;
            _helloTaskCompletionSource.SetResult(true);
            InvokeOnChannelOpened();
        }

        public override async Task SendAudio(ReadOnlyMemory<byte> audioData)
        {
            if (!_isConnected || !_isAudioChannelOpen) return;

            try
            {
                await _webSocket.SendAsync(
                    audioData,
                    WebSocketMessageType.Binary,
                    true,
                    _cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                InvokeOnNetworkError($"发送音频数据失败: {e.Message}");
            }
        }

        protected override async Task SendJson(object data)
        {
            if (!_isConnected) return;
            try
            {
                var jsonStr = JsonConvert.SerializeObject(data);
                var bytes = Encoding.UTF8.GetBytes(jsonStr);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                InvokeOnNetworkError($"发送JSON消息失败: {e.Message}");
            }
        }

        public override async Task CloseAudioChannel()
        {
            await CloseWebSocket();
        }

        private async Task CloseWebSocket()
        {
            if (_webSocket != null)
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await _webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Client initiated close",
                            CancellationToken.None);
                    }
                    catch (Exception)
                    {
                        // Ignore any errors during close
                    }
                }

                _webSocket.Dispose();
                _webSocket = null;
            }

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            _isAudioChannelOpen = false;
            _isConnected = false;
        }

        public override bool IsAudioChannelOpened()
        {
            return _isConnected && _isAudioChannelOpen && !_errorOccurred && !IsTimeout();
        }

        private bool IsTimeout()
        {
            if (_lastIncomingTime == default)
            {
                return false;
            }

            return (DateTime.Now - _lastIncomingTime).TotalSeconds > 120;
        }

        private void SetError(string errorMessage)
        {
            _errorOccurred = true;
            InvokeOnNetworkError(errorMessage);
        }
    }
}
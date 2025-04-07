using System;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace XiaoZhi.Unity
{
    public class OTA
    {
        private string _checkVersionUrl;
        private readonly Dictionary<string, string> _headers = new();
        private string _postData;

        public string ActivationMessage { get; private set; }

        public string ActivationCode { get; private set; }

        public void SetCheckVersionUrl(string url)
        {
            Debug.Log("Set check version URL: " + url);
            _checkVersionUrl = url;
        }

        public void SetHeader(string key, string value)
        {
            Debug.Log("Set header: " + key + " = " + value);
            _headers[key] = value;
        }

        public void SetPostData(string data)
        {
            Debug.Log("Set post data: ");
            Debug.Log(data);
            _postData = data;
        }

        public async Task<bool> CheckVersionAsync()
        {
            if (string.IsNullOrEmpty(_checkVersionUrl) || _checkVersionUrl.Length < 10)
            {
                Debug.LogError("Check version URL is not properly set");
                return false;
            }

            var webRequest = new UnityWebRequest();
            foreach (var header in _headers)
                webRequest.SetRequestHeader(header.Key, header.Value);
            webRequest.method = "POST";
            webRequest.url = _checkVersionUrl;
            webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(_postData));
            webRequest.uploadHandler.contentType = "application/json";
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.timeout = 5;
            try
            {
                await webRequest.SendWebRequest();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"UnityWebRequest Error: {webRequest.error}");
                webRequest.Dispose();
                return false;
            }

            var jsonResponse = webRequest.downloadHandler.text;
            webRequest.Dispose();
            Debug.Log("ota response: " + jsonResponse);
            var root = JObject.Parse(jsonResponse);
            if (!root.TryGetValue("firmware", out var firmware) ||
                firmware["version"] == null)
                return false;
            ActivationMessage = root["activation"]?["message"]?.ToString();
            ActivationCode = root["activation"]?["code"]?.ToString();
            return true;
        }
    }
}
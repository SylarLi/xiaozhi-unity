using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json.Linq;

namespace XiaoZhi.Unity
{
    public class OTA
    {
        private HttpClient _httpClient;
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

            _httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, _checkVersionUrl);
            request.Content = new StringContent(_postData, Encoding.UTF8, "application/json");
            foreach (var header in _headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Debug.LogError($"HTTP Error: {response.StatusCode}, {error}");
                _httpClient.Dispose();
                return false;
            }
            
            var jsonResponse = await response.Content.ReadAsStringAsync();
            Debug.Log("ota response: " + jsonResponse);
            _httpClient.Dispose();
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
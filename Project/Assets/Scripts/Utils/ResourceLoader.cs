using System;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace XiaoZhi.Unity
{
    /// <summary>
    /// 资源加载工具类
    /// </summary>
    public static class ResourceLoader
    {
        /// <summary>
        /// 资源类型
        /// </summary>
        public enum ResourceType
        {
            /// <summary>
            /// StreamingAssets目录
            /// </summary>
            StreamingAssets,

            /// <summary>
            /// 数据目录
            /// </summary>
            DataPath
        }

        /// <summary>
        /// 获取资源完整路径
        /// </summary>
        /// <param name="type">资源类型</param>
        /// <param name="relativePath">相对路径</param>
        /// <returns>完整的文件路径</returns>
        public static string GetFullPath(ResourceType type, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                throw new ArgumentException("路径不能为空", nameof(relativePath));
            relativePath = relativePath.Replace("\\", "/");
            switch (type)
            {
                case ResourceType.StreamingAssets:
                    return Path.Combine(Application.streamingAssetsPath, relativePath);
                case ResourceType.DataPath:
                    return Path.Combine(Application.persistentDataPath, relativePath);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// 同步读取文本文件
        /// </summary>
        /// <param name="type">资源类型</param>
        /// <param name="relativePath">相对路径</param>
        /// <returns>文件内容</returns>
        public static string ReadAllText(ResourceType type, string relativePath)
        {
            switch (type)
            {
                case ResourceType.StreamingAssets:
#if UNITY_ANDROID && !UNITY_EDITOR
                    var request = UnityWebRequest.Get(GetFullPath(type, relativePath));
                    request.SendWebRequest();
                    while (!request.isDone) { }
                    if (request.result != UnityWebRequest.Result.Success) 
                        throw new IOException($"读取文件失败: {request.error}");
                    return request.downloadHandler.text;
#else
                    return File.ReadAllText(GetFullPath(type, relativePath));
#endif
                case ResourceType.DataPath:
                    return File.ReadAllText(GetFullPath(type, relativePath));
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// 异步读取文本文件
        /// </summary>
        /// <param name="type">资源类型</param>
        /// <param name="relativePath">相对路径</param>
        /// <returns>文件内容</returns>
        public static async Task<string> ReadAllTextAsync(ResourceType type, string relativePath)
        {
            switch (type)
            {
                case ResourceType.StreamingAssets:
#if UNITY_ANDROID && !UNITY_EDITOR
                    var request = UnityWebRequest.Get(GetFullPath(type, relativePath));
                    await request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success) 
                        throw new IOException($"读取文件失败: {request.error}");
                    return request.downloadHandler.text;
#else
                    return await File.ReadAllTextAsync(GetFullPath(type, relativePath));
#endif
                case ResourceType.DataPath:
                    return await File.ReadAllTextAsync(GetFullPath(type, relativePath));
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// 同步读取二进制文件
        /// </summary>
        /// <param name="type">资源类型</param>
        /// <param name="relativePath">相对路径</param>
        /// <returns>文件字节数组</returns>
        public static byte[] ReadAllBytes(ResourceType type, string relativePath)
        {
            switch (type)
            {
                case ResourceType.StreamingAssets:
#if UNITY_ANDROID && !UNITY_EDITOR
                    var request = UnityWebRequest.Get(GetFullPath(type, relativePath));
                    request.SendWebRequest();
                    while (!request.isDone) { }
                    if (request.result != UnityWebRequest.Result.Success) 
                        throw new IOException($"读取文件失败: {request.error}");
                    return request.downloadHandler.data;
#else
                    return File.ReadAllBytes(GetFullPath(type, relativePath));
#endif
                case ResourceType.DataPath:
                    return File.ReadAllBytes(GetFullPath(type, relativePath));
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// 异步读取二进制文件
        /// </summary>
        /// <param name="type">资源类型</param>
        /// <param name="relativePath">相对路径</param>
        /// <returns>文件字节数组</returns>
        public static async Task<byte[]> ReadAllBytesAsync(ResourceType type, string relativePath)
        {
            switch (type)
            {
                case ResourceType.StreamingAssets:
#if UNITY_ANDROID && !UNITY_EDITOR
                    var request = UnityWebRequest.Get(GetFullPath(type, relativePath));
                    await request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success) 
                        throw new IOException($"读取文件失败: {request.error}");
                    return request.downloadHandler.data;
#else
                    return await File.ReadAllBytesAsync(GetFullPath(type, relativePath));
#endif
                case ResourceType.DataPath:
                    return await File.ReadAllBytesAsync(GetFullPath(type, relativePath));
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        /// <param name="type">资源类型</param>
        /// <param name="relativePath">相对路径</param>
        /// <returns>文件是否存在</returns>
        public static bool FileExists(ResourceType type, string relativePath)
        {
            
            switch (type)
            {
                case ResourceType.StreamingAssets:
#if UNITY_ANDROID && !UNITY_EDITOR
                    using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var assetManager = activity.Call<AndroidJavaObject>("getAssets"))
                    {
                        try
                        {
                            using var inputStream = assetManager.Call<AndroidJavaObject>("open", relativePath);
                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
#else
                    return File.Exists(GetFullPath(type, relativePath));
#endif
                case ResourceType.DataPath:
                    return File.Exists(GetFullPath(type, relativePath));
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
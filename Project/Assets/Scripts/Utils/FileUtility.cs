using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace XiaoZhi.Unity
{
    /// <summary>
    /// 文件加载管理
    /// </summary>
    public static class FileUtility
    {
        /// <summary>
        /// 资源类型
        /// </summary>
        public enum FileType
        {
            /// <summary>
            /// StreamingAssets目录
            /// </summary>
            StreamingAssets,

            /// <summary>
            /// 数据目录
            /// </summary>
            DataPath,

            /// <summary>
            /// Resources目录
            /// </summary>
            Resources
        }

        /// <summary>
        /// 获取资源完整路径
        /// </summary>
        /// <param name="type">资源类型</param>
        /// <param name="relativePath">相对路径</param>
        /// <returns>完整的文件路径</returns>
        public static string GetFullPath(FileType type, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                throw new ArgumentException("路径不能为空", nameof(relativePath));
            relativePath = relativePath.Replace("\\", "/");
            switch (type)
            {
                case FileType.StreamingAssets:
                    return Path.Combine(Application.streamingAssetsPath, relativePath);
                case FileType.DataPath:
                    return Path.Combine(Application.persistentDataPath, relativePath);
                case FileType.Resources:
                    return relativePath;
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
        public static string ReadAllText(FileType type, string relativePath)
        {
            switch (type)
            {
                case FileType.StreamingAssets:
#if UNITY_ANDROID && !UNITY_EDITOR
                    var request = UnityEngine.Networking.UnityWebRequest.Get(GetFullPath(type, relativePath));
                    request.SendWebRequest();
                    while (!request.isDone) { }
                    if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success) 
                        throw new IOException($"读取文件失败: {request.error}");
                    return request.downloadHandler.text;
#else
                    return File.ReadAllText(GetFullPath(type, relativePath));
#endif
                case FileType.DataPath:
                    return File.ReadAllText(GetFullPath(type, relativePath));
                case FileType.Resources:
                    var asset = Resources.Load<TextAsset>(relativePath);
                    var text = asset?.text;
                    Resources.UnloadAsset(asset);
                    return text;
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
        public static async Task<string> ReadAllTextAsync(FileType type, string relativePath)
        {
            switch (type)
            {
                case FileType.StreamingAssets:
#if UNITY_ANDROID && !UNITY_EDITOR
                    var request = UnityEngine.Networking.UnityWebRequest.Get(GetFullPath(type, relativePath));
                    await request.SendWebRequest();
                    if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success) 
                        throw new IOException($"读取文件失败: {request.error}");
                    return request.downloadHandler.text;
#else
                    return await File.ReadAllTextAsync(GetFullPath(type, relativePath));
#endif
                case FileType.DataPath:
                    return await File.ReadAllTextAsync(GetFullPath(type, relativePath));
                case FileType.Resources:
                    var asset = await Resources.LoadAsync<TextAsset>(relativePath) as TextAsset;
                    var text = asset?.text;
                    Resources.UnloadAsset(asset);
                    return text;
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
        public static byte[] ReadAllBytes(FileType type, string relativePath)
        {
            switch (type)
            {
                case FileType.StreamingAssets:
#if UNITY_ANDROID && !UNITY_EDITOR
                    var request = UnityEngine.Networking.UnityWebRequest.Get(GetFullPath(type, relativePath));
                    request.SendWebRequest();
                    while (!request.isDone) { }
                    if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success) 
                        throw new IOException($"读取文件失败: {request.error}");
                    return request.downloadHandler.data;
#else
                    return File.ReadAllBytes(GetFullPath(type, relativePath));
#endif
                case FileType.DataPath:
                    return File.ReadAllBytes(GetFullPath(type, relativePath));
                case FileType.Resources:
                    var asset = Resources.Load<TextAsset>(relativePath);
                    var bytes = asset?.bytes;
                    Resources.UnloadAsset(asset);
                    return bytes;
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
        public static async Task<byte[]> ReadAllBytesAsync(FileType type, string relativePath)
        {
            switch (type)
            {
                case FileType.StreamingAssets:
#if UNITY_ANDROID && !UNITY_EDITOR
                    var request = UnityEngine.Networking.UnityWebRequest.Get(GetFullPath(type, relativePath));
                    await request.SendWebRequest();
                    if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success) 
                        throw new IOException($"读取文件失败: {request.error}");
                    return request.downloadHandler.data;
#else
                    return await File.ReadAllBytesAsync(GetFullPath(type, relativePath));
#endif
                case FileType.DataPath:
                    return await File.ReadAllBytesAsync(GetFullPath(type, relativePath));
                case FileType.Resources:
                    var asset = await Resources.LoadAsync<TextAsset>(relativePath) as TextAsset;
                    var bytes = asset?.bytes;
                    Resources.UnloadAsset(asset);
                    return bytes;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// 将StreamingAssets目录下的资源复制到PersistentDataPath
        /// </summary>
        /// <param name="relativePath">相对路径</param>
        /// <param name="cancellationToken"></param>
        /// <returns>复制是否成功</returns>
        public static async UniTask<bool> CopyStreamingAssetsToDataPath(string relativePath, CancellationToken cancellationToken = default)
        {
            var targetPath = GetFullPath(FileType.DataPath, relativePath);
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);
            var request =
                UnityEngine.Networking.UnityWebRequest.Get(GetFullPath(FileType.StreamingAssets, relativePath));
            await request.SendWebRequest();
            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                return false;
            await File.WriteAllBytesAsync(targetPath, request.downloadHandler.data, cancellationToken);
            return true;
        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        /// <param name="type">资源类型</param>
        /// <param name="relativePath">相对路径</param>
        /// <returns>文件是否存在</returns>
        public static bool FileExists(FileType type, string relativePath)
        {
            switch (type)
            {
                case FileType.StreamingAssets:
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
                case FileType.DataPath:
                    return File.Exists(GetFullPath(type, relativePath));
                case FileType.Resources:
                    throw new NotSupportedException();
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
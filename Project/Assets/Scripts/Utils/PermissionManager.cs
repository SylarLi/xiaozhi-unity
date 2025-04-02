using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;

namespace XiaoZhi.Unity
{
    public enum PermissionType
    {
        ReadStorage,
        WriteStorage,
        Camera,
        Microphone,
    }

    public struct PermissionResult
    {
        public PermissionType Type;
        public bool Granted;
    }
    
    public class PermissionManager
    {
        public static async UniTask<IEnumerable<PermissionResult>> RequestPermissions(
            params PermissionType[] permissions)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var result =
                await AndroidRuntimePermissions.RequestPermissionsAsync(permissions.Select(ParseAndroidPermissionType)
                    .ToArray());
            return result.Select((i, index) => new PermissionResult
                { Type = permissions[index], Granted = ParseAndroidPermissionState(i) });
#else
            return permissions.Select(i => new PermissionResult { Type = i, Granted = true });
#endif
        }

        private static string ParseAndroidPermissionType(PermissionType type)
        {
            return type switch
            {
                PermissionType.ReadStorage => "android.permission.READ_EXTERNAL_STORAGE",
                PermissionType.WriteStorage => "android.permission.WRITE_EXTERNAL_STORAGE",
                PermissionType.Camera => "android.permission.CAMERA",
                PermissionType.Microphone => "android.permission.RECORD_AUDIO",
                _ => throw new NotSupportedException()
            };
        }

        private static bool ParseAndroidPermissionState(AndroidRuntimePermissions.Permission state)
        {
            return state == AndroidRuntimePermissions.Permission.Granted;
        }
    }
}
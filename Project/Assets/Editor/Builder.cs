using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Linq;
using System.IO.Compression;

namespace XiaoZhi.Unity
{
    public static partial class Builder
    {
        private static BuildPresets _buildPresets;
        private static BuildTarget _buildTarget;

        public static BuildReport Build(BuildPresets buildPresets, BuildTarget buildTarget)
        {
            _buildPresets = buildPresets;
            _buildTarget = buildTarget;
            if (_buildTarget == BuildTarget.Android) SetAndroidKeystore();
            var buildOptions = BuildOptions.None;
            if (buildPresets.Debug)
            {
                buildOptions |= BuildOptions.Development;
                buildOptions |= BuildOptions.AllowDebugging;
                buildOptions |= BuildOptions.ConnectWithProfiler;
            }

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray(),
                locationPathName = GetBuildPath(),
                target = buildTarget,
                options = buildOptions
            };

            return BuildPlayer(options);
        }

        private static string GetBuildPath()
        {
            var buildPath = Path.Combine(_buildPresets.OutputPath, Enum.GetName(typeof(BuildTarget), _buildTarget)!);
            if (!Directory.Exists(buildPath)) Directory.CreateDirectory(buildPath);
            var buildName = _buildTarget switch
            {
                BuildTarget.StandaloneWindows64 => $"{PlayerSettings.productName}.exe",
                BuildTarget.Android => $"{PlayerSettings.productName}.apk",
                _ => throw new ArgumentOutOfRangeException()
            };
            return Path.Combine(buildPath, buildName);
        }

        private static void SetAndroidKeystore()
        {
            var preset = _buildPresets.AndroidPreset;
            PlayerSettings.Android.keystoreName = preset.KeystorePath;
            PlayerSettings.Android.keystorePass = preset.KeystorePassword;
            PlayerSettings.Android.keyaliasName = preset.KeyAliasName;
            PlayerSettings.Android.keyaliasPass = preset.KeyAliasPassword;
        }

        private static BuildReport BuildPlayer(BuildPlayerOptions options)
        {
            Debug.Log($"Starting build for {options.target}...");
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build completed successfully at: {options.locationPathName}");
                PostProcessBuild(options.locationPathName);
            }
            else
            {
                Debug.LogError($"Build failed with {report.summary.totalErrors} errors.");
            }

            return report;
        }

        private static void PostProcessBuild(string buildPath)
        {
            switch (_buildTarget)
            {
                case BuildTarget.StandaloneWindows64:
                    CreateWindowsBuildZip(buildPath);
                    break;
            }
        }

        private static void CreateWindowsBuildZip(string exePath)
        {
            EditorUtility.DisplayProgressBar("Creating zip archive", "Please wait...", 0);
            var buildDir = Path.GetDirectoryName(exePath);
            var exeName = Path.GetFileNameWithoutExtension(exePath);
            var zipPath = Path.Combine(Path.GetDirectoryName(buildDir)!, $"{exeName}-{_buildTarget}.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            Debug.Log($"Creating zip archive at: {zipPath}");
            var backupFolderName = $"{exeName}_BackUpThisFolder_ButDontShipItWithYourGame";
            var burstDebugInfoFolderName = $"{exeName}_BurstDebugInformation_DoNotShip";
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var files = Directory.GetFiles(buildDir!, "*.*", SearchOption.AllDirectories);
                for (var index = 0; index < files.Length; index++)
                {
                    var file = files[index];
                    var relativePath = Path.GetRelativePath(buildDir, file);
                    if (!relativePath.StartsWith(backupFolderName) &&
                        !relativePath.StartsWith(burstDebugInfoFolderName))
                        archive.CreateEntryFromFile(file, relativePath);
                    EditorUtility.DisplayProgressBar("Creating zip archive", relativePath, (float)index / files.Length);
                }
            }

            EditorUtility.ClearProgressBar();
            Debug.Log("Zip archive created successfully.");
        }
    }
}
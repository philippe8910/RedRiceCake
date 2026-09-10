using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 命令列建置：
///   Unity.exe -batchmode -quit -projectPath &lt;專案&gt; -executeMethod MooncakeBuild.BuildQuest
/// 產物固定放在 Builds/Mooncake.apk。
/// </summary>
public static class MooncakeBuild
{
    private const string OutputDir = "Builds";
    private const string ApkName = "Mooncake.apk";

    [MenuItem("Tools/月餅 Demo/建置 Quest APK")]
    public static void BuildQuest()
    {
        string outPath = Path.Combine(OutputDir, ApkName);
        Directory.CreateDirectory(OutputDir);

        // 依 Build Settings 的順序取啟用中的場景
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Fail("Build Settings 裡沒有啟用的場景");
            return;
        }

        Debug.Log("[建置] 場景順序：\n  " + string.Join("\n  ", scenes));

        // Quest 必要設定：IL2CPP + ARM64
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = false;   // 要 .apk 不是 .aab

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("[建置] 切換平台到 Android…");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Fail("切換到 Android 平台失敗（可能未安裝 Android Build Support）");
                return;
            }
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[建置] 成功：{outPath}  " +
                      $"{summary.totalSize / 1048576f:0.0} MB  " +
                      $"耗時 {summary.totalTime.TotalSeconds:0} 秒");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        else
        {
            foreach (var step in report.steps)
                foreach (var msg in step.messages)
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Debug.LogError($"[建置] {step.name}: {msg.content}");

            Fail($"建置失敗：{summary.result}，{summary.totalErrors} 個錯誤");
        }
    }

    // 註（2026-09-06）：這裡曾經加過一段「補回 preloadedAssets 的 XR 設定」，
    // 已移除，**不要再加回來**。
    //
    // batchmode 確實會把 ProjectSettings 的 preloadedAssets 清成 []，但那對
    // 這個專案無害 —— 拿 9/4 已知正常的 APK 比對過，它的
    // globalgamemanagers.assets.split5（OpenXR feature 設定所在）跟不補
    // preloadedAssets 建出來的版本 **位元組完全相同**。
    //
    // 反而補回去會壞：preloadedAssets 原本指的是 .asset 檔裡的子物件
    // （XRGeneralSettingsPerBuildTarget 裡的「Android Settings」、
    // Open XR Package Settings 裡的「Android」），而
    // AssetDatabase.LoadAssetAtPath 回傳的是**主資產**。預載錯物件會讓
    // Android 平台的 OpenXR interaction profile 沒被註冊 →
    // **進得了 VR 但手把不顯示、沒有輸入**（實機實測過）。

    private static void Fail(string reason)
    {
        Debug.LogError("[建置] " + reason);
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}

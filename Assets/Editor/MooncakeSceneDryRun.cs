using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 掛上 <see cref="MooncakeSceneDryRunDriver"/> 並進 Play Mode 跑一遍實際場景。
///
/// CLI（不要加 -quit，試跑器跑完會自己決定 exit code）：
///   Unity.exe -batchmode -projectPath ... -executeMethod MooncakeSceneDryRun.RunIndonesian
///   Unity.exe -batchmode -projectPath ... -executeMethod MooncakeSceneDryRun.RunChinese
/// </summary>
public static class MooncakeSceneDryRun
{
    const string k_Chinese = "Assets/Scenes/ChineseMooncakeDemo.unity";
    const string k_Indonesian = "Assets/Scenes/IndonesianMooncakeDemo.unity";

    [MenuItem("Tools/月餅/試跑 中式場景")]
    public static void RunChineseFromMenu() { Launch(k_Chinese, false); }

    [MenuItem("Tools/月餅/試跑 印尼場景")]
    public static void RunIndonesianFromMenu() { Launch(k_Indonesian, false); }

    public static void RunChinese() { Launch(k_Chinese, true); }

    public static void RunIndonesian() { Launch(k_Indonesian, true); }

    static void Launch(string scenePath, bool exitWhenDone)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 只掛在記憶體裡的場景上，不存檔
        var go = new GameObject("MooncakeSceneDryRunDriver");
        var driver = go.AddComponent<MooncakeSceneDryRunDriver>();
        driver.exitEditorWhenDone = exitWhenDone;

        Debug.Log("[月餅][試跑] 進入 Play Mode：" + scenePath);
        EditorApplication.EnterPlaymode();
    }
}

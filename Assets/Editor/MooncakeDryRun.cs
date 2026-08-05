using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 掛上試跑器並進 Play Mode。
/// CLI：Unity.exe -batchmode -projectPath ... -executeMethod MooncakeDryRun.RunFromCommandLine
/// （不要加 -quit，試跑器跑完會自己決定 exit code）
/// </summary>
public static class MooncakeDryRun
{
    const string k_ScenePath = "Assets/Scenes/MooncakeDemo.unity";

    [MenuItem("Tools/月餅 Demo/試跑（進 Play Mode 自動走完兩條流程）")]
    public static void RunFromMenu()
    {
        Launch(false);
    }

    public static void RunFromCommandLine()
    {
        Launch(true);
    }

    static void Launch(bool exitWhenDone)
    {
        EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);

        // 只掛在記憶體裡的場景上，不存檔
        var go = new GameObject("MooncakeDryRunDriver");
        var driver = go.AddComponent<MooncakeDryRunDriver>();
        driver.exitEditorWhenDone = exitWhenDone;

        Debug.Log("[月餅][DryRun] 進入 Play Mode 開始試跑");
        EditorApplication.EnterPlaymode();
    }
}

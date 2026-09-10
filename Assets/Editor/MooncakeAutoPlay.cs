using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 掛上 <see cref="MooncakeAutoPlayDriver"/> 進 Play Mode，照腳本操作場景並拍照。
///
/// CLI（**不要加 -nographics**，沒有繪圖裝置會拍出全黑；也不要加 -quit）：
///   Unity.exe -batchmode -projectPath ... -executeMethod MooncakeAutoPlay.Run
///             autoScene=Assets/Scenes/IndonesianMooncakeDemo.unity
///             autoScript=D:\path\shots.txt
///             autoOut=D:\path\out
/// </summary>
public static class MooncakeAutoPlay
{
    [MenuItem("Tools/月餅 Demo/自動操作並拍照（用最近一次的腳本）")]
    public static void RunFromMenu()
    {
        Launch(Arg("autoScene", "Assets/Scenes/IndonesianMooncakeDemo.unity"),
               Arg("autoScript", ""), Arg("autoOut", ""), false);
    }

    public static void Run()
    {
        string scene = Arg("autoScene", "Assets/Scenes/IndonesianMooncakeDemo.unity");
        string script = Arg("autoScript", "");
        string outDir = Arg("autoOut", "");

        if (string.IsNullOrEmpty(script) || string.IsNullOrEmpty(outDir))
        {
            Debug.LogError("[月餅][自動操作] 需要 autoScript= 與 autoOut= 兩個參數");
            EditorApplication.Exit(2);
            return;
        }

        Launch(scene, script, outDir, true);
    }

    static void Launch(string scenePath, string script, string outDir, bool exitWhenDone)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 只掛在記憶體裡的場景上，不存檔
        var go = new GameObject("MooncakeAutoPlayDriver");
        var d = go.AddComponent<MooncakeAutoPlayDriver>();
        d.scriptPath = script;
        d.outputDir = outDir;
        d.exitEditorWhenDone = exitWhenDone;

        Debug.Log($"[月餅][自動操作] 進入 Play Mode：{scenePath}\n" +
                  $"  腳本 {script}\n  輸出 {outDir}");
        EditorApplication.EnterPlaymode();
    }

    static string Arg(string key, string fallback)
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a.StartsWith(key + "=")) return a.Substring(key.Length + 1);
        return fallback;
    }
}

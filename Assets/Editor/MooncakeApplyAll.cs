using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一次套用這批修改，省得每一項都開一次 batchmode（開一次要好幾分鐘）。
///
///   Unity.exe -batchmode -nographics -projectPath ... \
///             -executeMethod MooncakeApplyAll.RunFromCommandLine
///
/// 順序有意義：印章縮放會觸發 FBX 重新匯入，先做完再去動場景，
/// 場景存檔時才會帶到新的網格邊界。
/// </summary>
public static class MooncakeApplyAll
{
    [MenuItem("Tools/月餅 Demo/★ 全部套用（印章 / 配色 / 教學 / 包名圖示）")]
    public static void RunMenu()
    {
        Debug.Log(Run());
    }

    public static void RunFromCommandLine()
    {
        string report = Run();
        Debug.Log(report);

        string outPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "mooncake-apply-all.txt");
        foreach (var a in System.Environment.GetCommandLineArgs())
        {
            if (a.StartsWith("applyOut=")) outPath = a.Substring("applyOut=".Length);
        }
        System.IO.File.WriteAllText(outPath, report, new System.Text.UTF8Encoding(true));

        EditorApplication.Exit(0);
    }

    static string Run()
    {
        var lines = new List<string> { "[全部套用] ==================================================" };

        lines.Add(Step("① 印章匯入縮放", () => MooncakeStampScale.Apply()));
        lines.Add(Step("② 選關按鈕配色", () => MooncakeMenuTheme.Apply(true)));
        lines.Add(Step("③ 兩個場景的教學", () =>
        {
            MooncakeTutorialBuilder.BuildBoth(true);
            return true;
        }));
        lines.Add(Step("④ 包名與 App 圖示", () => MooncakeProjectSetup.Apply()));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        lines.Add("[全部套用] ==================================================");
        return string.Join("\n", lines);
    }

    static string Step(string name, System.Func<bool> action)
    {
        try
        {
            bool ok = action();
            return $"  {(ok ? "OK  " : "失敗")} {name}";
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            return $"  例外 {name}：{e.GetType().Name} {e.Message}";
        }
    }
}

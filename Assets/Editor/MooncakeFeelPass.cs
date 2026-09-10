using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 手感調整，兩個場景都套：
///   ③ 麵團放到桌上會一路滾 → 掛 MooncakeDoughAnchor（放開會停 + 靠近交接點會對正）
///   ⑤ 烤好的月餅拿不起來   → 掛 MooncakeSouvenir（出爐後可抓起來看）
///
/// 印章／烤盤／蛋液刷維持 kinematic（放空中會浮著）—— 那是原本就一致的設定，
/// 這一輪刻意不動，要改再說。
/// </summary>
public static class MooncakeFeelPass
{
    static readonly string[] k_Scenes =
    {
        "Assets/Scenes/ChineseMooncakeDemo.unity",
        "Assets/Scenes/IndonesianMooncakeDemo.unity",
    };

    [MenuItem("Tools/月餅 Demo/套用 手感調整（麵團定位/月餅可拿起）")]
    public static void ApplyMenu() { Debug.Log(Run()); }

    public static void ApplyFromCommandLine()
    {
        string report = Run();
        Debug.Log(report);
        string outPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mooncake-feel.txt");
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a.StartsWith("feelOut=")) outPath = a.Substring("feelOut=".Length);
        System.IO.File.WriteAllText(outPath, report, new System.Text.UTF8Encoding(true));
        EditorApplication.Exit(0);
    }

    static string Run()
    {
        var sb = new StringBuilder();

        sb.AppendLine("── ③ 麵團定位 ──");
        sb.AppendLine("  改在 MooncakeSpawnSource.Spawn() 生成時掛 MooncakeDoughAnchor，");
        sb.AppendLine("  不改 prefab 資產 —— 那兩個 prefab 含巢狀實例，重存會重排內部 fileID，");
        sb.AppendLine("  導致場景的 doughPiecePrefab 參考斷掉（已踩過並還原）。");

        foreach (var path in k_Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            sb.AppendLine($"── {scene.name} ──");

            int souvenirs = MakeBakedGrabbable(sb, scene);
            int anchors = AnchorSceneDough(sb, scene);

            if (souvenirs + anchors > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                sb.AppendLine("  已存檔");
            }
            else sb.AppendLine("  沒有需要改的東西");
        }

        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    // ------------------------------------------------------------------

    /// <summary>場景裡已經放著的麵團實例也補一份。</summary>
    static int AnchorSceneDough(StringBuilder sb, Scene scene)
    {
        int n = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var src in root.GetComponentsInChildren<MooncakeSpawnSource>(true))
            {
                if (src.spawnPrefab == null) continue;
                if (!src.spawnPrefab.CompareTag("DoughObject")) continue;
                sb.AppendLine($"  生成來源「{src.name}」 → prefab {src.spawnPrefab.name}" +
                              $"（addDoughAnchor={src.addDoughAnchor}，生成時才掛，prefab 不動）");
            }
        }
        return n;
    }

    /// <summary>⑤ 烤好的月餅補上 MooncakeSouvenir。</summary>
    static int MakeBakedGrabbable(StringBuilder sb, Scene scene)
    {
        var flow = Object.FindObjectOfType<MooncakeChineseFlow>();
        if (flow == null || flow.traySlots == null)
        {
            sb.AppendLine("  找不到 flow 或烤盤格子");
            return 0;
        }

        int n = 0;
        foreach (var slot in flow.traySlots)
        {
            if (slot == null || slot.bakedObject == null) continue;

            if (slot.bakedObject.GetComponent<MooncakeSouvenir>() == null)
            {
                slot.bakedObject.AddComponent<MooncakeSouvenir>();
                EditorUtility.SetDirty(slot.bakedObject);
                n++;
            }
        }

        sb.AppendLine($"  ⑤ 烤好的月餅：{n} 顆補上 MooncakeSouvenir（出爐後可抓起來看）");
        return n;
    }
}

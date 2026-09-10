using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 量兩個場景裡「握把」物件的實際世界尺寸，用來比對中式模具與印尼印章差多少。
/// 純診斷，不會改任何東西。
/// </summary>
public static class MooncakeSizeAudit
{
    struct Target
    {
        public string scene;
        public string objectName;
    }

    static readonly Target[] k_Targets =
    {
        new Target { scene = "Assets/Scenes/ChineseMooncakeDemo.unity",    objectName = "月餅-模具握把" },
        new Target { scene = "Assets/Scenes/IndonesianMooncakeDemo.unity", objectName = "月餅-印章握把" },
    };

    [MenuItem("Tools/月餅 Demo/量測 模具與印章尺寸")]
    public static void AuditMenu()
    {
        Debug.Log(Run());
    }

    public static void AuditFromCommandLine()
    {
        string report = Run();
        Debug.Log(report);

        // batchmode 的 log 有時會在 Exit 時被截掉，所以另外落一份檔案。
        string outPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "mooncake-size-audit.txt");
        foreach (var a in System.Environment.GetCommandLineArgs())
        {
            // 允許用 -auditOut <路徑> 指定輸出位置
            if (a.StartsWith("auditOut=")) outPath = a.Substring("auditOut=".Length);
        }
        System.IO.File.WriteAllText(outPath, report, new System.Text.UTF8Encoding(true));
        Debug.Log("[尺寸量測] 報告寫到 " + outPath);

        EditorApplication.Exit(0);
    }

    static string Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[尺寸量測] ==================================================");

        foreach (var t in k_Targets)
        {
            var scene = EditorSceneManager.OpenScene(t.scene, OpenSceneMode.Single);
            sb.AppendLine($"場景 {scene.name}");

            var go = FindByName(scene.GetRootGameObjects(), t.objectName);
            if (go == null)
            {
                sb.AppendLine($"  找不到「{t.objectName}」");
                continue;
            }

            var tr = go.transform;
            sb.AppendLine($"  物件      : {t.objectName}  (tag={go.tag})");
            sb.AppendLine($"  localScale: {tr.localScale.x:F4}  lossyScale: {tr.lossyScale.x:F4}");

            // 逐一列出每個 Renderer，才看得出「哪一塊」是模具／印章本體，
            // 哪一塊是掛在底下的月餅工件（工件不能跟著一起放大）。
            sb.AppendLine("  ── 逐物件 ──");
            foreach (var child in go.GetComponentsInChildren<Transform>(true))
            {
                string path = PathFrom(tr, child);
                var r = child.GetComponent<Renderer>();
                var mf = child.GetComponent<MeshFilter>();

                sb.Append($"  {path,-34} active={(child.gameObject.activeSelf ? "1" : "0")} " +
                          $"localScale={child.localScale.x:F3} lossy={child.lossyScale.x:F3}");

                if (r != null)
                {
                    var ws = r.bounds.size;
                    var ls = r.localBounds.size;
                    sb.Append($"\n      Renderer 世界={ws.x:F4} x {ws.y:F4} x {ws.z:F4}" +
                              $"  模型本身={ls.x:F4} x {ls.y:F4} x {ls.z:F4}");
                    if (mf != null && mf.sharedMesh != null)
                        sb.Append($"  mesh=「{mf.sharedMesh.name}」");
                }

                var col = child.GetComponent<Collider>();
                if (col != null)
                {
                    var cb = col.bounds.size;
                    sb.Append($"\n      {col.GetType().Name} trigger={col.isTrigger} " +
                              $"世界={cb.x:F4} x {cb.y:F4} x {cb.z:F4}");
                }

                sb.AppendLine();
            }
        }

        sb.AppendLine("[尺寸量測] ==================================================");
        return sb.ToString();
    }

    /// <summary>回傳 child 相對於 root 的路徑，root 本身回傳「.」。</summary>
    static string PathFrom(Transform root, Transform child)
    {
        if (child == root) return ".";
        var parts = new List<string>();
        var t = child;
        while (t != null && t != root)
        {
            parts.Insert(0, t.name);
            t = t.parent;
        }
        return string.Join("/", parts);
    }

    static GameObject FindByName(IEnumerable<GameObject> roots, string name)
    {
        foreach (var root in roots)
        {
            if (root.name == name) return root;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr.gameObject;
        }
        return null;
    }
}

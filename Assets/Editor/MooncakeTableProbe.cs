using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 從各站點往下打射線找桌面高度，用來確認印章、提示、脫離點有沒有穿到桌子裡。
/// 純讀取。
/// </summary>
public static class MooncakeTableProbe
{
    [MenuItem("Tools/月餅 Demo/量測 桌面高度")]
    public static void ProbeMenu() { Debug.Log(Run()); }

    public static void ProbeFromCommandLine()
    {
        string report = Run();
        Debug.Log(report);
        string outPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mooncake-table.txt");
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a.StartsWith("tableOut=")) outPath = a.Substring("tableOut=".Length);
        System.IO.File.WriteAllText(outPath, report, new System.Text.UTF8Encoding(true));
        EditorApplication.Exit(0);
    }

    static string Run()
    {
        var sb = new StringBuilder();

        foreach (var (path, handleName) in new[]
        {
            ("Assets/Scenes/ChineseMooncakeDemo.unity", "月餅-模具握把"),
            ("Assets/Scenes/IndonesianMooncakeDemo.unity", "月餅-印章握把"),
        })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            sb.AppendLine($"================ {scene.name} ================");

            var handle = Find(scene, handleName);
            if (handle == null) { sb.AppendLine($"找不到「{handleName}」"); continue; }

            var t = handle.transform;
            sb.AppendLine($"「{handleName}」 worldPos={V(t.position)}");

            var r = handle.GetComponent<Renderer>();
            if (r != null)
                sb.AppendLine($"  網格世界 Y 範圍 = {r.bounds.min.y:F4} ~ {r.bounds.max.y:F4}" +
                              $"（中心 {r.bounds.center.y:F4}）");

            // 從握把上方往下打，跳過握把自己
            Probe(sb, "握把正下方", t.position + Vector3.up * 0.5f, handle);

            foreach (var childName in new[] { "月餅-提示", "月餅-完成體", "月餅-脫離點" })
            {
                var c = FindChild(t, childName);
                if (c == null) continue;
                sb.AppendLine($"  「{childName}」 worldY={c.position.y:F4}");
            }
        }

        return sb.ToString();
    }

    static void Probe(StringBuilder sb, string label, Vector3 from, GameObject ignoreRoot)
    {
        var hits = Physics.RaycastAll(from, Vector3.down, 3f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool any = false;
        foreach (var h in hits)
        {
            // 跳過握把自己與它的子物件
            if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot.transform)) continue;

            sb.AppendLine($"  {label}：打到「{h.collider.gameObject.name}」 " +
                          $"Y={h.point.y:F4} 距離={h.distance:F4}");
            any = true;
            if (hits.Length > 0) break;   // 只要最上面那個
        }

        if (!any) sb.AppendLine($"  {label}：往下 3 公尺沒打到任何東西（沒有桌面碰撞體？）");
    }

    static string V(Vector3 v) => $"({v.x:F4}, {v.y:F4}, {v.z:F4})";

    static Transform FindChild(Transform root, string name)
    {
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            if (tr.name == name) return tr;
        return null;
    }

    static GameObject Find(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr.gameObject;
        }
        return null;
    }
}

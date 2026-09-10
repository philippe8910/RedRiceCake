using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 挖網格的實際形狀與 UV 佈局，用來決定印章該怎麼轉、握點放哪、UV 歪在哪。
/// 純讀取，只會在輸出目錄寫診斷用的 PNG。
/// </summary>
public static class MooncakeMeshProbe
{
    struct Job { public string asset; public string mesh; }

    static readonly Job[] k_Jobs =
    {
        // 印尼印章本體
        new Job { asset = "Assets/Model/MooncakeModel/Stamp.fbx", mesh = "Cylinder.001_UV_Mesh" },
        // 印尼月餅成品（UV 疑似不對）
        new Job { asset = "Assets/Model/MooncakeModel/Stamp.fbx", mesh = "Cylinder" },
    };

    [MenuItem("Tools/月餅 Demo/剖析 印章與月餅網格")]
    public static void ProbeMenu() { Debug.Log(Run(null)); }

    public static void ProbeFromCommandLine()
    {
        string outDir = System.IO.Path.GetTempPath();
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a.StartsWith("probeOut=")) outDir = a.Substring("probeOut=".Length);

        System.IO.Directory.CreateDirectory(outDir);
        string report = Run(outDir);
        Debug.Log(report);
        System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, "mesh-probe.txt"),
                                    report, new System.Text.UTF8Encoding(true));
        EditorApplication.Exit(0);
    }

    static string Run(string outDir)
    {
        var sb = new StringBuilder();

        // 場景裡的物件：預製物件內部的網格用資產路徑找不到，直接從實例上取最準
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/Scenes/IndonesianMooncakeDemo.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        foreach (var name in new[] { "月餅-印章握把", "月餅-完成體" })
        {
            var go = FindInScene(scene, name);
            var mf = go != null ? go.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) { sb.AppendLine($"場景裡找不到「{name}」的網格"); continue; }

            var m = mf.sharedMesh;
            sb.AppendLine($"================ 場景物件「{name}」 mesh={m.name} ================");
            sb.AppendLine($"頂點={m.vertexCount} bounds 中心={V(m.bounds.center)} 尺寸={V(m.bounds.size)}");
            sb.AppendLine($"資產來源={AssetDatabase.GetAssetPath(m)}");
            AxisProfile(sb, m);
            RadiusProfile(sb, m);
            UvProfile(sb, m);
            if (outDir != null) WriteUvLayout(m, System.IO.Path.Combine(outDir, $"uv-scene-{Safe(name)}.png"), sb);
        }

        foreach (var job in k_Jobs)
        {
            var mesh = LoadMesh(job.asset, job.mesh);
            if (mesh == null) { sb.AppendLine($"找不到網格 {job.mesh}（{job.asset}）"); continue; }

            sb.AppendLine($"================ {mesh.name} ================");
            sb.AppendLine($"頂點={mesh.vertexCount} 三角={mesh.triangles.Length / 3} " +
                          $"bounds 中心={V(mesh.bounds.center)} 尺寸={V(mesh.bounds.size)}");

            AxisProfile(sb, mesh);
            RadiusProfile(sb, mesh);
            UvProfile(sb, mesh);

            if (outDir != null) WriteUvLayout(mesh, System.IO.Path.Combine(outDir, $"uv-{Safe(mesh.name)}.png"), sb);
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------------

    /// <summary>每個軸切 12 段，看頂點怎麼分佈 —— 柄會在某一端形成一小撮。</summary>
    static void AxisProfile(StringBuilder sb, Mesh mesh)
    {
        sb.AppendLine("── 各軸頂點分佈（12 段，# 代表比例）──");
        var v = mesh.vertices;
        var b = mesh.bounds;

        for (int axis = 0; axis < 3; axis++)
        {
            float min = b.min[axis], size = b.size[axis];
            if (size <= 0f) continue;

            var bins = new int[12];
            foreach (var p in v)
            {
                int i = Mathf.Clamp((int)((p[axis] - min) / size * 12f), 0, 11);
                bins[i]++;
            }

            sb.AppendLine($"  軸 {"XYZ"[axis]} （{min:F4} → {b.max[axis]:F4}）");
            for (int i = 0; i < 12; i++)
            {
                float lo = min + size * i / 12f, hi = min + size * (i + 1) / 12f;
                int bars = Mathf.RoundToInt(40f * bins[i] / Mathf.Max(1, mesh.vertexCount));
                sb.AppendLine($"    [{lo,8:F4}, {hi,8:F4}] {bins[i],6}  {new string('#', bars)}");
            }
        }
    }

    /// <summary>沿著最短軸（推定是圓柱軸）看半徑變化，柄的部分半徑會突然變小。</summary>
    static void RadiusProfile(StringBuilder sb, Mesh mesh)
    {
        var b = mesh.bounds;
        int axis = 0;
        for (int i = 1; i < 3; i++) if (b.size[i] < b.size[axis]) axis = i;

        int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
        sb.AppendLine($"── 沿最短軸 {"XYZ"[axis]} 的半徑剖面（垂直於它的是 {"XYZ"[a1]}{"XYZ"[a2]} 平面）──");

        var v = mesh.vertices;
        var maxR = new float[12];
        var count = new int[12];
        float min = b.min[axis], size = b.size[axis];
        Vector3 c = b.center;

        foreach (var p in v)
        {
            int i = Mathf.Clamp((int)((p[axis] - min) / size * 12f), 0, 11);
            float dx = p[a1] - c[a1], dy = p[a2] - c[a2];
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r > maxR[i]) maxR[i] = r;
            count[i]++;
        }

        for (int i = 0; i < 12; i++)
        {
            float lo = min + size * i / 12f;
            int bars = Mathf.RoundToInt(40f * maxR[i] / Mathf.Max(0.0001f, Mathf.Max(maxR[0], maxR[11])));
            sb.AppendLine($"    {"XYZ"[axis]}={lo,8:F4}  半徑={maxR[i]:F4} 頂點={count[i],5}  {new string('=', Mathf.Max(0, bars))}");
        }

        sb.AppendLine($"  → 半徑小的那一端就是柄，握點應該放那邊；" +
                      $"半徑大的那一端是蓋章面，要朝向月餅。");
    }

    static void UvProfile(StringBuilder sb, Mesh mesh)
    {
        var uv = mesh.uv;
        if (uv == null || uv.Length == 0) { sb.AppendLine("── 沒有 UV0 ──"); return; }

        sb.AppendLine("── UV 佔用格點（16x16，# = 有 UV 落在該格）──");
        var grid = new int[16, 16];
        foreach (var p in uv)
        {
            int x = Mathf.Clamp((int)(p.x * 16f), 0, 15);
            int y = Mathf.Clamp((int)(p.y * 16f), 0, 15);
            grid[y, x]++;
        }

        for (int y = 15; y >= 0; y--)
        {
            var row = new StringBuilder("    ");
            for (int x = 0; x < 16; x++) row.Append(grid[y, x] > 0 ? '#' : '.');
            sb.AppendLine(row.ToString());
        }

        // UV 是否超出 0..1（超出會 wrap，看起來就是接縫錯位）
        int outside = 0;
        foreach (var p in uv) if (p.x < 0f || p.x > 1f || p.y < 0f || p.y > 1f) outside++;
        sb.AppendLine($"  超出 0..1 的 UV 點：{outside} / {uv.Length}");
    }

    /// <summary>把 UV 線框畫成 PNG，可以直接看出佈局有沒有歪。</summary>
    static void WriteUvLayout(Mesh mesh, string path, StringBuilder sb)
    {
        var uv = mesh.uv;
        if (uv == null || uv.Length == 0) return;

        const int S = 512;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color32[S * S];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(18, 18, 22, 255);

        var tris = mesh.triangles;
        for (int i = 0; i < tris.Length; i += 3)
        {
            DrawLine(px, S, uv[tris[i]], uv[tris[i + 1]]);
            DrawLine(px, S, uv[tris[i + 1]], uv[tris[i + 2]]);
            DrawLine(px, S, uv[tris[i + 2]], uv[tris[i]]);
        }

        tex.SetPixels32(px);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        sb.AppendLine($"  UV 佈局圖 → {path}");
    }

    static void DrawLine(Color32[] px, int S, Vector2 a, Vector2 b)
    {
        int x0 = Mathf.Clamp((int)(a.x * (S - 1)), 0, S - 1);
        int y0 = Mathf.Clamp((int)(a.y * (S - 1)), 0, S - 1);
        int x1 = Mathf.Clamp((int)(b.x * (S - 1)), 0, S - 1);
        int y1 = Mathf.Clamp((int)(b.y * (S - 1)), 0, S - 1);

        int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        var col = new Color32(120, 220, 160, 255);
        for (int guard = 0; guard < 4096; guard++)
        {
            px[y0 * S + x0] = col;
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    // ------------------------------------------------------------------

    static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr.gameObject;
        }
        return null;
    }

    static Mesh LoadMesh(string assetPath, string meshName)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            if (o is Mesh m && m.name == meshName) return m;
        return null;
    }

    static string Safe(string s)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    static string V(Vector3 v) => $"({v.x:F4}, {v.y:F4}, {v.z:F4})";
}

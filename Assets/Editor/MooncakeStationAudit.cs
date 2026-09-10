using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;   // XRI 3.x 起 XRGrabInteractable 移到這個命名空間

/// <summary>
/// 把兩個場景的「模具／印章握把」整站挖出來對照：階層、XR 抓取設定、
/// attach 點、接收器、以及月餅網格的 UV 狀況。純讀取。
///
/// 中式那站是對照組（已知手感正常），印尼那站是要修的。
/// </summary>
public static class MooncakeStationAudit
{
    struct Target { public string scene; public string handle; }

    static readonly Target[] k_Targets =
    {
        new Target { scene = "Assets/Scenes/ChineseMooncakeDemo.unity",    handle = "月餅-模具握把" },
        new Target { scene = "Assets/Scenes/IndonesianMooncakeDemo.unity", handle = "月餅-印章握把" },
    };

    [MenuItem("Tools/月餅 Demo/量測 模具站結構")]
    public static void AuditMenu() { Debug.Log(Run()); }

    public static void AuditFromCommandLine()
    {
        string report = Run();
        Debug.Log(report);

        string outPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mooncake-station.txt");
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a.StartsWith("stationOut=")) outPath = a.Substring("stationOut=".Length);

        System.IO.File.WriteAllText(outPath, report, new System.Text.UTF8Encoding(true));
        EditorApplication.Exit(0);
    }

    static string Run()
    {
        var sb = new StringBuilder();

        foreach (var t in k_Targets)
        {
            var scene = EditorSceneManager.OpenScene(t.scene, OpenSceneMode.Single);
            sb.AppendLine($"================ {scene.name} ================");

            var handle = Find(scene, t.handle);
            if (handle == null) { sb.AppendLine($"找不到「{t.handle}」"); continue; }

            DumpHierarchy(sb, handle.transform, handle.transform);
            DumpGrab(sb, handle);
            DumpStation(sb, handle);
            DumpMeshes(sb, handle.transform);
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------------

    static void DumpHierarchy(StringBuilder sb, Transform root, Transform t)
    {
        sb.AppendLine("── 階層與 transform ──");
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            string path = child == root ? "." : PathFrom(root, child);
            sb.AppendLine($"  {path}");
            sb.AppendLine($"      active={(child.gameObject.activeSelf ? 1 : 0)} " +
                          $"localPos={V(child.localPosition)} localEuler={V(child.localEulerAngles)} " +
                          $"localScale={V(child.localScale)}");
            sb.AppendLine($"      worldPos={V(child.position)} worldEuler={V(child.eulerAngles)} " +
                          $"lossy={V(child.lossyScale)}");

            var comps = new List<string>();
            foreach (var c in child.GetComponents<Component>())
                if (c != null && !(c is Transform)) comps.Add(c.GetType().Name);
            if (comps.Count > 0) sb.AppendLine($"      元件: {string.Join(", ", comps)}");
        }
    }

    static void DumpGrab(StringBuilder sb, GameObject handle)
    {
        sb.AppendLine("── XR 抓取 ──");
        var grab = handle.GetComponentInChildren<XRGrabInteractable>(true);
        if (grab == null) { sb.AppendLine("  沒有 XRGrabInteractable"); return; }

        sb.AppendLine($"  掛在「{grab.gameObject.name}」");
        sb.AppendLine($"  movementType={grab.movementType} useDynamicAttach={grab.useDynamicAttach}");
        sb.AppendLine($"  attachEaseInTime={grab.attachEaseInTime} throwOnDetach={grab.throwOnDetach}");
        sb.AppendLine($"  retainTransformParent={grab.retainTransformParent} trackRotation={grab.trackRotation}");

        if (grab.attachTransform == null)
        {
            sb.AppendLine("  attachTransform = (無) → 會用物件原點當握點");
        }
        else
        {
            var at = grab.attachTransform;
            sb.AppendLine($"  attachTransform = 「{at.name}」");
            sb.AppendLine($"      localPos={V(at.localPosition)} localEuler={V(at.localEulerAngles)}");
            sb.AppendLine($"      worldPos={V(at.position)} worldEuler={V(at.eulerAngles)}");

            // attach 點相對於模型中心差多遠 —— 這決定「抓起來手感」
            var r = grab.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                var c = r.bounds.center;
                sb.AppendLine($"      模型bounds中心={V(c)} 尺寸={V(r.bounds.size)}");
                sb.AppendLine($"      attach 距中心 = {Vector3.Distance(at.position, c):F4} 公尺");
            }
        }

        foreach (var col in handle.GetComponentsInChildren<Collider>(true))
            sb.AppendLine($"  Collider {col.GetType().Name} on「{col.gameObject.name}」 " +
                          $"trigger={col.isTrigger} 世界尺寸={V(col.bounds.size)}");
    }

    static void DumpStation(StringBuilder sb, GameObject handle)
    {
        sb.AppendLine("── MoldStation / Socket ──");
        var st = handle.GetComponentInChildren<MooncakeMoldStation>(true);
        if (st == null) { sb.AppendLine("  沒有 MooncakeMoldStation"); return; }

        sb.AppendLine($"  hintObject   = {Name(st.hintObject)}");
        sb.AppendLine($"  moldedObject = {Name(st.moldedObject)}");
        sb.AppendLine($"  autoFitGrabCollider={st.autoFitGrabCollider} padding={st.grabColliderPadding}");
        sb.AppendLine($"  grabCollider = {(st.grabCollider != null ? st.grabCollider.gameObject.name : "(空)")}");

        var socket = st.socket != null ? st.socket
                   : (st.hintObject != null ? st.hintObject.GetComponentInChildren<MooncakeDropSocket>(true) : null);
        if (socket == null) { sb.AppendLine("  socket = (找不到)"); return; }

        sb.AppendLine($"  socket 掛在「{socket.gameObject.name}」 worldPos={V(socket.transform.position)}");
        var sc = socket.GetComponent<Collider>();
        if (sc != null) sb.AppendLine($"  socket collider {sc.GetType().Name} 世界尺寸={V(sc.bounds.size)} " +
                                      $"中心={V(sc.bounds.center)}");
    }

    static void DumpMeshes(StringBuilder sb, Transform root)
    {
        sb.AppendLine("── 網格與 UV ──");
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m == null) continue;

            sb.AppendLine($"  「{mf.gameObject.name}」 mesh={m.name} 頂點={m.vertexCount} 子網格={m.subMeshCount}");
            sb.AppendLine($"      localBounds 尺寸={V(m.bounds.size)}");

            var uv = m.uv;
            if (uv == null || uv.Length == 0) { sb.AppendLine("      沒有 UV0"); continue; }

            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (var p in uv)
            {
                if (p.x < minU) minU = p.x; if (p.x > maxU) maxU = p.x;
                if (p.y < minV) minV = p.y; if (p.y > maxV) maxV = p.y;
            }
            sb.AppendLine($"      UV0 範圍 U[{minU:F4}, {maxU:F4}] V[{minV:F4}, {maxV:F4}]  " +
                          $"跨度 U={maxU - minU:F4} V={maxV - minV:F4}");
            if (maxU - minU > 0.0001f && maxV - minV > 0.0001f)
                sb.AppendLine($"      U/V 跨度比 = {(maxU - minU) / (maxV - minV):F4}（1.0 才是等比，偏離就會被拉歪）");
        }

        sb.AppendLine("── 材質 ──");
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                string tiling = mat.HasProperty("_BaseMap")
                    ? $"tiling={V2(mat.GetTextureScale("_BaseMap"))} offset={V2(mat.GetTextureOffset("_BaseMap"))}"
                    : "(無 _BaseMap)";
                sb.AppendLine($"  「{r.gameObject.name}」 材質={mat.name} shader={mat.shader.name} {tiling}");
            }
        }
    }

    // ------------------------------------------------------------------

    static string Name(GameObject go) => go != null ? go.name : "(空)";
    static string V(Vector3 v) => $"({v.x:F4}, {v.y:F4}, {v.z:F4})";
    static string V2(Vector2 v) => $"({v.x:F3}, {v.y:F3})";

    static string PathFrom(Transform root, Transform child)
    {
        var parts = new List<string>();
        var t = child;
        while (t != null && t != root) { parts.Insert(0, t.name); t = t.parent; }
        return string.Join("/", parts);
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

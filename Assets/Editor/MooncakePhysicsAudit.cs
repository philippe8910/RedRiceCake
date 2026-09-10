using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 查可抓取物件的剛體設定（回答「放在空中會不會浮空」），
/// 以及麵團交接點、烤好的月餅目前能不能抓。純讀取。
/// </summary>
public static class MooncakePhysicsAudit
{
    [MenuItem("Tools/月餅 Demo/量測 物理與可抓取狀態")]
    public static void AuditMenu() { Debug.Log(Run()); }

    public static void AuditFromCommandLine()
    {
        string report = Run();
        Debug.Log(report);
        string outPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mooncake-physics.txt");
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a.StartsWith("physOut=")) outPath = a.Substring("physOut=".Length);
        System.IO.File.WriteAllText(outPath, report, new System.Text.UTF8Encoding(true));
        EditorApplication.Exit(0);
    }

    static string Run()
    {
        var sb = new StringBuilder();

        foreach (var path in new[]
        {
            "Assets/Scenes/ChineseMooncakeDemo.unity",
            "Assets/Scenes/IndonesianMooncakeDemo.unity",
        })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            sb.AppendLine($"================ {scene.name} ================");

            sb.AppendLine("── 所有可抓取物件的剛體 ──");
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var g in root.GetComponentsInChildren<XRGrabInteractable>(true))
                {
                    var rb = g.GetComponent<Rigidbody>();
                    string phys = rb == null
                        ? "沒有 Rigidbody"
                        : $"isKinematic={rb.isKinematic} useGravity={rb.useGravity} " +
                          $"mass={rb.mass} constraints={rb.constraints} 偵測={rb.collisionDetectionMode}";
                    sb.AppendLine($"  「{g.gameObject.name}」");
                    sb.AppendLine($"      {phys}");
                    sb.AppendLine($"      movementType={g.movementType} throwOnDetach={g.throwOnDetach} " +
                                  $"retainParent={g.retainTransformParent} dynamicAttach={g.useDynamicAttach}");
                }
            }

            var flow = Object.FindObjectOfType<MooncakeChineseFlow>();
            if (flow != null)
            {
                sb.AppendLine("── 麵團交接點 ──");
                if (flow.doughHint != null)
                {
                    var h = flow.doughHint;
                    sb.AppendLine($"  doughHint =「{h.name}」 worldPos={V(h.transform.position)}");
                    foreach (var c in h.GetComponentsInChildren<Collider>(true))
                        sb.AppendLine($"      collider {c.GetType().Name} trigger={c.isTrigger} " +
                                      $"世界尺寸={V(c.bounds.size)}");
                }
                else sb.AppendLine("  doughHint = (空)");

                if (flow.doughBall != null)
                {
                    var prefab = flow.doughBall.spawnPrefab;
                    sb.AppendLine($"  生成來源「{flow.doughBall.name}」 prefab={(prefab != null ? prefab.name : "(空)")}");
                    if (prefab != null)
                    {
                        var rb = prefab.GetComponent<Rigidbody>();
                        sb.AppendLine($"      prefab tag={prefab.tag} " +
                                      (rb != null
                                        ? $"isKinematic={rb.isKinematic} useGravity={rb.useGravity} " +
                                          $"drag={rb.drag} angularDrag={rb.angularDrag} constraints={rb.constraints}"
                                        : "沒有 Rigidbody"));
                        foreach (var c in prefab.GetComponentsInChildren<Collider>(true))
                            sb.AppendLine($"      prefab collider {c.GetType().Name} trigger={c.isTrigger}");
                    }
                }

                sb.AppendLine("── 烤盤格子上的月餅 ──");
                if (flow.traySlots != null)
                {
                    foreach (var slot in flow.traySlots)
                    {
                        if (slot == null) continue;
                        sb.AppendLine($"  「{slot.name}」");
                        DumpPiece(sb, "生月餅 placedObject", slot.placedObject);
                        DumpPiece(sb, "烤好 bakedObject ", slot.bakedObject);
                    }
                }
            }
        }

        return sb.ToString();
    }

    static void DumpPiece(StringBuilder sb, string label, GameObject go)
    {
        if (go == null) { sb.AppendLine($"      {label} = (空)"); return; }

        var grab = go.GetComponent<XRGrabInteractable>();
        var rb = go.GetComponent<Rigidbody>();
        var col = go.GetComponent<Collider>();
        sb.AppendLine($"      {label} =「{go.name}」 tag={go.tag} 可抓={(grab != null)} " +
                      $"剛體={(rb != null)} 碰撞體={(col != null ? col.GetType().Name : "無")}");
    }

    static string V(Vector3 v) => $"({v.x:F4}, {v.y:F4}, {v.z:F4})";
}

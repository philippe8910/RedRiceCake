using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 修印尼場景四件事。**只動 IndonesianMooncakeDemo，中式場景完全不碰。**
///
/// 這些問題同一個病因：印尼場景是從中式複製出來的，印章換了模型
/// 但所有 transform／材質都照抄。
///
///   ① 印章躺著 ── 網格的圓柱軸是 local Z（蓋章面在 −Z、握柄在 +Z），
///      而握把只有 Y 軸旋轉，所以蓋章面朝側面。轉成面朝下。
///   ② 提示不在印章底下 ── 位置抄自模具的凹槽偏移，距印章中心 0.105 m，
///      但印章半徑只有 0.077 m。改成貼在蓋章面正下方。
///   ③ 月餅黏在印章上 ── 打開 MoldStation.detachAfterStamp，
///      並把烤盤格子改成收「手拿的月餅」。
///   ④ 材質用成中式的 ── CHmooncake* 換成 INDmooncake*。
/// </summary>
public static class MooncakeIndonesianFix
{
    const string k_Scene = "Assets/Scenes/IndonesianMooncakeDemo.unity";
    const string k_Handle = "月餅-印章握把";
    const string k_DetachPointName = "月餅-脫離點";

    // 蓋章面在網格 local −Z（半徑剖面實測：Z=−0.0593 半徑 0.0588，
    // Z=−0.0335 之後驟降到 0.013 以下變成握柄）
    const float k_FaceZ = -0.0593f;
    const float k_HandleZ = 0.0000f;   // 握柄段的中間，握點放這

    // 蓋章面要停在月餅上方多少（公尺，世界單位）——留一點縫才看得到底下的月餅
    const float k_FaceClearance = 0.012f;

    static readonly (string ch, string ind)[] k_MaterialSwaps =
    {
        ("Assets/Model/texture/material/CHmooncakeUnbake.mat",
         "Assets/Model/texture/material/INDmooncakeUnbake.mat"),
        ("Assets/Model/texture/material/CHmooncake.mat",
         "Assets/Model/texture/material/INDmooncake.mat"),
    };

    [MenuItem("Tools/月餅 Demo/修正 印尼場景（印章朝向/提示/脫離/材質）")]
    public static void FixMenu() { Debug.Log(Run(true)); }

    public static void FixFromCommandLine()
    {
        string report = Run(true);
        Debug.Log(report);

        string outPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mooncake-indfix.txt");
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a.StartsWith("fixOut=")) outPath = a.Substring("fixOut=".Length);

        System.IO.File.WriteAllText(outPath, report, new System.Text.UTF8Encoding(true));
        EditorApplication.Exit(0);
    }

    static string Run(bool save)
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene(k_Scene, OpenSceneMode.Single);

        var handle = Find(scene, k_Handle);
        if (handle == null) return $"找不到「{k_Handle}」，什麼都沒改";

        FixOrientationAndGrab(sb, handle);

        // 高度一律從實際量到的桌面反推，不寫死 —— 先前用相對距離
        // 結果整組陷進桌子裡 8 公分。
        float tableY = ProbeTableY(sb, handle);
        if (float.IsNaN(tableY)) return sb + "\n量不到桌面高度，位置沒改（避免又擺到桌子裡）";

        LiftStampAboveTable(sb, handle, tableY);
        FixHintPlacement(sb, handle, tableY);
        SetupDetach(sb, scene, handle, tableY);
        SwapMaterials(sb, scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (save) EditorSceneManager.SaveScene(scene);

        sb.AppendLine("已存檔（只動印尼場景）");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // ① 朝向與握點
    // ------------------------------------------------------------------

    static void FixOrientationAndGrab(StringBuilder sb, GameObject handle)
    {
        sb.AppendLine("── ① 印章朝向與握點 ──");

        var t = handle.transform;
        var before = t.localEulerAngles;

        // 冪等保護：轉過一次之後 localEuler.y 已經不是原本的 yaw 了，
        // 再跑一次會拿轉後的值當 yaw ⇒ 多轉 180°。已經朝下就別再動。
        float already = Vector3.Angle(t.rotation * Vector3.back, Vector3.down);
        if (already < 1f)
        {
            sb.AppendLine($"  已經是面朝下（夾角 {already:F2}°），旋轉不重複套用");
        }
        else
        {
            // 子物件的世界姿態先記下來：轉父物件會把它們一起帶歪，
            // 但月餅是扁圓餅、提示是麵團，都應該保持水平。
            var kids = new List<(Transform tr, Quaternion rot)>();
            foreach (Transform child in t) kids.Add((child, child.rotation));

            // 目標：網格 local −Z（蓋章面）朝世界下方 ⇒ local +Z 朝上。
            // 保留原本繞垂直軸的朝向，只把圓柱軸立起來。
            float yaw = before.y * Mathf.Deg2Rad;
            var yawDir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            t.rotation = Quaternion.LookRotation(Vector3.up, yawDir);

            foreach (var (child, rot) in kids) child.rotation = rot;
            sb.AppendLine($"  {kids.Count} 個子物件的世界旋轉已還原（不跟著印章翻）");
        }

        // 驗證：蓋章面法線應該接近正下方
        Vector3 faceDir = t.rotation * Vector3.back;
        sb.AppendLine($"  localEuler {V(before)} → {V(t.localEulerAngles)}");
        sb.AppendLine($"  蓋章面朝向 = {V(faceDir)}  （目標 (0,-1,0)，" +
                      $"與正下方夾角 {Vector3.Angle(faceDir, Vector3.down):F2}°）");

        // 握點：移到握柄的軸心上
        var grab = handle.GetComponent<XRGrabInteractable>();
        if (grab == null) { sb.AppendLine("  沒有 XRGrabInteractable，握點沒改"); return; }

        if (grab.attachTransform != null)
        {
            var at = grab.attachTransform;
            var oldPos = at.localPosition;
            at.localPosition = new Vector3(0f, 0f, k_HandleZ);
            at.localRotation = Quaternion.identity;
            sb.AppendLine($"  attach localPos {V(oldPos)} → {V(at.localPosition)}（握柄軸心）");
        }

        // 形狀不規則、又沒有唯一正確的握法 —— 用動態 attach，
        // 抓下去時保持當下的相對姿態，不會硬扭到某個寫死的角度。
        grab.useDynamicAttach = true;
        sb.AppendLine("  useDynamicAttach = True（抓起來維持當下角度，不再硬套模具的握法）");
    }

    // ------------------------------------------------------------------
    // 桌面高度：往下打射線實測，不要用猜的
    // ------------------------------------------------------------------

    static float ProbeTableY(StringBuilder sb, GameObject handle)
    {
        var from = handle.transform.position + Vector3.up * 0.6f;
        var hits = Physics.RaycastAll(from, Vector3.down, 3f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(handle.transform)) continue;
            sb.AppendLine($"── 桌面實測 ── 打到「{h.collider.gameObject.name}」 Y={h.point.y:F4}");
            return h.point.y;
        }

        sb.AppendLine("── 桌面實測 ── 往下 3 公尺沒打到東西");
        return float.NaN;
    }

    /// <summary>把印章抬到桌面之上，蓋章面剛好停在月餅上方一點點。</summary>
    static void LiftStampAboveTable(StringBuilder sb, GameObject handle, float tableY)
    {
        sb.AppendLine("── ①b 印章高度 ──");

        var r = handle.GetComponent<Renderer>();
        if (r == null) { sb.AppendLine("  沒有 Renderer，高度沒調"); return; }

        float faceY = r.bounds.min.y;                 // 轉正之後，最低點就是蓋章面
        float pieceTop = tableY + PieceThickness(handle);
        float targetFaceY = pieceTop + k_FaceClearance;
        float delta = targetFaceY - faceY;

        var t = handle.transform;
        var before = t.position;
        t.position = new Vector3(before.x, before.y + delta, before.z);

        sb.AppendLine($"  蓋章面 Y {faceY:F4} → {targetFaceY:F4}（桌面 {tableY:F4} + 月餅厚 " +
                      $"{PieceThickness(handle):F4} + 間隙 {k_FaceClearance:F4}）");
        sb.AppendLine($"  握把 Y {before.y:F4} → {t.position.y:F4}（位移 {delta:+0.0000;-0.0000}）");
        sb.AppendLine($"  調整後網格 Y 範圍 = {r.bounds.min.y:F4} ~ {r.bounds.max.y:F4}" +
                      $"（底部應該高於桌面 {tableY:F4}）");
    }

    /// <summary>成品月餅平放時的厚度（世界單位）。</summary>
    static float PieceThickness(GameObject handle)
    {
        var piece = FindChild(handle.transform, "月餅-完成體");
        var mf = piece != null ? piece.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) return 0.025f;

        var s = mf.sharedMesh.bounds.size;
        float thin = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
        return thin * Mathf.Abs(piece.lossyScale.y);
    }

    // ------------------------------------------------------------------
    // ② 提示／麵團／月餅放到蓋章面正下方、貼在桌面上
    // ------------------------------------------------------------------

    static void FixHintPlacement(StringBuilder sb, GameObject handle, float tableY)
    {
        sb.AppendLine("── ② 提示與月餅位置 ──");

        var t = handle.transform;
        float restY = tableY + PieceThickness(handle) * 0.5f;   // 平放在桌面上

        foreach (var name in new[] { "月餅-提示", "月餅-素體", "月餅-完成體" })
        {
            var child = FindChild(t, name);
            if (child == null) { sb.AppendLine($"  找不到「{name}」"); continue; }

            var before = child.position;
            // 對齊印章的軸心（X/Z），高度貼在桌面上
            child.position = new Vector3(t.position.x, restY, t.position.z);
            sb.AppendLine($"  {name,-12} worldY {before.y:F4} → {child.position.y:F4}" +
                          $"（桌面 {tableY:F4} 之上）");
        }
    }

    // ------------------------------------------------------------------
    // ③ 蓋完脫離、用手拿去烤盤
    // ------------------------------------------------------------------

    static void SetupDetach(StringBuilder sb, Scene scene, GameObject handle, float tableY)
    {
        sb.AppendLine("── ③ 蓋完脫離 ──");

        var station = handle.GetComponent<MooncakeMoldStation>();
        if (station == null) { sb.AppendLine("  找不到 MooncakeMoldStation"); return; }

        station.detachAfterStamp = true;
        station.detachedTag = "MooncakePiece";

        // 脫離點：印章正下方一點的位置，讓月餅掉到桌上
        var point = FindChild(handle.transform, k_DetachPointName);
        if (point == null)
        {
            var go = new GameObject(k_DetachPointName);
            go.transform.SetParent(handle.transform, false);
            point = go.transform;
        }
        // 脫離點：印章軸心正下方、貼在桌面上，月餅一放開就是平躺在桌上
        point.position = new Vector3(handle.transform.position.x,
                                     tableY + PieceThickness(handle) * 0.5f,
                                     handle.transform.position.z);
        point.rotation = Quaternion.identity;   // 不跟著印章翻，月餅要平放
        station.detachPoint = point;

        sb.AppendLine($"  detachAfterStamp=True detachedTag={station.detachedTag}");
        sb.AppendLine($"  脫離點 worldPos={V(point.position)}（桌面 {tableY:F4} 之上）");

        // 烤盤格子改成收「手拿的月餅」
        int slots = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var slot in root.GetComponentsInChildren<MooncakeTraySlot>(true))
            {
                slot.acceptTag = "MooncakePiece";
                slot.requireMooncakeOnMold = false;
                EditorUtility.SetDirty(slot);
                slots++;
            }
        }
        sb.AppendLine($"  {slots} 個烤盤格子改成 acceptTag=MooncakePiece、requireMooncakeOnMold=False");

        EditorUtility.SetDirty(station);
    }

    // ------------------------------------------------------------------
    // ④ 材質換成印尼版
    // ------------------------------------------------------------------

    static void SwapMaterials(StringBuilder sb, Scene scene)
    {
        sb.AppendLine("── ④ 材質 ──");

        foreach (var (chPath, indPath) in k_MaterialSwaps)
        {
            var ch = AssetDatabase.LoadAssetAtPath<Material>(chPath);
            var ind = AssetDatabase.LoadAssetAtPath<Material>(indPath);
            if (ch == null || ind == null)
            {
                sb.AppendLine($"  載不到 {chPath} 或 {indPath}，跳過");
                continue;
            }

            int swapped = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool hit = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] == ch) { mats[i] = ind; hit = true; }

                    if (!hit) continue;
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    swapped++;
                }
            }
            sb.AppendLine($"  {ch.name} → {ind.name}：{swapped} 個 Renderer");
        }
    }

    // ------------------------------------------------------------------

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

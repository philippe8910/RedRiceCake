using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;   // XRI 3.x 起 XRGrabInteractable 在這裡
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 在 Play Mode 裡照腳本「操作場景並拍照」，讓沒有頭盔的人也能實際看到畫面。
///
/// 之所以需要這個：MooncakeSceneDryRun 只驗流程有沒有跑完，驗不到「看起來對不對」
/// —— 印章朝向、教學面板位置、材質花紋，這些都得真的看到畫面才知道。
///
/// 腳本語法（每行一個指令，# 開頭是註解）：
///   look &lt;物件名&gt; &lt;距離&gt; &lt;水平角&gt; &lt;仰角&gt;   把鏡頭擺到該物件周圍看著它
///   at   &lt;x&gt; &lt;y&gt; &lt;z&gt; &lt;lookX&gt; &lt;lookY&gt; &lt;lookZ&gt;  直接指定鏡頭位置與注視點
///   fov  &lt;度數&gt;
///   do   &lt;方法名&gt;                              呼叫 flow 上的公開無參數方法
///   doon &lt;物件名&gt; &lt;方法名&gt;                     呼叫指定物件某個元件上的方法
///   wait &lt;秒&gt;
///   shot &lt;檔名&gt;                                拍一張存成 png
///
/// 兩個 batchmode 的雷（都繞開了）：
///   1. 一定要有繪圖裝置，**不能加 -nographics**，否則畫面全黑。
///   2. batchmode 下 WaitForEndOfFrame 不會恢復，URP 也不讓你直接
///      Camera.Render()。所以改成掛 targetTexture 讓它跟著正常迴圈畫，
///      等幾個 frame 之後再從 RenderTexture 讀回來。
/// </summary>
public class MooncakeAutoPlayDriver : MonoBehaviour
{
    [Header("腳本與輸出")]
    public string scriptPath;
    public string outputDir;

    [Header("拍照")]
    public int width = 1280;
    public int height = 720;
    [Tooltip("每次拍照前先讓畫面跑幾個 frame，等 RenderTexture 有內容")]
    public int warmupFrames = 4;

    [Header("結束後")]
    public bool exitEditorWhenDone = true;
    public float totalTimeout = 300f;

    private Camera _cam;
    private RenderTexture _rt;
    private MooncakeChineseFlow _flow;
    private readonly List<string> _log = new List<string>();
    private readonly List<string> _errors = new List<string>();
    private int _shotIndex;

    private IEnumerator Start()
    {
        yield return null;

        _flow = FindObjectOfType<MooncakeChineseFlow>();
        Note(_flow != null ? "找到 MooncakeChineseFlow" : "找不到 MooncakeChineseFlow（do 指令會失效）");

        SetupCamera();

        if (!System.IO.File.Exists(scriptPath))
        {
            Fail($"找不到腳本 {scriptPath}");
            Finish();
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + totalTimeout;

        foreach (var raw in System.IO.File.ReadAllLines(scriptPath))
        {
            if (Time.realtimeSinceStartup > deadline) { Fail("逾時"); break; }

            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            var t = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            switch (t[0].ToLowerInvariant())
            {
                case "look": DoLook(t); break;
                case "at": DoAt(t); break;
                case "fov": if (t.Length > 1) _cam.fieldOfView = F(t[1], 60f); break;
                case "front": DoFront(t); break;
                case "report": DoReport(t); break;
                case "reportall": DoReportAll(t); break;
                case "eye": DoEye(t); break;
                case "panelcam": DoBindPanelCamera(); break;
                case "do": DoInvokeFlow(t); break;
                case "doon": DoInvokeOn(t); break;
                case "doall": DoInvokeAll(t); break;
                case "scene":
                    if (t.Length > 1)
                    {
                        Note($"載入場景 {t[1]}");
                        UnityEngine.SceneManagement.SceneManager.LoadScene(t[1]);
                        yield return null;
                        yield return new WaitForSeconds(1.0f);
                        _flow = FindObjectOfType<MooncakeChineseFlow>();
                        SetupCamera();
                    }
                    break;
                case "wait":
                    float s = t.Length > 1 ? F(t[1], 0.5f) : 0.5f;
                    Note($"wait {s}s");
                    yield return new WaitForSeconds(s);
                    break;
                case "shot":
                    for (int i = 0; i < Mathf.Max(1, warmupFrames); i++) yield return null;
                    Capture(t.Length > 1 ? t[1] : $"shot{_shotIndex}");
                    break;
                default: Fail($"看不懂的指令：{line}"); break;
            }
        }

        Finish();
    }

    // ------------------------------------------------------------------

    private void SetupCamera()
    {
        var go = new GameObject("MooncakeAutoPlayCamera");
        go.transform.SetParent(transform, false);

        _cam = go.AddComponent<Camera>();
        _cam.clearFlags = CameraClearFlags.Skybox;
        _cam.fieldOfView = 60f;
        _cam.nearClipPlane = 0.01f;
        _cam.farClipPlane = 500f;
        _cam.cullingMask = ~0;

        _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 1
        };
        _rt.Create();

        // 掛 targetTexture 讓它跟著 URP 的正常迴圈畫，不用自己呼叫 Render
        _cam.targetTexture = _rt;

        Note($"拍照鏡頭已建立 {width}x{height}");
    }

    private void DoLook(string[] t)
    {
        if (t.Length < 2) { Fail("look 需要物件名"); return; }

        var target = Find(t[1]);
        if (target == null) { Fail($"look 找不到「{t[1]}」"); return; }

        float dist = t.Length > 2 ? F(t[2], 0.6f) : 0.6f;
        float yaw = t.Length > 3 ? F(t[3], 35f) : 35f;
        float pitch = t.Length > 4 ? F(t[4], 25f) : 25f;

        Vector3 center = CenterOf(target);
        Vector3 dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
        _cam.transform.position = center + dir * dist;
        _cam.transform.LookAt(center);

        Note($"look「{t[1]}」 中心={V(center)} 距離={dist} yaw={yaw} pitch={pitch}");
    }

    private void DoAt(string[] t)
    {
        if (t.Length < 7) { Fail("at 需要 6 個數字"); return; }
        var pos = new Vector3(F(t[1], 0), F(t[2], 0), F(t[3], 0));
        var look = new Vector3(F(t[4], 0), F(t[5], 0), F(t[6], 0));
        _cam.transform.position = pos;
        _cam.transform.LookAt(look);
        Note($"at {V(pos)} → {V(look)}");
    }

    /// <summary>
    /// 從物件的正面法線方向看它。UI 畫布是平面，用 look 繞角度很容易繞到側邊
    /// 只拍到一條邊（踩過一次），平面的東西一律用這個。
    /// </summary>
    private void DoFront(string[] t)
    {
        if (t.Length < 2) { Fail("front 需要物件名"); return; }

        var target = Find(t[1]);
        if (target == null) { Fail($"front 找不到「{t[1]}」"); return; }

        float dist = t.Length > 2 ? F(t[2], 1.0f) : 1.0f;
        Vector3 center = CenterOf(target);
        Vector3 n = target.transform.forward;

        // 實測：站在 +forward 側拍到的字是鏡像的，可讀的那一面在 -forward。
        // 想拍背面的話第三個參數給負值。
        _cam.transform.position = center - n * dist;
        _cam.transform.LookAt(center);

        Note($"front「{t[1]}」 中心={V(center)} 法線={V(n)} 距離={dist}（鏡頭在 -forward 側，字才是正的）");
    }

    /// <summary>
    /// 印出一個物件目前的實際狀態。拍不到東西的時候先用這個確認
    /// 「是沒顯示，還是顯示了但不在鏡頭裡」，別靠猜。
    /// </summary>
    private void DoReport(string[] t)
    {
        if (t.Length < 2) { Fail("report 需要物件名"); return; }

        var go = Find(t[1]);
        if (go == null) { Fail($"report 找不到「{t[1]}」"); return; }

        Note($"report「{t[1]}」 activeInHierarchy={go.activeInHierarchy} " +
             $"worldPos={V(go.transform.position)} scale={V(go.transform.lossyScale)}");

        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null) Note($"    CanvasGroup alpha={cg.alpha} interactable={cg.interactable}");

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null)
            Note($"    Canvas mode={canvas.renderMode} sortingOrder={canvas.sortingOrder} " +
                 $"enabled={canvas.enabled} worldCam={(canvas.worldCamera != null ? canvas.worldCamera.name : "無")}");

        foreach (var g in go.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            Note($"    Graphic「{g.gameObject.name}」 active={g.gameObject.activeInHierarchy} " +
                 $"enabled={g.enabled} color={g.color} rq={(g.materialForRendering != null ? g.materialForRendering.renderQueue : -1)}");

        foreach (var tm in go.GetComponentsInChildren<TMPro.TMP_Text>(true))
            Note($"    TMP「{tm.gameObject.name}」 active={tm.gameObject.activeInHierarchy} " +
                 $"文字=「{(tm.text != null && tm.text.Length > 20 ? tm.text.Substring(0, 20) + "…" : tm.text)}」");

        var r = go.GetComponent<Renderer>();
        if (r != null) Note($"    Renderer enabled={r.enabled} bounds中心={V(r.bounds.center)}");

        DumpInteraction(go);
    }

    /// <summary>
    /// 場上會有多個同名物件（例如三格烤盤各一顆「月餅-完成體」），
    /// report 只會抓到第一個，要驗全部得用這個。
    /// </summary>
    private void DoReportAll(string[] t)
    {
        if (t.Length < 2) { Fail("reportall 需要物件名"); return; }

        int n = 0;
        foreach (var tr in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (tr.name != t[1]) continue;
            if (tr.hideFlags != HideFlags.None) continue;
            if (!tr.gameObject.scene.IsValid()) continue;

            var go = tr.gameObject;
            Note($"reportall「{t[1]}」#{n} active={go.activeInHierarchy} " +
                 $"parent={(tr.parent != null ? tr.parent.name : "(無)")} worldPos={V(tr.position)}");
            DumpInteraction(go);
            n++;
        }

        if (n == 0) Fail($"reportall 找不到任何「{t[1]}」");
        else Note($"reportall「{t[1]}」共 {n} 個");
    }

    /// <summary>抓取相關的元件狀態 —— 驗「拿不拿得起來」看這些。</summary>
    private void DumpInteraction(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        var rb = go.GetComponent<Rigidbody>();
        var grab = go.GetComponent<XRGrabInteractable>();

        Note($"    碰撞體={(col != null ? $"{col.GetType().Name} enabled={col.enabled} trigger={col.isTrigger} 尺寸={V(col.bounds.size)}" : "無")}");
        Note($"    剛體={(rb != null ? $"isKinematic={rb.isKinematic} useGravity={rb.useGravity}" : "無")}");

        if (grab == null) { Note("    XRGrabInteractable=無 → 拿不起來"); return; }

        Note($"    XRGrabInteractable enabled={grab.enabled} " +
             $"colliders={grab.colliders.Count} dynamicAttach={grab.useDynamicAttach} " +
             $"throwOnDetach={grab.throwOnDetach}");

        // colliders 是空的話 XRI 根本偵測不到它，等於抓不到
        if (grab.colliders.Count == 0)
            Fail($"「{go.name}」的 XRGrabInteractable 沒有登記任何 collider，實際上抓不起來");
    }

    /// <summary>
    /// 把拍照鏡頭挪到玩家頭的位置（Camera.main），可再指定要看向哪個物件。
    /// 驗教學面板這種「跟著視線走」的東西一定要用這個，
    /// 不然面板錨在玩家攝影機上，從別的角度根本拍不到。
    /// </summary>
    private void DoEye(string[] t)
    {
        var main = Camera.main;
        if (main == null) { Fail("找不到 Camera.main"); return; }

        _cam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
        _cam.fieldOfView = main.fieldOfView;

        if (t.Length > 1)
        {
            var target = Find(t[1]);
            if (target != null) _cam.transform.LookAt(CenterOf(target));
            else Fail($"eye 找不到「{t[1]}」");
        }

        Note($"eye 玩家視角 {V(_cam.transform.position)}" +
             (t.Length > 1 ? $" 看向「{t[1]}」" : ""));
    }

    /// <summary>讓教學面板改錨定在拍照鏡頭上，這樣拍到的就是玩家真正看到的位置。</summary>
    private void DoBindPanelCamera()
    {
        int n = 0;
        foreach (var p in FindObjectsOfType<MooncakeTutorialPanel>(true))
        {
            p.playerCamera = _cam.transform;
            n++;
        }
        Note(n > 0 ? $"教學面板已改錨定拍照鏡頭（{n} 個）" : "找不到 MooncakeTutorialPanel");
        if (n == 0) Fail("panelcam 沒有作用");
    }

    private void DoInvokeFlow(string[] t)
    {
        if (t.Length < 2) { Fail("do 需要方法名"); return; }
        if (_flow == null) { Fail("沒有 flow，do 失效"); return; }
        Invoke(_flow, t[1], t.Length > 2 ? t[2] : null);
    }

    private void DoInvokeOn(string[] t)
    {
        if (t.Length < 3) { Fail("doon 需要物件名與方法名"); return; }

        var go = Find(t[1]);
        if (go == null) { Fail($"doon 找不到「{t[1]}」"); return; }

        string arg = t.Length > 3 ? t[3] : null;

        foreach (var c in go.GetComponents<MonoBehaviour>())
        {
            if (c == null) continue;
            if (TryInvoke(c, t[2], arg, $"doon「{t[1]}」.{c.GetType().Name}")) return;
        }
        Fail($"「{t[1]}」上找不到可呼叫的 {t[2]}");
    }

    private void DoInvokeAll(string[] t)
    {
        if (t.Length < 3) { Fail("doall 需要元件型別名與方法名"); return; }

        string arg = t.Length > 3 ? t[3] : null;
        int hit = 0;

        foreach (var c in FindObjectsOfType<MonoBehaviour>(true))
        {
            if (c == null || c.GetType().Name != t[1]) continue;
            if (TryInvoke(c, t[2], arg, $"doall {t[1]}#{hit}")) hit++;
        }

        if (hit == 0) Fail($"沒有任何 {t[1]} 上有可呼叫的 {t[2]}");
        else Note($"doall {t[1]}.{t[2]} 共 {hit} 個");
    }

    private void Invoke(object obj, string method, string arg)
    {
        if (!TryInvoke(obj, method, arg, "do"))
            Fail($"{obj.GetType().Name} 上沒有可呼叫的 {method}");
    }

    /// <summary>支援無參數，以及單一 bool／int／float 參數。</summary>
    private bool TryInvoke(object obj, string method, string arg, string label)
    {
        var type = obj.GetType();

        if (arg == null)
        {
            var m0 = type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance,
                                    null, System.Type.EmptyTypes, null);
            if (m0 == null) return false;
            m0.Invoke(obj, null);
            Note($"{label}.{method}()");
            return true;
        }

        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.Name != method) continue;
            var ps = m.GetParameters();
            if (ps.Length != 1) continue;

            object v = null;
            var pt = ps[0].ParameterType;
            if (pt == typeof(bool) && bool.TryParse(arg, out var b)) v = b;
            else if (pt == typeof(int) && int.TryParse(arg, out var i)) v = i;
            else if (pt == typeof(float) && float.TryParse(arg, out var f)) v = f;
            else if (pt == typeof(string)) v = arg;
            if (v == null) continue;

            m.Invoke(obj, new[] { v });
            Note($"{label}.{method}({arg})");
            return true;
        }
        return false;
    }

    private void Capture(string name)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        var prev = RenderTexture.active;
        RenderTexture.active = _rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        // 全黑通常代表跑了 -nographics，或鏡頭指到沒東西的地方
        var px = tex.GetPixels32();
        long sum = 0;
        for (int i = 0; i < px.Length; i += 97) sum += px[i].r + px[i].g + px[i].b;
        int sampled = (px.Length + 96) / 97;
        float avg = sampled > 0 ? sum / (float)(sampled * 3) : 0f;

        System.IO.Directory.CreateDirectory(outputDir);
        string file = System.IO.Path.Combine(outputDir, $"{_shotIndex:D2}-{name}.png");
        System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
        Destroy(tex);

        Note($"shot #{_shotIndex} → {System.IO.Path.GetFileName(file)}  平均亮度={avg:F1}" +
             (avg < 2f ? "  ← 幾乎全黑，檢查是不是加了 -nographics" : ""));
        _shotIndex++;
    }

    // ------------------------------------------------------------------

    private static Vector3 CenterOf(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return go.transform.position;

        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b.center;
    }

    private static GameObject Find(string name)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name != name) continue;
            if (t.hideFlags != HideFlags.None) continue;
            if (t.gameObject.scene.IsValid()) return t.gameObject;
        }
        return null;
    }

    private static float F(string s, float fallback)
        => float.TryParse(s, out var v) ? v : fallback;

    private static string V(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";

    private void Note(string s) { _log.Add("  " + s); Debug.Log("[月餅][自動操作] " + s); }
    private void Fail(string s) { _errors.Add("  X " + s); Debug.LogError("[月餅][自動操作] " + s); }

    private void Finish()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[月餅][自動操作] 結果");
        foreach (var l in _log) sb.AppendLine(l);
        foreach (var e in _errors) sb.AppendLine(e);
        sb.AppendLine(_errors.Count == 0
            ? $"[月餅][自動操作] OK，拍了 {_shotIndex} 張"
            : $"[月餅][自動操作] 有 {_errors.Count} 項問題，拍了 {_shotIndex} 張");

        Debug.Log(sb.ToString());

        if (!string.IsNullOrEmpty(outputDir))
        {
            System.IO.Directory.CreateDirectory(outputDir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(outputDir, "autoplay.txt"),
                                        sb.ToString(), new UTF8Encoding(true));
        }

        if (_cam != null) _cam.targetTexture = null;
        if (_rt != null) _rt.Release();

#if UNITY_EDITOR
        if (exitEditorWhenDone) EditorApplication.Exit(_errors.Count == 0 ? 0 : 1);
#endif
    }
}

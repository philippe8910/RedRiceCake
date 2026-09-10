using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 把「第一顆月餅教學」整組搭進月餅場景：
/// 文字板（world-space canvas + 打字效果）、箭頭範本、以及自動接線的 MooncakeTutorial。
///
/// 兩個場景都能建。站點是執行時自動找的，所以兩邊唯一的差別是
/// <see cref="MooncakeTutorial.Variant"/>——它決定成形那一步要說
/// 「塞進模具」還是「蓋上印章」。
///
/// 重複執行會整組重建，只砍自己建的根物件「月餅-教學」，不動場景其他東西。
/// </summary>
public static class MooncakeTutorialBuilder
{
    const string k_ChineseScene = "Assets/Scenes/ChineseMooncakeDemo.unity";
    const string k_IndonesianScene = "Assets/Scenes/IndonesianMooncakeDemo.unity";
    const string k_RootName = "月餅-教學";
    const string k_MatFolder = "Assets/Materials/MooncakeDemo";
    const string k_MeshFolder = "Assets/Meshes";
    const string k_MatPath = k_MatFolder + "/教學箭頭.mat";
    const string k_MeshPath = k_MeshFolder + "/MooncakeTutorialArrow.mesh";
    const string k_FontFolder = "Assets/Fonts";

    // 面板尺寸（canvas 單位）與換算成公尺的縮放
    const float k_PanelWidth = 760f;
    const float k_PanelHeight = 210f;
    const float k_PanelScale = 0.0009f;

    [MenuItem("Tools/月餅 Demo/建立(重建) 第一顆教學 - 中式")]
    public static void BuildChineseMenu()
    {
        Build(k_ChineseScene, MooncakeTutorial.Variant.中式模具, true);
    }

    [MenuItem("Tools/月餅 Demo/建立(重建) 第一顆教學 - 印尼")]
    public static void BuildIndonesianMenu()
    {
        Build(k_IndonesianScene, MooncakeTutorial.Variant.印尼印章, true);
    }

    [MenuItem("Tools/月餅 Demo/建立(重建) 第一顆教學 - 兩個場景")]
    public static void BuildBothMenu()
    {
        BuildBoth(true);
    }

    [MenuItem("Tools/月餅 Demo/移除 第一顆教學（兩個場景）")]
    public static void RemoveMenu()
    {
        int removed = 0;
        foreach (var path in new[] { k_ChineseScene, k_IndonesianScene })
        {
            var scene = OpenTargetScene(path);
            removed += RemoveExisting(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log($"[月餅教學] 已移除 {removed} 個舊的教學根物件");
    }

    /// <summary>給 -executeMethod 用的入口：兩個場景一起建。</summary>
    public static void BuildFromCommandLine()
    {
        BuildBoth(true);
        EditorApplication.Exit(0);
    }

    /// <summary>給 -executeMethod 用：只重建印尼那一個場景，中式完全不碰。</summary>
    public static void BuildIndonesianFromCommandLine()
    {
        Build(k_IndonesianScene, MooncakeTutorial.Variant.印尼印章, true);
        EditorApplication.Exit(0);
    }

    public static void BuildBoth(bool saveScene)
    {
        Build(k_ChineseScene, MooncakeTutorial.Variant.中式模具, saveScene);
        Build(k_IndonesianScene, MooncakeTutorial.Variant.印尼印章, saveScene);
    }

    // ------------------------------------------------------------------

    public static void Build(string scenePath, MooncakeTutorial.Variant variant, bool saveScene)
    {
        var scene = OpenTargetScene(scenePath);

        var flow = Object.FindObjectOfType<MooncakeChineseFlow>();
        if (flow == null)
        {
            Debug.LogError($"[月餅教學] 「{scenePath}」裡找不到 MooncakeChineseFlow，教學建不起來");
            return;
        }

        RemoveExisting(scene);

        var root = new GameObject(k_RootName);
        root.transform.position = flow.transform.position + Vector3.up * 1.4f;

        var tutorial = root.AddComponent<MooncakeTutorial>();
        tutorial.variant = variant;
        tutorial.flow = flow;
        tutorial.texts = MooncakeTutorial.BuildDefaultTexts(variant);

        tutorial.panel = BuildPanel(root.transform);
        tutorial.arrowTemplate = BuildArrowTemplate(root.transform);

        // Debug：鍵盤 1～5 切語言
        root.AddComponent<MooncakeLanguageDebugKeys>();

        EnsureFontFallback(scene);
        MooncakeTutorialLocalization.EnsureTerms();

        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene) EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = root;

        Debug.Log($"[月餅教學] 「{scene.name}」建置完成（{variant}）：文字板 + 箭頭範本已放進「{k_RootName}」。\n" +
                  "站點（麵團球／壓扁站／模具／烤盤／烤箱／餡料碗／蛋液刷）都是執行時自動找，不用手動連線。\n" +
                  "註：中文字要等執行時的動態字型 fallback 才會出現，Editor 裡看到方框是正常的。");
    }

    static Scene OpenTargetScene(string scenePath)
    {
        var active = SceneManager.GetActiveScene();
        if (active.path == scenePath) return active;

        return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }

    static int RemoveExisting(Scene scene)
    {
        int n = 0;
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name != k_RootName) continue;
            Object.DestroyImmediate(go);
            n++;
        }
        return n;
    }

    // ------------------------------------------------------------------
    // 文字板
    // ------------------------------------------------------------------

    static MooncakeTutorialPanel BuildPanel(Transform parent)
    {
        var go = new GameObject("教學面板", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(k_PanelWidth, k_PanelHeight);
        rect.localScale = Vector3.one * k_PanelScale;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3f;
        scaler.referencePixelsPerUnit = 100f;

        go.AddComponent<CanvasGroup>();

        // 底板
        var bg = NewImage("底板", rect);
        Stretch(bg.rectTransform, 0f);
        bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.05f, 0.07f, 0.88f);

        // 左邊的橘色側條，跟箭頭同一個顏色語彙
        var accent = NewImage("側條", rect);
        var ar = accent.rectTransform;
        ar.anchorMin = new Vector2(0f, 0f);
        ar.anchorMax = new Vector2(0f, 1f);
        ar.pivot = new Vector2(0f, 0.5f);
        ar.offsetMin = new Vector2(10f, 12f);
        ar.offsetMax = new Vector2(0f, -12f);
        ar.sizeDelta = new Vector2(9f, ar.sizeDelta.y);
        accent.color = new Color(1f, 0.72f, 0.2f, 1f);

        // 標題
        var title = NewText("標題", rect, 44f, new Color(1f, 0.82f, 0.42f, 1f));
        var tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(34f, 0f);
        tr.offsetMax = new Vector2(-24f, -16f);
        tr.sizeDelta = new Vector2(tr.sizeDelta.x, 56f);
        title.alignment = TextAlignmentOptions.TopLeft;
        title.fontStyle = FontStyles.Bold;
        title.text = "① 拿一團麵團";

        // 內文
        var body = NewText("內文", rect, 33f, new Color(0.96f, 0.96f, 0.96f, 1f));
        var br = body.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(34f, 18f);
        br.offsetMax = new Vector2(-24f, -76f);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.text = "把手伸到麵團球上，按下扳機鍵抓一團麵團出來。";

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;       // 打字聲直接在耳邊，不做空間化
        audio.volume = 1f;

        // 世界空間 Canvas 一樣吃深度測試，桌子／烤箱擋在前面時字會被切掉。
        // 這支把 UI 與 TMP 材質的 ZTest 改成 Always、renderQueue 拉到 Overlay，
        // 面板就不會再被場景物件穿過去。
        go.AddComponent<MooncakeAlwaysOnTop>();

        var panel = go.AddComponent<MooncakeTutorialPanel>();
        panel.group = go.GetComponent<CanvasGroup>();
        panel.titleText = title;
        panel.bodyText = body;
        panel.scaleRoot = go.transform;
        panel.audioSource = audio;

        return panel;
    }

    static Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.color = color;
        t.raycastTarget = false;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    // ------------------------------------------------------------------
    // 箭頭
    // ------------------------------------------------------------------

    static MooncakeTutorialArrow BuildArrowTemplate(Transform parent)
    {
        var go = new GameObject("教學箭頭範本");
        go.transform.SetParent(parent, false);

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = LoadOrCreateMesh();

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = LoadOrCreateMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var arrow = go.AddComponent<MooncakeTutorialArrow>();
        go.SetActive(false);   // 只當範本，執行時複製

        return arrow;
    }

    static Mesh LoadOrCreateMesh()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(k_MeshPath);
        if (existing != null) return existing;

        EnsureFolder(k_MeshFolder);

        var mesh = MooncakeTutorialArrow.BuildMesh(14, 0.46f, 0.23f, 0.075f);
        mesh.name = "MooncakeTutorialArrow";

        AssetDatabase.CreateAsset(mesh, k_MeshPath);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    static Material LoadOrCreateMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(k_MatPath);
        if (existing != null) return existing;

        EnsureFolder(k_MatFolder);

        var shader = Shader.Find("Mooncake/Tutorial Overlay");
        if (shader == null)
        {
            Debug.LogWarning("[月餅教學] 找不到 Mooncake/Tutorial Overlay，改用 URP Unlit（會被物件遮住）");
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        var mat = new Material(shader) { name = "教學箭頭" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.72f, 0.2f, 1f));

        AssetDatabase.CreateAsset(mat, k_MatPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    // ------------------------------------------------------------------
    // 中文字型
    // ------------------------------------------------------------------

    /// <summary>
    /// TMP 預設字型沒有 CJK，這個場景又不像主選單那樣掛了動態 fallback，
    /// 直接單獨開這個場景測試會整排方框，所以缺的話補一份進來。
    ///
    /// 字型清單直接掃 Assets/Fonts：之後要補日文／韓文字型，丟進那個資料夾再跑一次就好。
    /// （NotoSansTC 含繁中、假名、日文漢字，但**沒有諺文**，韓文需要另外補 Noto Sans KR）
    /// </summary>
    static void EnsureFontFallback(Scene scene)
    {
        var fallback = Object.FindObjectOfType<MooncakeTMPFontFallback>(true);
        if (fallback == null)
        {
            var go = new GameObject("TMP 中文字型");
            fallback = go.AddComponent<MooncakeTMPFontFallback>();
            fallback.dontDestroyOnLoad = false;   // 這個場景自己用，不跨場景

            Debug.Log("[月餅教學] 場景缺 CJK 字型 fallback，已補上「TMP 中文字型」");
        }

        var added = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { k_FontFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);

            if (font == null || fallback.fallbackFonts.Contains(font)) continue;

            fallback.fallbackFonts.Add(font);
            added.Add(font.name);
        }

        if (added.Count > 0)
            Debug.Log("[月餅教學] fallback 字型加入：" + string.Join("、", added));

        if (fallback.fallbackFonts.Count == 0)
            Debug.LogWarning($"[月餅教學] {k_FontFolder} 裡沒有字型，CJK 會顯示成方框");
    }
}

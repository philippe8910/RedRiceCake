using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Sirenix.OdinInspector;

/// <summary>
/// 中日韓字型 fallback。
///
/// TMP 的字型資產一般要用 Editor 的 Font Asset Creator 先把字烘成圖集，
/// 但 CJK 有上萬字、而且四種語言要好幾套。這裡改用 TMP 的 Dynamic 模式：
/// 執行時用 TMP_FontAsset.CreateFontAsset() 從 .ttf 直接建字型資產，
/// 再掛進 TMP_Settings 的全域 fallback，缺字就自動從這些字型補上、
/// 用到才進圖集，不需要事先烘焙。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-500)]     // 要比任何 TMP 文字先跑
public class MooncakeTMPFontFallback : MonoBehaviour
{
    [Header("要當 fallback 的字型（依序）")]
    [Tooltip("每個 .ttf/.otf 都會在執行時轉成 Dynamic 的 TMP 字型資產。\n" +
             "涵蓋範圍不足的語言請自行補上對應字型（例如 Noto Sans JP / KR）")]
    public List<Font> fallbackFonts = new List<Font>();

    [Header("圖集設定")]
    [Tooltip("取樣點數，越大越清晰但圖集吃越兇")]
    public int samplingPointSize = 60;
    public int atlasPadding = 6;
    [Tooltip("動態圖集尺寸，CJK 建議 1024 以上")]
    public int atlasWidth = 1024;
    public int atlasHeight = 1024;

    [Header("行為")]
    [Tooltip("一般不需要開。fallback 是掛在 TMP_Settings 這個全域資產上，" +
             "本來就跨場景有效；而且這個元件常跟別的元件共用同一個根物件，" +
             "開了會把整個根物件（例如選單 UI）一起帶到下個場景。")]
    public bool dontDestroyOnLoad;

    private static bool _applied;
    private static readonly List<TMP_FontAsset> _created = new List<TMP_FontAsset>();

    private void Awake()
    {
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        if (_applied)
        {
            // 換場景後 TMP_Settings 的清單會保留，不用重建
            return;
        }
        Apply();
    }

    [Button("建立並套用 fallback", ButtonSizes.Large), GUIColor(0.6f, 0.9f, 0.6f)]
    public void Apply()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[字型] 動態字型要在 Play Mode 下才能建立", this);
            return;
        }

        if (TMP_Settings.instance == null)
        {
            Debug.LogError("[字型] 找不到 TMP Settings，請先執行 Window → TextMeshPro → Import TMP Essential Resources", this);
            return;
        }

        var list = TMP_Settings.fallbackFontAssets;
        if (list == null)
        {
            Debug.LogError("[字型] TMP_Settings.fallbackFontAssets 是 null", this);
            return;
        }

        int added = 0, already = 0;
        foreach (var font in fallbackFonts)
        {
            if (font == null) continue;

            // 已經有人（多半是 Tools → 建立 CJK 字型資產 做出來的資產）
            // 把同一支 .ttf 掛進全域 fallback 了，就不用再現做一份
            if (AlreadyCovered(list, font))
            {
                already++;
                continue;
            }

            var asset = TMP_FontAsset.CreateFontAsset(
                font, samplingPointSize, atlasPadding,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                atlasWidth, atlasHeight,
                AtlasPopulationMode.Dynamic, true);

            if (asset == null)
            {
                Debug.LogWarning($"[字型] 「{font.name}」建立動態字型失敗", this);
                continue;
            }

            asset.name = font.name + " (Dynamic)";
            if (!list.Contains(asset))
            {
                list.Add(asset);
                _created.Add(asset);
                added++;
            }
        }

        _applied = true;
        Debug.Log($"[字型] 已加入 {added} 個 fallback 字型（{already} 個已經有資產、略過），" +
                  $"目前 TMP 全域 fallback 共 {list.Count} 個", this);
    }

    private static bool AlreadyCovered(List<TMP_FontAsset> list, Font font)
    {
        foreach (var a in list)
            if (a != null && a.sourceFontFile == font)
                return true;
        return false;
    }

    [Button("Debug：列出目前的 fallback", ButtonSizes.Medium), GUIColor(0.6f, 0.8f, 1f)]
    public void DebugListFallbacks()
    {
        var list = TMP_Settings.fallbackFontAssets;
        if (list == null || list.Count == 0)
        {
            Debug.Log("[字型] 目前沒有任何全域 fallback", this);
            return;
        }

        var names = new List<string>();
        foreach (var f in list) names.Add(f != null ? f.name : "(null)");
        Debug.Log("[字型] 全域 fallback: " + string.Join(", ", names), this);
    }
}

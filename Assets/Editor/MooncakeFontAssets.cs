using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 把 Assets/Fonts 底下的 Noto CJK 字型做成 TMP 字型資產（Dynamic 模式），
/// 並登記到 TMP Settings 的全域 fallback。
///
/// 原本是靠 MooncakeTMPFontFallback 在執行時現做，缺點是：
///   1. Edit Mode 看不到中日韓，Scene/Game 視窗全是 □
///   2. 每次進 Play 都要重建三套字型，開場多花時間
/// 改成資產之後兩個問題都沒了，執行時那支腳本會自動略過已經有的字型。
///
/// Dynamic 模式不需要事先烘字，用到哪個字才進圖集，所以資產本身很小。
/// </summary>
public static class MooncakeFontAssets
{
    private const string FontDir = "Assets/Fonts";
    private const string OutDir = "Assets/Fonts/TMP";

    private const int SamplingPointSize = 60;
    private const int AtlasPadding = 6;
    private const int AtlasWidth = 1024;
    private const int AtlasHeight = 1024;

    private static readonly string[] SourceFonts =
    {
        "NotoSansTC-VF.ttf",
        "NotoSansJP-VF.ttf",
        "NotoSansKR-VF.ttf",
    };

    [MenuItem("Tools/月餅 Demo/建立 CJK 字型資產並登記 fallback")]
    public static void BuildAndRegister()
    {
        if (TMP_Settings.instance == null)
        {
            Debug.LogError("[字型] 找不到 TMP Settings");
            return;
        }

        Directory.CreateDirectory(OutDir);

        var assets = new List<TMP_FontAsset>();
        foreach (var file in SourceFonts)
        {
            var asset = GetOrCreate(file);
            if (asset != null) assets.Add(asset);
        }

        if (assets.Count == 0)
        {
            Debug.LogError("[字型] 一個字型資產都沒建出來");
            return;
        }

        Register(assets);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[字型] 完成：{assets.Count} 個字型資產已登記到 TMP Settings 全域 fallback");
    }

    private static TMP_FontAsset GetOrCreate(string fileName)
    {
        string srcPath = $"{FontDir}/{fileName}";
        string outPath = $"{OutDir}/{Path.GetFileNameWithoutExtension(fileName)} SDF.asset";

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>(srcPath);
        if (font == null)
        {
            Debug.LogWarning($"[字型] 找不到 {srcPath}");
            return null;
        }

        var asset = TMP_FontAsset.CreateFontAsset(
            font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
            AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic, true);

        if (asset == null)
        {
            Debug.LogWarning($"[字型] {fileName} 建立失敗");
            return null;
        }

        asset.name = Path.GetFileNameWithoutExtension(outPath);
        AssetDatabase.CreateAsset(asset, outPath);

        // 圖集與材質要當成子資產一起存，否則重開專案會變成遺失參照
        if (asset.atlasTextures != null)
        {
            for (int i = 0; i < asset.atlasTextures.Length; i++)
            {
                var tex = asset.atlasTextures[i];
                if (tex == null) continue;
                tex.name = asset.name + " Atlas" + (i == 0 ? "" : " " + i);
                AssetDatabase.AddObjectToAsset(tex, asset);
            }
        }

        if (asset.material != null)
        {
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }

        EditorUtility.SetDirty(asset);
        Debug.Log($"[字型] 已建立 {outPath}");
        return asset;
    }

    /// <summary>把字型接到 TMP Settings 的 m_fallbackFontAssets，已存在就不重複加。</summary>
    private static void Register(List<TMP_FontAsset> assets)
    {
        var so = new SerializedObject(TMP_Settings.instance);
        var list = so.FindProperty("m_fallbackFontAssets");
        if (list == null)
        {
            Debug.LogError("[字型] TMP Settings 沒有 m_fallbackFontAssets 欄位");
            return;
        }

        var have = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
        {
            var v = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (v != null) have.Add(v);
        }

        int added = 0;
        foreach (var a in assets)
        {
            if (have.Contains(a)) continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = a;
            added++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(TMP_Settings.instance);
        Debug.Log($"[字型] TMP Settings fallback 新增 {added} 個，目前共 {list.arraySize} 個");
    }
}

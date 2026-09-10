using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// 專案層級的設定：Android 包名與 app 圖示。
///
/// 圖示原本是空的（m_BuildTargetIcons: []），所以裝到頭盔上顯示的是
/// Unity 預設圖示。這裡把 Tools/GenerateAppIcon.py 產生的三張掛上去。
/// </summary>
public static class MooncakeProjectSetup
{
    public const string k_PackageName = "com.vcm.redricecake";

    /// <summary>頭盔上顯示的 app 名稱（Android 的 application-label 就取這個）。</summary>
    public const string k_ProductName = "Mooncake";

    const string k_IconFolder = "Assets/Textures/AppIcon/";
    const string k_IconLegacy = k_IconFolder + "AppIcon.png";
    const string k_IconBackground = k_IconFolder + "AppIcon_Background.png";
    const string k_IconForeground = k_IconFolder + "AppIcon_Foreground.png";

    [MenuItem("Tools/月餅 Demo/套用 包名、App 名稱與圖示")]
    public static void ApplyMenu()
    {
        Apply();
        AssetDatabase.SaveAssets();
    }

    /// <summary>給 -executeMethod 用的入口（選單那支不會結束 batchmode）。</summary>
    public static void ApplyFromCommandLine()
    {
        Apply();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    public static bool Apply()
    {
        bool ok = ApplyPackageName();
        ok &= ApplyProductName();
        ok &= ApplyIcons();
        return ok;
    }

    static bool ApplyProductName()
    {
        string before = PlayerSettings.productName;
        if (before == k_ProductName)
        {
            Debug.Log($"[專案設定] app 名稱已經是 {k_ProductName}");
            return true;
        }

        PlayerSettings.productName = k_ProductName;
        Debug.Log($"[專案設定] app 名稱 {before} → {k_ProductName}（頭盔上顯示的就是這個）");
        return true;
    }

    // ------------------------------------------------------------------

    static bool ApplyPackageName()
    {
        string before = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        if (before == k_PackageName)
        {
            Debug.Log($"[專案設定] 包名已經是 {k_PackageName}");
            return true;
        }

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, k_PackageName);
        Debug.Log($"[專案設定] Android 包名 {before} → {k_PackageName}\n" +
                  "註：包名換了等於換一個 app，頭盔上舊版不會被覆蓋，會變成兩個並存。");
        return true;
    }

    // ------------------------------------------------------------------

    static bool ApplyIcons()
    {
        var legacy = LoadIcon(k_IconLegacy);
        var background = LoadIcon(k_IconBackground);
        var foreground = LoadIcon(k_IconForeground);

        if (legacy == null || background == null || foreground == null)
        {
            Debug.LogError("[專案設定] 圖示貼圖載不到，" +
                           "先跑 python Tools/GenerateAppIcon.py 產生 Assets/Textures/AppIcon/");
            return false;
        }

        // 不寫死 AndroidPlatformIconKind：那個型別在平台擴充組件裡，
        // 直接引用會讓沒裝 Android module 的機器編不起來。
        // 改成問 Unity 這個平台支援哪些 kind，再依「這個 kind 吃幾層」決定貼哪張。
        // 註：2022.3 的這一支還是收 BuildTargetGroup，隔壁 Get/SetPlatformIcons
        // 才是 NamedBuildTarget，兩邊不一致，別看到 NamedBuildTarget 就一路套下去。
        var kinds = PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.Android);
        if (kinds == null || kinds.Length == 0)
        {
            Debug.LogError("[專案設定] 這個 Unity 沒回報 Android 的圖示種類，Android module 可能沒裝");
            return false;
        }

        var summary = new List<string>();

        foreach (var kind in kinds)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (icons == null || icons.Length == 0) continue;

            foreach (var icon in icons)
            {
                if (icon.maxLayerCount >= 2)
                {
                    // adaptive：第 0 層背景、第 1 層前景
                    icon.SetTexture(background, 0);
                    icon.SetTexture(foreground, 1);
                }
                else
                {
                    icon.SetTexture(legacy, 0);
                }
            }

            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            summary.Add($"{kind}×{icons.Length}");
        }

        if (summary.Count == 0)
        {
            Debug.LogError("[專案設定] 沒有任何圖示欄位被設定");
            return false;
        }

        Debug.Log("[專案設定] Android app 圖示已掛上：" + string.Join("、", summary));
        return true;
    }

    /// <summary>
    /// 圖示貼圖要能被建置流程讀出像素，所以強制關壓縮並開 Read/Write；
    /// 沒開的話某些壓縮格式會讓產出的圖示糊掉或整片黑。
    /// </summary>
    static Texture2D LoadIcon(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool dirty = false;

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                dirty = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}

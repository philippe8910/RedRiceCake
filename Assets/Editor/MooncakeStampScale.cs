using UnityEditor;
using UnityEngine;

/// <summary>
/// 修正印尼場景印章太小的問題。
///
/// 印尼場景是從中式場景複製出來的，transform 照抄了中式模具的 1.1856，
/// 但 Stamp.fbx 建模時的單位比 Mold.fbx 小非常多：
///   中式模具網格  0.236 x 0.034 x 0.181 公尺
///   印尼印章網格  0.012 x 0.011 x 0.009 公尺   ← 只有 1.2 公分
/// 對照要蓋的月餅成品是 0.142 公尺直徑，等於拿一顆骰子去蓋一個盤子。
///
/// 修在匯入縮放而不是場景 transform，是因為印章底下掛著月餅工件
/// （月餅-素體／完成體），放大 transform 會把月餅一起放大；
/// 改 globalScale 只動網格頂點，整個階層的 transform 完全不受影響。
/// Stamp.fbx 也只有印尼場景在用，不會波及別處。
/// </summary>
public static class MooncakeStampScale
{
    const string k_StampPath = "Assets/Model/MooncakeModel/Stamp.fbx";

    /// <summary>
    /// 0.0094（模型本身最長邊）x 12.5 x 1.1856（場景 transform）≈ 0.139 公尺，
    /// 跟月餅成品的 0.142 公尺直徑相稱 —— 印章剛好蓋滿一顆月餅。
    /// </summary>
    public const float k_ScaleFactor = 12.5f;

    [MenuItem("Tools/月餅 Demo/修正 印章匯入縮放")]
    public static void ApplyMenu()
    {
        Apply();
    }

    public static bool Apply()
    {
        var importer = AssetImporter.GetAtPath(k_StampPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[印章尺寸] 找不到 {k_StampPath}，或它不是模型資產");
            return false;
        }

        if (Mathf.Approximately(importer.globalScale, k_ScaleFactor))
        {
            Debug.Log($"[印章尺寸] 已經是 {k_ScaleFactor}，不用再改");
            return true;
        }

        float before = importer.globalScale;
        importer.globalScale = k_ScaleFactor;
        importer.SaveAndReimport();

        Debug.Log($"[印章尺寸] Stamp.fbx 匯入縮放 {before} → {k_ScaleFactor}");
        return true;
    }
}

using System.Collections.Generic;

/// <summary>
/// 月餅種類。主畫面上選擇後決定要跑哪一條步驟流程。
/// </summary>
public enum MooncakeType
{
    Chinese,      // 中式月餅
    Indonesian,   // 印尼月餅 Tiong Ciu Pia
}

/// <summary>
/// 製作步驟。抓取／拍打／加料／揉捏／壓模 沿用紅龜粿既有機制，
/// 蓋章／刷蛋液／烘烤／取出 是這次的新機制。
/// </summary>
public enum MooncakeStep
{
    Grab,        // 抓取
    Flatten,     // 拍打
    AddFilling,  // 加料
    Knead,       // 揉捏
    Mold,        // 壓模
    Stamp,       // 蓋章（新）
    EggWash,     // 刷蛋液（新）
    Bake,        // 烘烤（新）
    TakeOut,     // 取出（新）
    Done,        // 完成
}

/// <summary>
/// 流程中的一格。同一種 step 在一條流程裡可能出現多次（例如中式要烘烤兩輪），
/// 每一格自己帶說明文字，所以用 struct 而不是單純的 enum 陣列。
/// </summary>
public struct MooncakeStepEntry
{
    public MooncakeStep Step;
    public string Hint;

    public MooncakeStepEntry(MooncakeStep step, string hint)
    {
        Step = step;
        Hint = hint;
    }
}

/// <summary>
/// 兩條流程的定義，內容對應企劃流程圖。
/// </summary>
public static class MooncakeRecipes
{
    static readonly MooncakeStepEntry[] k_Chinese =
    {
        new MooncakeStepEntry(MooncakeStep.Grab,       "抓取麵團"),
        new MooncakeStepEntry(MooncakeStep.Flatten,    "將麵團打扁"),
        new MooncakeStepEntry(MooncakeStep.AddFilling, "將餡料放在麵餅上"),
        new MooncakeStepEntry(MooncakeStep.Knead,      "將麵餅揉成球形"),
        new MooncakeStepEntry(MooncakeStep.Mold,       "將麵餅壓模成型"),
        new MooncakeStepEntry(MooncakeStep.Bake,       "放入烤箱烘烤"),
        new MooncakeStepEntry(MooncakeStep.TakeOut,    "取出"),
        new MooncakeStepEntry(MooncakeStep.EggWash,    "拿刷子刷蛋液"),
        new MooncakeStepEntry(MooncakeStep.Bake,       "放入烤箱烘烤"),
        new MooncakeStepEntry(MooncakeStep.TakeOut,    "取出"),
        new MooncakeStepEntry(MooncakeStep.Done,       "完成"),
    };

    static readonly MooncakeStepEntry[] k_Indonesian =
    {
        new MooncakeStepEntry(MooncakeStep.Grab,       "抓取麵團"),
        new MooncakeStepEntry(MooncakeStep.Flatten,    "將麵團打扁"),
        new MooncakeStepEntry(MooncakeStep.AddFilling, "將餡料放在麵餅上"),
        new MooncakeStepEntry(MooncakeStep.Knead,      "將麵餅揉成球形"),
        new MooncakeStepEntry(MooncakeStep.Flatten,    "將麵餅拍扁"),
        new MooncakeStepEntry(MooncakeStep.Stamp,      "蓋上印章"),
        new MooncakeStepEntry(MooncakeStep.Bake,       "放入烤箱烘烤"),
        new MooncakeStepEntry(MooncakeStep.TakeOut,    "取出"),
        new MooncakeStepEntry(MooncakeStep.Done,       "完成"),
    };

    public static IReadOnlyList<MooncakeStepEntry> For(MooncakeType type)
    {
        return type == MooncakeType.Chinese ? k_Chinese : k_Indonesian;
    }

    public static string TypeLabel(MooncakeType type)
    {
        return type == MooncakeType.Chinese ? "中式月餅" : "印尼月餅";
    }

    public static string StepLabel(MooncakeStep step)
    {
        switch (step)
        {
            case MooncakeStep.Grab:       return "抓取";
            case MooncakeStep.Flatten:    return "拍打";
            case MooncakeStep.AddFilling: return "加料";
            case MooncakeStep.Knead:      return "揉捏";
            case MooncakeStep.Mold:       return "壓模";
            case MooncakeStep.Stamp:      return "蓋章";
            case MooncakeStep.EggWash:    return "刷蛋液";
            case MooncakeStep.Bake:       return "烘烤";
            case MooncakeStep.TakeOut:    return "取出";
            default:                      return "完成";
        }
    }

    /// <summary>流程圖上標紅點的新機制，UI 上會另外標記。</summary>
    public static bool IsNewMechanic(MooncakeStep step)
    {
        return step == MooncakeStep.Stamp
            || step == MooncakeStep.EggWash
            || step == MooncakeStep.Bake
            || step == MooncakeStep.TakeOut;
    }
}

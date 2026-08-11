using System.Reflection;
using UnityEngine;

/// <summary>
/// I2 Localization 的薄包裝。
///
/// 跟 <see cref="MooncakeSettingsPanel"/> 一樣走反射，這樣就算之後把 I2 整個資料夾拿掉，
/// 月餅這邊也不會編不過，只是文字退回腳本內建的中文文案。
/// </summary>
public static class MooncakeLocalization
{
    private static bool _resolved;
    private static MethodInfo _getTranslation;
    private static PropertyInfo _currentLanguage;

    /// <summary>場上有沒有可用的 I2。</summary>
    public static bool Available
    {
        get { Resolve(); return _getTranslation != null; }
    }

    /// <summary>目前語言名稱；沒有 I2 時回空字串，設定也會靜靜地被忽略。</summary>
    public static string CurrentLanguage
    {
        get
        {
            Resolve();
            if (_currentLanguage == null || !_currentLanguage.CanRead) return "";

            return _currentLanguage.GetValue(null) as string ?? "";
        }
        set
        {
            Resolve();
            if (_currentLanguage == null || !_currentLanguage.CanWrite) return;

            _currentLanguage.SetValue(null, value);
        }
    }

    private static void Resolve()
    {
        if (_resolved) return;
        _resolved = true;

        var type = System.Type.GetType("I2.Loc.LocalizationManager, Assembly-CSharp");
        if (type == null) return;

        // 全部參數都明寫，反射沒有預設值可以省略
        _getTranslation = type.GetMethod(
            "GetTranslation",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[]
            {
                typeof(string), typeof(bool), typeof(int), typeof(bool),
                typeof(bool), typeof(GameObject), typeof(string), typeof(bool)
            },
            null);

        _currentLanguage = type.GetProperty("CurrentLanguage", BindingFlags.Static | BindingFlags.Public);

        if (_getTranslation == null)
            Debug.LogWarning("[月餅在地化] 找到 I2 但對不上 GetTranslation，文字會用內建文案");
    }

    /// <summary>查一個 term；查不到（或翻譯是空的）回 false。</summary>
    public static bool TryGet(string term, out string text)
    {
        text = null;

        Resolve();
        if (_getTranslation == null || string.IsNullOrEmpty(term)) return false;

        try
        {
            text = _getTranslation.Invoke(null,
                new object[] { term, true, 0, true, false, null, null, true }) as string;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[月餅在地化] 取「{term}」失敗：{e.Message}");
            return false;
        }

        // 查不到時 I2 可能回 null、空字串、或原封不動把 term 丟回來
        if (string.IsNullOrEmpty(text) || text == term)
        {
            text = null;
            return false;
        }
        return true;
    }

    /// <summary>查一個 term，查不到就用 fallback。</summary>
    public static string Get(string term, string fallback)
    {
        return TryGet(term, out var text) ? text : fallback;
    }
}

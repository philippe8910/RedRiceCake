using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// I2 Localization 的薄包裝。
/// 用反射呼叫，這樣就算之後移除 I2，遊戲仍然編得過、只是回退到英文原字串。
/// </summary>
public static class MooncakeLoc
{
    private static bool _resolved;
    private static MethodInfo _getTranslation;
    private static PropertyInfo _currentLanguage;

    private static void Resolve()
    {
        if (_resolved) return;
        _resolved = true;

        var t = Type.GetType("I2.Loc.LocalizationManager, Assembly-CSharp");
        if (t == null) return;

        // GetTranslation 有一長串預設參數，抓第一個參數是 string 的多載
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.Name != "GetTranslation") continue;
            var ps = m.GetParameters();
            if (ps.Length > 0 && ps[0].ParameterType == typeof(string))
            {
                _getTranslation = m;
                break;
            }
        }
        _currentLanguage = t.GetProperty("CurrentLanguage", BindingFlags.Public | BindingFlags.Static);
    }

    /// <summary>查一個 term；查不到（或翻譯是空的、或原封不動回傳 term）回 false。</summary>
    public static bool TryGet(string term, out string text)
    {
        text = null;

        if (string.IsNullOrEmpty(term)) return false;

        Resolve();
        if (_getTranslation == null) return false;

        try
        {
            var ps = _getTranslation.GetParameters();
            var args = new object[ps.Length];
            args[0] = term;
            for (int i = 1; i < ps.Length; i++)
                args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;

            text = _getTranslation.Invoke(null, args) as string;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[在地化] 取「{term}」失敗：{e.Message}");
            return false;
        }

        // 查不到時 I2 可能回 null、空字串，或原封不動把 term 丟回來
        if (string.IsNullOrEmpty(text) || text == term)
        {
            text = null;
            return false;
        }
        return true;
    }

    /// <summary>取翻譯；查不到就回傳 fallback（通常是原文）。</summary>
    public static string T(string term, string fallback = null)
    {
        return TryGet(term, out var text) ? text : (fallback ?? term ?? string.Empty);
    }

    /// <summary>同 <see cref="T"/>，給既有呼叫端沿用的別名。</summary>
    public static string Get(string term, string fallback) => T(term, fallback);

    public static string CurrentLanguage
    {
        get
        {
            Resolve();
            return _currentLanguage?.GetValue(null) as string ?? "English";
        }
        set
        {
            Resolve();
            if (_currentLanguage != null && _currentLanguage.CanWrite)
                _currentLanguage.SetValue(null, value);
        }
    }

    public static bool IsAvailable
    {
        get
        {
            Resolve();
            return _getTranslation != null;
        }
    }
}

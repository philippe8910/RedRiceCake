using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

/// <summary>
/// Debug 用：鍵盤 1～5 直接切語言，不用戴頭盔進設定選單。
/// 專案是 Input System (New)，所以直接讀 Keyboard.current，沒有鍵盤（實機）就整支跳過。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeLanguageDebugKeys : MonoBehaviour
{
    [Tooltip("留空則沿用 MooncakeSettings 的語言清單；填了才覆寫")]
    public string[] languages;

    [Tooltip("同時也叫 MooncakeSettings 切語言，PlayerPrefs 與事件才會一起更新")]
    public bool syncWithSettings = true;

    [Tooltip("切換時在 Console 印一行")]
    public bool logOnSwitch = true;

    [Tooltip("關掉就完全不吃鍵盤（正式版建議關）")]
    public bool enableKeys = true;

    [ShowInInspector, ReadOnly] private string _current;

    private MooncakeSettings _settings;

    /// <summary>實際使用的語言清單：優先用自己的，否則跟著設定走。</summary>
    private string[] Languages =>
        (languages != null && languages.Length > 0) ? languages
        : (_settings != null ? _settings.languages : null);

    private void Start()
    {
        _settings = MooncakeSettings.Instance ?? FindObjectOfType<MooncakeSettings>(true);

        _current = MooncakeLoc.CurrentLanguage;

        if (!MooncakeLoc.IsAvailable)
            Debug.LogWarning("[月餅語言] 場上沒有可用的 I2，數字鍵切語言不會有作用", this);
    }

    private void Update()
    {
        if (!enableKeys) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;   // 實機沒鍵盤

        if (keyboard.digit1Key.wasPressedThisFrame) Switch(0);
        else if (keyboard.digit2Key.wasPressedThisFrame) Switch(1);
        else if (keyboard.digit3Key.wasPressedThisFrame) Switch(2);
        else if (keyboard.digit4Key.wasPressedThisFrame) Switch(3);
        else if (keyboard.digit5Key.wasPressedThisFrame) Switch(4);
    }

    /// <summary>切到清單裡第 index 個語言。</summary>
    public void Switch(int index)
    {
        var list = Languages;
        if (list == null || index < 0 || index >= list.Length) return;

        string lang = list[index];
        if (string.IsNullOrEmpty(lang)) return;

        // 走設定元件，PlayerPrefs 與 onLanguageChanged 才會一起更新
        int i = System.Array.IndexOf(_settings != null ? _settings.languages : list, lang);
        if (syncWithSettings && _settings != null && i >= 0) _settings.SetLanguage(i);
        else MooncakeLoc.CurrentLanguage = lang;

        _current = MooncakeLoc.CurrentLanguage;

        if (logOnSwitch)
            Debug.Log($"[月餅語言] 數字鍵 {index + 1} → {lang}（目前：{_current}）", this);
    }

    [Button("Debug：下一個語言", ButtonSizes.Medium), GUIColor(0.6f, 0.8f, 1f)]
    public void NextLanguage()
    {
        var list = Languages;
        if (list == null || list.Length == 0) return;

        int i = System.Array.IndexOf(list, MooncakeLoc.CurrentLanguage);
        Switch((i + 1) % list.Length);
    }
}

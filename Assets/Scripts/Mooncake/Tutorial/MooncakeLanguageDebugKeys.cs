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
    [Tooltip("依序對應數字鍵 1、2、3、4、5；名稱要跟 I2 裡的語言一致")]
    public string[] languages = { "Chinese", "English", "Japanese", "Korean", "Indonesian" };

    [Tooltip("同時也叫設定面板的 SetLanguage，讓設定狀態跟著一起走")]
    public bool syncWithSettingsPanel = true;

    [Tooltip("切換時在 Console 印一行")]
    public bool logOnSwitch = true;

    [Tooltip("關掉就完全不吃鍵盤（正式版建議關）")]
    public bool enableKeys = true;

    [ShowInInspector, ReadOnly] private string _current;

    private MooncakeSettingsPanel _settings;

    private void Start()
    {
        if (syncWithSettingsPanel) _settings = FindObjectOfType<MooncakeSettingsPanel>(true);

        _current = MooncakeLocalization.CurrentLanguage;

        if (!MooncakeLocalization.Available)
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
        if (languages == null || index < 0 || index >= languages.Length) return;

        string lang = languages[index];
        if (string.IsNullOrEmpty(lang)) return;

        // 設定面板存在就走它，PlayerPrefs 與 onLanguageChanged 才會一起更新
        if (_settings != null) _settings.SetLanguage(lang);
        else MooncakeLocalization.CurrentLanguage = lang;

        _current = MooncakeLocalization.CurrentLanguage;

        if (logOnSwitch)
            Debug.Log($"[月餅語言] 數字鍵 {index + 1} → {lang}（目前：{_current}）", this);
    }

    [Button("Debug：下一個語言", ButtonSizes.Medium), GUIColor(0.6f, 0.8f, 1f)]
    public void NextLanguage()
    {
        if (languages == null || languages.Length == 0) return;

        int i = System.Array.IndexOf(languages, MooncakeLocalization.CurrentLanguage);
        Switch((i + 1) % languages.Length);
    }
}

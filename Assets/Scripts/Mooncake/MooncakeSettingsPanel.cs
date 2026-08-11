using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using Sirenix.OdinInspector;

/// <summary>
/// 基本設定：音量、語言（I2 Localization）、轉向方式、畫質。
/// 每一項都有 public 方法給 UI 綁，也都有 Odin 按鈕可以在 Editor 直接測。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeSettingsPanel : MonoBehaviour
{
    private const string KeyMaster = "mc_volume_master";
    private const string KeyMusic = "mc_volume_music";
    private const string KeySnapTurn = "mc_snap_turn";
    private const string KeyQuality = "mc_quality";

    [Header("音量")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.7f;
    [Tooltip("背景音樂的 AudioSource（可留空）")]
    public AudioSource musicSource;

    [Header("移動")]
    [Tooltip("true = 分段轉向（較不暈），false = 平滑轉向")]
    public bool snapTurn = true;

    [Header("畫質")]
    [Tooltip("對應 QualitySettings 的等級索引")]
    public int qualityLevel = 1;

    [Header("語言（I2 Localization）")]
    [Tooltip("依序對應 I2 裡的語言名稱")]
    public string[] languages = { "English", "Chinese", "Japanese", "Korean", "Indonesian" };
    [ShowInInspector, ReadOnly] private int _languageIndex;

    [Header("事件")]
    public UnityEvent onSettingsChanged;
    public MooncakeStringEvent onLanguageChanged;

    public string CurrentLanguage =>
        (languages != null && languages.Length > 0)
            ? languages[Mathf.Clamp(_languageIndex, 0, languages.Length - 1)]
            : "English";

    private void Awake()
    {
        Load();
        ApplyAll();
    }

    // ---------------- 讀寫 ----------------

    public void Load()
    {
        masterVolume = PlayerPrefs.GetFloat(KeyMaster, masterVolume);
        musicVolume = PlayerPrefs.GetFloat(KeyMusic, musicVolume);
        snapTurn = PlayerPrefs.GetInt(KeySnapTurn, snapTurn ? 1 : 0) == 1;
        qualityLevel = PlayerPrefs.GetInt(KeyQuality, qualityLevel);
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(KeyMaster, masterVolume);
        PlayerPrefs.SetFloat(KeyMusic, musicVolume);
        PlayerPrefs.SetInt(KeySnapTurn, snapTurn ? 1 : 0);
        PlayerPrefs.SetInt(KeyQuality, qualityLevel);
        PlayerPrefs.Save();
    }

    [FoldoutGroup("Debug（Editor 直接按）")]
    [Button("套用目前所有設定", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void ApplyAll()
    {
        SetMasterVolume(masterVolume);
        SetMusicVolume(musicVolume);
        SetQuality(qualityLevel);
        ApplyLanguage();
        Save();
        onSettingsChanged?.Invoke();
    }

    // ---------------- 各項設定 ----------------

    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeyMaster, masterVolume);

        // Fader 會把音量乘上遮罩透明度，淡入淡出中就交給它算，避免互相打架
        var fader = MooncakeScreenFader.Instance;
        if (fader == null || !fader.fadeAudio) AudioListener.volume = masterVolume;

        onSettingsChanged?.Invoke();
    }

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (musicSource != null) musicSource.volume = musicVolume;
        onSettingsChanged?.Invoke();
    }

    public void SetSnapTurn(bool value)
    {
        snapTurn = value;
        // XR Origin 的 ControllerInputActionManager 有 m_SmoothTurnEnabled，
        // 這裡用反射避免硬相依 Starter Assets 的型別
        foreach (var mb in FindObjectsOfType<MonoBehaviour>(true))
        {
            var t = mb.GetType();
            if (t.Name != "ControllerInputActionManager") continue;
            var f = t.GetField("m_SmoothTurnEnabled",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (f != null) f.SetValue(mb, !snapTurn);
        }
        onSettingsChanged?.Invoke();
    }

    public void SetQuality(int level)
    {
        qualityLevel = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(qualityLevel, true);
        onSettingsChanged?.Invoke();
    }

    // ---------------- 語言 ----------------

    public void SetLanguage(int index)
    {
        if (languages == null || languages.Length == 0) return;

        _languageIndex = ((index % languages.Length) + languages.Length) % languages.Length;
        ApplyLanguage();
    }

    public void SetLanguage(string language)
    {
        if (languages == null) return;

        int i = System.Array.IndexOf(languages, language);
        if (i >= 0) SetLanguage(i);
        else Debug.LogWarning($"[設定] 語言清單裡沒有「{language}」", this);
    }

    [FoldoutGroup("Debug（Editor 直接按）")]
    [Button("下一個語言", ButtonSizes.Medium), GUIColor(0.6f, 0.8f, 1f)]
    public void NextLanguage()
    {
        SetLanguage(_languageIndex + 1);
    }

    private void ApplyLanguage()
    {
        string lang = CurrentLanguage;

        // 用反射呼叫 I2，這樣就算之後移除 I2 這支腳本也不會編不過
        var t = System.Type.GetType("I2.Loc.LocalizationManager, Assembly-CSharp");
        if (t != null)
        {
            var prop = t.GetProperty("CurrentLanguage",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(null, lang);
                Debug.Log($"[設定] 語言切到「{lang}」", this);
            }
        }
        else
        {
            Debug.LogWarning("[設定] 找不到 I2.Loc.LocalizationManager，語言只記錄不套用", this);
        }

        onLanguageChanged?.Invoke(lang);
        onSettingsChanged?.Invoke();
    }

    [FoldoutGroup("Debug（Editor 直接按）")]
    [Button("列出 I2 目前有哪些語言", ButtonSizes.Medium), GUIColor(1f, 0.8f, 0.4f)]
    public void DebugListLanguages()
    {
        var t = System.Type.GetType("I2.Loc.LocalizationManager, Assembly-CSharp");
        if (t == null)
        {
            Debug.LogWarning("[設定] 找不到 I2.Loc.LocalizationManager", this);
            return;
        }

        var m = t.GetMethod("GetAllLanguages", new[] { typeof(bool) });
        if (m == null)
        {
            Debug.LogWarning("[設定] 找不到 GetAllLanguages", this);
            return;
        }

        var list = m.Invoke(null, new object[] { true }) as List<string>;
        Debug.Log("[設定] I2 目前的語言: " +
                  (list == null || list.Count == 0 ? "(空的，還沒建立 Language Source)" : string.Join(", ", list)), this);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

/// <summary>
/// 設定的資料層。每一項都用同一套介面描述（顯示名稱、目前值、上一個／下一個），
/// UI 那邊就能用同一種「設定列」元件套用到所有項目，不必每項手刻。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeSettings : MonoBehaviour
{
    public static MooncakeSettings Instance { get; private set; }

    public enum Id
    {
        Language,
        MasterVolume, MusicVolume, SfxVolume,
        QualityLevel, RenderScale, Shadows,
        TurnMode, SnapAngle, Vignette, MoveSpeed,
    }

    /// <summary>一個設定項目的定義。</summary>
    public class Option
    {
        public Id id;
        public string labelKey;                 // 給 I2 用的 term，同時也是英文預設顯示
        public Func<string> display;            // 目前值要顯示成什麼
        public Action<int> step;                // +1 / -1

        // 連續型的項目（音量之類）額外提供 0~1 存取，UI 就能改用拉桿
        public bool isSlider;
        public Func<float> get01;
        public Action<float> set01;
    }

    [Header("套用對象")]
    [Tooltip("背景音樂，留空則只記錄數值")]
    public AudioSource musicSource;
    [Tooltip("音效用的 AudioSource（可留空）")]
    public AudioSource sfxSource;

    [Header("目前的值")]
    [ShowInInspector] public int language;              // 對應 languages[]
    [ShowInInspector, Range(0, 100)] public int masterVolume = 100;
    [ShowInInspector, Range(0, 100)] public int musicVolume = 70;
    [ShowInInspector, Range(0, 100)] public int sfxVolume = 100;
    [ShowInInspector] public int qualityLevel = 1;
    [ShowInInspector] public int renderScaleStep = 3;   // 0.7 ~ 1.2
    [ShowInInspector] public bool shadows = true;
    [ShowInInspector] public bool smoothTurn;           // false = 分段轉向
    [ShowInInspector] public int snapAngleStep = 1;     // 15/30/45/60
    [ShowInInspector] public int vignette = 1;          // 關/弱/中/強
    [ShowInInspector] public int moveSpeedStep = 2;     // 慢/普通/快

    [Header("語言")]
    public string[] languages = { "English", "Chinese", "Japanese", "Korean", "Indonesian" };
    [Tooltip("顯示用的名稱，數量要跟上面一致")]
    public string[] languageDisplay = { "English", "中文", "日本語", "한국어", "Bahasa Indonesia" };

    [Header("事件")]
    public UnityEvent onChanged;
    public MooncakeStringEvent onLanguageChanged;

    private static readonly float[] RenderScales = { 0.7f, 0.8f, 0.9f, 1.0f, 1.1f, 1.2f };
    private static readonly int[] SnapAngles = { 15, 30, 45, 60 };
    private static readonly string[] VignetteNames = { "Off", "Low", "Medium", "High" };
    private static readonly float[] MoveSpeeds = { 1.0f, 1.6f, 2.4f };
    private static readonly string[] MoveSpeedNames = { "Slow", "Normal", "Fast" };

    private readonly Dictionary<Id, Option> _options = new Dictionary<Id, Option>();

    public IReadOnlyDictionary<Id, Option> Options => _options;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        BuildOptions();
        Load();
        ApplyAll();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------- 定義 ----------------

    private void BuildOptions()
    {
        Add(Id.Language, "Language",
            () => (languageDisplay != null && language < languageDisplay.Length)
                  ? languageDisplay[language] : Lang,
            d => { language = Wrap(language + d, languages.Length); ApplyLanguage(); });

        AddSlider(Id.MasterVolume, "Master Volume", () => masterVolume + "%",
            d => { masterVolume = Clamp(masterVolume + d * 5, 0, 100); ApplyAudio(); },
            () => masterVolume / 100f,
            v => { masterVolume = Mathf.RoundToInt(v * 100f); ApplyAudio(); });
        AddSlider(Id.MusicVolume, "Music Volume", () => musicVolume + "%",
            d => { musicVolume = Clamp(musicVolume + d * 5, 0, 100); ApplyAudio(); },
            () => musicVolume / 100f,
            v => { musicVolume = Mathf.RoundToInt(v * 100f); ApplyAudio(); });
        AddSlider(Id.SfxVolume, "Sound Effects", () => sfxVolume + "%",
            d => { sfxVolume = Clamp(sfxVolume + d * 5, 0, 100); ApplyAudio(); },
            () => sfxVolume / 100f,
            v => { sfxVolume = Mathf.RoundToInt(v * 100f); ApplyAudio(); });

        Add(Id.QualityLevel, "Quality",
            () => QualitySettings.names[Mathf.Clamp(qualityLevel, 0, QualitySettings.names.Length - 1)],
            d => { qualityLevel = Clamp(qualityLevel + d, 0, QualitySettings.names.Length - 1); ApplyQuality(); });
        AddSlider(Id.RenderScale, "Render Scale",
            () => RenderScales[renderScaleStep].ToString("0.0") + "x",
            d => { renderScaleStep = Clamp(renderScaleStep + d, 0, RenderScales.Length - 1); ApplyRenderScale(); },
            () => renderScaleStep / (float)(RenderScales.Length - 1),
            v => { renderScaleStep = Mathf.RoundToInt(v * (RenderScales.Length - 1)); ApplyRenderScale(); });
        Add(Id.Shadows, "Shadows", () => MooncakeLoc.T(shadows ? "Value/On" : "Value/Off", shadows ? "On" : "Off"),
            d => { shadows = !shadows; ApplyQuality(); });

        Add(Id.TurnMode, "Turn Mode", () => MooncakeLoc.T(smoothTurn ? "Value/Smooth" : "Value/Snap", smoothTurn ? "Smooth" : "Snap"),
            d => { smoothTurn = !smoothTurn; ApplyComfort(); });
        Add(Id.SnapAngle, "Snap Angle", () => SnapAngles[snapAngleStep] + "°",
            d => { snapAngleStep = Clamp(snapAngleStep + d, 0, SnapAngles.Length - 1); ApplyComfort(); });
        Add(Id.Vignette, "Comfort Vignette", () => MooncakeLoc.T("Value/" + VignetteNames[vignette], VignetteNames[vignette]),
            d => { vignette = Clamp(vignette + d, 0, VignetteNames.Length - 1); ApplyComfort(); });
        Add(Id.MoveSpeed, "Movement Speed", () => MooncakeLoc.T("Value/" + MoveSpeedNames[moveSpeedStep], MoveSpeedNames[moveSpeedStep]),
            d => { moveSpeedStep = Clamp(moveSpeedStep + d, 0, MoveSpeeds.Length - 1); ApplyComfort(); });

    }

    private void Add(Id id, string label, Func<string> display, Action<int> step)
    {
        _options[id] = new Option { id = id, labelKey = label, display = display, step = step };
    }

    /// <summary>連續型（拉桿）項目。</summary>
    private void AddSlider(Id id, string label, Func<string> display, Action<int> step,
                           Func<float> get01, Action<float> set01)
    {
        _options[id] = new Option
        {
            id = id, labelKey = label, display = display, step = step,
            isSlider = true, get01 = get01, set01 = set01,
        };
    }

    /// <summary>直接切到指定語言索引（給 debug 快捷鍵或外部呼叫用）。</summary>
    public void SetLanguage(int index)
    {
        if (languages == null || languages.Length == 0) return;

        language = Wrap(index, languages.Length);
        ApplyLanguage();
        Save();
        onChanged?.Invoke();
    }

    /// <summary>依語言名稱切換；名稱不在清單裡就忽略並發警告。</summary>
    public void SetLanguage(string languageName)
    {
        int i = Array.IndexOf(languages ?? Array.Empty<string>(), languageName);
        if (i >= 0) SetLanguage(i);
        else Debug.LogWarning($"[設定] 語言清單裡沒有「{languageName}」", this);
    }

    public bool IsSlider(Id id) => _options.TryGetValue(id, out var o) && o.isSlider;

    public float GetNormalized(Id id)
        => _options.TryGetValue(id, out var o) && o.get01 != null ? o.get01() : 0f;

    public void SetNormalized(Id id, float v)
    {
        if (!_options.TryGetValue(id, out var o) || o.set01 == null) return;
        o.set01(Mathf.Clamp01(v));
        Save();
        onChanged?.Invoke();
    }

    private static int Clamp(int v, int lo, int hi) => Mathf.Clamp(v, lo, hi);
    private static int Wrap(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;

    public string Lang => (languages != null && languages.Length > 0)
        ? languages[Mathf.Clamp(language, 0, languages.Length - 1)] : "English";

    // ---------------- 對外 ----------------

    public string GetLabel(Id id)
    {
        string key = _options.TryGetValue(id, out var o) ? o.labelKey : id.ToString();
        return MooncakeLoc.T("Settings/" + id, key);
    }

    public string GetDisplay(Id id) => _options.TryGetValue(id, out var o) ? o.display() : "-";

    public void Step(Id id, int dir)
    {
        if (!_options.TryGetValue(id, out var o)) return;
        o.step(dir);
        Save();
        onChanged?.Invoke();
    }

    // ---------------- 套用 ----------------

    [Button("套用全部", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void ApplyAll()
    {
        ApplyAudio();
        ApplyQuality();
        ApplyRenderScale();
        ApplyComfort();
        ApplyLanguage();
        onChanged?.Invoke();
    }

    private void ApplyAudio()
    {
        PlayerPrefs.SetFloat("mc_volume_master", masterVolume / 100f);

        // 淡入淡出中的音量由 Fader 依遮罩透明度乘算，避免互相覆蓋
        var fader = MooncakeScreenFader.Instance;
        if (fader == null || !fader.fadeAudio) AudioListener.volume = masterVolume / 100f;

        if (musicSource != null) musicSource.volume = musicVolume / 100f;
        if (sfxSource != null) sfxSource.volume = sfxVolume / 100f;
    }

    private void ApplyQuality()
    {
        qualityLevel = Clamp(qualityLevel, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(qualityLevel, true);
        QualitySettings.shadows = shadows ? ShadowQuality.All : ShadowQuality.Disable;
    }

    private void ApplyRenderScale()
    {
        float s = RenderScales[Mathf.Clamp(renderScaleStep, 0, RenderScales.Length - 1)];
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (rp == null) return;

        // 用反射設定 URP Asset 的 renderScale，避免硬相依 URP 型別
        var prop = rp.GetType().GetProperty("renderScale");
        if (prop != null && prop.CanWrite) prop.SetValue(rp, s);
    }

    private void ApplyComfort()
    {
        foreach (var mb in FindObjectsOfType<MonoBehaviour>(true))
        {
            var t = mb.GetType();
            if (t.Name != "ControllerInputActionManager") continue;

            SetField(mb, t, "m_SmoothTurnEnabled", smoothTurn);
            SetField(mb, t, "m_SmoothMotionEnabled", true);
        }
    }

    private static void SetField(object obj, Type t, string name, object value)
    {
        var f = t.GetField(name, System.Reflection.BindingFlags.Instance |
                                 System.Reflection.BindingFlags.NonPublic |
                                 System.Reflection.BindingFlags.Public);
        if (f != null) f.SetValue(obj, value);
    }

    private void ApplyLanguage()
    {
        string lang = Lang;

        // 統一走 MooncakeLoc，執行期只有這一個 I2 入口
        MooncakeLoc.CurrentLanguage = lang;

        onLanguageChanged?.Invoke(lang);
    }

    // ---------------- 存讀 ----------------

    public void Save()
    {
        PlayerPrefs.SetInt("mc_lang", language);
        PlayerPrefs.SetInt("mc_vol_master", masterVolume);
        PlayerPrefs.SetInt("mc_vol_music", musicVolume);
        PlayerPrefs.SetInt("mc_vol_sfx", sfxVolume);
        PlayerPrefs.SetInt("mc_quality", qualityLevel);
        PlayerPrefs.SetInt("mc_renderscale", renderScaleStep);
        PlayerPrefs.SetInt("mc_shadows", shadows ? 1 : 0);
        PlayerPrefs.SetInt("mc_smoothturn", smoothTurn ? 1 : 0);
        PlayerPrefs.SetInt("mc_snapangle", snapAngleStep);
        PlayerPrefs.SetInt("mc_vignette", vignette);
        PlayerPrefs.SetInt("mc_movespeed", moveSpeedStep);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        language = PlayerPrefs.GetInt("mc_lang", language);
        masterVolume = PlayerPrefs.GetInt("mc_vol_master", masterVolume);
        musicVolume = PlayerPrefs.GetInt("mc_vol_music", musicVolume);
        sfxVolume = PlayerPrefs.GetInt("mc_vol_sfx", sfxVolume);
        qualityLevel = PlayerPrefs.GetInt("mc_quality", qualityLevel);
        renderScaleStep = PlayerPrefs.GetInt("mc_renderscale", renderScaleStep);
        shadows = PlayerPrefs.GetInt("mc_shadows", shadows ? 1 : 0) == 1;
        smoothTurn = PlayerPrefs.GetInt("mc_smoothturn", smoothTurn ? 1 : 0) == 1;
        snapAngleStep = PlayerPrefs.GetInt("mc_snapangle", snapAngleStep);
        vignette = PlayerPrefs.GetInt("mc_vignette", vignette);
        moveSpeedStep = PlayerPrefs.GetInt("mc_movespeed", moveSpeedStep);
    }

    [Button("回復預設值", ButtonSizes.Medium), GUIColor(1f, 0.5f, 0.3f)]
    public void ResetToDefaults()
    {
        language = 0; masterVolume = 100; musicVolume = 70; sfxVolume = 100;
        qualityLevel = 1; renderScaleStep = 3; shadows = true;
        smoothTurn = false; snapAngleStep = 1; vignette = 1; moveSpeedStep = 1;
        ApplyAll();
        Save();
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;

/// <summary>
/// VR 畫面淡入淡出。遮罩是執行時用程式生出來的一片 Quad，貼在相機前方，
/// 所以不需要在場景裡預先擺任何東西，每個場景掛這一個元件就好。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeScreenFader : MonoBehaviour
{
    public static MooncakeScreenFader Instance { get; private set; }

    [Header("外觀")]
    public Color fadeColor = Color.black;
    [Tooltip("遮罩離相機多遠。要比近裁剪面遠一點，又要夠近才蓋得住視野")]
    public float distance = 0.3f;
    [Tooltip("遮罩尺寸，夠大就好")]
    public float size = 4f;

    [Header("時間")]
    public float fadeInDuration = 1.0f;
    public float fadeOutDuration = 0.6f;
    [Tooltip("開場自動從黑色淡入")]
    public bool fadeInOnStart = true;
    [Tooltip("開場淡入前先等一下，讓場景載入穩定")]
    public float startDelay = 0.15f;

    [Header("音樂")]
    [Tooltip("音量跟著畫面一起淡：全黑=靜音，全亮=設定裡的音量")]
    public bool fadeAudio = true;
    [Tooltip("設定選單存主音量用的 PlayerPrefs key")]
    public string masterVolumePrefKey = "mc_volume_master";

    [Header("事件")]
    public UnityEvent onFadeInComplete;
    public UnityEvent onFadeOutComplete;

    [ShowInInspector, ReadOnly] private float _alpha = 1f;

    private Camera _cam;
    private Transform _quad;
    private Material _mat;
    private Coroutine _routine;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public bool IsFading => _routine != null;

    private void Awake()
    {
        // 換場景時保留，這樣淡出可以跨場景延續
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildQuad();
        SetAlpha(1f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (fadeInOnStart) FadeIn();
        else SetAlpha(0f);
    }

    // ---------------- 遮罩 ----------------

    private void BuildQuad()
    {
        _cam = Camera.main;
        if (_cam == null)
        {
            Debug.LogWarning("[淡入淡出] 找不到 Main Camera，等有相機時會再試一次", this);
            return;
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "ScreenFadeQuad";
        Destroy(go.GetComponent<Collider>());          // 不要擋住射線互動

        _quad = go.transform;
        _quad.SetParent(_cam.transform, false);
        _quad.localPosition = new Vector3(0f, 0f, Mathf.Max(0.05f, distance));
        _quad.localRotation = Quaternion.identity;
        _quad.localScale = new Vector3(size, size, 1f);

        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        SetupTransparent(_mat);
        r.material = _mat;

        // 蓋在所有東西前面
        _mat.renderQueue = 4000;
    }

    private static void SetupTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);                    // Transparent
        m.SetFloat("_Blend", 0f);                      // Alpha
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = 4000;
    }

    private void SetAlpha(float a)
    {
        _alpha = Mathf.Clamp01(a);

        ApplyAudio();

        if (_mat == null) return;

        var c = fadeColor;
        c.a = _alpha;
        if (_mat.HasProperty(BaseColorId)) _mat.SetColor(BaseColorId, c);
        if (_mat.HasProperty(ColorId)) _mat.SetColor(ColorId, c);

        if (_quad != null) _quad.gameObject.SetActive(_alpha > 0.001f);
    }

    /// <summary>
    /// 音量跟著遮罩走。AudioListener.volume 是全域且跨場景保留的，
    /// 所以換場景時舊場景先淡到靜音、新場景的 Fader 再淡回設定值。
    /// </summary>
    private void ApplyAudio()
    {
        if (!fadeAudio) return;

        float master = PlayerPrefs.GetFloat(masterVolumePrefKey, 1f);
        AudioListener.volume = master * (1f - _alpha);
    }

    // ---------------- 對外 ----------------

    /// <summary>從全黑淡到透明。</summary>
    [Button("Debug：淡入", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void FadeIn()
    {
        Run(Fade(1f, 0f, fadeInDuration, startDelay, onFadeInComplete));
    }

    /// <summary>從透明淡到全黑。</summary>
    [Button("Debug：淡出", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void FadeOut()
    {
        Run(Fade(_alpha, 1f, fadeOutDuration, 0f, onFadeOutComplete));
    }

    /// <summary>淡出後載入場景（新場景的 Fader 會自己淡入）。</summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[淡入淡出] 沒有指定場景名稱", this);
            return;
        }
        Run(FadeOutThenLoad(sceneName));
    }

    private void Run(IEnumerator routine)
    {
        if (!Application.isPlaying) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    private IEnumerator Fade(float from, float to, float duration, float delay, UnityEvent done)
    {
        if (_quad == null) BuildQuad();

        SetAlpha(from);
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;           // 暫停選單時也要能動
            SetAlpha(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }

        SetAlpha(to);
        _routine = null;
        done?.Invoke();
    }

    private IEnumerator FadeOutThenLoad(string sceneName)
    {
        if (_quad == null) BuildQuad();

        float t = 0f;
        float d = Mathf.Max(0.01f, fadeOutDuration);
        float from = _alpha;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, 1f, t / d));
            yield return null;
        }
        SetAlpha(1f);
        onFadeOutComplete?.Invoke();

        _routine = null;
        SceneManager.LoadScene(sceneName);
    }
}

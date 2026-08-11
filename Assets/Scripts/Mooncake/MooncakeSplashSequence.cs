using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;

/// <summary>
/// 開場 LOGO 序列：全黑 → LOGO 漸顯 → 停留 → LOGO 漸隱回全黑 → 切到選關畫面。
/// LOGO 用程式生成的一片 Quad 貼在相機前，不需要在場景裡預先擺 UI。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeSplashSequence : MonoBehaviour
{
    [Header("LOGO")]
    public Texture2D logo;
    [Tooltip("LOGO 離相機多遠")]
    public float distance = 1.4f;
    [Tooltip("LOGO 高度（公尺），寬度會依貼圖比例自動算")]
    public float height = 0.6f;

    [Header("節奏（秒）")]
    public float blackHoldBefore = 0.6f;
    public float logoFadeIn = 1.4f;
    public float logoHold = 1.8f;
    public float logoFadeOut = 1.2f;
    public float blackHoldAfter = 0.5f;

    [Header("下一個場景")]
    public string nextScene = "MainMenu";
    [Tooltip("關掉的話只跑動畫不換場景，方便單獨測試")]
    public bool loadNextScene = true;

    [Header("事件")]
    public UnityEvent onSplashFinished;

    private Transform _quad;
    private Material _mat;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private void Start()
    {
        BuildLogoQuad();
        SetLogoAlpha(0f);
        StartCoroutine(Run());
    }

    private void BuildLogoQuad()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[開場 LOGO] 找不到 Main Camera", this);
            return;
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "SplashLogoQuad";
        Destroy(go.GetComponent<Collider>());

        _quad = go.transform;
        _quad.SetParent(cam.transform, false);
        _quad.localPosition = new Vector3(0f, 0f, Mathf.Max(0.1f, distance));
        _quad.localRotation = Quaternion.identity;

        float aspect = (logo != null && logo.height > 0) ? (float)logo.width / logo.height : 1f;
        _quad.localScale = new Vector3(height * aspect, height, 1f);

        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        _mat.SetFloat("_Surface", 1f);
        _mat.SetFloat("_Blend", 0f);
        _mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetFloat("_ZWrite", 0f);
        _mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        if (logo != null)
        {
            if (_mat.HasProperty(BaseMapId)) _mat.SetTexture(BaseMapId, logo);
            if (_mat.HasProperty(MainTexId)) _mat.SetTexture(MainTexId, logo);
        }

        // 比淡入淡出遮罩(4000)低一階，這樣 Fader 仍蓋得住 LOGO
        _mat.renderQueue = 3990;
        r.material = _mat;
    }

    private void SetLogoAlpha(float a)
    {
        if (_mat == null) return;

        var c = Color.white;
        c.a = Mathf.Clamp01(a);
        if (_mat.HasProperty(BaseColorId)) _mat.SetColor(BaseColorId, c);
        if (_mat.HasProperty(ColorId)) _mat.SetColor(ColorId, c);

        if (_quad != null) _quad.gameObject.SetActive(c.a > 0.001f);
    }

    private IEnumerator Run()
    {
        // 全黑開場：Fader 開場就是全黑，這裡不讓它自動淡入
        var fader = MooncakeScreenFader.Instance;
        if (fader != null) fader.fadeInOnStart = false;

        yield return new WaitForSeconds(blackHoldBefore);

        yield return FadeLogo(0f, 1f, logoFadeIn);
        yield return new WaitForSeconds(logoHold);
        yield return FadeLogo(1f, 0f, logoFadeOut);

        yield return new WaitForSeconds(blackHoldAfter);

        onSplashFinished?.Invoke();

        if (!loadNextScene) yield break;

        if (fader != null) fader.LoadScene(nextScene);
        else SceneManager.LoadScene(nextScene);
    }

    private IEnumerator FadeLogo(float from, float to, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetLogoAlpha(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetLogoAlpha(to);
    }

    [Button("Debug：重跑開場", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugReplay()
    {
        if (!Application.isPlaying) return;
        StopAllCoroutines();
        StartCoroutine(Run());
    }
}

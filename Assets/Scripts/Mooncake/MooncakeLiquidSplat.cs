using UnityEngine;

/// <summary>
/// 液體落點：貼在被滴到的物件上，稍微長大 → 停留 → 淡出 → 自己刪掉。
/// 走的是老派省效能作法，沒有 decal projector，就是一片壓扁的半透明網格。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeLiquidSplat : MonoBehaviour
{
    [Header("時間")]
    [Tooltip("冒出來時長大的時間")]
    public float growSeconds = 0.12f;
    [Tooltip("完全不透明停留多久")]
    public float holdSeconds = 1.5f;
    [Tooltip("淡出時間")]
    public float fadeSeconds = 2.5f;

    [Header("外觀")]
    [Tooltip("出現時的起始縮放比例")]
    public float startScale = 0.3f;

    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Vector3 _targetScale;
    private float _age;
    private float _baseAlpha = 1f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>
    /// 由 <see cref="MooncakeLiquidDrop"/> 在「掛好父物件之後」呼叫，指定想要的世界大小。
    /// 沒呼叫的話就用 Awake 當下的 localScale。
    /// </summary>
    public void SetWorldScale(Vector3 worldScale)
    {
        Vector3 ls = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        _targetScale = new Vector3(SafeDiv(worldScale.x, ls.x),
                                   SafeDiv(worldScale.y, ls.y),
                                   SafeDiv(worldScale.z, ls.z));
        transform.localScale = _targetScale * Mathf.Max(0.01f, startScale);
    }

    private static float SafeDiv(float a, float b)
    {
        return Mathf.Abs(b) > 1e-6f ? a / Mathf.Abs(b) : a;
    }

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();

        _targetScale = transform.localScale;
        transform.localScale = _targetScale * Mathf.Max(0.01f, startScale);

        if (_renderers.Length > 0)
        {
            var m = _renderers[0].sharedMaterial;
            if (m != null)
            {
                if (m.HasProperty(BaseColorId)) _baseAlpha = m.GetColor(BaseColorId).a;
                else if (m.HasProperty(ColorId)) _baseAlpha = m.GetColor(ColorId).a;
            }
        }
    }

    private void Update()
    {
        _age += Time.deltaTime;

        if (growSeconds > 0f && _age < growSeconds)
        {
            float t = _age / growSeconds;
            transform.localScale = Vector3.Lerp(_targetScale * startScale, _targetScale, t);
        }
        else if (transform.localScale != _targetScale)
        {
            transform.localScale = _targetScale;
        }

        float fadeStart = growSeconds + holdSeconds;
        if (_age < fadeStart) return;

        if (fadeSeconds <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float k = Mathf.Clamp01((_age - fadeStart) / fadeSeconds);
        SetAlpha(Mathf.Lerp(_baseAlpha, 0f, k));

        if (k >= 1f) Destroy(gameObject);
    }

    /// <summary>用 MaterialPropertyBlock 改透明度，不會產生材質實例。</summary>
    private void SetAlpha(float a)
    {
        if (_renderers == null) return;

        foreach (var r in _renderers)
        {
            if (r == null) continue;

            var m = r.sharedMaterial;
            if (m == null) continue;

            Color c = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId)
                : (m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white);
            c.a = a;

            r.GetPropertyBlock(_mpb);
            if (m.HasProperty(BaseColorId)) _mpb.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) _mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }
}

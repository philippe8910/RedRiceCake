using DG.Tweening;
using UnityEngine;

/// <summary>
/// 玩家手上那顆麵團／月餅本體。demo 階段用圓球＋方塊當替身，
/// 造型變化一律靠 scale 與顏色表現，之後換成真模型只要把 Renderer 換掉即可。
/// </summary>
public class MooncakeWorkpiece : MonoBehaviour
{
    [Header("Visual")]
    public Renderer bodyRenderer;
    public Transform fillingVisual;   // 加料後露出的內餡（小球）
    public Transform stampVisual;     // 蓋章後露出的印記（薄方塊）

    [Header("造型 Scale")]
    public Vector3 ballScale = new Vector3(0.13f, 0.13f, 0.13f);
    public Vector3 flatScale = new Vector3(0.20f, 0.045f, 0.20f);
    public Vector3 moldedScale = new Vector3(0.17f, 0.075f, 0.17f);

    [Header("顏色")]
    public Color doughColor = new Color(0.93f, 0.89f, 0.78f);
    public Color eggWashColor = new Color(0.95f, 0.75f, 0.30f);
    [Tooltip("每烤一輪往這個顏色靠近的比例")]
    [Range(0f, 1f)] public float bakeDarkenPerRound = 0.45f;
    public Color bakedColor = new Color(0.55f, 0.32f, 0.12f);

    [Header("Tween")]
    public float morphTime = 0.25f;

    public int BakeCount { get; private set; }
    public bool HasFilling { get; private set; }
    public bool HasStamp { get; private set; }
    public bool IsEggWashed { get; private set; }

    Material _mat;
    Color _currentColor;
    Tween _scaleTween;
    Tween _colorTween;

    void Awake()
    {
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
        if (bodyRenderer != null) _mat = bodyRenderer.material;   // 取實體避免改到共用材質

        _currentColor = doughColor;
        ApplyColorImmediate(_currentColor);

        if (fillingVisual != null) fillingVisual.gameObject.SetActive(false);
        if (stampVisual != null) stampVisual.gameObject.SetActive(false);

        transform.localScale = ballScale;
    }

    void OnDestroy()
    {
        _scaleTween?.Kill();
        _colorTween?.Kill();
    }

    // ---------- 造型 ----------

    public void MorphToBall()    { MorphTo(ballScale); }
    public void MorphToFlat()    { MorphTo(flatScale); }
    public void MorphToMolded()  { MorphTo(moldedScale); }

    void MorphTo(Vector3 target)
    {
        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(target, morphTime).SetEase(Ease.OutBack);
    }

    /// <summary>每一下拍打／揉捏的即時回饋，沿用紅龜粿的彈性縮放手感。</summary>
    public void PlayActionPulse(float amplitude = 0.12f)
    {
        var baseScale = transform.localScale;
        _scaleTween?.Kill();
        _scaleTween = DOTween.Sequence()
            .Append(transform.DOScale(baseScale * (1f + amplitude), 0.06f).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(baseScale, 0.22f).SetEase(Ease.OutElastic));
    }

    // ---------- 步驟結果 ----------

    public void SetFilling(Color fillingColor)
    {
        HasFilling = true;
        if (fillingVisual == null) return;

        fillingVisual.gameObject.SetActive(true);
        var r = fillingVisual.GetComponent<Renderer>();
        if (r != null) r.material.color = fillingColor;

        var s = fillingVisual.localScale;
        fillingVisual.localScale = s * 0.2f;
        fillingVisual.DOScale(s, 0.3f).SetEase(Ease.OutBack);
    }

    public void ShowStamp()
    {
        HasStamp = true;
        if (stampVisual == null) return;

        stampVisual.gameObject.SetActive(true);
        var s = stampVisual.localScale;
        stampVisual.localScale = new Vector3(s.x * 0.2f, s.y, s.z * 0.2f);
        stampVisual.DOScale(s, 0.25f).SetEase(Ease.OutBack);
    }

    public void ApplyEggWash()
    {
        IsEggWashed = true;
        FadeColorTo(Color.Lerp(_currentColor, eggWashColor, 0.8f));
    }

    public void ApplyBake()
    {
        BakeCount++;
        FadeColorTo(Color.Lerp(_currentColor, bakedColor, bakeDarkenPerRound));
    }

    // ---------- 顏色 ----------

    void FadeColorTo(Color c)
    {
        _currentColor = c;
        if (_mat == null) return;

        _colorTween?.Kill();
        _colorTween = _mat.DOColor(c, 0.4f);
    }

    void ApplyColorImmediate(Color c)
    {
        if (_mat != null) _mat.color = c;
    }
}

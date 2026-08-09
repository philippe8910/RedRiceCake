using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 中式月餅 - 抓取站（麵團球）。
/// 手把靠近 → 手把震動 + BlendShape "small" 彈簧回饋（0→12→0→10→0…遞減回 0）。
/// 靠近時按下 XR Trigger → 生出一顆麵團 Prefab，並帶彈簧縮放。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeDoughBall : MooncakeSpawnSource
{
    [Header("BlendShape 彈簧")]
    [Tooltip("BlendShape 名稱；找不到完全相符時會忽略大小寫與括號等符號再比對一次")]
    public string blendShapeName = "small";
    [Tooltip("大於等於 0 時直接指定索引，忽略上面的名稱")]
    public int blendShapeIndexOverride = -1;
    [Tooltip("靜止值（預設 0）")]
    public float restValue = 0f;
    [Tooltip("第一次彈到的峰值")]
    public float firstPeak = 12f;
    [Tooltip("每彈一次峰值遞減多少：12 → 10 → 8 …")]
    public float peakStep = 2f;
    [Tooltip("峰值低於此值就停止，直接回到靜止值")]
    public float minPeak = 1f;
    [Tooltip("彈上去（或掉回來）單程所需時間")]
    public float halfCycleDuration = 0.09f;
    [Tooltip("手還在範圍內時再次進入是否重播")]
    public bool retriggerOnReenter = true;

    private SkinnedMeshRenderer _skinned;
    private int _index = -1;
    private Sequence _blendSeq;

    private void Awake()
    {
        _skinned = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (_skinned == null)
        {
            Debug.LogWarning("[月餅麵團球] 找不到 SkinnedMeshRenderer，BlendShape 彈簧會被略過", this);
            return;
        }

        _index = MooncakeBlendShapeUtil.ResolveIndex(_skinned, blendShapeName, blendShapeIndexOverride);
        if (_index < 0)
            Debug.LogWarning($"[月餅麵團球] 找不到 BlendShape「{blendShapeName}」", this);
        else
            _skinned.SetBlendShapeWeight(_index, restValue);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _blendSeq?.Kill();
        _blendSeq = null;
    }

    private void OnDestroy()
    {
        _blendSeq?.Kill();
        DOTween.Kill(this);
    }

    protected override void HandleHandEnter(HandRef hand, bool firstHand)
    {
        if (!firstHand && !retriggerOnReenter) return;

        base.HandleHandEnter(hand, firstHand);   // 震動
        PlaySpringFeedback();
    }

    protected override void OnSpawnedInternal(GameObject spawned)
    {
        PlaySpringFeedback();
    }

    /// <summary>0 → 12 → 0 → 10 → 0 … 峰值遞減，最後停在靜止值。</summary>
    [Button("測試：彈簧回饋", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.3f)]
    public void PlaySpringFeedback()
    {
        if (_skinned == null || _index < 0) return;

        _blendSeq?.Kill();
        _skinned.SetBlendShapeWeight(_index, restValue);

        if (firstPeak <= minPeak) return;   // 沒有任何一次可彈，避免建立空的 Sequence

        float step = Mathf.Max(0.01f, peakStep);
        float dur = Mathf.Max(0.01f, halfCycleDuration);
        var seq = DOTween.Sequence().SetTarget(this);

        int guard = 0;
        for (float peak = firstPeak; peak > minPeak && guard < 64; peak -= step, guard++)
        {
            seq.Append(DOTween.To(GetWeight, SetWeight, peak, dur).SetEase(Ease.OutQuad));
            seq.Append(DOTween.To(GetWeight, SetWeight, restValue, dur).SetEase(Ease.InQuad));
        }

        seq.OnComplete(() => SetWeight(restValue));
        _blendSeq = seq;
    }

    /// <summary>立刻停止彈簧並回到靜止值。</summary>
    public void ResetBlendShape()
    {
        _blendSeq?.Kill();
        _blendSeq = null;
        SetWeight(restValue);
    }

    private float GetWeight()
    {
        return _skinned != null && _index >= 0 ? _skinned.GetBlendShapeWeight(_index) : 0f;
    }

    private void SetWeight(float v)
    {
        if (_skinned != null && _index >= 0) _skinned.SetBlendShapeWeight(_index, v);
    }
}

using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 中式月餅 - 拍打／包餡站（月餅-麵團-壓扁）。
///
/// 流程：
///   1. 拍打：手把每碰一次，Ball Shape 與 Flat 兩個 BlendShape 彈簧式往目標值推進，戳滿 N 次到底。
///   2. 到底後打開「內陷提示」，等待帶有餡料 Tag 的物件碰到提示的 Trigger。
///   3. 餡料放進去後關掉「內陷提示」、打開子物件「內陷」。
///   4. 包餡：再碰 N 次，BlendShape 慢慢回到最一開始的樣子。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeFlattenStation : MooncakeHandTarget
{
    public enum Phase
    {
        Flatten,      // 拍扁中
        WaitFilling,  // 等待放餡料
        Wrap,         // 包起來（BlendShape 回原狀）
        Done
    }

    [Header("BlendShape：Ball Shape")]
    public string ballShapeName = "Ball shape";
    public int ballShapeIndexOverride = -1;
    [Tooltip("一開始的值")]
    public float ballStart = 75.8f;
    [Tooltip("拍扁到底的值")]
    public float ballEnd = 0f;

    [Header("BlendShape：Flat")]
    public string flatShapeName = "flat";
    public int flatShapeIndexOverride = -1;
    [Tooltip("一開始的值")]
    public float flatStart = 0f;
    [Tooltip("拍扁到底的值")]
    public float flatEnd = 45f;

    [Header("要戳幾次")]
    public int pokesToFlatten = 5;
    public int pokesToWrap = 5;
    [Tooltip("兩次觸碰之間的最短間隔，避免同一下被算兩次")]
    public float pokeCooldown = 0.15f;

    [Header("彈簧手感")]
    [Tooltip("每一下彈過頭的進度比例（0.12 = 多推 12%）")]
    public float overshoot = 0.12f;
    public float springUpTime = 0.08f;
    public float springSettleTime = 0.35f;

    [Header("關聯物件")]
    [Tooltip("子物件「月餅-內陷提示」，拍扁到底時打開")]
    public GameObject hintObject;
    [Tooltip("子物件「月餅-內陷」，餡料放進去後打開")]
    public GameObject fillingObject;
    [Tooltip("掛在「月餅-內陷提示」上的接收器；留空會自動從 hintObject 抓")]
    public MooncakeDropSocket socket;
    [Tooltip("開場時強制套用起始 BlendShape 與物件開關狀態")]
    public bool applyStartOnAwake = true;

    [Header("餡料外觀")]
    [Tooltip("要換成餡料材質的物件（預設子物件「月餅-內陷」）")]
    public GameObject[] fillingVisuals;
    [Tooltip("包完之後把材質換回原本的提示材質")]
    public bool restoreMaterialOnWrapComplete = true;

    [Header("事件")]
    public UnityEvent onFlattenComplete;
    public MooncakeGameObjectEvent onFillingPlaced;
    [Tooltip("辨識出是哪一種餡料時送出 fillingId")]
    public MooncakeStringEvent onFillingIdentified;
    public UnityEvent onWrapComplete;

    [ShowInInspector, ReadOnly] private Phase _phase = Phase.Flatten;
    [ShowInInspector, ReadOnly] private int _pokes;

    private SkinnedMeshRenderer _skinned;
    private int _ballIndex = -1;
    private int _flatIndex = -1;
    private float _progress;      // 0 = 原狀，1 = 拍扁到底
    private float _lastPokeTime = -999f;
    private Tween _tween;

    // 每個 fillingVisual 底下所有 Renderer 的原始材質，用來還原
    private Renderer[] _fillingRenderers;
    private Material[][] _originalMaterials;

    public Phase CurrentPhase => _phase;

    /// <summary>這一輪用的是哪一種餡料。</summary>
    public string CurrentFillingId { get; private set; }
    public Material CurrentFillingMaterial { get; private set; }

    /// <summary>這一站是用碰的，不需要 Trigger 鍵。</summary>
    protected override bool UsesTriggerInput => false;

    private void Awake()
    {
        _skinned = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (_skinned == null)
        {
            Debug.LogWarning("[月餅壓扁站] 找不到 SkinnedMeshRenderer", this);
        }
        else
        {
            _ballIndex = MooncakeBlendShapeUtil.ResolveIndex(_skinned, ballShapeName, ballShapeIndexOverride);
            _flatIndex = MooncakeBlendShapeUtil.ResolveIndex(_skinned, flatShapeName, flatShapeIndexOverride);

            if (_ballIndex < 0) Debug.LogWarning($"[月餅壓扁站] 找不到 BlendShape「{ballShapeName}」", this);
            if (_flatIndex < 0) Debug.LogWarning($"[月餅壓扁站] 找不到 BlendShape「{flatShapeName}」", this);
        }

        if (socket == null && hintObject != null)
            socket = hintObject.GetComponentInChildren<MooncakeDropSocket>(true);

        if (socket != null) socket.Received += OnFillingPlaced;

        CacheFillingMaterials();

        if (applyStartOnAwake) ResetStation();
    }

    private void CacheFillingMaterials()
    {
        var renderers = new System.Collections.Generic.List<Renderer>();
        var originals = new System.Collections.Generic.List<Material[]>();

        if (fillingVisuals != null)
        {
            foreach (var go in fillingVisuals)
            {
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    renderers.Add(r);
                    originals.Add(r.sharedMaterials);
                }
            }
        }

        _fillingRenderers = renderers.ToArray();
        _originalMaterials = originals.ToArray();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _tween?.Kill();
        _tween = null;
    }

    private void OnDestroy()
    {
        if (socket != null) socket.Received -= OnFillingPlaced;
        _tween?.Kill();
        DOTween.Kill(this);
    }

    // ---------------- 拍打 / 包餡 ----------------

    protected override void HandleHandEnter(HandRef hand, bool firstHand)
    {
        if (Time.time - _lastPokeTime < pokeCooldown) return;
        Poke(hand);
    }

    /// <summary>戳一下：依目前階段推進拍打或包餡的進度。</summary>
    private void Poke(HandRef hand)
    {
        if (_phase != Phase.Flatten && _phase != Phase.Wrap) return;

        _lastPokeTime = Time.time;
        _pokes++;
        if (hand != null) SendHaptic(hand, hapticAmplitude, hapticDuration);

        if (_phase == Phase.Flatten)
        {
            int total = Mathf.Max(1, pokesToFlatten);
            float target = Mathf.Clamp01((float)_pokes / total);
            SpringTo(target);

            if (_pokes >= total) CompleteFlatten();
        }
        else
        {
            int total = Mathf.Max(1, pokesToWrap);
            float target = Mathf.Clamp01(1f - (float)_pokes / total);
            SpringTo(target);

            if (_pokes >= total) CompleteWrap();
        }
    }

    private void CompleteFlatten()
    {
        _phase = Phase.WaitFilling;
        _pokes = 0;

        if (hintObject != null) hintObject.SetActive(true);
        onFlattenComplete?.Invoke();
    }

    private void CompleteWrap()
    {
        _phase = Phase.Done;

        // 包起來了，內餡看不到了 → 換回原本的提示材質
        if (restoreMaterialOnWrapComplete) RestoreFillingMaterials();

        onWrapComplete?.Invoke();
    }

    /// <summary>由「月餅-內陷提示」上的 <see cref="MooncakeDropSocket"/> 呼叫。</summary>
    public void OnFillingPlaced(GameObject filling)
    {
        if (_phase != Phase.WaitFilling) return;

        _phase = Phase.Wrap;
        _pokes = 0;

        IdentifyFilling(filling);

        if (hintObject != null) hintObject.SetActive(false);
        if (fillingObject != null) fillingObject.SetActive(true);

        onFillingPlaced?.Invoke(filling);
    }

    // ---------------- 餡料辨識與材質切換 ----------------

    private void IdentifyFilling(GameObject filling)
    {
        CurrentFillingId = null;
        CurrentFillingMaterial = null;

        var kind = MooncakeFillingKind.Find(filling);
        if (kind != null)
        {
            CurrentFillingId = kind.fillingId;
            CurrentFillingMaterial = kind.ResolveMaterial();
        }
        else if (filling != null)
        {
            // 沒掛 MooncakeFillingKind 就退而求其次，直接拿它的材質
            var r = filling.GetComponentInChildren<Renderer>(true);
            if (r != null) CurrentFillingMaterial = r.sharedMaterial;
        }

        if (CurrentFillingMaterial != null) ApplyFillingMaterial(CurrentFillingMaterial);
        else Debug.LogWarning("[月餅壓扁站] 認不出餡料材質，維持原本的提示材質", this);

        if (!string.IsNullOrEmpty(CurrentFillingId))
        {
            Debug.Log($"[月餅壓扁站] 這一顆包的是「{CurrentFillingId}」", this);
            onFillingIdentified?.Invoke(CurrentFillingId);
        }
    }

    private void ApplyFillingMaterial(Material mat)
    {
        if (_fillingRenderers == null) return;

        foreach (var r in _fillingRenderers)
        {
            if (r == null) continue;

            var slots = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = mat;
            r.sharedMaterials = slots;
        }
    }

    private void RestoreFillingMaterials()
    {
        if (_fillingRenderers == null || _originalMaterials == null) return;

        for (int i = 0; i < _fillingRenderers.Length && i < _originalMaterials.Length; i++)
        {
            if (_fillingRenderers[i] == null) continue;
            _fillingRenderers[i].sharedMaterials = _originalMaterials[i];
        }
    }

    // ---------------- BlendShape ----------------

    /// <summary>彈簧式推到目標進度：先彈過頭，再彈性收回。</summary>
    private void SpringTo(float target)
    {
        _tween?.Kill();

        float from = _progress;
        float direction = Mathf.Sign(target - from);
        float peak = target + direction * Mathf.Max(0f, overshoot);

        var seq = DOTween.Sequence().SetTarget(this);
        seq.Append(DOTween.To(() => _progress, ApplyProgress, peak, Mathf.Max(0.01f, springUpTime))
            .SetEase(Ease.OutQuad));
        seq.Append(DOTween.To(() => _progress, ApplyProgress, target, Mathf.Max(0.01f, springSettleTime))
            .SetEase(Ease.OutElastic));

        _tween = seq;
    }

    private void ApplyProgress(float t)
    {
        _progress = t;
        if (_skinned == null) return;

        // 用 Unclamped，彈過頭時才看得出彈性
        if (_ballIndex >= 0) _skinned.SetBlendShapeWeight(_ballIndex, Mathf.LerpUnclamped(ballStart, ballEnd, t));
        if (_flatIndex >= 0) _skinned.SetBlendShapeWeight(_flatIndex, Mathf.LerpUnclamped(flatStart, flatEnd, t));
    }

    // ---------------- Debug ----------------

    [Button("Debug：戳一下", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugPoke()
    {
        _lastPokeTime = -999f;
        Poke(null);
    }

    [Button("Debug：直接拍扁到底", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugFlattenAll()
    {
        int guard = 0;
        while (_phase == Phase.Flatten && guard++ < 64) DebugPoke();
    }

    [Tooltip("Debug 按鈕要模擬放進來的餡料 Prefab（用來取得材質與 fillingId）")]
    public GameObject debugFillingPrefab;

    [Button("Debug：直接放餡料", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugPlaceFilling()
    {
        if (_phase != Phase.WaitFilling)
        {
            Debug.LogWarning($"[月餅壓扁站] 現在是 {_phase}，還不能放餡料", this);
            return;
        }
        OnFillingPlaced(debugFillingPrefab);
    }

    [Button("Debug：直接包完", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugWrapAll()
    {
        int guard = 0;
        while (_phase == Phase.Wrap && guard++ < 64) DebugPoke();
    }

    // ---------------- 重置 ----------------

    [Button("重置這一站", ButtonSizes.Large), GUIColor(1f, 0.5f, 0.3f)]
    public void ResetStation()
    {
        _tween?.Kill();
        _tween = null;

        _phase = Phase.Flatten;
        _pokes = 0;
        _lastPokeTime = -999f;

        ApplyProgress(0f);

        CurrentFillingId = null;
        CurrentFillingMaterial = null;
        RestoreFillingMaterials();

        if (socket != null) socket.ResetSocket();

        if (hintObject != null) hintObject.SetActive(false);
        if (fillingObject != null) fillingObject.SetActive(false);
    }
}

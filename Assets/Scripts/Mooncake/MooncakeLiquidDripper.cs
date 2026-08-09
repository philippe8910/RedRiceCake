using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;
using UnityEngine.XR.Interaction.Toolkit;              // SelectEnterEventArgs / SelectExitEventArgs
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 假液體滴落：拿起刷子後從刷頭滴下一顆顆小球，碰到東西就在落點留痕跡。
/// 走老派省效能路線 —— 剛體小球 + 半透明貼片，沒有流體模擬也沒有 decal projector。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeLiquidDripper : MonoBehaviour
{
    public enum TipAxis { 自動取最長軸, X, Y, Z }

    [Header("滴落來源")]
    [Tooltip("指定滴落點；留空就自動找刷頭")]
    public Transform dripPoint;
    [Tooltip("自動找刷頭：用網格 bounds 的最長軸端點，而不是物件原點")]
    public bool autoFindBrushTip = true;
    [Tooltip("沿哪個軸找端點")]
    public TipAxis tipAxis = TipAxis.自動取最長軸;
    [Tooltip("刷毛在該軸的負端還是正端。Brush.fbx 的刷毛在 -Z，所以預設勾選")]
    public bool tipAtNegativeEnd = true;
    [Tooltip("1 = 剛好在端點，調小往中間收，調大往外伸")]
    [Range(0f, 1.3f)] public float tipReach = 1f;
    [Tooltip("在自動找到的位置上再微調（刷子的 local 空間）")]
    public Vector3 dripLocalOffset = Vector3.zero;
    public GameObject dropPrefab;

    [Header("時機")]
    [Tooltip("只有被抓起來時才滴")]
    public bool dripOnlyWhenHeld = true;
    [Tooltip("開場就開始滴（dripOnlyWhenHeld 關掉時才有意義）")]
    public bool drippingAtStart = false;
    [Tooltip("每次抓起來最多滴幾滴，0 = 不限制")]
    public int dropsPerPickup = 0;

    [Header("節奏")]
    public float dripInterval = 0.25f;
    [Tooltip("間隔的隨機浮動，讓它不要太規律")]
    public float dripIntervalRandom = 0.12f;
    [Tooltip("出生點的隨機散佈半徑")]
    public float dripSpread = 0.008f;
    [Tooltip("初速（往下一點點，不要純自由落體）")]
    public float initialDownSpeed = 0.05f;

    [Header("效能上限")]
    [Tooltip("同時存在的液滴上限，超過就把最舊的收掉")]
    public int maxActiveDrops = 24;

    [Header("XR")]
    [Tooltip("留空會自動抓自己身上的 XRGrabInteractable")]
    public XRGrabInteractable grabInteractable;

    [Header("事件")]
    public UnityEvent onDripStart;
    public UnityEvent onDripStop;

    [ShowInInspector, ReadOnly] private bool _dripping;
    [ShowInInspector, ReadOnly] private int _droppedThisPickup;

    private float _timer;
    private readonly Queue<GameObject> _active = new Queue<GameObject>();

    public bool IsDripping => _dripping;

    private void Awake()
    {
        if (grabInteractable == null) grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(HandleGrabbed);
            grabInteractable.selectExited.AddListener(HandleReleased);
        }

        _dripping = !dripOnlyWhenHeld && drippingAtStart;
        _timer = NextInterval();
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(HandleGrabbed);
            grabInteractable.selectExited.RemoveListener(HandleReleased);
        }
    }

    private void HandleGrabbed(SelectEnterEventArgs args)
    {
        if (!dripOnlyWhenHeld) return;
        _droppedThisPickup = 0;
        SetDripping(true);
    }

    private void HandleReleased(SelectExitEventArgs args)
    {
        if (!dripOnlyWhenHeld) return;
        SetDripping(false);
    }

    public void SetDripping(bool value)
    {
        if (_dripping == value) return;

        _dripping = value;
        _timer = value ? NextInterval() : 0f;

        if (value) onDripStart?.Invoke();
        else onDripStop?.Invoke();
    }

    private void Update()
    {
        if (!_dripping) return;
        if (dropsPerPickup > 0 && _droppedThisPickup >= dropsPerPickup) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = NextInterval();
        SpawnDrop();
    }

    private float NextInterval()
    {
        return Mathf.Max(0.02f, dripInterval + Random.Range(-dripIntervalRandom, dripIntervalRandom));
    }

    /// <summary>Debug：不用抓著也能一直滴，再按一次停。</summary>
    [Button("@_dripping ? \"Debug：停止連續滴\" : \"Debug：開始連續滴\"", ButtonSizes.Large)]
    [GUIColor("@_dripping ? new Color(1f, 0.5f, 0.3f) : new Color(0.4f, 0.8f, 1f)")]
    public void DebugToggleDripping()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[月餅] 連續滴要在 Play Mode 下才會跑（靠 Update 計時）", this);
            return;
        }

        _droppedThisPickup = 0;      // 順便清掉每次拿起的滴數上限
        SetDripping(!_dripping);
    }

    [Button("Debug：滴一滴", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public GameObject SpawnDrop()
    {
        if (dropPrefab == null)
        {
            Debug.LogWarning($"[月餅] {name} 沒有指定 Drop Prefab", this);
            return null;
        }

        Vector3 pos = DripWorldPosition
                      + Random.insideUnitSphere * Mathf.Max(0f, dripSpread);

        // 不掛父物件，液滴大小才會跟 Prefab 一致
        var go = Instantiate(dropPrefab, pos, Random.rotation);
        go.transform.localScale = dropPrefab.transform.localScale;

        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) rb.velocity = Vector3.down * Mathf.Max(0f, initialDownSpeed);

        // 出生點就在刷頭裡面，不排除的話一生出來就撞到刷子、當場留下痕跡
        var drop = go.GetComponent<MooncakeLiquidDrop>();
        if (drop != null) drop.IgnoreSource(SourceColliders());

        _droppedThisPickup++;
        TrackAndTrim(go);
        return go;
    }

    // ---------------- 自動找刷頭 ----------------

    private Vector3 _tipLocal;
    private bool _tipResolved;

    /// <summary>滴落點的世界座標。</summary>
    public Vector3 DripWorldPosition
    {
        get
        {
            if (dripPoint != null) return dripPoint.TransformPoint(dripLocalOffset);

            if (!_tipResolved) ResolveTip();
            return transform.TransformPoint(_tipLocal + dripLocalOffset);
        }
    }

    /// <summary>
    /// 用網格 bounds 找出刷頭。Brush.fbx 的 isReadable 是 0，讀不到頂點，
    /// 但 sharedMesh.bounds 一定拿得到，所以走 bounds 的最長軸端點。
    /// </summary>
    [Button("重新尋找刷頭", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.3f)]
    public void ResolveTip()
    {
        _tipResolved = true;
        _tipLocal = Vector3.zero;

        if (!autoFindBrushTip) return;

        Bounds local = default;
        bool has = false;

        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            has |= Accumulate(mf.sharedMesh, mf.transform, ref local, has);

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            has |= Accumulate(smr.sharedMesh, smr.transform, ref local, has);

        if (!has) return;

        int axis = (int)tipAxis - 1;
        if (axis < 0)
        {
            Vector3 e = local.extents;
            axis = e.x >= e.y && e.x >= e.z ? 0 : (e.y >= e.z ? 1 : 2);
        }

        Vector3 dir = Vector3.zero;
        dir[axis] = tipAtNegativeEnd ? -1f : 1f;

        _tipLocal = local.center + Vector3.Scale(dir, local.extents) * tipReach;
    }

    private bool Accumulate(Mesh mesh, Transform t, ref Bounds local, bool has)
    {
        if (mesh == null) return false;

        Bounds b = mesh.bounds;   // 不需要 isReadable
        for (int i = 0; i < 8; i++)
        {
            var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
            Vector3 world = t.TransformPoint(b.center + Vector3.Scale(b.extents, sign));
            Vector3 p = transform.InverseTransformPoint(world);

            if (!has) { local = new Bounds(p, Vector3.zero); has = true; }
            else local.Encapsulate(p);
        }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        // 在 Scene 視窗直接看得到滴落點，不用進 Play Mode
        ResolveTip();
        Gizmos.color = new Color(1f, 0.85f, 0.3f);
        Gizmos.DrawWireSphere(DripWorldPosition, 0.012f);
    }

    private Collider[] _sourceColliders;

    /// <summary>刷子自己（含被抓著時的手把）的碰撞體，液滴要忽略它們。</summary>
    private Collider[] SourceColliders()
    {
        if (_sourceColliders == null || _sourceColliders.Length == 0)
            _sourceColliders = GetComponentsInChildren<Collider>(true);

        return _sourceColliders;
    }

    /// <summary>控制同時存在的數量，超過上限就收掉最舊的。</summary>
    private void TrackAndTrim(GameObject go)
    {
        _active.Enqueue(go);

        int limit = Mathf.Max(1, maxActiveDrops);
        while (_active.Count > limit)
        {
            var oldest = _active.Dequeue();
            if (oldest != null) Destroy(oldest);
        }
    }
}

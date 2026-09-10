using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 抓取來源：手把靠近會震動，按下 Trigger 生出一個 Prefab（帶彈簧縮放）。
/// 麵團桶、餡料碗都是用這個；麵團球再另外加 BlendShape 回饋。
/// </summary>
public class MooncakeSpawnSource : MooncakeHandTarget
{
    [Header("抓取生成")]
    [Tooltip("要抓出來的 Prefab（可自行替換）")]
    public GameObject spawnPrefab;
    [Tooltip("指定生成點；留空則依 Spawn At Hand 決定")]
    public Transform spawnPoint;
    [Tooltip("勾選：生在手上；不勾：生在自己身上")]
    public bool spawnAtHand = true;
    [Tooltip("相對於生成基準的位移（手部座標系 / 自身座標系）")]
    public Vector3 spawnOffset = new Vector3(0f, 0f, 0.02f);
    [Tooltip("生成後是否掛在手把底下（餡料建議打開，直接黏在手上帶走）")]
    public bool parentToHand = false;
    [Tooltip("最多可以抓幾次，0 = 不限制")]
    public int maxSpawnCount = 0;
    [Tooltip("兩次抓取之間的冷卻秒數")]
    public float respawnCooldown = 0.5f;

    [Header("抓取時的震動")]
    [Range(0f, 1f)] public float grabHapticAmplitude = 0.8f;
    public float grabHapticDuration = 0.15f;

    [Header("生成物彈簧")]
    [Tooltip("生成瞬間的起始縮放比例")]
    public float spawnStartScale = 0.2f;
    [Tooltip("彈過頭的倍率")]
    public float spawnOvershoot = 1.25f;
    public float spawnUpTime = 0.08f;
    public float spawnSettleTime = 0.45f;

    [Header("生成事件")]
    public MooncakeGameObjectEvent onSpawned;

    [Header("Debug（不用戴頭盔）")]
    [Tooltip("Debug 按鈕生出來的東西會慢慢飛去這個物件")]
    public GameObject debugFlyTarget;
    [Tooltip("飛過去要幾秒")]
    public float debugFlyDuration = 1.2f;
    [Tooltip("飛到之後，如果沒有被 trigger 收走就直接塞給目標的 DropSocket")]
    public bool debugForceDeliver = true;

    private int _spawnCount;
    private float _lastSpawnTime = -999f;

    protected override void HandleHandEnter(HandRef hand, bool firstHand)
    {
        SendHaptic(hand, hapticAmplitude, hapticDuration);
    }

    protected override void HandleTriggerPressed(HandRef hand)
    {
        Spawn(hand);
    }

    [Button("測試：抓出一個", ButtonSizes.Medium), GUIColor(0.3f, 0.8f, 1f)]
    public GameObject Spawn()
    {
        return Spawn(null);
    }

    [Header("麵團定位")]
    [Tooltip("生出來的麵團自動補上 MooncakeDoughAnchor：放開會停下來、靠近交接點會對正。\n" +
             "只對 Tag 是 DoughObject 的生成物作用，餡料不受影響")]
    public bool addDoughAnchor = true;

    /// <summary>
    /// 麵團 prefab 是球形碰撞體配 angularDrag 0.05，放到桌上會一路滾。
    ///
    /// 這件事刻意在生成時做、不寫進 prefab —— 這兩個 prefab 內含巢狀
    /// prefab 實例，用 SaveAsPrefabAsset 重存會把內部 fileID 全部重排，
    /// 場景裡指向它們的參考（flow.doughPiecePrefab）就會斷掉。踩過一次了。
    /// </summary>
    private void EnsureDoughAnchor(GameObject go)
    {
        if (!addDoughAnchor || go == null) return;
        if (!go.CompareTag("DoughObject")) return;
        if (go.GetComponent<Rigidbody>() == null) return;
        if (go.GetComponent<MooncakeDoughAnchor>() != null) return;

        go.AddComponent<MooncakeDoughAnchor>();
    }

    public GameObject Spawn(HandRef hand)
    {
        if (Time.time - _lastSpawnTime < respawnCooldown) return null;
        if (maxSpawnCount > 0 && _spawnCount >= maxSpawnCount) return null;

        if (spawnPrefab == null)
        {
            Debug.LogWarning($"[月餅] {name} 沒有指定 Spawn Prefab", this);
            return null;
        }

        Transform handRoot = hand != null ? hand.root : null;

        Vector3 pos;
        Quaternion rot;
        if (spawnPoint != null)
        {
            pos = spawnPoint.TransformPoint(spawnOffset);
            rot = spawnPoint.rotation;
        }
        else if (spawnAtHand && handRoot != null)
        {
            pos = handRoot.TransformPoint(spawnOffset);
            rot = handRoot.rotation;
        }
        else
        {
            pos = transform.TransformPoint(spawnOffset);
            rot = spawnPrefab.transform.rotation;
        }

        var go = Instantiate(spawnPrefab, pos, rot);

        EnsureDoughAnchor(go);

        if (parentToHand && handRoot != null)
        {
            go.transform.SetParent(handRoot, true);

            // 掛在手上就不能再受重力，不然照樣會掉下去
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
            }
        }

        _spawnCount++;
        _lastSpawnTime = Time.time;

        PlaySpawnSpring(go.transform);
        SendHaptic(hand, grabHapticAmplitude, grabHapticDuration);
        OnSpawnedInternal(go);
        onSpawned?.Invoke(go);

        return go;
    }

    /// <summary>子類別可在生成後追加表現（例如麵團球的 BlendShape 回彈）。</summary>
    protected virtual void OnSpawnedInternal(GameObject spawned) { }

    /// <summary>抓出來的東西：縮小 → 彈過頭 → 彈性回到原尺寸。</summary>
    protected void PlaySpawnSpring(Transform target)
    {
        if (target == null) return;

        Vector3 baseScale = target.localScale;
        target.localScale = baseScale * Mathf.Max(0.001f, spawnStartScale);

        DOTween.Kill(target);
        DOTween.Sequence()
            .SetTarget(target)
            .Append(target.DOScale(baseScale * Mathf.Max(1f, spawnOvershoot), Mathf.Max(0.01f, spawnUpTime))
                .SetEase(Ease.OutQuad))
            .Append(target.DOScale(baseScale, Mathf.Max(0.01f, spawnSettleTime))
                .SetEase(Ease.OutElastic));
    }

    // ---------------- Debug ----------------

    /// <summary>Debug：生一個出來，讓它慢慢飛到 debugFlyTarget，抵達後觸發那邊的 DropSocket。</summary>
    [Button("Debug：生成並飛向目標", ButtonSizes.Large), GUIColor(0.6f, 0.9f, 0.6f)]
    public GameObject DebugSpawnAndFly()
    {
        float savedCooldown = respawnCooldown;
        respawnCooldown = 0f;                 // Debug 不要被冷卻擋住
        var go = Spawn(null);
        respawnCooldown = savedCooldown;

        if (go == null) return null;

        // 飛行途中不要受重力影響
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (debugFlyTarget == null)
        {
            Debug.LogWarning($"[月餅] {name} 沒有指定 Debug Fly Target，生成物停在原地", this);
            return go;
        }

        var target = debugFlyTarget;
        go.transform.DOMove(target.transform.position, Mathf.Max(0.05f, debugFlyDuration))
            .SetEase(Ease.InOutSine)
            .OnComplete(() => DeliverTo(go, target));

        return go;
    }

    private void DeliverTo(GameObject go, GameObject target)
    {
        if (!debugForceDeliver) return;
        if (go == null || target == null) return;   // 已經被 trigger 收走了

        var socket = target.GetComponentInChildren<MooncakeDropSocket>(true);
        if (socket != null) socket.Receive(go);
    }

    /// <summary>重置抓取次數上限的計數。</summary>
    public void ResetSpawnCount()
    {
        _spawnCount = 0;
        _lastSpawnTime = -999f;
    }
}

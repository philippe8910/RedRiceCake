using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

/// <summary>
/// 中式月餅整條流程的串接器。
///
///   拍打／包餡完成（麵團復原）
///     → 生出一顆可 XR 抓取的麵團、打開模具提示、收起壓扁站
///   麵團塞進模具提示
///     → 模具提示關、模具上的完成體開
///   帶著模具碰烤盤欄位
///     → 欄位關、對應的「月餅-放置」開
///   還不滿三顆
///     → 重置並重新打開壓扁站，繼續下一輪
/// </summary>
[DisallowMultipleComponent]
public class MooncakeChineseFlow : MonoBehaviour
{
    [Header("站點")]
    [Tooltip("「月餅-麵團球」上的抓取來源")]
    public MooncakeSpawnSource doughBall;
    [Tooltip("「月餅-麵團-提示」上的交接提示：收到麵團球後才打開壓扁站")]
    public MooncakeHandoffHint doughHint;
    public MooncakeFlattenStation flattenStation;
    public MooncakeMoldStation moldStation;
    [Tooltip("烤盤上的三個提示欄位")]
    public MooncakeTraySlot[] traySlots;
    [Tooltip("烤盤：三顆放滿之後才開放抓取")]
    public MooncakeBakingPan bakingPan;
    public MooncakeBakeOven ovenStation;

    [Header("抓取用麵團")]
    [Tooltip("跟「月餅-麵團-壓扁」同一個模型、可 XR Grab 的 Prefab")]
    public GameObject doughPiecePrefab;
    [Tooltip("生成位置；留空則用壓扁站自己的位置")]
    public Transform doughSpawnPoint;
    [Tooltip("相對生成位置的世界位移")]
    public Vector3 doughSpawnOffset = new Vector3(0f, 0.08f, 0f);

    [Header("行為")]
    [Tooltip("麵團抓出來後把壓扁站收起來，放到烤盤後再重新打開")]
    public bool hideFlattenStationWhileCarrying = true;
    [Tooltip("下一輪從「月餅-麵團-提示」重新開始（要再抓一次麵團球）。關掉則直接打開壓扁站")]
    public bool restartFromDoughHint = true;

    [Header("烘烤")]
    [Tooltip("要烤幾輪才算完成：第 1 輪出爐後刷蛋液，第 2 輪出爐才變成完成體")]
    public int bakesBeforeDone = 2;

    [Header("事件")]
    public UnityEvent onCycleStart;
    public MooncakeGameObjectEvent onDoughPieceSpawned;
    public UnityEvent onAllPlaced;
    [Tooltip("第一輪出爐，可以開始刷蛋液了")]
    public UnityEvent onEggWashStart;
    [Tooltip("三個都刷到蛋液，可以再放回烤箱了")]
    public UnityEvent onEggWashComplete;
    [Tooltip("最後一輪烤完，月餅換成完成體")]
    public UnityEvent onAllBaked;

    /// <summary>已經放到烤盤上的月餅數量。</summary>
    public int PlacedCount { get; private set; }

    private GameObject _currentPiece;

    private void Awake()
    {
        if (flattenStation != null)
            flattenStation.onWrapComplete.AddListener(HandleWrapComplete);

        if (ovenStation != null)
            ovenStation.onBakeComplete.AddListener(HandleBakeComplete);

        if (traySlots != null)
        {
            foreach (var slot in traySlots)
            {
                if (slot == null) continue;
                slot.Placed += HandleSlotPlaced;
                slot.EggWashed += HandleSlotEggWashed;
            }
        }
    }

    private void OnDestroy()
    {
        if (flattenStation != null)
            flattenStation.onWrapComplete.RemoveListener(HandleWrapComplete);

        if (ovenStation != null)
            ovenStation.onBakeComplete.RemoveListener(HandleBakeComplete);

        if (traySlots != null)
        {
            foreach (var slot in traySlots)
            {
                if (slot == null) continue;
                slot.Placed -= HandleSlotPlaced;
                slot.EggWashed -= HandleSlotEggWashed;
            }
        }
    }

    // ---------------- 烘烤 / 刷蛋液 ----------------

    /// <summary>已經烤完幾輪。</summary>
    public int BakeRound { get; private set; }

    private void HandleBakeComplete()
    {
        BakeRound++;

        if (BakeRound < Mathf.Max(1, bakesBeforeDone))
        {
            // 第一輪出爐：先不變完成體，開放刷蛋液
            if (traySlots != null)
            {
                foreach (var slot in traySlots)
                {
                    if (slot != null) slot.EnableEggWash(true);
                }
            }

            // 三個都刷到之前，烤箱不收烤盤
            if (ovenStation != null) ovenStation.SetAcceptPan(false);

            onEggWashStart?.Invoke();
            return;
        }

        // 最後一輪：換成烤過的完成體
        if (traySlots != null)
        {
            foreach (var slot in traySlots)
            {
                if (slot != null) slot.SetBaked(true);
            }
        }

        onAllBaked?.Invoke();
    }

    private void HandleSlotEggWashed(MooncakeTraySlot slot)
    {
        if (!AllEggWashed()) return;

        // 三個都沾到蛋液了 → 放行，可以再送進烤箱
        if (ovenStation != null) ovenStation.SetAcceptPan(true);
        onEggWashComplete?.Invoke();
    }

    private bool AllEggWashed()
    {
        if (traySlots == null || traySlots.Length == 0) return false;

        foreach (var slot in traySlots)
        {
            if (slot == null) continue;
            if (slot.IsFilled && !slot.IsEggWashed) return false;
        }
        return true;
    }

    // ---------------- 麵團做好 ----------------

    private void HandleWrapComplete()
    {
        SpawnDoughPiece();

        if (moldStation != null) moldStation.OpenHint();

        if (hideFlattenStationWhileCarrying && flattenStation != null)
            flattenStation.gameObject.SetActive(false);
    }

    [Button("測試：生出抓取用麵團", ButtonSizes.Medium), GUIColor(0.3f, 0.8f, 1f)]
    public GameObject SpawnDoughPiece()
    {
        if (doughPiecePrefab == null)
        {
            Debug.LogWarning("[月餅流程] 沒有指定 Dough Piece Prefab", this);
            return null;
        }

        Transform origin = doughSpawnPoint != null
            ? doughSpawnPoint
            : (flattenStation != null ? flattenStation.transform : transform);

        _currentPiece = Instantiate(doughPiecePrefab, origin.position + doughSpawnOffset, origin.rotation);
        onDoughPieceSpawned?.Invoke(_currentPiece);
        return _currentPiece;
    }

    // ---------------- 放到烤盤 ----------------

    private void HandleSlotPlaced(MooncakeTraySlot slot)
    {
        PlacedCount++;

        int total = traySlots != null ? traySlots.Length : 0;
        if (total > 0 && PlacedCount >= total)
        {
            // 三顆都放滿了 → 烤盤現在才能拿起來送進烤箱
            if (bakingPan != null) bakingPan.SetGrabbable(true);
            onAllPlaced?.Invoke();
            return;
        }

        StartNextCycle();
    }

    /// <summary>重置壓扁站並重新打開，讓玩家做下一顆。</summary>
    [Button("開始下一輪", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.3f)]
    public void StartNextCycle()
    {
        if (flattenStation != null) flattenStation.ResetStation();

        if (restartFromDoughHint && doughHint != null)
        {
            // 提示打開、壓扁站關著，玩家要再去抓一顆麵團球
            doughHint.ResetHint();
        }
        else if (flattenStation != null)
        {
            flattenStation.gameObject.SetActive(true);
        }

        if (moldStation != null) moldStation.ResetStation();

        onCycleStart?.Invoke();
    }

    // ================= Debug：不用戴頭盔，逐步或一鍵跑完 =================

    [FoldoutGroup("Debug"), Tooltip("一鍵模式每一步之間等幾秒，讓動畫看得完")]
    public float debugStepDelay = 1.4f;

    [FoldoutGroup("Debug"), Button("① 抓麵團球 → 飛到麵團提示", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugStep1_GrabDough()
    {
        if (doughBall != null) doughBall.DebugSpawnAndFly();
        else if (doughHint != null) doughHint.DebugHandoff();
        else Debug.LogWarning("[月餅流程] 沒有指定 Dough Ball / Dough Hint", this);
    }

    [FoldoutGroup("Debug"), Button("② 拍扁到底", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugStep2_Flatten()
    {
        if (flattenStation != null) flattenStation.DebugFlattenAll();
    }

    [FoldoutGroup("Debug"), Button("③ 放餡料", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugStep3_Filling()
    {
        if (flattenStation != null) flattenStation.DebugPlaceFilling();
    }

    [FoldoutGroup("Debug"), Button("④ 包完（生出可抓麵團）", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugStep4_Wrap()
    {
        if (flattenStation != null) flattenStation.DebugWrapAll();
    }

    [FoldoutGroup("Debug"), Button("⑤ 麵團放入模具", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugStep5_Mold()
    {
        if (_currentPiece != null) Destroy(_currentPiece);
        if (moldStation != null) moldStation.DebugReceiveDough();
    }

    [FoldoutGroup("Debug"), Button("⑥ 模具放上烤盤下一格", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugStep6_PlaceOnTray()
    {
        var slot = NextEmptySlot();
        if (slot == null)
        {
            Debug.LogWarning("[月餅流程] 烤盤已經滿了", this);
            return;
        }
        slot.Place(moldStation);
    }

    [FoldoutGroup("Debug"), Button("⑦ 烤盤送進烤箱（關門→倒數→開門）", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugStep7_Bake()
    {
        if (NextEmptySlot() != null)
        {
            Debug.LogWarning("[月餅流程] 還沒放滿三顆，烤盤不能進烤箱", this);
            return;
        }
        if (ovenStation != null) ovenStation.DebugInsertPan();
        else Debug.LogWarning("[月餅流程] 沒有指定 Oven Station", this);
    }

    [FoldoutGroup("Debug"), Button("⑧ 三顆都刷上蛋液", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugStep8_EggWash()
    {
        if (traySlots == null) return;
        foreach (var slot in traySlots)
        {
            if (slot != null && slot.IsFilled) slot.DebugEggWash();
        }
    }

    [FoldoutGroup("Debug"), Button("⑨ 再送進烤箱（第二輪）", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugStep9_BakeAgain()
    {
        if (!AllEggWashed())
        {
            Debug.LogWarning("[月餅流程] 還沒三個都刷到蛋液", this);
            return;
        }
        if (ovenStation != null)
        {
            ovenStation.SetAcceptPan(true);
            ovenStation.DebugInsertPan();
        }
    }

    [FoldoutGroup("Debug"), Button("一鍵跑完一顆", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f)]
    public void DebugRunOneCake()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[月餅流程] 一鍵模式要在 Play Mode 下按（要跑動畫與生成）", this);
            return;
        }
        StopAllCoroutines();
        StartCoroutine(RunOneCakeRoutine());
    }

    [FoldoutGroup("Debug"), Button("一鍵跑完三顆", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f)]
    public void DebugRunAllCakes()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[月餅流程] 一鍵模式要在 Play Mode 下按（要跑動畫與生成）", this);
            return;
        }
        StopAllCoroutines();
        StartCoroutine(RunAllRoutine());
    }

    private System.Collections.IEnumerator RunOneCakeRoutine()
    {
        float d = Mathf.Max(0.1f, debugStepDelay);

        DebugStep1_GrabDough();
        // 麵團要先飛過去，多等一點
        yield return new WaitForSeconds(d + (doughBall != null ? doughBall.debugFlyDuration : 0f));

        DebugStep2_Flatten();
        yield return new WaitForSeconds(d);

        DebugStep3_Filling();
        yield return new WaitForSeconds(d);

        DebugStep4_Wrap();
        yield return new WaitForSeconds(d);

        DebugStep5_Mold();
        yield return new WaitForSeconds(d);

        DebugStep6_PlaceOnTray();
    }

    private System.Collections.IEnumerator RunAllRoutine()
    {
        int total = traySlots != null ? traySlots.Length : 0;
        int guard = 0;

        while (NextEmptySlot() != null && guard++ < total + 2)
        {
            yield return RunOneCakeRoutine();
            yield return new WaitForSeconds(Mathf.Max(0.1f, debugStepDelay));
        }

        // 三顆都好了 → 第一輪烘烤 → 刷蛋液 → 第二輪烘烤
        float d = Mathf.Max(0.1f, debugStepDelay);

        DebugStep7_Bake();
        yield return new WaitForSeconds(d);
        yield return new WaitUntil(() => ovenStation == null || ovenStation.BakeCount >= 1);
        yield return new WaitForSeconds(d);

        DebugStep8_EggWash();
        yield return new WaitForSeconds(d);

        DebugStep9_BakeAgain();
    }

    private MooncakeTraySlot NextEmptySlot()
    {
        if (traySlots == null) return null;
        foreach (var slot in traySlots)
        {
            if (slot != null && !slot.IsFilled) return slot;
        }
        return null;
    }

    /// <summary>整條流程重來（含烤盤）。</summary>
    [Button("整條流程重置", ButtonSizes.Large), GUIColor(1f, 0.5f, 0.3f)]
    public void ResetAll()
    {
        PlacedCount = 0;

        if (_currentPiece != null) Destroy(_currentPiece);

        BakeRound = 0;
        if (ovenStation != null) ovenStation.ResetStation();

        if (traySlots != null)
        {
            foreach (var slot in traySlots)
            {
                if (slot != null) slot.ResetSlot();
            }
        }

        StartNextCycle();
    }
}

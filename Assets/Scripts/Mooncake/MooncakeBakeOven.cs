using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// 烘烤站。烤盤放進烤箱內部 → oven_door2 自動關到 (0,0,0) → 圓餅倒數 → 倒數結束把門轉回原本的開啟角度。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeBakeOven : MonoBehaviour
{
    [Header("門")]
    [Tooltip("從這裡往下找門；通常指向烤箱的根 Transform")]
    public Transform doorSearchRoot;
    [Tooltip("門的物件名稱，會在 doorSearchRoot 底下用名字找")]
    public string doorName = "oven_door2";
    [Tooltip("找不到名字時用的備援參照")]
    public Transform door;
    [Tooltip("關起來時的 local 旋轉")]
    public Vector3 closedEuler = Vector3.zero;
    [Tooltip("打開時的 local 旋轉；勾了下面那個就改用開場當下的角度")]
    public Vector3 openEuler = new Vector3(88.866f, 0f, 0f);
    public bool captureOpenEulerOnAwake = true;
    public float doorDuration = 0.8f;

    [Header("偵測")]
    [Tooltip("烤箱內部的偵測區（掛 MooncakeDropSocket 的那個子物件）")]
    public MooncakeDropSocket ovenZone;
    [Tooltip("烤盤放進去後要對齊的位置")]
    public Transform panAnchor;
    [Tooltip("放進去後把烤盤固定住，不讓玩家再拖走")]
    public bool lockPanInside = true;
    [Tooltip("烤完後重新開放抓取，讓玩家可以取出")]
    public bool unlockPanAfterBake = true;

    [Header("開門後推出烤盤")]
    [Tooltip("門開完之後把烤盤滑出來")]
    public bool slidePanOutAfterBake = true;
    [Tooltip("滑出去的目標 local X")]
    public float panSlideLocalX = 0.6f;
    public float panSlideDuration = 1f;

    [Header("開門煙霧")]
    [Tooltip("煙霧範本；留空會依下面的名稱在場景裡找（含未啟用的物件）。每次開門都會複製一份出來")]
    public ParticleSystem ovenSmoke;
    [Tooltip("場景裡那顆煙霧物件的名稱")]
    public string ovenSmokeName = "烤箱粒子位置";
    [Tooltip("開場把範本關掉，它只當位置用，不自己播")]
    public bool silenceSmokeOnAwake = true;
    [Tooltip("播完之後自動刪掉複製出來的那份")]
    public bool destroySmokeWhenFinished = true;

    [Header("倒數")]
    [Tooltip("圓餅倒數（Image 用 Filled / Radial360）")]
    public SimpleCountdownTimer countdown;
    [Tooltip("倒數的顯示物件，開場關閉、烘烤時才打開")]
    public GameObject countdownVisual;

    [Header("事件")]
    public UnityEvent onPanInserted;
    public UnityEvent onDoorClosed;
    public UnityEvent onBakeComplete;
    public UnityEvent onDoorOpened;
    [Tooltip("烤盤完全滑出來之後")]
    public UnityEvent onPanSlidOut;

    [ShowInInspector, ReadOnly] private bool _baking;
    [ShowInInspector, ReadOnly] private int _bakeCount;
    [ShowInInspector, ReadOnly] private bool _acceptPan = true;

    private MooncakeBakingPan _pan;
    private Tween _doorTween;
    private Tween _panTween;

    public bool IsBaking => _baking;
    /// <summary>已經烤完幾輪（刷蛋液前 1 輪、刷完再 1 輪）。</summary>
    public int BakeCount => _bakeCount;
    public bool AcceptingPan => _acceptPan;

    /// <summary>由流程控制：還沒刷完蛋液就不收烤盤。</summary>
    public void SetAcceptPan(bool value)
    {
        _acceptPan = value;

        if (value)
        {
            _baking = false;
            if (ovenZone != null) ovenZone.ResetSocket();   // socket 是 oneShot，要放行才收得到
        }
    }

    private void Awake()
    {
        ResolveDoor();

        if (door != null && captureOpenEulerOnAwake)
            openEuler = door.localEulerAngles;

        if (ovenZone != null) ovenZone.Received += HandlePanInserted;

        if (countdown != null) countdown.onCountdownComplete.AddListener(HandleBakeFinished);
        if (countdownVisual != null) countdownVisual.SetActive(false);

        ResolveSmoke();
    }

    private void ResolveSmoke()
    {
        if (ovenSmoke == null && !string.IsNullOrEmpty(ovenSmokeName))
        {
            // 含未啟用的物件一起找，這樣那顆煙霧可以在場景裡先關著當定位用
            foreach (var ps in Resources.FindObjectsOfTypeAll<ParticleSystem>())
            {
                if (ps.gameObject.scene.IsValid() && ps.name == ovenSmokeName)
                {
                    ovenSmoke = ps;
                    break;
                }
            }
        }

        if (ovenSmoke == null)
        {
            Debug.LogWarning($"[月餅烤箱] 場景裡找不到煙霧物件「{ovenSmokeName}」", this);
            return;
        }

        // 範本只當位置用：關掉它，Play On Awake 就不會在進場時噴一次
        if (silenceSmokeOnAwake && ovenSmoke.gameObject.activeSelf)
            ovenSmoke.gameObject.SetActive(false);
    }

    /// <summary>
    /// 開門時噴一次煙。這顆粒子的 rateOverTime 是 0、只靠 time=0 的一次性 burst，
    /// 同一幀 Stop→Play 重啟 burst 並不可靠，所以每次都複製一份新的出來播。
    /// </summary>
    [Button("Debug：噴一次烤箱煙霧", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public GameObject PlayOvenSmoke()
    {
        if (ovenSmoke == null) ResolveSmoke();
        if (ovenSmoke == null) return null;

        var src = ovenSmoke.transform;

        // 不掛父物件，尺寸才會跟範本一致
        var go = Instantiate(ovenSmoke.gameObject, src.position, src.rotation);
        go.transform.localScale = src.localScale;
        go.name = ovenSmoke.name + " (Playing)";
        go.SetActive(true);

        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null) ps.Play(true);

        if (destroySmokeWhenFinished) Destroy(go, SmokeLifetime(ps));
        return go;
    }

    private static float SmokeLifetime(ParticleSystem ps)
    {
        if (ps == null) return 3f;

        var main = ps.main;
        return main.duration + main.startLifetime.constantMax + 0.5f;
    }

    private void OnDestroy()
    {
        if (ovenZone != null) ovenZone.Received -= HandlePanInserted;
        if (countdown != null) countdown.onCountdownComplete.RemoveListener(HandleBakeFinished);
        _doorTween?.Kill();
        _panTween?.Kill();
    }

    private void ResolveDoor()
    {
        if (doorSearchRoot == null) return;

        foreach (var t in doorSearchRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != doorName) continue;
            door = t;
            return;
        }

        if (door == null)
            Debug.LogWarning($"[月餅烤箱] 在 {doorSearchRoot.name} 底下找不到「{doorName}」", this);
    }

    // ---------------- 流程 ----------------

    private void HandlePanInserted(GameObject pan)
    {
        if (_baking || !_acceptPan) return;

        _pan = pan != null ? pan.GetComponentInParent<MooncakeBakingPan>() : null;
        if (_pan == null) _pan = FindObjectOfType<MooncakeBakingPan>();

        _baking = true;
        onPanInserted?.Invoke();

        if (lockPanInside && _pan != null) _pan.LockInPlace(panAnchor);

        CloseDoor(StartBaking);
    }

    private void StartBaking()
    {
        onDoorClosed?.Invoke();

        if (countdownVisual != null) countdownVisual.SetActive(true);

        if (countdown != null) countdown.StartCountdown();
        else
        {
            Debug.LogWarning("[月餅烤箱] 沒有指定倒數計時器，直接視為烤好", this);
            HandleBakeFinished();
        }
    }

    private void HandleBakeFinished()
    {
        if (!_baking) return;

        _baking = false;
        _bakeCount++;
        _acceptPan = false;   // 下一輪要由流程放行

        if (countdownVisual != null) countdownVisual.SetActive(false);

        onBakeComplete?.Invoke();

        OpenDoor(() =>
        {
            onDoorOpened?.Invoke();

            // 門開完才把烤盤推出來，最後才開放抓取
            SlidePanOut(() =>
            {
                if (unlockPanAfterBake && _pan != null) _pan.SetGrabbable(true);
                onPanSlidOut?.Invoke();
            });
        });
    }

    /// <summary>烤盤沿著 local X 滑出烤箱。</summary>
    private void SlidePanOut(TweenCallback onDone)
    {
        Transform t = _pan != null ? _pan.transform : null;

        if (!slidePanOutAfterBake || t == null)
        {
            onDone?.Invoke();
            return;
        }

        _panTween?.Kill();
        _panTween = t.DOLocalMoveX(panSlideLocalX, Mathf.Max(0.01f, panSlideDuration))
            .SetEase(Ease.OutCubic)
            .OnComplete(onDone);
    }

    // ---------------- 門 ----------------

    private void CloseDoor(TweenCallback onDone = null)
    {
        RotateDoor(closedEuler, onDone);
    }

    private void OpenDoor(TweenCallback onDone = null)
    {
        // 只要門一開就噴煙，實機與 Debug 開門走同一條路徑
        PlayOvenSmoke();
        RotateDoor(openEuler, onDone);
    }

    private void RotateDoor(Vector3 euler, TweenCallback onDone)
    {
        if (door == null)
        {
            onDone?.Invoke();
            return;
        }

        _doorTween?.Kill();
        _doorTween = door.DOLocalRotate(euler, Mathf.Max(0.01f, doorDuration))
            .SetEase(Ease.OutCubic)
            .OnComplete(onDone);
    }

    // ---------------- Debug ----------------

    [Button("Debug：關門", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugCloseDoor()
    {
        ResolveDoor();
        CloseDoor(null);
    }

    [Button("Debug：開門", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugOpenDoor()
    {
        ResolveDoor();
        OpenDoor(null);
    }

    [Button("Debug：模擬放入烤盤（跑完整套）", ButtonSizes.Large), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugInsertPan()
    {
        HandlePanInserted(null);
    }

    [Button("Debug：直接烤完", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugFinishBake()
    {
        if (countdown != null) countdown.StopCountdown();
        _baking = true;
        HandleBakeFinished();
    }

    [Button("Debug：推出烤盤", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugSlidePanOut()
    {
        if (_pan == null) _pan = FindObjectOfType<MooncakeBakingPan>();
        SlidePanOut(null);
    }

    [Button("重置烤箱", ButtonSizes.Medium), GUIColor(1f, 0.5f, 0.3f)]
    public void ResetStation()
    {
        _doorTween?.Kill();
        _panTween?.Kill();

        // 烤盤推出去的位移也要收回來
        if (_pan != null && _pan.transform.parent == panAnchor && panAnchor != null)
        {
            var lp = _pan.transform.localPosition;
            _pan.transform.localPosition = new Vector3(0f, lp.y, lp.z);
        }

        _baking = false;
        _bakeCount = 0;
        _acceptPan = true;
        _pan = null;

        if (ovenZone != null) ovenZone.ResetSocket();
        if (countdown != null) countdown.ResetCountdown();
        if (countdownVisual != null) countdownVisual.SetActive(false);

        if (door != null) door.localEulerAngles = openEuler;
    }
}

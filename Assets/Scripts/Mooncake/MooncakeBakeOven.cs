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
    [ShowInInspector, ReadOnly] private bool _baked;

    private MooncakeBakingPan _pan;
    private Tween _doorTween;
    private Tween _panTween;

    public bool IsBaking => _baking;
    public bool HasBaked => _baked;

    private void Awake()
    {
        ResolveDoor();

        if (door != null && captureOpenEulerOnAwake)
            openEuler = door.localEulerAngles;

        if (ovenZone != null) ovenZone.Received += HandlePanInserted;

        if (countdown != null) countdown.onCountdownComplete.AddListener(HandleBakeFinished);
        if (countdownVisual != null) countdownVisual.SetActive(false);
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
        if (_baking || _baked) return;

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
        _baked = true;

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
        _baked = false;
        _pan = null;

        if (ovenZone != null) ovenZone.ResetSocket();
        if (countdown != null) countdown.ResetCountdown();
        if (countdownVisual != null) countdownVisual.SetActive(false);

        if (door != null) door.localEulerAngles = openEuler;
    }
}

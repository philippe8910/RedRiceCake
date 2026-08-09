using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 通用「放進來」接收器：掛在提示物件上，等指定 Tag 的東西碰進 trigger。
/// 內陷提示（收餡料）、模具提示（收麵團）共用同一支。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeDropSocket : MonoBehaviour
{
    [Tooltip("接受哪個 Tag 的物件")]
    public string acceptTag = "Filling";

    [Tooltip("收到後是否把該物件刪掉")]
    public bool consumeOnReceive = true;

    [Tooltip("只接收一次，之後不再反應（可用 ResetSocket 重新開放）")]
    public bool oneShot = true;

    [Tooltip("觸發範圍（世界公尺）。大於 0 時會在 Awake 依實際縮放重算 SphereCollider 半徑")]
    public float triggerWorldRadius = 0f;

    [Header("事件")]
    public MooncakeGameObjectEvent onReceived;

    /// <summary>程式端訂閱用（不序列化）。</summary>
    public event System.Action<GameObject> Received;

    private bool _got;

    public bool HasReceived => _got;

    private void Awake()
    {
        if (triggerWorldRadius > 0f)
            MooncakeColliderUtil.FitTrigger(GetComponent<SphereCollider>(), triggerWorldRadius);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_got && oneShot) return;
        if (!other.CompareTag(acceptTag)) return;

        // 碰撞體可能在子物件上，優先取 Rigidbody 所在的那一層
        var obj = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        Receive(obj);
    }

    /// <summary>不檢查 Tag 直接收下（Debug 按鈕、或飛過來的物件沒吃到 trigger 時的保險）。</summary>
    public void Receive(GameObject obj)
    {
        if (_got && oneShot) return;

        _got = true;

        Received?.Invoke(obj);
        onReceived?.Invoke(obj);

        if (consumeOnReceive && obj != null) Destroy(obj);
    }

    [Button("Debug：強制接收", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void ForceReceive()
    {
        Receive(null);
    }

    /// <summary>重新開放接收。</summary>
    public void ResetSocket()
    {
        _got = false;
    }
}

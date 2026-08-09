using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 交接提示：東西放進來就把提示收起來、打開下一個站點。
/// 例如「月餅-麵團-提示」收到麵團球 Prefab → 自己關閉、打開「月餅-麵團-壓扁」。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeHandoffHint : MonoBehaviour
{
    [Tooltip("接收器；留空會抓自己身上的")]
    public MooncakeDropSocket socket;

    [Tooltip("要收起來的提示物件；留空 = 自己")]
    public GameObject hintObject;

    [Tooltip("收到東西後要打開的物件")]
    public GameObject targetObject;

    [Tooltip("開場時把提示打開、目標關閉")]
    public bool applyStartOnAwake = true;

    [Header("事件")]
    public MooncakeGameObjectEvent onReceived;

    /// <summary>程式端訂閱用（不序列化）。</summary>
    public event System.Action<GameObject> Received;

    public bool IsHandedOff { get; private set; }

    private void Awake()
    {
        if (socket == null) socket = GetComponent<MooncakeDropSocket>();
        if (hintObject == null) hintObject = gameObject;

        if (socket != null) socket.Received += HandleReceived;

        if (applyStartOnAwake) ResetHint();
    }

    private void OnDestroy()
    {
        if (socket != null) socket.Received -= HandleReceived;
    }

    private void HandleReceived(GameObject incoming)
    {
        if (IsHandedOff) return;
        IsHandedOff = true;

        if (targetObject != null) targetObject.SetActive(true);

        Received?.Invoke(incoming);
        onReceived?.Invoke(incoming);

        // 最後才關自己，前面的事件才收得到
        if (hintObject != null) hintObject.SetActive(false);
    }

    [Button("Debug：強制交接", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugHandoff()
    {
        HandleReceived(null);
    }

    /// <summary>回到「提示開著、目標關著」的狀態，讓流程可以重來。</summary>
    [Button("重置提示", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.3f)]
    public void ResetHint()
    {
        IsHandedOff = false;

        if (socket != null) socket.ResetSocket();
        if (targetObject != null) targetObject.SetActive(false);
        if (hintObject != null) hintObject.SetActive(true);
    }
}

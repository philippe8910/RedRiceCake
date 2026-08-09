using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

/// <summary>
/// 壓模站（月餅-模具握把）。
/// 麵團復原完成後打開底下的「月餅-提示」，玩家把抓取式麵團塞進去 →
/// 提示關閉、「月餅-完成體」打開，接著整個模具可以被 XR 抓起來拿到烤盤上。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeMoldStation : MonoBehaviour
{
    [Header("關聯物件")]
    [Tooltip("子物件「月餅-提示」，等待麵團放進來")]
    public GameObject hintObject;
    [Tooltip("子物件「月餅-完成體」，麵團放好後打開")]
    public GameObject moldedObject;
    [Tooltip("掛在提示上的接收器；留空會自動從 hintObject 抓")]
    public MooncakeDropSocket socket;

    [Header("抓取碰撞體")]
    [Tooltip("開場時把這顆 BoxCollider 自動套到模具的 Renderer 範圍上")]
    public bool autoFitGrabCollider = true;
    public BoxCollider grabCollider;
    [Tooltip("自動套用時要放大多少（1.1 = 外擴 10%，比較好抓）")]
    public float grabColliderPadding = 1.1f;

    [Header("事件")]
    public MooncakeGameObjectEvent onDoughReceived;
    public UnityEvent onMooncakeReleased;

    /// <summary>模具上目前有沒有一顆待放置的月餅。</summary>
    public bool HasMooncake { get; private set; }

    private void Awake()
    {
        if (socket == null && hintObject != null)
            socket = hintObject.GetComponentInChildren<MooncakeDropSocket>(true);

        if (socket != null) socket.Received += HandleDoughReceived;

        if (grabCollider == null) grabCollider = GetComponent<BoxCollider>();
        if (autoFitGrabCollider)
            MooncakeColliderUtil.FitToRenderers(grabCollider, gameObject, grabColliderPadding);

        ResetStation();
    }

    private void OnDestroy()
    {
        if (socket != null) socket.Received -= HandleDoughReceived;
    }

    /// <summary>由流程控制呼叫：麵團做好了，打開提示等玩家塞進來。</summary>
    [Button("打開提示", ButtonSizes.Medium), GUIColor(0.3f, 0.8f, 1f)]
    public void OpenHint()
    {
        if (socket != null) socket.ResetSocket();
        if (hintObject != null) hintObject.SetActive(true);
        if (moldedObject != null) moldedObject.SetActive(false);
        HasMooncake = false;
    }

    private void HandleDoughReceived(GameObject dough)
    {
        if (hintObject != null) hintObject.SetActive(false);
        if (moldedObject != null) moldedObject.SetActive(true);

        HasMooncake = true;
        onDoughReceived?.Invoke(dough);
    }

    /// <summary>由烤盤欄位呼叫：月餅已經放到烤盤上，模具清空。</summary>
    public void ReleaseMooncake()
    {
        if (!HasMooncake) return;

        HasMooncake = false;
        if (moldedObject != null) moldedObject.SetActive(false);
        onMooncakeReleased?.Invoke();
    }

    [Button("Debug：模擬放入麵團", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugReceiveDough()
    {
        HandleDoughReceived(null);
    }

    [Button("重置模具", ButtonSizes.Medium), GUIColor(1f, 0.5f, 0.3f)]
    public void ResetStation()
    {
        HasMooncake = false;
        if (socket != null) socket.ResetSocket();
        if (hintObject != null) hintObject.SetActive(false);
        if (moldedObject != null) moldedObject.SetActive(false);
    }
}

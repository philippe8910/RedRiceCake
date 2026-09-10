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

    [Header("蓋完之後怎麼處理（印尼流程）")]
    [Tooltip("開啟＝蓋好的月餅脫離印章、掉到桌上，玩家再用手拿去烤盤。\n" +
             "關閉＝月餅留在印章／模具上，握著整支去壓烤盤（中式流程）")]
    public bool detachAfterStamp = false;
    [Tooltip("脫離後要放在哪；留空就用月餅原本的位置往下掉")]
    public Transform detachPoint;
    [Tooltip("脫離後給月餅的 Tag，要跟烤盤格子的 acceptTag 對得起來")]
    public string detachedTag = "MooncakePiece";

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

        if (detachAfterStamp) DetachMooncake();

        onDoughReceived?.Invoke(dough);
    }

    /// <summary>
    /// 把蓋好的月餅從印章上放開，變成桌上一顆可以用手抓的東西。
    ///
    /// 站點本身還是記著 HasMooncake=true，要等那顆月餅真的被放上烤盤
    /// （由 <see cref="MooncakeDetachedPiece.Consume"/> 呼叫 ReleaseMooncake）
    /// 才會放行下一顆，不然玩家可以無限蓋。
    /// </summary>
    private void DetachMooncake()
    {
        if (moldedObject == null) return;

        var t = moldedObject.transform;

        // 先記住世界姿態，脫離父物件時才不會被印章的旋轉帶歪
        Vector3 worldPos = detachPoint != null ? detachPoint.position : t.position;
        Quaternion worldRot = detachPoint != null ? detachPoint.rotation : t.rotation;
        Vector3 worldScale = t.lossyScale;

        t.SetParent(null, true);
        t.SetPositionAndRotation(worldPos, worldRot);
        t.localScale = worldScale;

        if (!string.IsNullOrEmpty(detachedTag)) moldedObject.tag = detachedTag;

        // 要抓得到就得有 collider；模型上只有 MeshFilter 的話補一顆
        var col = moldedObject.GetComponent<Collider>();
        if (col == null)
        {
            var box = moldedObject.AddComponent<BoxCollider>();
            MooncakeColliderUtil.FitToRenderers(box, moldedObject, 1f);
            col = box;
        }
        col.isTrigger = false;

        var rb = moldedObject.GetComponent<Rigidbody>();
        if (rb == null) rb = moldedObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        // 第二、三顆會重用同一個物件，殘留的速度會讓它一出現就飛出去
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        var grab = moldedObject.GetComponent<
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab == null)
        {
            grab = moldedObject.AddComponent<
                UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            // 形狀是扁圓餅，沒有固定握法 —— 用動態 attach 讓它留在手碰到的位置
            grab.useDynamicAttach = true;
        }

        var mark = moldedObject.GetComponent<MooncakeDetachedPiece>();
        if (mark == null) mark = moldedObject.AddComponent<MooncakeDetachedPiece>();
        mark.source = this;
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

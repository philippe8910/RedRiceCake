using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 烤盤：三顆月餅都放滿之後才允許被 XR 抓起來，然後送進烤箱。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeBakingPan : MonoBehaviour
{
    [Header("抓取")]
    [Tooltip("留空會自動抓自己身上的 XRGrabInteractable")]
    public XRGrabInteractable grabInteractable;
    [Tooltip("開場時就可以抓？流程沒放滿三顆之前應該是 false")]
    public bool grabbableAtStart = false;

    [Header("抓取碰撞體")]
    [Tooltip("開場時把 BoxCollider 自動套到烤盤的 Renderer 範圍上")]
    public bool autoFitGrabCollider = true;
    public BoxCollider grabCollider;
    public float grabColliderPadding = 1.05f;

    [Header("事件")]
    public UnityEvent onBecameGrabbable;

    private Rigidbody _rb;

    public bool IsGrabbable => grabInteractable != null && grabInteractable.enabled;

    private void Awake()
    {
        if (grabInteractable == null) grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabCollider == null) grabCollider = GetComponent<BoxCollider>();
        _rb = GetComponent<Rigidbody>();

        if (autoFitGrabCollider)
            MooncakeColliderUtil.FitToRenderers(grabCollider, gameObject, grabColliderPadding);

        SetGrabbable(grabbableAtStart);
    }

    /// <summary>開放／關閉抓取。</summary>
    public void SetGrabbable(bool value)
    {
        if (grabInteractable != null) grabInteractable.enabled = value;

        if (value) onBecameGrabbable?.Invoke();
    }

    /// <summary>放進烤箱後固定住，不讓玩家再拖走。</summary>
    public void LockInPlace(Transform anchor)
    {
        SetGrabbable(false);

        if (_rb != null) _rb.isKinematic = true;

        if (anchor != null)
        {
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            transform.SetParent(anchor, true);
        }
    }

    [Button("Debug：開放抓取", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugMakeGrabbable()
    {
        SetGrabbable(true);
    }
}

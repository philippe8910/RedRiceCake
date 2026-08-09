using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

// UnityEngine.XR 與 UnityEngine.InputSystem 都有 InputDevice / CommonUsages，這裡用別名避免撞名
using XRDevices = UnityEngine.XR.InputDevices;
using XRUsages = UnityEngine.XR.CommonUsages;
using XRNode = UnityEngine.XR.XRNode;

[System.Serializable]
public class MooncakeGameObjectEvent : UnityEvent<GameObject> { }

[System.Serializable]
public class MooncakeStringEvent : UnityEvent<string> { }

/// <summary>
/// 月餅站點共用底層：手把靠近偵測、手把震動、Trigger 鍵輸入。
/// 子類別覆寫 <see cref="HandleHandEnter"/> / <see cref="HandleTriggerPressed"/> 決定實際行為。
/// </summary>
public abstract class MooncakeHandTarget : MonoBehaviour
{
    /// <summary>目前伸進範圍內的一隻手。</summary>
    public class HandRef
    {
        public Transform root;
        public HapticImpulsePlayer haptics;
        public XRNode node;
    }

    [Header("手部偵測")]
    [Tooltip("手把碰撞體的 Tag，XR Origin 的 Left/Right Controller 已設為 Controller")]
    public string handTag = "Controller";

    [Header("觸覺回饋（手把震動）")]
    [Range(0f, 1f)] public float hapticAmplitude = 0.4f;
    public float hapticDuration = 0.1f;

    [Header("輸入（留空時自動讀取 XR 手把的 Trigger 鍵）")]
    public InputActionReference rightTriggerAction;
    public InputActionReference leftTriggerAction;
    [Range(0.05f, 1f)] public float triggerPressThreshold = 0.6f;

    [Header("事件")]
    public UnityEvent onHandEnter;
    public UnityEvent onHandExit;

    protected readonly Dictionary<Collider, HandRef> Hands = new Dictionary<Collider, HandRef>();

    private bool _leftDown, _rightDown;

    /// <summary>子類別不需要 Trigger 鍵時可覆寫成 false，省下每幀輪詢。</summary>
    protected virtual bool UsesTriggerInput => true;

    protected virtual void OnEnable()
    {
        if (rightTriggerAction != null && rightTriggerAction.action != null) rightTriggerAction.action.Enable();
        if (leftTriggerAction != null && leftTriggerAction.action != null) leftTriggerAction.action.Enable();
    }

    protected virtual void OnDisable()
    {
        if (rightTriggerAction != null && rightTriggerAction.action != null) rightTriggerAction.action.Disable();
        if (leftTriggerAction != null && leftTriggerAction.action != null) leftTriggerAction.action.Disable();
        Hands.Clear();
    }

    // ---------------- 靠近偵測 ----------------

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(handTag)) return;
        if (Hands.ContainsKey(other)) return;

        bool firstHand = Hands.Count == 0;
        var hand = BuildHandRef(other);
        Hands.Add(other, hand);

        HandleHandEnter(hand, firstHand);
        if (firstHand) onHandEnter?.Invoke();
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (!Hands.Remove(other)) return;

        HandleHandExit();
        if (Hands.Count == 0) onHandExit?.Invoke();
    }

    /// <summary>有一隻手伸進來。firstHand 代表這是範圍內的第一隻手。</summary>
    protected virtual void HandleHandEnter(HandRef hand, bool firstHand) { }

    protected virtual void HandleHandExit() { }

    /// <summary>手在範圍內時按下 Trigger。</summary>
    protected virtual void HandleTriggerPressed(HandRef hand) { }

    private HandRef BuildHandRef(Collider col)
    {
        var haptics = col.GetComponentInParent<HapticImpulsePlayer>();
        var root = haptics != null ? haptics.transform : col.transform;

        // XR Origin 的手把物件名稱是 "Left Controller" / "Right Controller"
        var node = root.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0
            ? XRNode.LeftHand
            : XRNode.RightHand;

        return new HandRef { root = root, haptics = haptics, node = node };
    }

    // ---------------- Trigger 輸入 ----------------

    protected virtual void Update()
    {
        if (!UsesTriggerInput) return;

        // 邊緣偵測要每幀更新，避免手伸進來的瞬間吃到舊的按住狀態
        bool left = ReadTrigger(XRNode.LeftHand, leftTriggerAction);
        bool right = ReadTrigger(XRNode.RightHand, rightTriggerAction);

        if (Hands.Count > 0)
        {
            if (left && !_leftDown) DispatchTrigger(XRNode.LeftHand);
            if (right && !_rightDown) DispatchTrigger(XRNode.RightHand);
        }

        _leftDown = left;
        _rightDown = right;
    }

    private void DispatchTrigger(XRNode node)
    {
        foreach (var kv in Hands)
        {
            if (kv.Value.node != node) continue;
            HandleTriggerPressed(kv.Value);
            return;
        }
    }

    private bool ReadTrigger(XRNode node, InputActionReference reference)
    {
        if (reference != null && reference.action != null)
        {
            var action = reference.action;
            if (action.expectedControlType == "Button" || action.type == InputActionType.Button)
                return action.IsPressed();

            return action.ReadValue<float>() >= triggerPressThreshold;
        }

        // 沒指定 InputActionReference 時，直接讀 XR 裝置的 Trigger
        var device = XRDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return false;

        if (device.TryGetFeatureValue(XRUsages.triggerButton, out bool pressed)) return pressed;
        if (device.TryGetFeatureValue(XRUsages.trigger, out float value)) return value >= triggerPressThreshold;
        return false;
    }

    // ---------------- 震動 ----------------

    /// <summary>優先用 XRI 的 HapticImpulsePlayer，沒有就退回 XR 裝置原生震動。</summary>
    protected void SendHaptic(HandRef hand, float amplitude, float duration)
    {
        if (amplitude <= 0f || duration <= 0f) return;

        if (hand != null && hand.haptics != null)
        {
            hand.haptics.SendHapticImpulse(amplitude, duration);
            return;
        }

        if (hand != null)
        {
            SendDeviceHaptic(hand.node, amplitude, duration);
            return;
        }

        SendDeviceHaptic(XRNode.LeftHand, amplitude, duration);
        SendDeviceHaptic(XRNode.RightHand, amplitude, duration);
    }

    private static void SendDeviceHaptic(XRNode node, float amplitude, float duration)
    {
        var device = XRDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return;

        if (device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
            device.SendHapticImpulse(0u, amplitude, duration);
    }
}

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

// UnityEngine.XR 與 UnityEngine.InputSystem 都有 InputDevice / CommonUsages，用別名避免撞名
using XRDevices = UnityEngine.XR.InputDevices;
using XRUsages = UnityEngine.XR.CommonUsages;
using XRNode = UnityEngine.XR.XRNode;

/// <summary>
/// 遊戲中的暫停選單：按手把的 B / Y 鍵開關，裡面可以調設定或回主選單。
/// 面板會出現在玩家正前方，跟著視線走。
/// </summary>
[DisallowMultipleComponent]
public class MooncakePauseMenu : MonoBehaviour
{
    [Header("面板")]
    [Tooltip("整個暫停面板的根物件，開場關閉")]
    public GameObject panel;
    [Tooltip("設定視窗（可留空）")]
    public GameObject settingsWindow;

    [Header("擺放")]
    [Tooltip("開啟時把面板放到玩家正前方")]
    public bool placeInFrontOnOpen = true;
    [Tooltip("要搬到玩家面前的物件。留空就用這個元件所在的物件。\n" +
             "注意要搬「共同的父層」而不是單一面板 —— 設定視窗是暫停面板的兄弟節點，\n" +
             "只搬面板的話設定視窗會留在原地（世界原點），按下去就找不到了。")]
    public Transform placementRoot;
    public float distance = 1.6f;
    public float heightOffset = -0.1f;

    [Header("輸入")]
    [Tooltip("右手 B 鍵（secondaryButton）")]
    public bool useRightSecondary = true;
    [Tooltip("左手 Y 鍵（secondaryButton）")]
    public bool useLeftSecondary = true;
    [Tooltip("留空則直接讀 XR 裝置的 secondaryButton")]
    public InputActionReference toggleAction;
    [Tooltip("鍵盤 Esc 也能開關，方便在 PC 上測")]
    public bool alsoUseEscapeKey = true;

    [Header("回主選單")]
    public string mainMenuScene = "MooncakeMainMenu";

    [Header("事件")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    [ShowInInspector, ReadOnly] private bool _open;

    private bool _prevPressed;

    public bool IsOpen => _open;

    private void Start()
    {
        Close();
    }

    private void Update()
    {
        bool pressed = ReadToggle();
        if (pressed && !_prevPressed) Toggle();
        _prevPressed = pressed;
    }

    private bool ReadToggle()
    {
        if (alsoUseEscapeKey && Keyboard.current != null &&
            Keyboard.current.escapeKey.isPressed) return true;

        if (toggleAction != null && toggleAction.action != null)
            return toggleAction.action.IsPressed();

        if (useRightSecondary && ReadSecondary(XRNode.RightHand)) return true;
        if (useLeftSecondary && ReadSecondary(XRNode.LeftHand)) return true;
        return false;
    }

    private static bool ReadSecondary(XRNode node)
    {
        var d = XRDevices.GetDeviceAtXRNode(node);
        if (!d.isValid) return false;
        return d.TryGetFeatureValue(XRUsages.secondaryButton, out bool v) && v;
    }

    // ---------------- 開關 ----------------

    [Button("Debug：開關暫停選單", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f)]
    public void Toggle()
    {
        if (_open) Close();
        else Open();
    }

    public void Open()
    {
        _open = true;

        if (placeInFrontOnOpen) PlaceInFront(placementRoot != null ? placementRoot : transform);

        if (panel != null) panel.SetActive(true);
        if (settingsWindow != null) settingsWindow.SetActive(false);

        onOpened?.Invoke();
    }

    public void Close()
    {
        _open = false;

        if (panel != null) panel.SetActive(false);
        if (settingsWindow != null) settingsWindow.SetActive(false);

        onClosed?.Invoke();
    }

    /// <summary>面板放到玩家正前方，並轉向面對玩家。</summary>
    private void PlaceInFront(Transform t)
    {
        var cam = Camera.main;
        if (cam == null || t == null) return;

        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();

        t.position = cam.transform.position + fwd * distance + Vector3.up * heightOffset;
        t.rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }

    // ---------------- 設定 ----------------

    public void OpenSettings()
    {
        if (settingsWindow != null) settingsWindow.SetActive(true);
        if (panel != null) panel.SetActive(false);
    }

    public void CloseSettings()
    {
        if (settingsWindow != null) settingsWindow.SetActive(false);
        if (panel != null) panel.SetActive(true);
    }

    // ---------------- 回主選單 ----------------

    [Button("Debug：回主選單", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.4f)]
    public void ReturnToMainMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[暫停選單] 要在 Play Mode 才會真的載入場景", this);
            return;
        }

        Close();

        var fader = MooncakeScreenFader.Instance;
        if (fader != null) fader.LoadScene(mainMenuScene);   // 淡出後才載入
        else UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuScene);
    }

    public void Resume() => Close();
}

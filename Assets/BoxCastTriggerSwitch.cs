using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Sirenix.OdinInspector;

public class BoxCastTriggerSwitch : MonoBehaviour
{
    [System.Serializable]
    public class SwitchData
    {
        [HorizontalGroup("Switch", Width = 0.3f)]
        [LabelText("開關物件"), LabelWidth(80)]
        public GameObject targetObject;
        
        [HorizontalGroup("Switch", Width = 0.2f)]
        [LabelText("初始狀態"), LabelWidth(80)]
        public bool initialState = false;
        
        [HorizontalGroup("Switch", Width = 0.5f)]
        [LabelText("當前狀態"), LabelWidth(80), ReadOnly]
        public bool currentState = false;
        
        [FoldoutGroup("Switch/事件")]
        [LabelText("開啟時觸發")]
        public UnityEvent onSwitchOn;
        
        [FoldoutGroup("Switch/事件")]
        [LabelText("關閉時觸發")]
        public UnityEvent onSwitchOff;
        
        [FoldoutGroup("Switch/事件")]
        [LabelText("切換時觸發 (bool)")]
        public UnityEvent<bool> onToggle;
    }
    
    [Title("BoxCast 偵測設定", "設定 BoxCast 的偵測參數", TitleAlignment = TitleAlignments.Centered)]
    [BoxGroup("偵測設定")]
    [LabelText("偵測中心點"), Tooltip("BoxCast 的中心位置")]
    [SerializeField] private Transform detectionCenter;
    
    [BoxGroup("偵測設定")]
    [LabelText("Box 大小"), Tooltip("BoxCast 的尺寸")]
    [SerializeField] private Vector3 boxSize = new Vector3(0.1f, 0.1f, 0.1f);
    
    [BoxGroup("偵測設定")]
    [LabelText("Box 旋轉"), Tooltip("BoxCast 的旋轉角度")]
    [SerializeField] private Vector3 boxRotation = Vector3.zero;
    
    [BoxGroup("偵測設定")]
    [LabelText("偵測方向"), Tooltip("BoxCast 射出的方向")]
    [SerializeField] private Vector3 castDirection = Vector3.forward;
    
    [BoxGroup("偵測設定")]
    [LabelText("偵測距離"), Tooltip("BoxCast 的最大距離"), MinValue(0)]
    [SerializeField] private float castDistance = 0.1f;
    
    [BoxGroup("偵測設定")]
    [LabelText("目標 Layer"), Tooltip("要偵測的物件 Layer (可選)")]
    [SerializeField] private LayerMask targetLayer = -1; // -1 表示所有 Layer
    
    [BoxGroup("偵測設定")]
    [LabelText("偵測組件類型"), Tooltip("輸入要偵測的組件腳本名稱")]
    [InfoBox("例如: BallShapeKnockDetector, Rigidbody, PlayerController 等", InfoMessageType.Info)]
    [SerializeField] private string componentTypeName = "BallShapeKnockDetector";
    
    [Title("開關設定", "設定要控制的開關物件列表")]
    [ListDrawerSettings(
        ShowIndexLabels = true,
        ListElementLabelName = "targetObject",
        DraggableItems = true,
        ShowPaging = true,
        NumberOfItemsPerPage = 5
    )]
    [SerializeField] private List<SwitchData> switches = new List<SwitchData>();
    
    [Title("行為設定")]
    [BoxGroup("行為")]
    [LabelText("觸發模式"), Tooltip("選擇如何觸發開關")]
    [SerializeField] private TriggerMode triggerMode = TriggerMode.Toggle;
    
    [BoxGroup("行為")]
    [LabelText("持續偵測"), Tooltip("是否每幀都進行偵測")]
    [SerializeField] private bool continuousDetection = true;
    
    [BoxGroup("行為")]
    [LabelText("冷卻時間"), Tooltip("觸發後的冷卻時間"), MinValue(0)]
    [ShowIf("continuousDetection")]
    [SerializeField] private float cooldownTime = 0.5f;
    
    [BoxGroup("行為")]
    [LabelText("需要特定Tag"), Tooltip("是否需要檢查 Tag")]
    [SerializeField] private bool requireTag = false;
    
    [BoxGroup("行為")]
    [LabelText("目標 Tag")]
    [ShowIf("requireTag")]
    [SerializeField] private string requiredTag = "Player";
    
    [Title("除錯設定")]
    [BoxGroup("除錯")]
    [LabelText("顯示偵測範圍")]
    [SerializeField] private bool showDebugGizmos = true;
    
    [BoxGroup("除錯")]
    [LabelText("Gizmos 顏色 - 未偵測到")]
    [ShowIf("showDebugGizmos")]
    [SerializeField] private Color gizmosColorIdle = Color.yellow;
    
    [BoxGroup("除錯")]
    [LabelText("Gizmos 顏色 - 偵測到")]
    [ShowIf("showDebugGizmos")]
    [SerializeField] private Color gizmosColorDetected = Color.green;
    
    [BoxGroup("除錯")]
    [LabelText("顯示偵測資訊")]
    [ReadOnly, ShowInInspector]
    private string detectionInfo = "等待偵測...";
    
    public enum TriggerMode
    {
        [LabelText("切換 (Toggle)")]
        Toggle,
        [LabelText("進入時開啟")]
        OnEnter,
        [LabelText("進入時關閉")]
        OnEnterOff,
        [LabelText("持續按住")]
        WhileInside
    }
    
    // 私有變數
    private HashSet<Component> componentsInRange = new HashSet<Component>();
    private HashSet<Component> previousFrameComponents = new HashSet<Component>();
    private float lastTriggerTime = 0f;
    private bool isDetecting = false;
    
    // 公開事件
    [FoldoutGroup("全域事件")]
    [LabelText("任何開關開啟時")]
    public UnityEvent onAnySwitchOn;
    
    [FoldoutGroup("全域事件")]
    [LabelText("任何開關關閉時")]
    public UnityEvent onAnySwitchOff;
    
    [FoldoutGroup("全域事件")]
    [LabelText("偵測到物體進入")]
    public UnityEvent<GameObject> onObjectDetected;
    
    [FoldoutGroup("全域事件")]
    [LabelText("物體離開偵測")]
    public UnityEvent<GameObject> onObjectLeft;
    
    [FoldoutGroup("全域事件")]
    [LabelText("偵測到目標組件")]
    public UnityEvent<Component> onComponentFound;
    
    [FoldoutGroup("全域事件")]
    [LabelText("目標組件完成 (BallShapeKnockDetector)")]
    public UnityEvent<Component> onComponentComplete;
    
    private void Start()
    {
        if (detectionCenter == null)
            detectionCenter = transform;
        
        // 初始化開關狀態
        foreach (var switchData in switches)
        {
            switchData.currentState = switchData.initialState;
            ApplySwitchState(switchData, switchData.initialState, false);
        }
    }
    
    private void Update()
    {
        if (continuousDetection)
        {
            PerformDetection();
        }
    }
    
    private void PerformDetection()
    {
        // 儲存上一幀的組件
        previousFrameComponents.Clear();
        previousFrameComponents.UnionWith(componentsInRange);
        
        // 清空當前組件列表
        componentsInRange.Clear();
        
        // 執行 BoxCast
        Quaternion boxOrientation = detectionCenter.rotation * Quaternion.Euler(boxRotation);
        Vector3 direction = detectionCenter.TransformDirection(castDirection.normalized);
        
        RaycastHit[] hits = Physics.BoxCastAll(
            detectionCenter.position,
            boxSize * 0.5f,
            direction,
            boxOrientation,
            castDistance,
            targetLayer
        );
        
        // 檢查是否偵測到帶有指定組件的物體
        bool detected = false;
        foreach (var hit in hits)
        {
            // 檢查 Tag (如果需要)
            if (requireTag && !hit.collider.CompareTag(requiredTag))
                continue;
            
            // 使用反射來尋找指定名稱的組件
            Component component = FindComponentByName(hit.collider.gameObject, componentTypeName);
            
            if (component != null)
            {
                detected = true;
                componentsInRange.Add(component);
            }
        }
        
        isDetecting = detected;
        detectionInfo = detected ? $"偵測到 {componentsInRange.Count} 個 {componentTypeName}" : $"未偵測到 {componentTypeName}";
        
        // 檢查新進入的組件
        foreach (var component in componentsInRange)
        {
            if (!previousFrameComponents.Contains(component))
            {
                OnComponentEnter(component);
            }
        }
        
        // 檢查離開的組件
        foreach (var component in previousFrameComponents)
        {
            if (!componentsInRange.Contains(component))
            {
                OnComponentExit(component);
            }
        }
        
        // WhileInside 模式的處理
        if (triggerMode == TriggerMode.WhileInside)
        {
            bool hasComponents = componentsInRange.Count > 0;
            foreach (var switchData in switches)
            {
                if (switchData.currentState != hasComponents)
                {
                    ApplySwitchState(switchData, hasComponents, true);
                }
            }
        }
    }
    
    /// <summary>
    /// 根據組件名稱尋找組件 (支援在物件、父物件、子物件中尋找)
    /// </summary>
    private Component FindComponentByName(GameObject obj, string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;
        
        // 先在當前物件尋找
        Component[] components = obj.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp != null && comp.GetType().Name == typeName)
                return comp;
        }
        
        // 在父物件中尋找
        Transform parent = obj.transform.parent;
        if (parent != null)
        {
            components = parent.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp != null && comp.GetType().Name == typeName)
                    return comp;
            }
        }
        
        // 在子物件中尋找
        components = obj.GetComponentsInChildren<Component>();
        foreach (var comp in components)
        {
            if (comp != null && comp.GetType().Name == typeName)
                return comp;
        }
        
        return null;
    }
    
    private void OnComponentEnter(Component component)
    {
        Debug.Log($"{componentTypeName} 進入偵測: {component.gameObject.name}");
        onObjectDetected?.Invoke(component.gameObject);
        onComponentFound?.Invoke(component);
        
        // 如果是 BallShapeKnockDetector,訂閱完成事件
        if (component is BallShapeKnockDetector detector)
        {
            detector.onKnockComplete.AddListener(() => OnKnockComplete(component));
        }
        
        // 檢查冷卻時間
        if (Time.time - lastTriggerTime < cooldownTime)
            return;
        
        lastTriggerTime = Time.time;
        
        // 根據模式觸發開關
        switch (triggerMode)
        {
            case TriggerMode.Toggle:
                ToggleAllSwitches();
                break;
            case TriggerMode.OnEnter:
                SetAllSwitches(true);
                break;
            case TriggerMode.OnEnterOff:
                SetAllSwitches(false);
                break;
            case TriggerMode.WhileInside:
                // 在 PerformDetection 中處理
                break;
        }
    }
    
    private void OnComponentExit(Component component)
    {
        Debug.Log($"{componentTypeName} 離開偵測: {component.gameObject.name}");
        onObjectLeft?.Invoke(component.gameObject);
        
        // 如果是 BallShapeKnockDetector,取消訂閱完成事件
        if (component is BallShapeKnockDetector detector)
        {
            detector.onKnockComplete.RemoveListener(() => OnKnockComplete(component));
        }
    }
    
    private void OnKnockComplete(Component component)
    {
        Debug.Log($"{componentTypeName} 完成所有段數: {component.gameObject.name}");
        onComponentComplete?.Invoke(component);
    }
    
    private void ToggleAllSwitches()
    {
        foreach (var switchData in switches)
        {
            bool newState = !switchData.currentState;
            ApplySwitchState(switchData, newState, true);
        }
    }
    
    private void SetAllSwitches(bool state)
    {
        foreach (var switchData in switches)
        {
            ApplySwitchState(switchData, state, true);
        }
    }
    
    private void ApplySwitchState(SwitchData switchData, bool state, bool triggerEvents)
    {
        switchData.currentState = state;
        
        // 設定物件啟用狀態
        if (switchData.targetObject != null)
        {
            switchData.targetObject.SetActive(state);
        }
        
        // 觸發事件
        if (triggerEvents)
        {
            if (state)
            {
                switchData.onSwitchOn?.Invoke();
                onAnySwitchOn?.Invoke();
            }
            else
            {
                switchData.onSwitchOff?.Invoke();
                onAnySwitchOff?.Invoke();
            }
            
            switchData.onToggle?.Invoke(state);
        }
    }
    
    #region 公開方法
    
    [Button("手動觸發偵測", ButtonSizes.Medium)]
    [GUIColor(0.3f, 0.8f, 0.3f)]
    public void ManualDetect()
    {
        PerformDetection();
    }
    
    [Button("切換所有開關", ButtonSizes.Medium)]
    [GUIColor(0.3f, 0.6f, 1f)]
    public void ManualToggle()
    {
        ToggleAllSwitches();
    }
    
    [Button("開啟所有開關", ButtonSizes.Medium)]
    [GUIColor(0.3f, 1f, 0.3f)]
    public void TurnOnAll()
    {
        SetAllSwitches(true);
    }
    
    [Button("關閉所有開關", ButtonSizes.Medium)]
    [GUIColor(1f, 0.3f, 0.3f)]
    public void TurnOffAll()
    {
        SetAllSwitches(false);
    }
    
    [Button("重置為初始狀態", ButtonSizes.Medium)]
    [GUIColor(1f, 0.8f, 0.3f)]
    public void ResetToInitialState()
    {
        foreach (var switchData in switches)
        {
            ApplySwitchState(switchData, switchData.initialState, true);
        }
    }
    
    /// <summary>
    /// 設定特定索引的開關狀態
    /// </summary>
    public void SetSwitchState(int index, bool state)
    {
        if (index >= 0 && index < switches.Count)
        {
            ApplySwitchState(switches[index], state, true);
        }
    }
    
    /// <summary>
    /// 切換特定索引的開關
    /// </summary>
    public void ToggleSwitch(int index)
    {
        if (index >= 0 && index < switches.Count)
        {
            var switchData = switches[index];
            ApplySwitchState(switchData, !switchData.currentState, true);
        }
    }
    
    /// <summary>
    /// 取得當前偵測到的所有組件
    /// </summary>
    public List<Component> GetDetectedComponents()
    {
        return new List<Component>(componentsInRange);
    }
    
    /// <summary>
    /// 取得當前偵測到的特定類型組件
    /// </summary>
    public List<T> GetDetectedComponents<T>() where T : Component
    {
        List<T> result = new List<T>();
        foreach (var component in componentsInRange)
        {
            if (component is T typedComponent)
            {
                result.Add(typedComponent);
            }
        }
        return result;
    }
    
    #endregion
    
    #region Gizmos
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || detectionCenter == null)
            return;
        
        Quaternion boxOrientation = detectionCenter.rotation * Quaternion.Euler(boxRotation);
        Vector3 direction = detectionCenter.TransformDirection(castDirection.normalized);
        Vector3 endPosition = detectionCenter.position + direction * castDistance;
        
        // 繪製起始 Box
        Gizmos.color = isDetecting ? gizmosColorDetected : gizmosColorIdle;
        Gizmos.matrix = Matrix4x4.TRS(detectionCenter.position, boxOrientation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
        
        // 繪製結束 Box
        Gizmos.matrix = Matrix4x4.TRS(endPosition, boxOrientation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
        
        // 繪製方向線
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawLine(detectionCenter.position, endPosition);
        
        // 繪製箭頭
        DrawArrow(detectionCenter.position, direction * castDistance, isDetecting ? gizmosColorDetected : gizmosColorIdle);
        
        // 繪製偵測到的組件位置
        if (Application.isPlaying && componentsInRange.Count > 0)
        {
            Gizmos.color = Color.cyan;
            foreach (var component in componentsInRange)
            {
                if (component != null)
                {
                    Gizmos.DrawWireSphere(component.transform.position, 0.1f);
                }
            }
        }
    }
    
    private void DrawArrow(Vector3 start, Vector3 direction, Color color)
    {
        Gizmos.color = color;
        Vector3 end = start + direction;
        Gizmos.DrawLine(start, end);
        
        // 箭頭
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;
        Gizmos.DrawLine(end, end + right * 0.05f);
        Gizmos.DrawLine(end, end + left * 0.05f);
    }
    
    #endregion
}
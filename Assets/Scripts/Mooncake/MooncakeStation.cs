using UnityEngine;

/// <summary>
/// 所有製作站點的共同底：負責「現在輪不輪得到我」、麵團有沒有在區域內、以及高亮提示。
/// 實際互動由子類別實作。
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class MooncakeStation : MonoBehaviour
{
    [Header("流程")]
    public MooncakeStep step;
    public MooncakeFlow flow;

    [Tooltip("只在某一種月餅的流程出現（例如中式專用的餡料碗）")]
    public bool restrictByType;
    public MooncakeType onlyForType = MooncakeType.Chinese;

    [Header("提示")]
    public string stationName = "";
    public Renderer zoneRenderer;
    public Color idleColor = new Color(0.55f, 0.55f, 0.6f, 1f);
    public Color activeColor = new Color(0.35f, 0.85f, 0.45f, 1f);

    protected MooncakeWorkpiece PieceInside { get; private set; }

    /// <summary>目前流程正好停在這個站點負責的步驟。</summary>
    public bool IsActiveStep => flow != null && flow.IsRunning && flow.CurrentStep == step;

    Material _zoneMat;
    Color _lastColor = new Color(-1f, -1f, -1f, -1f);

    protected virtual void Awake()
    {
        if (zoneRenderer == null) zoneRenderer = GetComponent<Renderer>();
        if (zoneRenderer != null) _zoneMat = zoneRenderer.material;
        if (string.IsNullOrEmpty(stationName)) stationName = MooncakeRecipes.StepLabel(step);
    }

    protected virtual void Update()
    {
        UpdateHighlight();
    }

    void UpdateHighlight()
    {
        if (_zoneMat == null) return;

        var target = IsActiveStep ? activeColor : idleColor;
        if (target == _lastColor) return;

        _zoneMat.color = target;
        _lastColor = target;
    }

    // ---------- 麵團進出區域 ----------

    protected virtual void OnTriggerEnter(Collider other)
    {
        var piece = other.GetComponentInParent<MooncakeWorkpiece>();
        if (piece == null || (flow != null && piece != flow.CurrentPiece)) return;

        PieceInside = piece;
        OnPieceEnter(piece);
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        var piece = other.GetComponentInParent<MooncakeWorkpiece>();
        if (piece == null || piece != PieceInside) return;

        PieceInside = null;
        OnPieceExit(piece);
    }

    protected virtual void OnPieceEnter(MooncakeWorkpiece piece) { }
    protected virtual void OnPieceExit(MooncakeWorkpiece piece) { }

    protected static bool IsHand(Collider other)
    {
        return other.CompareTag("Controller");
    }

    // ---------- 對外 ----------

    /// <summary>流程有變動時被呼叫，站點可以重設自己的計數。</summary>
    public virtual void OnFlowChanged() { }

    /// <summary>UI 的「執行動作」：等同在 VR 裡對這個站點做一次互動。</summary>
    public abstract void PerformAction();

    /// <summary>UI 的「跳過此步」：直接把這一步做完（含視覺結果）。</summary>
    public virtual void ForceComplete()
    {
        CompleteStep();
    }

    protected bool CompleteStep()
    {
        return flow != null && flow.CompleteStep(step);
    }
}

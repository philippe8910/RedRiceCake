using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 月餅製作流程的狀態機：主畫面選種類 → 依流程表逐步推進 → 完成 → 回主畫面。
/// 站點（MooncakeStation）只負責偵測互動，推進與否一律由這裡決定。
/// </summary>
public class MooncakeFlow : MonoBehaviour
{
    [Header("場上的站點（留空會在 Awake 自動搜尋）")]
    public List<MooncakeStation> stations = new List<MooncakeStation>();

    [Header("Events")]
    public UnityEvent onFlowChanged;      // 任何狀態變動，UI 重繪用
    public UnityEvent onRecipeStarted;
    public UnityEvent onRecipeFinished;
    public UnityEvent onReturnedToMenu;

    public MooncakeType CurrentType { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }
    public int StepIndex { get; private set; }
    public IReadOnlyList<MooncakeStepEntry> Steps { get; private set; }

    /// <summary>玩家目前正在處理的那顆麵團／月餅。抓取站生成後設進來。</summary>
    public MooncakeWorkpiece CurrentPiece { get; set; }

    public MooncakeStepEntry CurrentEntry
    {
        get
        {
            if (Steps == null || StepIndex < 0 || StepIndex >= Steps.Count)
                return new MooncakeStepEntry(MooncakeStep.Done, "完成");
            return Steps[StepIndex];
        }
    }

    public MooncakeStep CurrentStep => CurrentEntry.Step;

    void Awake()
    {
        if (stations == null || stations.Count == 0)
            stations = new List<MooncakeStation>(FindObjectsOfType<MooncakeStation>(true));

        foreach (var s in stations)
            if (s != null) s.flow = this;

        ReturnToMenu();
    }

    // ---------- 流程控制 ----------

    public void StartChinese()    { StartRecipe(MooncakeType.Chinese); }
    public void StartIndonesian() { StartRecipe(MooncakeType.Indonesian); }

    public void StartRecipe(MooncakeType type)
    {
        CurrentType = type;
        Steps = MooncakeRecipes.For(type);
        StepIndex = 0;
        IsRunning = true;
        IsFinished = false;

        DestroyPiece();
        RefreshStations();

        onRecipeStarted?.Invoke();
        onFlowChanged?.Invoke();
    }

    /// <summary>站點完成了自己負責的步驟。step 對不上目前進度就忽略，避免亂序推進。</summary>
    public bool CompleteStep(MooncakeStep step)
    {
        if (!IsRunning || IsFinished) return false;
        if (CurrentStep != step) return false;

        StepIndex++;
        RefreshStations();

        if (CurrentStep == MooncakeStep.Done)
        {
            IsFinished = true;
            IsRunning = false;
            onRecipeFinished?.Invoke();
        }

        onFlowChanged?.Invoke();
        return true;
    }

    /// <summary>UI 上的「跳過此步」，展示流程用。</summary>
    public void ForceCompleteCurrentStep()
    {
        if (!IsRunning || IsFinished) return;

        var station = FindActiveStation();
        if (station != null) station.ForceComplete();
        else CompleteStep(CurrentStep);
    }

    /// <summary>UI 上的「執行動作」，等同在 VR 裡對站點做一次互動（拍一下、刷一下…）。</summary>
    public void PerformActionOnCurrentStation()
    {
        var station = FindActiveStation();
        if (station != null) station.PerformAction();
    }

    public void ReturnToMenu()
    {
        IsRunning = false;
        IsFinished = false;
        StepIndex = 0;
        Steps = null;

        DestroyPiece();
        RefreshStations();

        onReturnedToMenu?.Invoke();
        onFlowChanged?.Invoke();
    }

    // ---------- 內部 ----------

    public MooncakeStation FindActiveStation()
    {
        foreach (var s in stations)
        {
            if (s == null || !s.isActiveAndEnabled) continue;
            if (s.IsActiveStep) return s;
        }
        return null;
    }

    void RefreshStations()
    {
        foreach (var s in stations)
        {
            if (s == null) continue;

            // 只屬於某一種月餅的站點（例如中式的餡料碗）在別的流程裡直接關掉
            bool visible = !IsRunning || !s.restrictByType || s.onlyForType == CurrentType;
            if (s.gameObject.activeSelf != visible) s.gameObject.SetActive(visible);

            if (s.isActiveAndEnabled) s.OnFlowChanged();
        }
    }

    void DestroyPiece()
    {
        if (CurrentPiece != null) Destroy(CurrentPiece.gameObject);
        CurrentPiece = null;
    }
}

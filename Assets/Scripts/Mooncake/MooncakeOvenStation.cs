using DG.Tweening;
using UnityEngine;

/// <summary>
/// 烘烤（新機制）：麵餅放進烤箱 → 關門 → 計時 → 上色。
/// 烤箱外型之後會參考舊來發餅舖的老烤箱，demo 先用方塊。
/// </summary>
public class MooncakeOvenStation : MooncakeStation
{
    [Header("烤箱")]
    public Transform door;
    public Vector3 doorClosedEuler = Vector3.zero;
    public Vector3 doorOpenEuler = new Vector3(-80f, 0f, 0f);
    public float doorTime = 0.4f;
    public Transform trayPoint;

    [Header("烘烤")]
    public float bakeSeconds = 4f;

    public bool IsBaking { get; private set; }
    public float BakeProgress01 { get; private set; }

    MooncakeWorkpiece _baking;
    Rigidbody _bakingBody;
    float _bakeTimer;

    protected override void Awake()
    {
        step = MooncakeStep.Bake;
        base.Awake();
        SetDoor(true, true);
    }

    protected override void Update()
    {
        base.Update();

        if (!IsBaking) return;

        _bakeTimer += Time.deltaTime;
        BakeProgress01 = bakeSeconds <= 0f ? 1f : Mathf.Clamp01(_bakeTimer / bakeSeconds);

        if (BakeProgress01 >= 1f) FinishBake();
    }

    protected override void OnPieceEnter(MooncakeWorkpiece piece)
    {
        if (!IsActiveStep || IsBaking) return;
        StartBake(piece);
    }

    public override void PerformAction()
    {
        if (!IsActiveStep || IsBaking) return;

        var piece = PieceInside != null ? PieceInside : flow.CurrentPiece;
        if (piece == null) return;

        // UI 觸發時麵餅可能還在別處，直接送進烤盤
        if (trayPoint != null) piece.transform.position = trayPoint.position;
        StartBake(piece);
    }

    public override void ForceComplete()
    {
        var piece = flow != null ? flow.CurrentPiece : null;
        if (piece != null) piece.ApplyBake();

        StopBake();
        CompleteStep();
    }

    void StartBake(MooncakeWorkpiece piece)
    {
        _baking = piece;
        _bakingBody = piece.GetComponentInChildren<Rigidbody>();

        // 烘烤中固定在烤盤上，避免滾出來
        if (_bakingBody != null) _bakingBody.isKinematic = true;
        if (trayPoint != null) piece.transform.position = trayPoint.position;

        IsBaking = true;
        _bakeTimer = 0f;
        BakeProgress01 = 0f;
        SetDoor(false);
    }

    void FinishBake()
    {
        if (_baking != null) _baking.ApplyBake();

        StopBake();
        CompleteStep();
    }

    void StopBake()
    {
        IsBaking = false;
        _bakeTimer = 0f;
        BakeProgress01 = 0f;
    }

    /// <summary>取出步驟開始時呼叫：開門並放開麵餅。</summary>
    public void ReleaseForTakeOut()
    {
        SetDoor(true);
        if (_bakingBody != null) _bakingBody.isKinematic = false;
        _baking = null;
        _bakingBody = null;
    }

    public void SetDoor(bool open, bool instant = false)
    {
        if (door == null) return;

        var target = open ? doorOpenEuler : doorClosedEuler;
        if (instant) door.localEulerAngles = target;
        else door.DOLocalRotate(target, doorTime).SetEase(Ease.OutQuad);
    }

    public override void OnFlowChanged()
    {
        if (!IsActiveStep && IsBaking) StopBake();
    }
}

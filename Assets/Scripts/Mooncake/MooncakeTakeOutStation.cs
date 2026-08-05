using UnityEngine;

/// <summary>
/// 取出（新機制）：烤好後開門，把月餅拿離烤箱就算完成。
/// 跟烘烤共用同一個烤箱，但因為是流程上不同的一步，拆成獨立站點。
/// </summary>
public class MooncakeTakeOutStation : MooncakeStation
{
    [Header("烤箱")]
    public MooncakeOvenStation oven;
    [Tooltip("UI 執行動作時，把月餅放到這個位置（出爐架）")]
    public Transform coolingPoint;

    bool _doorOpened;

    protected override void Awake()
    {
        step = MooncakeStep.TakeOut;
        base.Awake();
    }

    public override void OnFlowChanged()
    {
        if (IsActiveStep && !_doorOpened)
        {
            if (oven != null) oven.ReleaseForTakeOut();
            _doorOpened = true;
        }
        else if (!IsActiveStep)
        {
            _doorOpened = false;
        }
    }

    protected override void OnPieceExit(MooncakeWorkpiece piece)
    {
        if (!IsActiveStep) return;
        CompleteStep();
    }

    public override void PerformAction()
    {
        if (!IsActiveStep) return;

        var piece = flow.CurrentPiece;
        if (piece != null && coolingPoint != null)
            piece.transform.position = coolingPoint.position;

        CompleteStep();
    }

    public override void ForceComplete()
    {
        PerformAction();
        CompleteStep();
    }
}

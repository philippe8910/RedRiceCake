using UnityEngine;

/// <summary>
/// 加料：餡料碗。demo 用不同顏色代表不同口味，碗前面立名牌。
/// 中式＝豆沙／蓮蓉，印尼＝巧克力／波羅蜜。
/// </summary>
public class MooncakeFillingStation : MooncakeStation
{
    [Header("餡料")]
    public string fillingName = "豆沙";
    public Color fillingColor = new Color(0.42f, 0.22f, 0.12f);

    protected override void Awake()
    {
        step = MooncakeStep.AddFilling;
        base.Awake();
        stationName = fillingName;
    }

    protected override void OnPieceEnter(MooncakeWorkpiece piece)
    {
        TryFill(piece);
    }

    public override void PerformAction()
    {
        TryFill(flow != null ? flow.CurrentPiece : null);
    }

    public override void ForceComplete()
    {
        TryFill(flow != null ? flow.CurrentPiece : null);
    }

    void TryFill(MooncakeWorkpiece piece)
    {
        if (!IsActiveStep) return;
        if (piece == null)
        {
            CompleteStep();
            return;
        }

        piece.SetFilling(fillingColor);
        CompleteStep();
    }
}

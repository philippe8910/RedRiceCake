using UnityEngine;

/// <summary>
/// 需要重複動作幾下才算完成的站點：拍打、揉捏、刷蛋液。
/// 沿用紅龜粿 doughInteractionComponent 的「計次 + 彈性縮放回饋」手感。
/// </summary>
public class MooncakeActionStation : MooncakeStation
{
    public enum ResultShape { None, Flat, Ball }

    [Header("完成條件")]
    public int requiredCount = 5;
    [Tooltip("麵團必須放在這個區域內才算數")]
    public bool requirePieceInside = true;
    [Tooltip("兩次動作之間的最短間隔，避免手在邊界抖動被灌爆")]
    public float actionCooldown = 0.2f;

    [Header("動作來源")]
    [Tooltip("除了手（Tag=Controller）之外，也可以指定工具，例如蛋液刷")]
    public Transform actionTool;

    [Header("完成後的造型結果")]
    public ResultShape result = ResultShape.None;
    public bool applyEggWash;

    public int CurrentCount { get; private set; }
    public float Progress01 => requiredCount <= 0 ? 1f : Mathf.Clamp01((float)CurrentCount / requiredCount);

    float _lastActionTime = -99f;

    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        if (IsHand(other) || IsTool(other)) PerformAction();
    }

    bool IsTool(Collider other)
    {
        return actionTool != null && other.transform.IsChildOf(actionTool);
    }

    public override void PerformAction()
    {
        if (!IsActiveStep) return;
        if (Time.time - _lastActionTime < actionCooldown) return;

        var piece = requirePieceInside ? PieceInside : flow.CurrentPiece;
        if (piece == null) return;

        _lastActionTime = Time.time;
        CurrentCount++;
        piece.PlayActionPulse();

        if (CurrentCount >= requiredCount) Finish(piece);
    }

    public override void ForceComplete()
    {
        var piece = flow != null ? flow.CurrentPiece : null;
        if (piece == null)
        {
            CompleteStep();
            return;
        }
        Finish(piece);
    }

    void Finish(MooncakeWorkpiece piece)
    {
        switch (result)
        {
            case ResultShape.Flat: piece.MorphToFlat(); break;
            case ResultShape.Ball: piece.MorphToBall(); break;
        }

        if (applyEggWash) piece.ApplyEggWash();

        CurrentCount = 0;
        CompleteStep();
    }

    public override void OnFlowChanged()
    {
        CurrentCount = 0;
    }
}

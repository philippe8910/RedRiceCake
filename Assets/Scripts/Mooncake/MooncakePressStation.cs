using DG.Tweening;
using UnityEngine;

/// <summary>
/// 壓下去成型的站點：壓模（中式）與蓋章（印尼）。
/// 麵團放進區域後，模具／印章壓下再抬起，壓到底的瞬間套用造型結果。
/// </summary>
public class MooncakePressStation : MooncakeStation
{
    public enum PressResult { Mold, Stamp }

    [Header("壓具")]
    public Transform presser;
    public float pressDistance = 0.1f;
    public float pressDownTime = 0.18f;
    public float pressUpTime = 0.25f;

    [Header("行為")]
    public PressResult result = PressResult.Mold;
    [Tooltip("麵團就位後自動壓下（demo 用；關掉就要靠手推壓具或 UI 執行動作）")]
    public bool autoPress = true;
    public float autoPressDelay = 0.7f;

    Vector3 _presserHome;
    bool _pressing;
    float _pieceSettledTime = -1f;

    protected override void Awake()
    {
        base.Awake();
        if (presser != null) _presserHome = presser.localPosition;
    }

    protected override void Update()
    {
        base.Update();

        if (!autoPress || _pressing || !IsActiveStep) return;
        if (PieceInside == null)
        {
            _pieceSettledTime = -1f;
            return;
        }

        if (_pieceSettledTime < 0f) _pieceSettledTime = Time.time;
        if (Time.time - _pieceSettledTime >= autoPressDelay) PerformAction();
    }

    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        // 手直接推壓具也算
        if (IsHand(other)) PerformAction();
    }

    public override void PerformAction()
    {
        if (!IsActiveStep || _pressing) return;

        var piece = PieceInside != null ? PieceInside : flow.CurrentPiece;
        if (piece == null) return;

        DoPress(piece);
    }

    public override void ForceComplete()
    {
        var piece = flow != null ? flow.CurrentPiece : null;
        if (piece == null || _pressing)
        {
            CompleteStep();
            return;
        }
        DoPress(piece);
    }

    void DoPress(MooncakeWorkpiece piece)
    {
        _pressing = true;
        _pieceSettledTime = -1f;

        if (presser == null)
        {
            ApplyResult(piece);
            _pressing = false;
            return;
        }

        var down = _presserHome + Vector3.down * pressDistance;

        DOTween.Sequence()
            .Append(presser.DOLocalMove(down, pressDownTime).SetEase(Ease.InQuad))
            .AppendCallback(() => ApplyResult(piece))
            .Append(presser.DOLocalMove(_presserHome, pressUpTime).SetEase(Ease.OutQuad))
            .OnComplete(() => _pressing = false);
    }

    void ApplyResult(MooncakeWorkpiece piece)
    {
        if (piece == null) return;

        if (result == PressResult.Mold) piece.MorphToMolded();
        else piece.ShowStamp();

        CompleteStep();
    }

    public override void OnFlowChanged()
    {
        _pieceSettledTime = -1f;
    }
}

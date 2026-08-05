using UnityEngine;

/// <summary>
/// 抓取：麵團桶。手伸進去（或按 UI 的執行動作）就生出一顆可抓取的麵團。
/// </summary>
public class MooncakeGrabStation : MooncakeStation
{
    [Header("生成")]
    public MooncakeWorkpiece doughPrefab;
    public Transform spawnPoint;

    protected override void Awake()
    {
        step = MooncakeStep.Grab;
        base.Awake();
    }

    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        if (IsHand(other)) PerformAction();
    }

    public override void PerformAction()
    {
        if (!IsActiveStep) return;
        if (flow.CurrentPiece != null) return;   // 手上已經有一顆了
        if (doughPrefab == null)
        {
            Debug.LogWarning("[月餅] 抓取站沒有指定麵團 prefab", this);
            return;
        }

        var pos = spawnPoint != null ? spawnPoint.position : transform.position + Vector3.up * 0.15f;
        var piece = Instantiate(doughPrefab, pos, Quaternion.identity);
        piece.name = "MooncakeDough";

        flow.CurrentPiece = piece;
        CompleteStep();
    }

    public override void ForceComplete()
    {
        PerformAction();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 無人操作的試跑器：在 Play Mode 裡把兩條流程各走一遍。
/// 走的是真正的互動路徑（生麵團 → 移進站點觸發 → 重複動作 → 等烤箱計時 → 拿出爐），
/// 不是直接呼叫 flow.CompleteStep，所以碰撞觸發、計時、造型結果都會被驗到。
/// 由 Tools/月餅 Demo/試跑 或 MooncakeDryRun.RunFromCommandLine 掛上來。
/// </summary>
public class MooncakeDryRunDriver : MonoBehaviour
{
    [Header("逾時")]
    public float stepTimeout = 20f;
    public float totalTimeout = 300f;

    [Header("結束後")]
    public bool exitEditorWhenDone = true;

    readonly List<string> _errors = new List<string>();
    readonly List<string> _log = new List<string>();
    float _startRealtime;
    bool _done;

    IEnumerator Start()
    {
        _startRealtime = Time.realtimeSinceStartup;
        yield return null;

        var flow = FindObjectOfType<MooncakeFlow>();
        if (flow == null)
        {
            Fail("場景裡找不到 MooncakeFlow");
            Finish();
            yield break;
        }

        Note("站點數 " + flow.stations.Count);

        yield return RunRecipe(flow, MooncakeType.Chinese, 2, true, false);

        flow.ReturnToMenu();
        yield return new WaitForSeconds(0.4f);

        if (flow.CurrentPiece != null) Fail("回主畫面後麵團沒有被清掉");

        yield return RunRecipe(flow, MooncakeType.Indonesian, 1, false, true);

        Finish();
    }

    void Update()
    {
        if (_done) return;
        if (Time.realtimeSinceStartup - _startRealtime > totalTimeout)
        {
            Fail("整體逾時 " + totalTimeout + " 秒");
            Finish();
        }
    }

    // ------------------------------------------------------------------

    IEnumerator RunRecipe(MooncakeFlow flow, MooncakeType type,
        int expectedBakes, bool expectEggWash, bool expectStamp)
    {
        var label = MooncakeRecipes.TypeLabel(type);
        Note("── " + label + " ──");

        flow.StartRecipe(type);
        yield return null;

        MooncakeWorkpiece piece = null;
        int guard = 0;

        while (flow.IsRunning && guard++ < 40)
        {
            int before = flow.StepIndex;
            var step = flow.CurrentStep;
            var station = flow.FindActiveStation();

            if (station == null)
            {
                Fail(label + " 第 " + (before + 1) + " 步「" + MooncakeRecipes.StepLabel(step) +
                     "」找不到啟用中的站點");
                yield break;
            }

            yield return Drive(flow, station, step);

            float t0 = Time.realtimeSinceStartup;
            while (flow.StepIndex == before && Time.realtimeSinceStartup - t0 < stepTimeout)
                yield return null;

            if (flow.StepIndex == before)
            {
                Fail(label + " 第 " + (before + 1) + " 步「" + MooncakeRecipes.StepLabel(step) +
                     "」在 " + stepTimeout + " 秒內沒有完成（站點 " + station.name + "）");
                yield break;
            }

            if (flow.CurrentPiece != null) piece = flow.CurrentPiece;
            Note("  ✓ " + (before + 1) + ". " + MooncakeRecipes.StepLabel(step) +
                 "（" + station.name + "）");
        }

        if (!flow.IsFinished) Fail(label + " 沒有跑到完成狀態");

        // 造型結果驗證
        if (piece == null)
        {
            Fail(label + " 全程沒有產生麵團");
            yield break;
        }

        if (!piece.HasFilling) Fail(label + " 完成品沒有餡料");
        if (piece.BakeCount != expectedBakes)
            Fail(label + " 烘烤次數應為 " + expectedBakes + "，實際 " + piece.BakeCount);
        if (expectEggWash && !piece.IsEggWashed) Fail(label + " 完成品沒有刷過蛋液");
        if (expectStamp && !piece.HasStamp) Fail(label + " 完成品沒有蓋章");
        if (!expectStamp && piece.HasStamp) Fail(label + " 不該有印章卻蓋了章");

        Note("  完成品：餡料=" + piece.HasFilling + " 烘烤=" + piece.BakeCount +
             " 蛋液=" + piece.IsEggWashed + " 印章=" + piece.HasStamp);
    }

    /// <summary>把麵團送到站點該去的位置，並做出該站需要的互動。</summary>
    IEnumerator Drive(MooncakeFlow flow, MooncakeStation station, MooncakeStep step)
    {
        // 抓取：手伸進麵團桶
        if (station is MooncakeGrabStation)
        {
            station.PerformAction();
            yield break;
        }

        var piece = flow.CurrentPiece;
        if (piece == null)
        {
            Fail("步驟「" + MooncakeRecipes.StepLabel(step) + "」開始時手上沒有麵團");
            yield break;
        }

        // 取出：把月餅拿離烤箱
        if (station is MooncakeTakeOutStation)
        {
            yield return HoldAt(piece, station.transform.position + Vector3.up * 2f, 0.6f);
            yield break;
        }

        // 其餘站點：先把麵團放進區域
        yield return HoldAt(piece, station.transform.position, 0.4f);

        var action = station as MooncakeActionStation;
        if (action != null)
        {
            for (int i = 0; i < action.requiredCount + 2 && flow.CurrentStep == step; i++)
            {
                action.PerformAction();
                yield return HoldAt(piece, station.transform.position, action.actionCooldown + 0.06f);
            }
            yield break;
        }

        // 烤箱：放進去之後由烤箱自己關門計時
        if (station is MooncakeOvenStation) yield break;

        // 餡料碗／壓模／蓋章：進區域就會觸發，壓具還要等自動壓下
        yield return HoldAt(piece, station.transform.position, 1.6f);
    }

    /// <summary>模擬「手抓著麵團擺在某處」——逐幀壓住位置，讓觸發器正常進出。</summary>
    IEnumerator HoldAt(MooncakeWorkpiece piece, Vector3 pos, float seconds)
    {
        var rb = piece != null ? piece.GetComponent<Rigidbody>() : null;
        float t = 0f;

        while (t < seconds && piece != null)
        {
            if (rb != null && !rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            piece.transform.position = pos;

            t += Time.deltaTime;
            yield return null;
        }
    }

    // ------------------------------------------------------------------

    void Note(string s)
    {
        _log.Add(s);
    }

    void Fail(string s)
    {
        _errors.Add(s);
        _log.Add("  ✗ " + s);
    }

    void Finish()
    {
        if (_done) return;
        _done = true;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[月餅][DryRun] 試跑結果");
        foreach (var l in _log) sb.AppendLine(l);
        sb.AppendLine(_errors.Count == 0
            ? "[月餅][DryRun] DRY RUN PASSED"
            : "[月餅][DryRun] DRY RUN FAILED，共 " + _errors.Count + " 項");

        Debug.Log(sb.ToString());

#if UNITY_EDITOR
        if (exitEditorWhenDone) EditorApplication.Exit(_errors.Count == 0 ? 0 : 1);
        else EditorApplication.isPlaying = false;
#endif
    }
}

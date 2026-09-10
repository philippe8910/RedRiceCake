using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 實際遊玩場景的無人試跑器：在 Play Mode 裡用流程控制自己的 Debug 步驟
/// 把一整輪走完（三顆麵團 → 模具／印章 → 烤盤 → 烤箱 → 出爐），
/// 然後檢查結果符不符合這條流程該有的樣子。
///
/// 驗到的是真的跑起來才看得到的東西：站點事件有沒有串起來、烤箱倒數會不會結束、
/// 烘烤輪數對不對、印尼版有沒有多刷一次蛋液。
///
/// 由 Tools/月餅/試跑 或 MooncakeSceneDryRun.RunFromCommandLine 掛上來。
/// </summary>
public class MooncakeSceneDryRunDriver : MonoBehaviour
{
    [Header("逾時")]
    public float totalTimeout = 240f;

    [Header("結束後")]
    public bool exitEditorWhenDone = true;

    readonly List<string> _errors = new List<string>();
    readonly List<string> _log = new List<string>();
    bool _done;

    IEnumerator Start()
    {
        yield return null;

        var flow = FindObjectOfType<MooncakeChineseFlow>();
        if (flow == null)
        {
            Fail("場景裡找不到 MooncakeChineseFlow");
            Finish();
            yield break;
        }

        int bakes = Mathf.Max(1, flow.bakesBeforeDone);
        bool expectEggWash = bakes > 1;
        int slotCount = flow.traySlots != null ? flow.traySlots.Length : 0;

        Note("烘烤 " + bakes + " 輪，刷蛋液=" + expectEggWash + "，烤盤 " + slotCount + " 格");

        if (slotCount == 0)
        {
            Fail("烤盤沒有任何欄位，沒得跑");
            Finish();
            yield break;
        }

        if (flow.moldStation == null)
        {
            Fail("流程控制沒有接上模具／印章站，包餡之後就走不下去了");
            Finish();
            yield break;
        }

        bool allBaked = false;
        flow.onAllBaked.AddListener(() => allBaked = true);

        flow.DebugRunAllCakes();

        float t0 = Time.realtimeSinceStartup;
        int lastPlaced = -1;

        while (!allBaked && Time.realtimeSinceStartup - t0 < totalTimeout)
        {
            if (flow.PlacedCount != lastPlaced)
            {
                lastPlaced = flow.PlacedCount;
                Note("  烤盤已放 " + lastPlaced + "/" + slotCount);
            }
            yield return null;
        }

        if (!allBaked)
        {
            Fail("跑了 " + totalTimeout + " 秒還沒烤完（放上烤盤 " + flow.PlacedCount +
                 "/" + slotCount + "，烘烤 " + flow.BakeRound + "/" + bakes + " 輪）");
            Finish();
            yield break;
        }

        // 出爐之後烤盤還要滑出來，多等一下再驗結果
        yield return new WaitForSeconds(3f);

        if (flow.PlacedCount != slotCount)
            Fail("放到烤盤上的月餅有 " + flow.PlacedCount + " 顆，應該是 " + slotCount + " 顆");

        if (flow.BakeRound != bakes)
            Fail("烘烤了 " + flow.BakeRound + " 輪，應該是 " + bakes + " 輪");

        for (int i = 0; i < flow.traySlots.Length; i++)
        {
            var slot = flow.traySlots[i];
            if (slot == null) { Fail("烤盤第 " + (i + 1) + " 格是空的"); continue; }

            if (!slot.IsFilled) Fail(slot.name + " 上面沒有月餅");
            if (!slot.IsBaked) Fail(slot.name + " 的月餅沒有換成烤過的完成體");

            if (expectEggWash && !slot.IsEggWashed) Fail(slot.name + " 的月餅沒有刷到蛋液");
            if (!expectEggWash && slot.IsEggWashed) Fail(slot.name + " 這條流程不該刷蛋液卻刷了");
        }

        Note("  完成：放置 " + flow.PlacedCount + "/" + slotCount +
             "，烘烤 " + flow.BakeRound + " 輪");

        Finish();
    }

    void Note(string s)
    {
        _log.Add(s);
    }

    void Fail(string s)
    {
        _errors.Add(s);
        _log.Add("  X " + s);
    }

    void Finish()
    {
        if (_done) return;
        _done = true;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[月餅][試跑] 結果");
        foreach (var l in _log) sb.AppendLine(l);
        sb.AppendLine(_errors.Count == 0
            ? "[月餅][試跑] DRY RUN PASSED"
            : "[月餅][試跑] DRY RUN FAILED，共 " + _errors.Count + " 項");

        Debug.Log(sb.ToString());

#if UNITY_EDITOR
        if (exitEditorWhenDone) EditorApplication.Exit(_errors.Count == 0 ? 0 : 1);
        else EditorApplication.isPlaying = false;
#endif
    }
}

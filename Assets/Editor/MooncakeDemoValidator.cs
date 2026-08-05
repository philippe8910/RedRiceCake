using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 無頭驗證：跑一遍兩條流程的狀態機，並檢查 demo 場景的接線有沒有漏。
/// 用法：選單 Tools/月餅 Demo/驗證，或 -executeMethod MooncakeDemoValidator.ValidateFromCommandLine
/// </summary>
public static class MooncakeDemoValidator
{
    const string k_ScenePath = "Assets/Scenes/MooncakeDemo.unity";

    static readonly List<string> s_errors = new List<string>();
    static readonly StringBuilder s_log = new StringBuilder();

    [MenuItem("Tools/月餅 Demo/驗證")]
    public static void ValidateMenu()
    {
        Run();
    }

    public static void ValidateFromCommandLine()
    {
        var ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool Run()
    {
        s_errors.Clear();
        s_log.Length = 0;

        ValidateRecipeFlow(MooncakeType.Chinese, 10);
        ValidateRecipeFlow(MooncakeType.Indonesian, 8);
        ValidateScene();

        s_log.AppendLine(s_errors.Count == 0
            ? "[月餅] 驗證通過 ALL CHECKS PASSED"
            : "[月餅] 驗證失敗，共 " + s_errors.Count + " 項");

        foreach (var e in s_errors) s_log.AppendLine("  ✗ " + e);

        Debug.Log(s_log.ToString());
        return s_errors.Count == 0;
    }

    /// <summary>純狀態機測試：照流程表一步步完成，最後必須進到完成狀態。</summary>
    static void ValidateRecipeFlow(MooncakeType type, int expectedActionSteps)
    {
        var go = new GameObject("FlowTest");
        var flow = go.AddComponent<MooncakeFlow>();
        flow.stations = new List<MooncakeStation>();

        flow.StartRecipe(type);

        var label = MooncakeRecipes.TypeLabel(type);

        if (flow.Steps.Count != expectedActionSteps + 1)
            s_errors.Add(label + " 步驟數應為 " + (expectedActionSteps + 1) + "（含完成），實際 " + flow.Steps.Count);

        int guard = 0;
        while (flow.IsRunning && guard++ < 50)
        {
            var step = flow.CurrentStep;
            if (!flow.CompleteStep(step))
            {
                s_errors.Add(label + " 在第 " + flow.StepIndex + " 步（" +
                             MooncakeRecipes.StepLabel(step) + "）推不動");
                break;
            }
        }

        if (!flow.IsFinished)
            s_errors.Add(label + " 跑完流程後沒有進入完成狀態");
        else
            s_log.AppendLine("[月餅] " + label + " 流程 " + expectedActionSteps + " 步 → 完成 OK");

        // 亂序推進必須被擋掉
        flow.StartRecipe(type);
        if (flow.CompleteStep(MooncakeStep.Bake) && flow.CurrentStep != MooncakeStep.Bake)
            s_errors.Add(label + " 亂序步驟沒有被擋下");

        flow.ReturnToMenu();
        if (flow.IsRunning || flow.IsFinished)
            s_errors.Add(label + " 回主畫面後狀態沒有清乾淨");

        Object.DestroyImmediate(go);
    }

    static void ValidateScene()
    {
        var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);

        GameObject root = null;
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == "MooncakeDemo") root = go;

        if (root == null)
        {
            s_errors.Add("場景裡找不到 MooncakeDemo 根物件");
            return;
        }

        var flow = root.GetComponentInChildren<MooncakeFlow>(true);
        if (flow == null) { s_errors.Add("找不到 MooncakeFlow"); return; }

        var stations = root.GetComponentsInChildren<MooncakeStation>(true);
        if (stations.Length != flow.stations.Count)
            s_errors.Add("flow.stations 有 " + flow.stations.Count + " 筆，場上實際 " + stations.Length + " 個站點");

        // 每個步驟都要有站點負責（Done 除外）
        foreach (MooncakeStep step in System.Enum.GetValues(typeof(MooncakeStep)))
        {
            if (step == MooncakeStep.Done) continue;

            bool found = false;
            foreach (var s in stations) if (s.step == step) found = true;
            if (!found) s_errors.Add("步驟「" + MooncakeRecipes.StepLabel(step) + "」沒有對應站點");
        }

        // 兩條流程各自的站點都要湊得齊（考慮 restrictByType）
        foreach (MooncakeType type in System.Enum.GetValues(typeof(MooncakeType)))
        {
            foreach (var entry in MooncakeRecipes.For(type))
            {
                if (entry.Step == MooncakeStep.Done) continue;

                bool found = false;
                foreach (var s in stations)
                    if (s.step == entry.Step && (!s.restrictByType || s.onlyForType == type)) found = true;

                if (!found)
                    s_errors.Add(MooncakeRecipes.TypeLabel(type) + " 的「" +
                                 MooncakeRecipes.StepLabel(entry.Step) + "」在場上沒有可用站點");
            }
        }

        var grab = root.GetComponentInChildren<MooncakeGrabStation>(true);
        if (grab == null) s_errors.Add("找不到抓取站");
        else
        {
            if (grab.doughPrefab == null) s_errors.Add("抓取站沒有接上麵團 prefab");
            if (grab.spawnPoint == null) s_errors.Add("抓取站沒有 spawnPoint");
        }

        var oven = root.GetComponentInChildren<MooncakeOvenStation>(true);
        if (oven == null) s_errors.Add("找不到烤箱站");
        else
        {
            if (oven.door == null) s_errors.Add("烤箱沒有接上門");
            if (oven.trayPoint == null) s_errors.Add("烤箱沒有接上烤盤點");
        }

        var takeOut = root.GetComponentInChildren<MooncakeTakeOutStation>(true);
        if (takeOut == null) s_errors.Add("找不到取出站");
        else if (takeOut.oven == null) s_errors.Add("取出站沒有接上烤箱");

        var ui = root.GetComponentInChildren<MooncakeDemoUI>(true);
        if (ui == null) s_errors.Add("找不到 MooncakeDemoUI");
        else
        {
            if (ui.flow == null) s_errors.Add("UI 沒有接上 flow");
            if (ui.menuPanel == null || ui.playPanel == null || ui.finishPanel == null)
                s_errors.Add("UI 三個面板沒有接齊");
            if (ui.chineseButton == null || ui.indonesianButton == null)
                s_errors.Add("主畫面兩顆種類按鈕沒有接齊");
            if (ui.stepListText == null || ui.currentStepText == null)
                s_errors.Add("步驟文字沒有接齊");
        }

        var press = root.GetComponentsInChildren<MooncakePressStation>(true);
        foreach (var p in press)
            if (p.presser == null) s_errors.Add(p.name + " 沒有接上壓具");

        s_log.AppendLine("[月餅] 場景檢查：" + stations.Length + " 個站點");
    }
}
